# 02 – Konfiguratoren & Preislogik (Shop-Konfigurator vs. Schließanlagen-Konfigurator)

- Analysestand: Commit `9cc4a13` (Merged PR 938 „Fix: Shop configurator price calculation“, 05.10.2026)
- Methode: rein statische Code- und Git-Analyse. Es wurde nichts ausgeführt, kein Netzwerkzugriff, keine Änderungen am Repo.
- Fundstellen: `Pfad:Zeile` relativ zum Repo-Root `webshop/`.
- Kennzeichnung **[datenabhängig]**: Ob das Szenario eintritt, hängt von den Stammdaten in JTL ab (z. B. ob Längen als Variation oder als Konfig-Gruppe gepflegt sind). Die Code-Pfade selbst sind belegt.

---

## 1. Zusammenfassung

1. **Kein Single Source of Truth für Preise.** Es gibt mindestens **sieben** unabhängige Preisberechnungen: Produktdetailseite, Add-to-Cart im Shop, Preisanzeige im großen Konfigurator, Warenkorb/Checkout (`cartCalculations.ts`), Backend-Gesamtsumme (`utils/items.py`), ERP-Positionspreise (`jtl_helper/raw_order.py`) und Versandkosten in Frontend und Backend. Sie unterscheiden sich in Steuersatz, Kundengruppenpreisen, Mengenskalierung, Heuristiken und Rundung.
2. **Das Backend rechnet den Warenpreis zwar neu**, und zwar aus `items_svc`-Normalpreisen × 1,19 (`backend/business/src/business_api/utils/items.py:34-117`). Frontend-Preise werden nicht übernommen. **Mengen, Konfig-Artikel, Versandkosten und Freigrenze kommen aber ungeprüft vom Client.** Ein Kunde kann den Endbetrag manipulieren, ohne einen Preis zu senden: negative Mengen, eigene `config_group_items`, `GrossPrice: 0`, `free_shipping_at: 0`.
3. **Kritisch: Zahlung ist umgehbar.** `PATCH /api/order/{id}/mark_paid` ist über Traefik (`PathPrefix(/api)`) öffentlich erreichbar und hat weder Authentifizierung noch Stripe-Prüfung. Die Bestellung wird als bezahlt markiert und mit dem vollen Betrag als Stripe-Zahlung ins ERP gebucht (`backend/business/src/business_api/v1/endpoints/order.py:110-138`, `backend/business/src/business_api/utils/order.py:566-583`). Die Bestellnummer ist sequenziell (ab 20000) und für die Suche zugelassen.
4. **Kritisch: Stripe-Betrag und Bestellung laufen auseinander.** Der PaymentIntent wird beim ersten Mal erzeugt und danach unverändert wiederverwendet (`backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33`). Die Bestellung wird aber bei jedem Checkout-Schritt neu gepreist (Upsert). Wechselt man nach der Intent-Erzeugung Versandart oder Warenkorb, zahlt man den alten Betrag. Das ERP bucht trotzdem den neuen Gesamtbetrag als bezahlt. Das passiert auch im normalen, nicht manipulierten Ablauf.
5. **Der Schließplan erreicht die Bestellung nicht.** `matrix_config_id`/`matrix_config_url` werden im Checkout verworfen (`frontend/src/lib/services/orderService.ts:102-108`). Das Backend kann den Schließcode (B–F) deshalb nicht prüfen, und das ERP erhält keinen Bezug zur Matrix.
6. **Der große Konfigurator verliert oder verfälscht Aufpreise:**
   - Identische Zylinder oder Schlüssel werden im Warenkorb zusammengelegt, ohne die Mengen der Konfig-Artikel anzupassen. Die Aufpreise ab dem 2. Stück fallen weg (`frontend/src/lib/stores/productStore.ts:2030-2042`).
   - Längen-Aufpreise aus Variationen werden ignoriert (`frontend/src/routes/(configurator)/configurator/configure/[productId]/+page.svelte:574-580`).
   - Der Schließcode wird per Regex aus SKU oder Name „geraten“, mit Fallback `\b([BCDEF])\b`.
   - Fehler werden still geschluckt.
7. **Rabattlogik nur im Frontend.** Sonderpreise werden anhand einer `customer_group_id` aus `localStorage` angezeigt. `items_svc` erhält aber nie eine Benutzer-ID, und das Backend ignoriert Sonderpreise vollständig. Kunden mit Sonderpreis sehen einen niedrigeren Preis, als Stripe abbucht.
8. **MwSt hart 19 %** im Backend und in der ERP-Übergabe (`backend/business/src/business_api/utils/items.py:40`, `backend/business/src/business_api/utils/order.py:213,400`). Das Frontend nutzt `tax_class.rate_factor`. Es gibt keine Länderlogik (EU-OSS/Drittland), keine Cent-Rundung pro Position und durchgehend Gleitkomma-Arithmetik.
9. **Regressionsmuster in der Git-Historie.** Seit Juni 2026 gab es 45 von 70 Nicht-Merge-Commits mit „fix“. Allein die Mengen-Skalierung der Konfig-Artikel wurde in vier Commits von zwei Entwicklern getrennt in Frontend und Backend geändert. Daraus entstanden belegbare Zeitfenster mit q-facher Über- bzw. Unterberechnung. Es existieren **keine** Preistests (nur `frontend/src/demo.spec.ts`).
10. **Geschäftsregeln sind hartcodiert und mehrfach implementiert:** JTL-Artikel-IDs, Gruppennamen-Strings, die „3 Schlüssel“-Regel, die Geschenk-Schwelle 200 € und die Speditions-IDs 22/43. Mock-Daten (`lib/mocks/cylinderMockData.ts`) sind weiterhin im produktiven Konfigurator eingebunden.

---

## 2. Detailanalyse

### 2.1 Architekturüberblick (Datenfluss)

```
JTL-Wawi ──sync──> items_svc (Mongo "items", evaluated_data, apply_discounts)
                         │  POST /api/items/items/  (api_query, optional user_id)
                         ▼
 Frontend (SvelteKit) ──> productStore.mapApiItemToProduct  -> Product.price (brutto)
   ├─ /shop/[slug]  (kleiner Konfigurator: Variationen + config_groups)
   ├─ /configurator/configure/[productId] (großer Konfigurator: Zylinder, Schlüssel, Matrix)
   ▼
 Cart-Store (localStorage "cart_items" | POST /api/cart/{user_id})  – nur IDs/Mengen
   ▼
 Checkout: POST /api/order/  (FormData payload: checkout_data + checkout_items)
   ▼
 business_api build_order(): Preise aus items_svc (net×1,19), Versand aus Request
   ▼  Mongo "order" (pending)            ▼ bank_transfer: sofort ERP
 payment_svc POST /api/payment/intents/ {order_id} -> StillToPay -> Stripe PI
   ▼  Stripe-Webhook -> PATCH business_api /order/{id}/mark_paid -> ERP + Zahlung + Mail
```

### 2.2 Kleiner Konfigurator (`/shop/[slug]`)

**Ablauf**
1. `frontend/src/routes/(store)/shop/[slug]/+page.svelte` lädt das Produkt per Slug bzw. ID über `productStore.fetchProductBySlug/ById`. Parent-Artikel mit `child_items` + `child_variation_mapping` sind nicht bestellbar, es muss eine Variation gewählt werden (`frontend/src/routes/(store)/shop/[slug]/+page.svelte:61-68,121-151`).
2. Variationen werden über `ProductVariations` gewählt. Pflichtvariationen vom Typ 5 sind Texteingaben (`frontend/src/routes/(store)/shop/[slug]/+page.svelte:72-85,232-266`).
3. Konfig-Gruppen werden über `frontend/src/lib/components/shop/ProductConfigGroups.svelte` gewählt:
   - Je Gruppe werden die `config_items` per `POST /api/items/items/` geladen und auf `active_sales_channels.length>0` gefiltert. Danach holt `products.fetchConfigItems` die Preise (`frontend/src/lib/components/shop/ProductConfigGroups.svelte:63-140`).
   - Gruppentyp: 0 = Mehrfachauswahl, 1 = Einfachauswahl, 2 = Dropdown einfach, 3 = Dropdown mehrfach (`frontend/src/lib/components/shop/ProductConfigGroups.svelte:142-167`).
   - Die Items werden **nach Preis aufsteigend sortiert**, und das **günstigste Item jeder Gruppe wird automatisch gewählt** (`frontend/src/lib/components/shop/ProductConfigGroups.svelte:126-130,225-253`).
   - **Jede** Gruppe muss mindestens eine Auswahl haben, auch Mehrfachgruppen (`frontend/src/lib/components/shop/ProductConfigGroups.svelte:255-262`).
   - `amount_min/max/init` werden nur in der UI (QuantitySelector) erzwungen.
4. Preisanzeige: `frontend/src/lib/components/shop/ProductPrice.svelte:162-244`.
5. Warenkorb: `addToCart()` (`frontend/src/routes/(store)/shop/[slug]/+page.svelte:219-372`) berechnet `price` = Basis + Variationsaufpreise + Σ Konfig-Artikel × Menge. Dabei wird `originalPrice: product.price` gesetzt (Zeile 333). Anschließend folgt `cart.addItem(...)` (Zeile 358).

**Zustand:** `selectedVariations`, `selectedConfigItems: Record<groupId, itemId[]>`, `selectedConfigQuantities: Record<groupId, Record<itemId, qty>>` im Product-Objekt (`frontend/src/lib/stores/productStore.ts:338-348`).

**Persistenz:** Gespeichert wird nur ein `MinimalCartItem {id, quantity, variation_ids, variation_inputs, config_group_items[{id,quantity,group_id}], matrix_config_id, matrix_config_url}` (`frontend/src/lib/stores/productStore.ts:409-417,1582-1600`). Beim Reload wird alles neu aus `items_svc` hydriert (`frontend/src/lib/stores/productStore.ts:1602-1675`). Der beim Hinzufügen berechnete `price` geht dabei verloren.

### 2.3 Großer Konfigurator (`/configurator/configure/[productId]`)

Datei: `frontend/src/routes/(configurator)/configurator/configure/[productId]/+page.svelte` (2618 Zeilen). Im Folgenden kurz **„CFG“**; die Zeilenangaben beziehen sich jeweils auf diese Datei.

**Ablauf**
1. **Serie/Kategorie bestimmen:** `mapProductIdToSeries(productId)` liefert `productSeries`, `matchedCategoryId` und `baseProductId` (CFG Z. 74-81).
2. **Zylinder anlegen** (`CylinderForm.svelte`):
   - Typen kommen über `fetchCylinderTypesForSeries` (API) (CFG Z. 162-182).
   - Modelle über `getAvailableModels` (CFG Z. 196-230), Sicherheitsstufen über `getAvailableSecurityLevels` (CFG Z. 232-249).
   - Längen kommen aus `evaluated_data.zylinder_length.outside/inside` des Serienartikels (`frontend/src/lib/services/configuratorCylinders.ts:233-271`).
   - Optionen: `fetchConfiguratorConfigurations(model.id)` (CFG Z. 299-325).
   - Modifikationen kommen **aus Mock-Daten** (CFG Z. 325-327).
   - `addCylinder()` (CFG Z. 520-631) leitet aus der `reference` `item.id=NNN` die Längen-Artikel-ID ab und holt deren Preis.
3. **Schlüssel anlegen** (`KeyForm.svelte`):
   - Schlüsseltyp, -modell, -farbe und -konfiguration kommen als Props **aus Mock-Daten** (CFG Z. 160, 340-342).
   - KeyForm lädt zusätzlich eigene API-Daten über `fetchKeyConfigGroup` (`frontend/src/routes/(configurator)/configurator/configure/[productId]/KeyForm.svelte:164-166`).
   - `addKey()` (CFG Z. 659-768) speichert `model` als **Label-String** (nicht als ID).
4. **Matrix** (`frontend/src/lib/components/configurator/Matrix.svelte`): `cylinderKeyMatrix[cylId][keyId]: boolean`. Mengen lassen sich auch in der Matrix ändern (`frontend/src/lib/components/configurator/Matrix.svelte`, `handleCylinderQuantityChange`/`handleKeyQuantityChange`).
5. **Vollständigkeit:** Jeder Zylinder braucht ≥ 1 Schlüssel, jeder Schlüssel ≥ 1 Zylinder (CFG Z. 110-130).
6. **Schließplan-Typ:** `determineLockPlanType()` (`frontend/src/lib/utils/determineLockPlanType.ts:4-293`), siehe 2.9.
7. **Speichern:** `POST /api/configurator/` mit `StoredConfig {config_id, user_id, category, cylinderKeyMatrix, cylinders, keys, config_name, address…}` (CFG Z. 936-990; `backend/business/src/business_api/v1/endpoints/configurator.py:15-39`).
8. **In den Warenkorb:** `addMatrixToCart()` (CFG Z. 1452-1922):
   - Je Zylinder wird ein `MinimalCartItem` mit `id = Modell-Artikel`, `quantity = cyl.quantity` und `config_group_items = buildConfigGroupItemsForCylinder()` angelegt (CFG Z. 1497-1523).
   - Je Schlüssel werden ein bis zwei Positionen nach `analyzeKeyAllocation()` angelegt, plus Farbkappe als Konfig-Artikel (CFG Z. 1700-1897).
   - Alle Positionen tragen `matrix_config_id`/`matrix_config_url`.
   - Fallback: Wurde nichts hinzugefügt, landet das Basisprodukt mit Menge 1 im Warenkorb (CFG Z. 1902-1915).
9. **Ändern einer gespeicherten Konfiguration:** Alle Positionen mit gleicher `matrixConfigId` werden entfernt und neu hinzugefügt (CFG Z. 2012-2029).

**Datenquellen / Felder (Auszug)**

| Zweck | Endpunkt | Felder |
|---|---|---|
| Artikel/Preise | `POST /api/items/items/` | `price_data.normal_price.sales_price_net`, `price_data.special_prices[]`, `tax_class.rate_factor`, `config_groups[].{group_id,name,config_items,type}`, `config_group_data.*`, `variations[].data[].additional_charge.{price,netto}`, `evaluated_data.zylinder_length.{outside,inside}[].{value,price,reference}`, `categories`, `sku`, `name` |
| Schlüsselgruppen | `POST /api/items/items/` (Filter `categories.id`) | Gruppennamen „schlüssel“, „schlüsselform“, „schlüssel auswahl“, „mehrschlüssel“, „schlüsselmenge“, „…farbkappe…auswahl“, „schlüsselfarben auswählen“ (`frontend/src/lib/services/configuratorKeys.ts:156-379`) |
| Konfig speichern/laden | `POST/GET /api/configurator/` | siehe `backend/business/src/business_api/schemas/configurator.py` |
| Versand | `POST /api/items/shipping/?country_code=` | `Id, Name, GrossPrice, free_shipping_at, …` (`frontend/src/lib/services/orderService.ts:277-325`) |

### 2.4 Warenkorb → Checkout → Bestellung → Zahlung (gemeinsamer Pfad)

1. **Warenkorb:** Gäste nutzen `localStorage["cart_items"]`, eingeloggte Nutzer `POST /api/cart/{user_id}` (`frontend/src/lib/stores/productStore.ts:1720-1830`, `frontend/src/lib/services/cartService.ts:71-117`).
2. **Checkout:**
   - Anzeige über `calculateCartItemTotal` (`frontend/src/routes/(store)/shop/checkout/+page.svelte:193-215`).
   - Payload über `upsertOrder()` (`frontend/src/routes/(store)/shop/checkout/+page.svelte:444-605`): `checkout_items[{id, quantity, variation_ids[{id,data_id}], variation_inputs, config_group_items}]`. Es werden **keine Preise** und **kein** `matrix_config_id` gesendet.
   - `checkout_data.shippingMethod` ist das **komplette Versandobjekt inkl. `GrossPrice` und `free_shipping_at`** (Zeile 528).
   - Bei Shop-Artikeln werden die Konfig-Mengen × Positionsmenge gesendet, bei Matrix-Artikeln unverändert. Das Hauptprodukt wird aus seinen eigenen Konfig-Artikeln herausgefiltert (Zeile 565-572).
3. **Backend `POST /order/`** (`backend/business/src/business_api/v1/endpoints/order.py:43-85`):
   - Ist `order_id` gesetzt und existiert die Bestellung, wird sie **überschrieben**, unabhängig von Status und Eigentümer.
   - `build_order()` (`backend/business/src/business_api/utils/order.py:454-516`) setzt Positionen, Versand und `SalesOrderPaymentDetails.TotalGrossAmount = StillToPay = round(items + shipping, 2)` (Zeile 410-440).
   - Bei Vorkasse wird die Bestellung sofort ans ERP übertragen (Zeile 80-81 in order.py).
4. **Zahlung:**
   - `POST /api/payment/intents/ {order_id}` holt `StillToPay` per `GET business_api/order/single_order` und erzeugt den Stripe-PI. **Existiert schon ein Intent, wird er unverändert zurückgegeben** (`backend/payment_svc/src/payment/v1/endpoints/intents.py:30-36`, `backend/payment_svc/src/payment/utils/pricing.py:40-63`).
   - Der Webhook (signaturgeprüft) ruft `PATCH /order/{id}/mark_paid` auf (`backend/payment_svc/src/payment/utils/webhook.py:63-104`).
   - `mark_paid` setzt `StillToPay=0` und überträgt Bestellung und Zahlung ans ERP (`backend/business/src/business_api/v1/endpoints/order.py:110-138`, `backend/business/src/business_api/utils/order.py:519-583`).
5. **ERP:**
   - Positionen mit `SalesPriceNet/Gross` aus den Normalpreisen von `items_svc`.
   - Danach `set_item_prices` mit `total = Preis × Menge`, ungerundet (`backend/business/src/business_api/jtl_helper/raw_order.py:239-259`).
   - Variationsaufpreise werden **netto** addiert (`backend/business/src/business_api/jtl_helper/order_handling.py:95-116`).
   - Konfig-Zuordnung über `adjust_config_item` (`backend/business/src/business_api/jtl_helper/order_handling.py:31-54`).

### 2.5 Preisformeln (vollständig, mit Fundstellen)

Notation: `R` = `tax_class.rate_factor` (Fallback 1,19); `N` = `sales_price_net`; `n` = Positionsmenge; `q_i` = Menge Konfig-Artikel i; `P_i` = Preis Konfig-Artikel i.

#### A. Artikelpreis im Frontend (`mapApiItemToProduct`)
`frontend/src/lib/stores/productStore.ts:508-541`
```
sp            = special_prices.find(customer_group_id == localStorage.customer_group_id && is_active)
                // start_date/end_date/sales_channel_id/till_amount werden NICHT geprüft
priceNet      = sp ? sp.net_price : N
price (brutto)= priceNet * R                       // ungerundet
originalPrice = sp ? N * R : undefined
UVP           = suggested_retail_price * R         // in priceCalculations.ts:63-76 dagegen als BRUTTO behandelt
discount%     = round(sp.reduce_standard_price_by_percent)
```
Konfig-Artikel: `P_i = N_i * R_i`, **ohne** Sonderpreis und ohne Beachtung von `default_price/price_bool/rabatt_bool/surcharge_bool/ignore_multiplier` (`frontend/src/lib/stores/productStore.ts:1316-1341`).

#### B. Produktdetailseite (Anzeige)
`frontend/src/lib/components/shop/ProductPrice.svelte:203-244`
```
base  = specialNet ? specialNet*R : product.price           (netto-Modus: /R)
total = base + Σ variation.additionalCharge.price            (netto-Modus: .netto || price/R)
            + Σ P_i * q_i                                     (netto-Modus: /R des HAUPTartikels)
```
`product.price_data` existiert am gemappten `Product` nicht. `getSpecialPriceNet()` liefert daher in der Regel `null`, und der Sonderpreis kommt indirekt über `product.price`.

#### C. Add-to-Cart Shop
`frontend/src/routes/(store)/shop/[slug]/+page.svelte:273-349`
```
price         = product.price + Σ var.price + Σ P_i*q_i
price_net     = product.price_net + Σ var.netto + Σ (P_i/R)*q_i
originalPrice = product.price          // überschreibt die Bedeutung "Normalpreis vor Rabatt"
```

#### D. Großer Konfigurator – Anzeige je Karte („testPrice“, als TEMP markiert)
CFG Z. 2146-2240; `frontend/src/routes/(configurator)/configurator/configure/[productId]/ItemList.svelte:33-41`; `frontend/src/lib/components/configurator/ItemCard.svelte:23,190-211`
```
Zylinder: (product.price + Σ P_i * (cgi.qty / cyl.qty)) * cyl.qty
Schlüssel: product(resolveKeyProductId).price * key.qty      // ohne Farbkappe, ohne 3+n-Aufteilung
```

#### E. Großer Konfigurator – Warenkorbpositionen
`buildConfigGroupItemsForCylinder` (CFG Z. 1342-1384):
```
q = max(1, cyl.quantity)
für jede Option (configurations-Wert = Artikel-ID):  qty[id] += q
für outer/inner Längen-ItemId (nur reference "item.id=…"): qty[id] += q   // gleiche ID → 2q
Schließcode: code = lockPlanType; if code=='B' → 'C'; qty[codeMap[code]] = q   // überschreibt
```
`analyzeKeyAllocation` (CFG Z. 1390-1450):
```
connected = Anzahl Zylinder, die der Schlüssel schließt
if connected <= 1: min(qty,3) als "schlüssel auswahl" + (qty-3) als "mehrschlüssel"
else:              qty als "mehrschlüssel"
Farbkappe: config_group_items [{colorConfigId, qty}] (nicht im Mengenmodus)
```

#### F. Warenkorb / Checkout-Anzeige
`frontend/src/lib/utils/cartCalculations.ts:20-136`
```
rawBase   = originalPrice || price
d         = matrixConfigId ? max(1,n) : 1
S         = Σ P_i * q_i / d
base0     = (Konfig vorhanden && S>0 && |rawBase - S| < 0.01) ? 0          // Heuristik!
          : rawBase(+Σ var.price)                                           (netto: /R)
unit      = base0 + Σ P_i*q_i/d                                             (netto: /R des Hauptartikels)
line      = unit * n
Subtotal  = Σ line   (Checkout inkl. Geschenke; Cart-Seite für Geschenkschwelle ohne Geschenke)
MwSt      = Subtotal_brutto - Subtotal_netto
Versand   = (free_shipping_at > 0 && Subtotal >= free_shipping_at) ? 0 : GrossPrice
Total     = Subtotal + Versand    (an Stripe-Element als orderTotal, checkout +page.svelte:887)
```
Zusätzlich existiert `cartTotal = Σ product.price * n` (`frontend/src/lib/stores/productStore.ts:2053-2055`), eine weitere, inkonsistente Summe.

#### G. Backend (verbindlicher Zahlbetrag)
`backend/business/src/business_api/utils/items.py:34-117`, `backend/business/src/business_api/utils/order.py:387-440`
```
gross(item)   = N(item, Default-Kundengruppe aus items_svc) * 1.19     // fix, kein tax_class, kein special_price
unit          = gross(main) + Σ variation.additional_charge.price
total_items   = Σ_pos [ unit * n + Σ gross(cgi) * cgi.quantity ]       // Konfig NICHT × n (Frontend liefert schon q×n)
                // Positionen/Konfig-Artikel, die items_svc nicht liefert → stillschweigend 0
                // round(total_items, 2) erst am Ende
shipping_gross= (total_items < Request.free_shipping_at || Request.id in {22,43}) ? Request.gross_price : 0
shipping_net  = shipping_gross / 1.19
TotalGross = StillToPay = round(total_items + shipping_gross, 2)
discountCode  → ignoriert (auskommentiert, utils/order.py:501)
```
Stripe: `euros_to_cents(StillToPay)` mit `Decimal(str(float))`, ROUND_HALF_UP (`backend/payment_svc/src/payment/utils/pricing.py:15-37`).

#### H. ERP (JTL)
```
Position:  SalesPriceNet = N, SalesPriceGross = N*1.19, TaxRate = 19, Quantity = n bzw. cgi.quantity
set_item_prices: total_netto = N*Qty, total_gross = N*1.19*Qty     (ungerundet)
Variation:  SalesPriceNet += netto; SalesPriceGross += netto*1.19     (Stripe nutzt dagegen .price)
Zahlung:    fBetrag = TotalGrossAmount (nicht der von Stripe tatsächlich vereinnahmte Betrag)
```
Fundstellen: `backend/business/src/business_api/utils/order.py:206-250,566-583`, `backend/business/src/business_api/jtl_helper/raw_order.py:239-259`, `backend/business/src/business_api/jtl_helper/order_handling.py:95-116`.

#### I. Rabatte / Kundengruppen
- `items_svc` wendet Kundengruppenpreise und persönliche Rabatte über den Body-Parameter `user_id` an, Default `"userID"` (`backend/items_svc/src/jtl/v1/endpoints/items.py:174-195`, `backend/items_svc/src/jtl/v1/utils/discount_logic.py:6-90`).
- Das Frontend sendet stattdessen `customer_group_id` (`frontend/src/lib/stores/productStore.ts:912-916,1288-1292`). Der Parameter wird von der Endpoint-Signatur nicht ausgewertet. **Ergebnis: Jeder erhält Default-Gruppenpreise**, auch das Backend bei der Bestellung (`backend/business/src/business_api/utils/items.py:6-31`).
- `special_prices` wertet **nur** das Frontend aus, mit der Gruppe aus `localStorage` (`frontend/src/lib/stores/customerGroupStore.ts:9-31`).

#### J. Geschenke („gifts“)
- Der Geschenk-Artikel wird mit `price: 0, isGift: true` in den Warenkorb gelegt (`frontend/src/lib/components/shop/FreeGiftCarousel.svelte:85`, `frontend/src/lib/services/freeGiftService.ts:26`).
- Die Schwelle `FREE_ITEMS_THRESHOLD = 200` (brutto, ohne Geschenke) und das automatische Entfernen gibt es **nur** auf der Warenkorbseite (`frontend/src/routes/(store)/shop/cart/+page.svelte:31-63`).
- `isGift` ist nicht Teil von `MinimalCartItem`. Nach Reload oder Login wird das Geschenk als normaler Artikel mit Katalogpreis hydriert (`frontend/src/lib/stores/productStore.ts:409-417,1602-1627`).
- Im Checkout wird das Geschenk als normale Position gesendet. Das Backend kennt keine Geschenke und berechnet den JTL-Preis.

#### K. Rundung / Gleitkomma
- Frontend: `number`-Arithmetik ohne Rundung. Es wird nur bei der Anzeige formatiert.
- Backend: `float`, Rundung nur auf die Gesamtsumme.
- ERP: ungerundete Positionssummen.
- Netto-Anzeige im Frontend = Brutto / R (Rückrechnung).
- Eine Cent-genaue Übereinstimmung zwischen Anzeige, Stripe und Rechnung ist damit nicht garantiert.

### 2.6 Gegenüberstellung

| Aspekt | Shop (klein) | Konfigurator (groß) | Warenkorb/Checkout (FE) | Backend (zahlungsrelevant) | ERP |
|---|---|---|---|---|---|
| Basispreis | `N*R`, ggf. Sonderpreis (localStorage-Gruppe) | `product.price` (wie Shop) | `originalPrice‖price`; nach Reload = Normalpreis, frisch = Sonderpreis | `N(Default-Gruppe)*1.19` | `N`, TaxRate 19 |
| MwSt | `tax_class.rate_factor` | `rate_factor` | `rate_factor` des Hauptartikels für alle Konfig-Artikel | fix 19 % | fix 19 % |
| Variationen | `+additional_charge.price` (brutto) | nicht genutzt (Längen-Variationen ignoriert) | `+price` | `+price` | `+netto*1.19` |
| Konfig-Artikel | `P_i*q_i` je Einheit | Anzeige `P_i*q_i/q`, Warenkorb `q_i = q` (bzw. 2q) | `P_i*q_i/d`, ×n | `gross_i*q_i` einmal | Positionen Qty `q_i` |
| Mengen-Skalierung Konfig | × n (Checkout multipliziert) | bereits × q | Divisor d=n bei Matrix | nicht × n | – |
| Heuristik „Basis in Konfig“ | – | – | **ja** (`<0,01`) | **nein** (seit 25.08.) | nein |
| Zusammenlegen gleicher Positionen | `addItem`: n += , q_i unverändert | `addMinimal`: n += , q_i unverändert | – | – | – |
| Sonderpreise | ja (Anzeige) | ja (über product.price) | teilweise | **nein** | nein |
| JTL-Konfig-Flags | ignoriert | ignoriert | ignoriert | ignoriert | JTL wendet ggf. eigene an [datenabhängig] |
| Versand | – | – | `free_shipping_at>0 && ≥` | `< free_shipping_at` oder ID 22/43, **Werte aus Request** | Versandartikel |
| Geschenke | – | – | price 0 (nur bis Reload) | normaler Preis | normale Position |
| Rundung | keine | keine | keine | `round(...,2)` am Ende | ungerundet |
| Schließcode | – | FE-Regex/Heuristik | – | **nicht validierbar** (Matrix fehlt) | Konfig-Position |

### 2.7 Konkrete Fehlerszenarien (Eingabe → Preis A vs. Preis B)

Beispielwerte sind fiktiv, aber rechnerisch exakt nach den Formeln in 2.5.

| # | Eingabe | Frontend-Anzeige (A) | Backend/Stripe (B) | Korrekt | Ursache |
|---|---|---|---|---|---|
| S1 | Schließplan: 2 identische Zylinder (Tür A, Tür B) je 1 Stk., Basis 89,00 €, Länge außen 10,00 €, innen 10,00 €, Code-E-Artikel 15,00 € | 213,00 € | 213,00 € | 248,00 € | `addMinimal` legt zu n=2 zusammen, `q_i` bleibt 1 → 35 € Verlust je weiterem identischen Zylinder (K-07) |
| S2 | Wie S1, zusätzlich liegt im Warenkorb ein anderer Schließplan mit demselben Zylinder | Menge des **fremden** Plans steigt mit | dito | – | `items.map` ohne `matrixConfigId`-Vergleich (`frontend/src/lib/stores/productStore.ts:2037-2042`) |
| S3 | Shop-Artikel 24,90 € + 1 Konfig-Artikel 24,90 € | 24,90 € | 49,80 € | 49,80 € | FE-Heuristik setzt Basis 0 (K-06); Kunde zahlt mehr als angezeigt |
| S4 | Kunde mit Kundengruppe + aktivem Sonderpreis: Normal 100,00 € netto, Sonder 80,00 € netto | Produktseite/frischer Warenkorb 95,20 €; nach Reload 119,00 € | 119,00 € | 95,20 € (vertraglich angezeigt) | Backend ignoriert `special_prices` (K-05) |
| S5 | Versandart mit `free_shipping_at = 0`, GrossPrice 5,95 € | 5,95 € | 0,00 € | 5,95 € | entgegengesetzte Bedingungen (K-13) |
| S6 | Spedition ID 22, Warenwert 150 €, Freigrenze 99 € | 0,00 € | GrossPrice | GrossPrice | ID-Liste nur im Backend (K-13) |
| S7 | Checkout bis Schritt 3 (online, Intent 119,00 €), zurück zu Schritt 2, Spedition 89,00 € gewählt, Bestellung abschließen | 208,00 € | **Stripe belastet 119,00 €**, ERP bucht 208,00 € als bezahlt | 208,00 € | Intent-Reuse ohne Betragsabgleich (K-02) |
| S8 | Manipulierter Request: `shippingMethod.GrossPrice = 0` | – | 0,00 € Versand | regulär | Versand aus Request (K-03) |
| S9 | Manipulierter Request: Zylinder n=1 (119 €) + `config_group_items:[{id:<beliebiger Artikel 100 €>, quantity:-1}]` | – | 19,00 € | 119,00 € | keine `ge=1`-Validierung (K-04) |
| S10 | Eigene Bestellung anlegen, dann `PATCH /api/order/<Nummer>/mark_paid {"status":"paid","intent_id":"x"}` | – | 0 € gezahlt, ERP „bezahlt“ | – | öffentlicher Endpunkt (K-01) |
| S11 | Shop-Artikel n=3, Konfig-Artikel Netto 8,40 € (Brutto 9,996 €) | Zeile 29,99 € (29,988) | 29,99 € (round am Ende) | je nach ERP-Rundung 30,00 € | keine Rundung pro Position (K-17) |
| S12 | Artikel mit 7 % (`rate_factor 1.07`), Netto 10,00 € | 10,70 € | 11,90 € | 10,70 € | 19 % fix (K-12) |
| S13 | Schlüssel, Menge 5, schließt 1 Zylinder (3 Standard + 2 Mehrschlüssel zu anderem Preis) | Karte: 5 × Preis des Modells | 3×Standard + 2×Mehrschlüssel (+ Farbkappe) | Warenkorbwert | unterschiedliche Auflösung Anzeige/Warenkorb (K-15) |
| S14 | Modell mit Längen als **Variation** (`reference "variations.name=…"`) [datenabhängig] | ohne Längenaufpreis | ohne Längenaufpreis, **Länge fehlt in Bestellung** | mit Aufpreis | `extractItemId` nur `item.id=` (K-09) |
| S15 | Modell mit kombinierter Längengruppe „Zylinderlänge“ (Items „30/35“ …) [datenabhängig] | Längen-Artikel ggf. 2× (2q) oder zwei verschiedene Items | dito | 1× richtiges Item | outer/inner unabhängig aus gleicher Gruppe (K-09) |
| S16 | Geschenk gewählt (Warenwert ≥ 200 €), danach Seite neu geladen | Geschenk mit Katalogpreis | Katalogpreis | 0 € | `isGift` nicht persistiert (K-14) |
| S17 | Geschenk-Artikel-ID per Request bei Warenwert 20 € [datenabhängig: JTL-Preis 0] | – | 0 € | nicht zulässig | Schwelle nur im FE (K-14) |
| S18 | 2 Shop-Positionen mit unterschiedlicher Konfig-Menge desselben Artikels in einer Bestellung | – | Preis korrekt, aber ERP-Konfig-Zuordnung der 1. Position überschrieben | – | `dict` nach `item.id` (K-20) |

### 2.8 Backend-Validierung & Manipulierbarkeit (Belege)

**Was das Backend neu berechnet:** Artikel- und Konfig-Artikelpreise aus `items_svc` (`backend/business/src/business_api/utils/items.py:69-117`). Der Client sendet keine Preise. Das ist gut, reicht aber nicht.

**Was ungeprüft vom Client übernommen wird:**

| Feld | Beleg | Effekt |
|---|---|---|
| `checkout_data.shippingMethod.{Id, GrossPrice, free_shipping_at, ExtraWeight, Priority}` | `backend/business/src/business_api/schemas/orders_schemas.py:41`, `backend/business/src/business_api/schemas/global_data_raw_schemas.py:59-66`, `backend/business/src/business_api/utils/order.py:392-409,509` | Versandkosten frei wählbar, keine Abgleichung mit `/order/shipping_methods` |
| `checkout_items[].quantity`, `config_group_items[].quantity` | `backend/business/src/business_api/schemas/orders_schemas.py:67-79` (`int` ohne `ge=1`) | 0/negative Mengen senken die Summe |
| `config_group_items[].id` | `backend/business/src/business_api/utils/items.py:72-111` | beliebige Artikel als „Konfig“, keine Prüfung gegen `config_groups` des Hauptartikels, `amount_min/max`, `in_shop`, `is_active`, Vertriebskanal |
| `variation_ids[].data_id` | `backend/business/src/business_api/utils/items.py:55-66` | unbekannte Option → Aufpreis 0, keine Fehlermeldung |
| `checkout_data.order_id` | `backend/business/src/business_api/v1/endpoints/order.py:64-75` | überschreibt jede existierende Bestellung (auch bezahlte/fremde), setzt `PaymentStatus` zurück |
| Geschenke | – | Backend kennt das Konzept nicht |
| `discountCode` | `backend/business/src/business_api/utils/order.py:501` | wird ignoriert (Kunde erwartet ggf. Rabatt) |

**Zahlungsfluss-Lücken:**
1. `mark_paid` ohne Auth (`backend/business/src/business_api/v1/endpoints/order.py:110-138`). Die Route ist öffentlich: `compose/services_deploy/backend/business_api.yml:42-48` (`PathPrefix(/api)` + StripPrefix, keine Auth-Middleware), `backend/business/src/business_api/main.py:52-60` (keine globale Dependency). Die Order-Suche akzeptiert die sequenzielle `Number` (`backend/business/src/business_api/utils/order.py:142-177`, Start 20000 in Zeile 36, Vergabe Zeile 194-197).
2. Intent-Reuse (`backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33`) bei gleichzeitigem Order-Upsert (`frontend/src/routes/(store)/shop/checkout/+page.svelte:361-367,395,616-620`). Weder Webhook noch `mark_paid` vergleichen `amount_received` mit `TotalGrossAmount` (`backend/payment_svc/src/payment/v1/endpoints/webhook.py:63-77`, `backend/payment_svc/src/payment/utils/webhook.py:63-104`).
3. ERP-Zahlung bucht `TotalGrossAmount` statt des tatsächlich gezahlten Betrags (`backend/business/src/business_api/utils/order.py:566-583`).
4. Die Endpoint-Beschreibung behauptet eine Validierung („validates the total amount against the item prices“, `backend/payment_svc/src/payment/v1/endpoints/intents.py:19-21`). Implementiert ist sie nicht. `backend/payment_svc/src/payment/schemas/pricing.py` ist ungenutzt.
5. Weitere offene Endpunkte im Umfeld:
   - `GET /order/single_order?number=` liefert beliebige Bestellungen inkl. Adressen (`backend/business/src/business_api/v1/endpoints/order.py:102-107`).
   - `GET /order/send_order_to_system/{id}` (Zeile 153-163).
   - `POST /configurator/` überschreibt fremde Konfigurationen per `config_id`; `GET ?user_id=` listet fremde Konfigurationen (`backend/business/src/business_api/v1/endpoints/configurator.py:15-64`).
   - `POST/GET/DELETE /cart/{user_id}` (`backend/business/src/business_api/v1/endpoints/cart.py:12-62`).

**Fazit Manipulation:** Ja. Ein Kunde kann den Zahlbetrag mit DevTools/curl senken (Versand, Mengen, Konfig-Artikel) oder die Zahlung ganz umgehen (`mark_paid`). Ohne Absicht entstehen Fehlbeträge durch Intent-Reuse.

### 2.9 Konfigurationslogik (Typcodes, Längen, Matrix, Regeln)

**Schließplan-Typen** (`frontend/src/lib/utils/determineLockPlanType.ts:281-292`; Texte in `frontend/messages/de.json`, Schlüssel `configurator_matrix_info_code_*`):

| Code | Bedeutung (Text) | Regel im Code (Prüfreihenfolge) |
|---|---|---|
| B | Gleichschließung | jeder Schlüssel schließt **alle** Zylinder (auch 1×1 → B) |
| D | Einzelschließungen mit einfacher Zentralstruktur | 2 Zeilen-/2 Spaltensignaturen mit Muster [[1,0],[1,1]] **oder** Zentralstruktur mit technischem Pass-Schlüssel ohne Pass-Schlüssel |
| E | Hauptschlüsselanlage | Pass-Schlüssel + je Zylinder genau ein Einzelschlüssel, oder Zentralstruktur mit Pass-Schlüssel |
| C | Einzelschließung | Permutation (n Zylinder = n Schlüssel, je genau 1) |
| F | Gruppen- und Hauptschlüsselanlage | **alles Übrige (Auffangklasse)** |

Befunde:
- **Widersprüche/Auffälligkeiten:**
  - B wird beim Warenkorb auf den **C-Artikel** gemappt („Special case“, CFG Z. 1370-1371).
  - Für C sagt der Hinweistext, dass der Planer nicht nötig sei; die Bestellung über den Planer bleibt trotzdem möglich.
  - F ist keine positive Regel, sondern Fallback. Jede nicht erkannte Struktur wird als GHS bepreist.
  - Die Reihenfolge B vor C bewirkt, dass 1 Zylinder/1 Schlüssel als B gilt.
- **Code → Artikel:**
  - `fetchClosingCodeMapping` sucht Gruppen namens „zylinderfunktion“, „zylinderschliessfunktion“ oder „schliessfunktion“ und extrahiert den Code per Regex-Kaskade aus SKU oder Name (CFG Z. 1005-1119).
  - Letzte Stufe ist `\b([BCDEF])\b` (Z. 1040). Ein einzelner Buchstabe wie „E“ irgendwo im Namen genügt.
  - Fallbacks über Namensmuster: „einzelschlie“ → C, „gleichschlie“ → B.
  - Bei Fehlern wird kein Aufpreis berechnet und nur `console.error` geschrieben (Z. 1377-1379).
- **Matrix → Bestellung:** Die Matrix existiert nur in `StoredConfig` und gelangt nicht in die Bestellung (siehe K-08). Eine serverseitige Neuklassifizierung ist unmöglich.
- **Zylinderlängen:**
  - `items_svc` erzeugt Längen aus Variationen (`reference=variations.name=…&&variations.data.id=…`, Preis = `netto`) **oder** aus Konfig-Gruppen (`reference=item.id=…`, Preis = `sales_price_net`) (`backend/items_svc/src/jtl/jtl_helper/evaluated_data.py:41-339`).
  - Das Frontend versteht nur `item.id=` (CFG Z. 574-580).
  - Bei kombinierten Gruppen („zylinderlänge“) zeigen outer und inner auf dasselbe Item; außen und innen werden aber unabhängig gewählt (`frontend/src/lib/services/configuratorCylinders.ts:254-270`, `find` nimmt den ersten Treffer).
  - Gültige Kombinationen außen/innen werden nicht geprüft.
- **Schlüssel:**
  - Hartcodierte Abbildung Standard → Mehrschlüssel je Serie (`frontend/src/lib/services/configuratorKeys.ts:48-62`, Kommentar „currently hard coded, will be added in JTL“).
  - Ausgeschlossene IDs: 30894, 14321, 35778 (Z. 425), 38849 (Z. 255), 44365 (Z. 360). Farbkappen-IDs: 46869 (Z. 46). „Lange Reide“: 33826, 33825 (Z. 427).
  - Die „3 Schlüssel“-Regel steht in CFG Z. 1424-1438.
  - Schlüsselmodelle werden per **Namensvergleich** (`m.name === key.model`) zugeordnet; Farben ebenfalls per normalisiertem Label.
- **Duplizierte Logik:**
  - Gruppen-Normalisierung (ä→ae …) mindestens 4×: CFG Z. 1056-1063 und 1170ff., `frontend/src/lib/services/configuratorKeys.ts:90-98`, `utils/keyQuantityLabel`.
  - Zylinder→Produkt-ID-Auflösung 4× (CFG Z. 1497-1503, 1665-1671, 2035-2061, 1258-1290).
  - Schlüsselauflösung 2× und dabei **unterschiedlich**: Anzeige Z. 2063-2144 vs. Warenkorb Z. 1700-1895.
  - Sonderpreis-Lookup 3×: `frontend/src/lib/stores/productStore.ts:513-541`, `frontend/src/lib/utils/priceCalculations.ts:88-175`, `frontend/src/lib/components/shop/ProductPrice.svelte:56-120`.
- **Validierungsregeln:**
  - Nur im Frontend: Matrix vollständig, Variationen gewählt, Konfig-Gruppen gewählt, `amount_min/max` per UI.
  - Im Backend gibt es keine einzige fachliche Validierung.
- **Mock-Daten im Produktivcode:**
  - `frontend/src/lib/mocks/index.js` re-exportiert `cylinderMockData.ts` (1474 Zeilen, z. B. `cylinderModels` mit `'abus-bravus-3000-111'`).
  - Genutzt in CFG Z. 5-11, 160 (`getKeyTypesForSeries`), 326 (`getModificationsForModel(model.name)`: Lookup per Mock-ID mit realem Namen, liefert praktisch immer `[]`), 340-342 (Schlüsselmodelle, -farben, -konfigurationen als Props an KeyForm und `createKeyItem`).
  - Die Mock-Werte können als Defaults (`keyType`, `keyModel`) in gespeicherte Konfigurationen gelangen.

### 2.10 Git-Historie (seit Juni 2026)

- 70 Nicht-Merge-Commits seit 01.06.2026, davon 45 mit „fix“ im Betreff.
- Commits pro Datei seit Juni: CFG 14, `frontend/src/lib/stores/productStore.ts` 11, `frontend/src/lib/utils/cartCalculations.ts` 4, `frontend/src/lib/services/configuratorKeys.ts` 4, `backend/business/src/business_api/utils/items.py` 3 (= alle), `frontend/src/lib/utils/determineLockPlanType.ts` 1.

**Chronologie Preis-/Konfigurationsfixes**

| Datum | Commit | Inhalt | Beobachtete Regression / Muster |
|---|---|---|---|
| 13.06. | 6b61762 (PR 695) | FE-Heuristik „Basis ≈ Σ Konfig → Basis 0“ eingeführt | Symptombehandlung; erzeugt S3 |
| 16.06. | f06957a (PR 705) | „Fixed Configurator prices + Codes“ (254 Zeilen CFG) | großer Sammelfix ohne Tests |
| 17.08. | b6d41be / ef0fe86 | Neue Checkout-Logik: Backend berechnet Summe selbst: `(unit + Σcfg)·n` inkl. Heuristik. Vorher kam der Betrag aus dem JTL-Auftrag | Matrix-Konfig-Mengen sind bereits ×q → **q-fache Überberechnung der Zylinder-Aufpreise** (FE zeigte dasselbe, ebenfalls ×q²) |
| 25.08. | bbb8d79 | Backend: Heuristik entfernt | **FE behält Heuristik** → dauerhafte Divergenz (K-06) |
| 06.09. | 023755b | „fix missing cylinder lengths“ | – |
| 07.09. | ce8f3a5 | Reihenfolge der Konfig-Auswahl, neues `configSelections.ts` | – |
| 08.09. | a8656b4 | FE: Divisor für Matrix, Checkout multipliziert Shop-Konfig-Mengen ×n | Backend noch `(unit+Σcfg)·n` → **1 Tag n-fache Überberechnung** bei Shop-Konfig-Artikeln; Matrix: Anzeige korrekt, Abbuchung ×q |
| 09.09. | c94dd9e | Backend: `unit·n + Σcfg` | FE und BE erstmals konsistent bei Mengen |
| 17.09. | 43c17c6 | doppelte Farbkappen (Produkt = eigene Farbe) | Altdaten in Warenkörben weiter betroffen (Filter nur im Checkout) |
| 23.09. | 4199394 | Schließcode-Regex erweitert. **Vorher**: jede SKU `^K\d+[A-Z]*$` → 'C' | bis 23.09. vermutlich **keine D/E/F-Aufpreise** (codeMap enthielt nur C) [datenabhängig] |
| 01.10. | 3bf9fc4 | `determineLockPlanType` umgeschrieben (+159/−78) | ohne Testfälle; F bleibt Auffangklasse |
| 02.10. | cc3dd74 | ProductPrice: Konfig-Preise per `$effect` statt Race | – |
| 02.10. | 66013f2 | Geschenkmenge nicht änderbar | `isGift`-Persistenz weiter offen |

**Muster:**
1. Frontend (DominicB) und Backend (vitandi) ändern dieselbe Formel in getrennten Commits, ohne gemeinsamen Vertrag. Die Folge sind Zeitfenster mit falschen Beträgen.
2. Heuristiken statt Datenmodell: Toleranz 0,01 €, Regex auf SKU, Namensvergleiche.
3. Hartcodierte JTL-IDs als Fix.
4. Sammel-PRs („Prio 1/2/3 issues“).
5. Keine Tests, keine Golden-Daten.
6. Silent Failures (`catch {}`) in der Preis- und Warenkorblogik.

---

## 3. Befunde-Tabelle

| ID | Schwere | Fundstelle | Beschreibung | Auswirkung | Empfehlung |
|---|---|---|---|---|---|
| K-01 | Kritisch | `backend/business/src/business_api/v1/endpoints/order.py:110-138`; `compose/services_deploy/backend/business_api.yml:42-48`; `backend/business/src/business_api/utils/order.py:142-177,566-583` | `PATCH /api/order/{id}/mark_paid` öffentlich, ohne Auth und ohne Stripe-Abgleich; Order per sequenzieller Nummer adressierbar | Ware ohne Zahlung; ERP verbucht Stripe-Zahlung in voller Höhe | Endpunkt nur intern (eigener Router ohne Traefik-Exposition) bzw. mTLS/Shared Secret; Statuswechsel ausschließlich aus verifiziertem Stripe-Event mit `amount_received == StillToPay`; Sofortmaßnahme: Traefik-Regel für `/api/order/*/mark_paid` und `/send_order_to_system` sperren |
| K-02 | Kritisch | `backend/payment_svc/src/payment/v1/endpoints/intents.py:30-36`; `backend/business/src/business_api/v1/endpoints/order.py:64-75`; `frontend/src/routes/(store)/shop/checkout/+page.svelte:361-367,395,616-620`; `backend/payment_svc/src/payment/utils/webhook.py:63-104` | Intent wird wiederverwendet, Bestellung danach neu gepreist; kein Betragsabgleich | Unterzahlung im Normalbetrieb (S7); ERP „bezahlt“ | Bei jedem Upsert `PaymentIntent.modify(amount)` oder Intent verwerfen/neu anlegen; Bestellung nach Intent-Erzeugung einfrieren; Webhook prüft Betrag + Währung + order_id-Metadaten |
| K-03 | Kritisch | `backend/business/src/business_api/schemas/orders_schemas.py:41`; `backend/business/src/business_api/utils/order.py:387-409` | Versandpreis, Freigrenze und ID kommen aus dem Request | Versandkosten beliebig senkbar (S8) | Nur `shipping_method_id` annehmen; Preis/Freigrenze serverseitig aus `items_svc`-Versandlogik (Land, Versandklassen) ermitteln |
| K-04 | Kritisch | `backend/business/src/business_api/schemas/orders_schemas.py:67-79`; `backend/business/src/business_api/utils/items.py:69-117` | Keine Validierung von Mengen (≥1), Konfig-Zugehörigkeit, `amount_min/max`, Aktiv-/Shop-Status; fehlende Artikel → 0 € | Summe beliebig reduzierbar (S9), Fantasie-Kombinationen im ERP | Pydantic `conint(ge=1, le=…)`; serverseitige Konfig-Validierung gegen `config_groups`/`config_group_data`; unbekannte IDs → 422 |
| K-05 | Hoch | `backend/items_svc/src/jtl/v1/endpoints/items.py:174-195`; `frontend/src/lib/stores/productStore.ts:508-541,912-916,1288-1292`; `frontend/src/lib/stores/customerGroupStore.ts:9-31`; `backend/business/src/business_api/utils/items.py:34-47` | Kundengruppen-/Sonderpreise nur im FE (localStorage-Gruppe); `items_svc` erhält nie `user_id`; Backend nutzt Default-Gruppe ohne `special_prices` | Angezeigter ≠ abgebuchter Preis (S4), Preisangaben-Risiko | Preisermittlung serverseitig anhand der authentifizierten Session (Cookie → Kundengruppe), inkl. Gültigkeitszeitraum/Kanal der Sonderpreise; FE zeigt nur Server-Preise |
| K-06 | Hoch | `frontend/src/lib/utils/cartCalculations.ts:88-96` | Heuristik „Basis ≈ Σ Konfig (±0,01) → Basis 0“ nur im FE | Anzeige zu niedrig, Abbuchung höher (S3) | Heuristik entfernen; Datenmodell klären (Hauptartikel als Konfig-Artikel explizit kennzeichnen) |
| K-07 | Hoch | `frontend/src/lib/stores/productStore.ts:2030-2042,1877-1883,1574-1580` | Zusammenlegen identischer Positionen erhöht `quantity`, nicht die `config_group_items`-Mengen; `map` ohne Matrix-ID | Aufpreise ab dem 2. identischen Zylinder/Schlüssel fehlen (S1), fremde Pläne werden mitgezählt (S2) | Matrix-Positionen nie mergen (eigene Zeilen-ID je Zylinder/Schlüssel) bzw. Konfig-Mengen mitskalieren; Mengenmodell vereinheitlichen (Konfig-Menge **pro Einheit**) |
| K-08 | Hoch | `frontend/src/lib/services/orderService.ts:102-108`; `frontend/src/routes/(store)/shop/checkout/+page.svelte:579-585` | `matrix_config_id/url` gelangen nicht in die Bestellung | ERP-Auftrag ohne Schließplan; Code B–F serverseitig nicht prüfbar; manuelle Nacharbeit/Fehlfertigung | Schließplan als First-Class-Objekt an Bestellung (ID + Snapshot); Server klassifiziert Code und bepreist |
| K-09 | Hoch | CFG Z. 566-617, 1354-1362; `backend/items_svc/src/jtl/jtl_helper/evaluated_data.py:41-145,307-339`; `frontend/src/lib/services/configuratorCylinders.ts:254-270` | Längen aus Variationen ignoriert; kombinierte Längengruppen erzeugen doppelte bzw. falsche Items; keine Prüfung gültiger Kombinationen [datenabhängig] | Fehlender oder doppelter Längenaufpreis, falsche oder fehlende Länge in der Bestellung (S14/S15) | Server-API „verfügbare Längen“ liefert **bestellbare Kombination** (Item-ID oder Variation-ID) statt freier außen/innen-Werte; Bestellung referenziert diese |
| K-10 | Hoch | CFG Z. 1005-1119, 1364-1380; `frontend/src/lib/utils/determineLockPlanType.ts:281-292` | Schließcode per Regex (Fallback Einzelbuchstabe) aus SKU/Name; B→C; Fehler still; F = Auffangklasse | Falscher/fehlender Code-Aufpreis; vor 23.09. vermutlich nur C [datenabhängig] | Code-Zuordnung als Stammdatum in JTL (Merkmal/Custom Field „Schließcode“); Klassifikation serverseitig mit Golden-Tests; unbekannt → Fehler statt F |
| K-11 | Hoch | CFG Z. 1503-1508, 1787-1793, 1902-1915 | Nicht auflösbare Zylinder/Schlüssel werden übersprungen (Warnung nur in DEV); Fallback legt Basisprodukt ×1 in den Warenkorb | Unvollständige Bestellungen, zu niedriger Preis, Kunde merkt es nicht | Fehler sichtbar machen und Add-to-Cart abbrechen; serverseitige Auflösung |
| K-12 | Hoch | `backend/business/src/business_api/utils/items.py:40`; `backend/business/src/business_api/utils/order.py:213,400-401` | MwSt 19 % fix für Artikel, Konfig-Artikel und Versand; keine Länderlogik | Falsche Beträge bei 7 %/0 %-Artikeln (S12), EU-/Drittlandlieferungen falsch versteuert | Steuersatz je Artikel aus `tax_class` + Lieferland (OSS) serverseitig; Netto als führender Wert, Brutto gerundet je Position |
| K-13 | Mittel | `frontend/src/routes/(store)/shop/checkout/+page.svelte:204-215`; `frontend/src/lib/components/shop/CartShippingCalculator.svelte:90`; `frontend/src/lib/components/checkout/CheckoutShippingMethod.svelte:92`; `backend/business/src/business_api/utils/order.py:387-399` | Versandregeln dreifach im FE, abweichend im BE (`free_shipping_at=0`, IDs 22/43, Bemessungsgrundlage) | Abweichende Versandkosten (S5/S6) | Versandkosten nur per Server-Quote; FE zeigt Quote an |
| K-14 | Mittel | `frontend/src/lib/stores/productStore.ts:409-417,1582-1627`; `frontend/src/lib/components/shop/FreeGiftCarousel.svelte:85`; `frontend/src/routes/(store)/shop/cart/+page.svelte:31-63` | `isGift` nicht persistiert; Schwelle 200 € nur auf der Warenkorbseite; Backend kennt keine Geschenke | Geschenk nach Reload kostenpflichtig (S16) oder ohne Schwelle (S17) | Geschenk als Promotion-Regel serverseitig (Schwelle, max. 1, Preis 0, eigene Positionsart) |
| K-15 | Mittel | CFG Z. 2031-2240 vs. 1390-1450, 1700-1895; `frontend/src/lib/components/configurator/ItemCard.svelte:23` | Preisanzeige im Konfigurator („TEMP test pricing“) nutzt andere Schlüssel- und Kategorieauflösung als der Warenkorb, keine Gesamtsumme | Kunde sieht abweichende Preise (S13) | Anzeige ausschließlich aus Server-Quote derselben Konfiguration |
| K-16 | Mittel | `frontend/src/lib/stores/productStore.ts:210-225,1316-1341`; `backend/business/src/business_api/utils/items.py:34-47` | JTL-Konfigurator-Flags (`price_bool`, `default_price`, `rabatt_bool`, `surcharge_bool`, `ignore_multiplier`, `amount_*`) ignoriert; Konfig-Artikel ohne Kundengruppenpreis | Abweichung zu JTL-Preislogik [datenabhängig] | JTL-Semantik dokumentieren und im Server-Pricing abbilden |
| K-17 | Mittel | `frontend/src/lib/utils/cartCalculations.ts` (gesamt); `backend/business/src/business_api/utils/items.py:117`; `backend/business/src/business_api/jtl_helper/raw_order.py:245-251` | Float-Arithmetik, keine Rundung pro Position, Netto per Division | Cent-Differenzen Anzeige/Stripe/Rechnung (S11) | Integer-Cent bzw. `Decimal`; Rundungsregel (pro Position, kaufmännisch) einmal im Server |
| K-18 | Mittel | CFG Z. 5-11, 160, 326, 340-342; `frontend/src/lib/mocks/index.js` | Mock-Daten im produktiven Konfigurator | Fehlende Modifikationen, Mock-Defaults in Konfigurationen | Mocks aus Produktivpfad entfernen; Daten nur aus API |
| K-19 | Mittel | `frontend/src/lib/services/configuratorKeys.ts:46-62,255,360,425-427`; CFG Z. 1424; `frontend/src/routes/(store)/shop/cart/+page.svelte:31`; `backend/business/src/business_api/utils/order.py:387-391,419` | Hartcodierte JTL-IDs und Geschäftsregeln im Code | Jede Stammdatenänderung bricht Preise/Logik | Regeln als Stammdaten (JTL-Merkmale/CMS-Konfiguration) und serverseitig |
| K-20 | Mittel | `backend/business/src/business_api/utils/order.py:276-283,311-333`; `backend/business/src/business_api/jtl_helper/order_handling.py:31-54,95-116` | Konfig-/Variationszuordnung als `dict[item_id]` → gleiche Artikel in mehreren Positionen überschreiben sich | Falsche Konfig-Zuordnung im ERP (S18) | Zuordnung über Positionsindex/eindeutige Line-ID |
| K-21 | Mittel | `backend/business/src/business_api/v1/endpoints/order.py:64-75,102-107,153-163`; `backend/business/src/business_api/v1/endpoints/configurator.py:15-64`; `backend/business/src/business_api/v1/endpoints/cart.py:12-62` | Unauthentifizierte/IDOR-Endpunkte (fremde Bestellung überschreiben/lesen, fremde Konfigurationen/Warenkörbe) | Datenschutz (Adressen), Manipulation fremder Bestellungen | AuthN/AuthZ (Owner-Check), keine Adressierung per sequenzieller Nummer |
| K-22 | Mittel | `backend/payment_svc/src/payment/v1/endpoints/intents.py:19-21,35`; `backend/payment_svc/src/payment/schemas/pricing.py` | Dokumentierte Preisvalidierung existiert nicht; `amount=None` wird an Stripe übergeben, wenn die Order nicht gefunden wird | Falsche Sicherheitsannahme; 500er | Validierung implementieren oder Doku korrigieren; Fehlerfall sauber behandeln |
| K-23 | Niedrig | `frontend/src/lib/services/orderService.ts:73`; `backend/business/src/business_api/utils/order.py:501` | Gutscheincode wird angenommen, aber ignoriert | Kundenbeschwerden | Feld entfernen oder serverseitig implementieren |
| K-24 | Niedrig | `frontend/src/lib/stores/productStore.ts:530-532`; `frontend/src/lib/utils/priceCalculations.ts:63-76` | UVP einmal als netto (×R), einmal als brutto behandelt | Falsche Streichpreise (×1,19) | Eine Semantik festlegen |
| K-25 | Niedrig | `frontend/src/lib/stores/productStore.ts:543,2053-2055` | `inStock` immer true; `cartTotal` (price×n) inkonsistent | Fehlanzeigen | Entfernen bzw. auf Server-Quote umstellen |
| K-26 | Niedrig | `frontend/src/lib/services/cartService.ts:53-63`; `backend/business/src/business_api/schemas/cart.py:4-26` | FE sendet `variation_ids` als `number[]`, BE erwartet `{id,data_id}` → 422 (Fehler nur in Konsole) | DB-Warenkorb eingeloggter Nutzer mit Variationsartikeln wird nicht gespeichert | Einheitliches Schema, Fehler behandeln |
| K-27 | Niedrig | `Readme.md:22-26` | Zugangsdaten im Repo: Stripe-Test-Keys (`sk_t…`, `whse…`, `pk_t…`), Custom-API-Token (`15e1…`) | Missbrauch der Test-/Custom-API möglich | Rotieren, aus Repo + Historie entfernen, Secret-Scanning |
| K-28 | Mittel | `frontend/src/demo.spec.ts` (einziger FE-Test); keine Tests zu `backend/business/src/business_api/utils/items.py` | Keine Preis-/Konfigurationstests | Regressionen (siehe 2.10) | Golden-Tests (Abschnitt 4.4) als Merge-Gate |
| K-29 | Mittel | `frontend/src/lib/components/shop/ProductConfigGroups.svelte:126-130,184-192,225-262` | Automatische Wahl des günstigsten Items in **jeder** Gruppe; alle Gruppen Pflicht; Netto-Anzeige mit Steuersatz des Hauptartikels | Ungewollte Zusatzartikel, abweichende Netto-Anzeige | Pflicht/optional und Defaults aus JTL (`selected`, `amount_init`, `recommended`) übernehmen |

---

## 4. Empfehlungen

### 4.1 Sofortmaßnahmen (vor jeder weiteren Funktionsarbeit)
1. **K-01:** `mark_paid`, `send_order_to_system` und `single_order` per Traefik-Regel von außen sperren. Interner Aufruf nur mit Shared Secret.
2. **K-02:** Im Intents-Endpunkt bei bestehendem Intent den aktuellen `StillToPay` mit `intent.amount` vergleichen und bei Abweichung `PaymentIntent.modify` aufrufen bzw. den Intent neu erzeugen. Im Webhook `amount_received == StillToPay` prüfen, sonst Bestellung in Klärung. Die ERP-Zahlung muss den tatsächlichen Betrag buchen.
3. **K-03/K-04:** Versand nur per ID, Preis serverseitig. `quantity ≥ 1`, Konfig-Artikel gegen die `config_groups` des Hauptartikels prüfen. Unbekannte Artikel → 422.
4. **Nachprüfung der Bestellungen** in den Zeitfenstern aus 2.10:
   - 17.08.–09.09.: Matrix-Mengen > 1.
   - 08.09.–09.09.: Shop-Konfig-Mengen > 1.
   - bis 23.09.: Schließcodes D/E/F.
   - laufend: Abweichung Stripe-`amount_received` ↔ `TotalGrossAmount`.
   Abgleich Stripe-Payouts gegen ERP-Rechnungen.
5. Secrets aus `Readme.md` rotieren (K-27).

### 4.2 Zielbild: Logik ins Backend (Single Source of Truth)

| Gehört ins Backend | Bleibt im Frontend |
|---|---|
| Artikel-/Kundengruppen-/Sonderpreise, Steuersatz (Artikel + Lieferland), Rundung | Darstellung, Formatierung, Brutto/Netto-Umschalter (nur Anzeige der Server-Werte) |
| Konfig-Validierung (Gruppen, Pflicht, min/max, Kombinationen, Längenpaare) | UI-Vorvalidierung als Komfort |
| Schließplan-Klassifikation (B–F) und Code-Artikel, Schlüssel-Aufteilung (Standard/Mehrschlüssel), Farbkappen | Matrix-Editor |
| Versandkosten (Land, Versandklassen, Freigrenze, Speditionsregeln) | Auswahl der Versandart-ID |
| Promotionen (Geschenke, Gutscheine) | Auswahl des Geschenks |
| Zahlbetrag, Intent-Lebenszyklus, Zahlungsabgleich | Stripe-Element |

Der Warenkorb speichert nur Auswahl-IDs. Jede Preisangabe im Frontend stammt aus einer **Quote** des Servers mit `quote_id` und `pricing_version`.

### 4.3 API-Entwurf

**1) `POST /api/pricing/quote`**: zustandslos, für Produktseite, Konfigurator, Warenkorb und Checkout.
```jsonc
// Request
{
  "currency": "EUR",
  "ship_to_country": "DE",                // optional, für Steuer/Versand
  "shipping_method_id": 12,               // optional
  "lines": [
    {
      "line_ref": "L1",
      "item_id": 4711,
      "quantity": 2,
      "variations": [{"variation_id": 10, "value_id": 55}],
      "variation_inputs": [{"variation_id": 11, "value": "Haustür"}],
      "config": [{"group_id": 300, "item_id": 9001, "quantity_per_unit": 1}]
    }
  ],
  "lock_plan": {                           // optional, großer Konfigurator
    "config_id": "a1b2…",                  // oder inline:
    "series": "keso-8000",
    "cylinders": [{"ref":"C1","model_item_id":123,"length":{"combination_id":"item:5555"},
                   "options":[{"group_id":301,"item_id":9100}],"quantity":1}],
    "keys": [{"ref":"K1","model_item_id":456,"color_item_id":789,"quantity":5}],
    "matrix": [["C1","K1"]]
  },
  "promotions": {"gift_item_id": 777, "coupon": null}
}
// Response
{
  "quote_id": "q_…", "pricing_version": "2026-10-07.1", "valid_until": "…",
  "customer_group_id": 3,
  "lock_plan": {"code": "E", "code_item_id": 9301, "warnings": []},
  "lines": [{
    "line_ref": "L1", "item_id": 4711, "quantity": 2,
    "unit": {"net_cents": 7479, "gross_cents": 8900, "tax_rate": "19.00"},
    "components": [
      {"type":"base","item_id":4711,"net_cents":7479},
      {"type":"variation","value_id":55,"net_cents":840},
      {"type":"config","item_id":9001,"qty":2,"net_cents":1680},
      {"type":"lock_code","item_id":9301,"qty":2,"net_cents":2520}
    ],
    "total": {"net_cents": 25038, "tax_cents": 4757, "gross_cents": 29795},
    "discounts": [{"source":"special_price","amount_net_cents":0}]
  }],
  "shipping": {"method_id": 12, "gross_cents": 595, "free_from_gross_cents": 9900, "applied_free": false},
  "totals": {"net_cents": …, "tax": [{"rate":"19.00","tax_cents":…}], "gross_cents": …},
  "errors": [{"line_ref":"L1","code":"CONFIG_ITEM_NOT_IN_GROUP"}]
}
```

**2) `POST /api/configurator/validate`**: Plan → Code, erlaubte Kombinationen, Fehler. Es kann auch Teil der Quote sein.

**3) `GET /api/configurator/series/{series}/options`**: Typen, Modelle, **bestellbare Längenkombinationen** (je eine ID für Item oder Variation), Optionen, Schlüsselmodelle, Farben, Aufteilungsregel. Ersetzt Mocks und Regex.

**4) `POST /api/order/`**: akzeptiert `quote_id`, Adressen, `shipping_method_id` und `payment_method`.
- Der Server re-quotet, vergleicht und friert die Bestellung ein (`status=pending_payment`, `amount_cents`).
- Bei Preisabweichung antwortet er mit 409 und der neuen Quote.
- Keine Versandpreise und keine `order_id` vom Client. Updates nur durch den Owner und nur im Status `draft`.

**5) `POST /api/payment/intents/`**: verwendet `order.amount_cents`. Ändert sich die Bestellung, wird der Intent aktualisiert oder neu erzeugt. `metadata.order_id` wird gesetzt.

**6) Webhook → interner Statuswechsel:** Er prüft `amount_received`, Währung und `metadata.order_id`. `mark_paid` ist nur intern erreichbar.

### 4.4 Pflicht-Testfälle (Golden-Tests)

Golden-Datensatz mit festen Artikeln (Netto, Steuersatz, Kundengruppen-/Sonderpreise, Konfig-Gruppen inkl. JTL-Flags, Längen als Variation **und** als Konfig-Gruppe, Schließcode-Artikel, Versandarten). Jeder Test prüft **identische Cent-Beträge** für Quote, Bestellung, Stripe-Betrag und ERP-Positionssumme.

**Preise allgemein**
1. Einzelartikel ohne Optionen, n=1 und n=3 (Rundung je Position, z. B. Netto 8,40 €).
2. Artikel mit 7 % und mit 0 % Steuersatz; Lieferland DE, AT (OSS), CH (Drittland).
3. Variation mit Aufpreis (brutto/netto-Konsistenz `price` vs. `netto`).
4. Kundengruppenpreis, persönlicher Rabatt, Sonderpreis aktiv/inaktiv/abgelaufen/anderer Kanal, Gast vs. eingeloggt.
5. UVP-Anzeige (Semantik netto/brutto).

**Kleiner Konfigurator**
6. Konfig-Artikel mit `quantity_per_unit=2`, Hauptmenge n=3 → 6 Stück.
7. `ignore_multiplier=true` → Menge unabhängig von n.
8. `default_price`/`price_bool` gesetzt (Preis-Override).
9. Basispreis = Konfig-Summe (ehemalige Heuristik): beide müssen berechnet werden.
10. Gruppentypen 0–3, Pflicht/optional, `amount_min/max` verletzt → Fehler.
11. Konfig-Artikel, der nicht zur Gruppe gehört → 422.

**Großer Konfigurator / Schließplan**
12. Klassifikation B/C/D/E/F mit mindestens 2 Matrizen je Code, plus Grenzfälle: 1×1, Schlüssel ohne Zylinder, Zylinder ohne Schlüssel (Fehler), nicht klassifizierbar (Fehler statt F).
13. Code B → Artikelzuordnung (fachlich klären: B-Artikel oder C-Artikel).
14. Zwei identische Zylinder in einem Plan → 2× alle Aufpreise.
15. Gleicher Zylinder in zwei verschiedenen Plänen → getrennte Positionen.
16. Länge als Variation, als getrennte Gruppen außen/innen und als kombinierte Gruppe; ungültige Kombination → Fehler.
17. Schlüssel: Menge 1, 3, 4, 10, jeweils für einen Zylinder bzw. für mehrere Zylinder (3+n-Regel) und mit/ohne Farbkappe.
18. Serie mit Mengenmodus („schlüsselmenge“), Formmodus („schlüsselform“), Keso-6000-Farbkappen.
19. Konfiguration ändern → Warenkorb ersetzt exakt die Positionen dieses Plans.
20. Schließplan-ID und Snapshot sind in Bestellung und ERP vorhanden.

**Versand, Geschenke, Gutscheine**
21. Freigrenze knapp darunter/darauf/darüber (99,99/100,00), `free_shipping_at=0`, Spedition 22/43.
22. Manipulierter Versandpreis im Request → ignoriert.
23. Geschenk ab 200 €: Reload, Login-Merge, Unterschreiten der Schwelle, Geschenk ohne Schwelle per API → abgelehnt.
24. Gutscheincode → definiertes Verhalten.

**Sicherheit/Zahlung**
25. Negative und Null-Mengen → 422.
26. Änderung der Bestellung nach Intent-Erzeugung → Intent-Betrag aktualisiert bzw. Zahlung abgelehnt.
27. Webhook mit abweichendem `amount_received` → Bestellung nicht „bezahlt“.
28. `mark_paid` von außen → 401/404.
29. Fremde `order_id` im Upsert → 403.
30. Rundungs-Property-Test: Σ Positionen (gerundet) == Gesamtsumme == Stripe-Cents für zufällige Warenkörbe.

### 4.5 Migrationspfad
1. Sofortmaßnahmen 4.1 (Tage).
2. Server-Pricing-Modul in `business_api`. Zunächst werden die heutigen Backend-Formeln + Golden-Tests 1–11, 21–30 abgebildet. Das Frontend zeigt Quote und eigenen Wert parallel und loggt Abweichungen (Shadow-Mode).
3. Schließplan-Klassifikation und Längen- bzw. Schlüsselauflösung ins Backend (Tests 12–20). Mocks und Regex entfernen, Stammdaten-Merkmale in JTL anlegen (Schließcode, Mehrschlüssel-Zuordnung, Farbkappen).
4. Frontend-Preislogik (`cartCalculations.ts`, `ProductPrice.svelte`-Summen, Konfigurator-`priceMap`, Shop-`addToCart`-Preise) entfernen. Die Anzeige erfolgt nur noch aus Quotes.
5. Golden-Tests als Pflicht-Gate im CI. Jede Preisänderung muss Frontend und Backend in **einem** PR betreffen bzw. nur noch das Backend.
