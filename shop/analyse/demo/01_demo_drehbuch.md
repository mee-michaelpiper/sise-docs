# Sicherheits-Demo für die Geschäftsführung – Drehbuch

> **Zweck:** Der Geschäftsführung in einer Live-Demo zeigen, dass ein normaler Kunde mit Bordmitteln des Browsers Preis, Warenkorb und Zahlung des Shops manipulieren kann. Ziel ist Sensibilisierung und die Freigabe der Stabilisierung (siehe `00_gesamtbericht.md`, Abschnitt 5).
> **Autorisierung:** Dies ist eine beauftragte Sicherheitsprüfung am eigenen System. Sie wird ausschließlich gegen eine Test-/Staging-Umgebung mit Stripe-Testmodus und ausschließlich mit eigenen Testbestellungen durchgeführt.
> **Stand:** 07.10.2026 · vertraulich, nicht für externe Dienstleister

---

## 0. Vor der Demo – unbedingt vorbereiten

| # | Punkt | Warum |
|---|---|---|
| 1 | **Nur Test-/Staging** oder lokaler Stack (`docker-compose.yml` aus dem Repo). Niemals Produktion. | In Prod entstünden echte Bestellungen und echte Datenschutzvorfälle. |
| 2 | **Stripe-Testmodus** (die Keys in der `Readme.md` sind bereits `sk_test`/`pk_test`). Testkartennummer `4242 4242 4242 4242`, beliebiges Datum/CVC. | Keine echten Zahlungen. |
| 3 | **Nur eigene Testbestellungen.** Keine fremden Bestellnummern abrufen. | Fremde Daten wären auch intern ein DSGVO-Vorfall (S-01). |
| 4 | **Kurze Autorisierungs-Mail** vorab an GF/CTO: „Autorisierte Sicherheitsdemonstration am … gegen die Testumgebung.“ | Macht aus „Hacken“ eine beauftragte Prüfung. |
| 5 | **Jede Station einmal trocken** durchspielen und je einen Screenshot/Screencast als Fallback aufnehmen. | Falls live etwas klemmt, bricht der Vortrag nicht ab. |
| 6 | Browser mit **offenen DevTools** vorbereiten (Tab „Network“ und „Application“), Zoom hoch, damit es im Beamer lesbar ist. | Lesbarkeit. |

**Grundtechnik vorab erklären (ein Satz für die GF):** „Ich benutze nur den Browser, den jeder hat. Keine Spezialsoftware.“

### Die zwei Werkzeuge, die du brauchst

1. **Application → Local Storage** (im DevTools-Tab „Application“, links „Local Storage“ → die Shop-Domain). Hier kann man gespeicherte Werte direkt anlegen/ändern. Braucht man für Station 5.
2. **Request wiederholen statt abfangen.** Die DevTools können einen *laufenden* Request nicht umschreiben. Man fängt ihn nicht ab, sondern **schickt eine geänderte Kopie**:
   - Network-Tab → den betreffenden Request mit Rechtsklick → **Copy → Copy as fetch** (Chrome) bzw. **Copy Value → Copy as fetch** (Firefox).
   - In die **Console** einfügen, im `body` den gewünschten Wert ändern, Enter. Die (manipulierte) Bestellung wird durch diese Kopie angelegt.
   - Alternativ **Copy as cURL** → im Terminal den Wert ändern → ausführen. Oder den Request in **Postman** nachbauen.
   - Für echte „Abfangen und ändern“-Optik: ein lokaler Proxy wie **Requestly**, **mitmproxy** oder **Burp** im Testnetz. Für die GF-Demo ist „Copy as fetch“ anschaulicher, weil man den Vorher/Nachher-Wert direkt sieht.

> Wichtig für die Aussage: Der Shop prüft diese Werte **serverseitig nicht**. Deshalb genügt die geänderte Kopie – das Backend nimmt sie an.

---

## Dramaturgie

Eskalierend von „passiert jedem Kunden“ zu „Bezahlen ist optional“. Sieben Stationen, ca. 15–20 Min.

---

### Station 0 – Der ehrliche Kauf *(Baseline, 30 Sek.)*

**Ablauf:** Normalen Artikel in den Warenkorb, Checkout, mit Stripe-Testkarte zahlen. Bestellnummer und Gesamtbetrag notieren/zeigen.
**Kernsatz:** „Merken Sie sich diesen Betrag und diese Bestellnummer.“
**Technik:** keine. Nur die Referenz.

---

### Station 1 – Ein Produkt, zwei Preise *(K-01 im Bericht 02 / ohne Manipulation)*

**Was es zeigt:** Derselbe Konfigurationsartikel kostet im kleinen Konfigurator (`/shop/…`) etwas anderes als im großen (`/configurator/configure/…`). Das ist kein Angriff, das trifft jeden echten Kunden.
**Ablauf:**
1. Gleiche Schließanlage/gleichen Zylinder in beiden Oberflächen zusammenstellen.
2. Beide Endpreise nebeneinander zeigen.
**Fundstelle zum Nachlesen:** `02_konfiguratoren_preislogik.md`, Abschnitt 2.5–2.7. Die sieben getrennten Berechnungen sind dort als Formeln A–K aufgeführt.
**Kernsatz:** „Niemand hat etwas manipuliert. Trotzdem stimmt der Preis nicht mit sich selbst überein – das sind die Verluste der letzten Monate.“

---

### Station 2 – Der Warenkorb rechnet sich selbst schön *(K-06)*

**Was es zeigt:** Eine Toleranzregel setzt den sichtbaren Grundpreis auf 0, wenn er zufällig der Summe der Konfig-Artikel entspricht (±0,01 €). Anzeige und tatsächlich gebuchter Betrag können auseinanderlaufen.
**Fundstelle:** `frontend/src/lib/utils/cartCalculations.ts:92` (`Math.abs(rawBase - configItemsGrossSum) < 0.01`).
**Ablauf:** Eine Konstellation wählen, in der die Regel greift (siehe Szenario in 02, Abschnitt 2.7), und Warenkorb-Anzeige vs. Bestätigung gegenüberstellen.
**Kernsatz:** „Der Kunde sieht einen anderen Preis, als gebucht wird. Das ist auch rechtlich heikel.“

---

### Station 3 – Der Kunde bestimmt die Versandkosten *(K-03, bestätigt)*

**Was es zeigt:** Versandpreis (`GrossPrice`) und Versandkostenfrei-Grenze (`free_shipping_at`) reisen im Bestell-Request mit; das Backend übernimmt beide ungeprüft in die Gesamtsumme.
**Beleg:** `backend/business/src/business_api/utils/order.py:393-411` – `shipping_price_gross = checkout_data.shipping_method.gross_price if total_gross < free_shipping_at or is_always_paid_shipping else 0`, danach `total_with_shipping = _items_gross + shipping_price_gross`. Kein Abgleich gegen die hinterlegte Versandart (nur `id` wird gespeichert).

**Wichtig – drei Stolperfallen (sonst scheitert die Vorführung):**
1. Der POST auf `/api/order/` ist **`multipart/form-data`**, nicht JSON (`orderService.ts:110`): Feld `payload` = JSON-String plus `files`. Der Wert muss **im `payload`-JSON** geändert werden, nicht als separater Body.
2. Pfad im Payload: `checkout_data.shippingMethod.GrossPrice` bzw. `.free_shipping_at` (genaue Schreibweise, Alias `GrossPrice`).
3. Versand fällt nur an, wenn **Warenkorbsumme < `free_shipping_at`** (Default 99). Darum einen **Warenkorb unter der Freigrenze** nehmen – sonst ist der Versand ohnehin 0 und die Änderung bleibt wirkungslos.

**Ablauf:**
1. Günstigen Warenkorb (unter Freigrenze) in den Checkout bringen, Versandart **ohne** die Spedition-IDs 22/43 wählen (die werden immer berechnet).
2. Den `POST /api/order/` mit einem lokalen Proxy im Testnetz (mitmproxy/Requestly/Burp) abfangen oder in Postman nachbauen; im `payload`-JSON `checkout_data.shippingMethod.GrossPrice` auf 0 setzen.
3. Die erzeugte Bestellung öffnen: `total_with_shipping` ist um den Versand reduziert.
**Kernsatz:** „Ab hier manipuliere ich bewusst – der Server glaubt mir die Versandkosten.“

---

### Station 4 – Mengen und fremde Positionen *(K-04)*

**Was es zeigt:** Mengen dürfen 0 oder negativ sein, und beliebige Artikel werden als Konfig-Position (`config_group_items`) akzeptiert, ohne Prüfung der Zugehörigkeit.
**Fundstelle:** `frontend/src/lib/types/order.ts:46` (`quantity`), `:49` (`config_group_items`); Bewertung in 02, K-04.
**Ablauf:** Wie Station 3 den `/api/order/`-Request kopieren, im `body` `quantity` bzw. die `config_group_items` verändern, resenden, Ergebnis zeigen.
**Kernsatz:** „Das Backend prüft nicht, ob das überhaupt zusammengehört – es vertraut dem Browser.“

---

### Station 5 – Jeder macht sich zum Großkunden *(K-05)*

**Was es zeigt:** Die Kundengruppe liegt im `localStorage`. Wer sie setzt, bekommt die Sonderpreise dieser Gruppe – ganz ohne Request-Manipulation, weil das Frontend den Wert anhängt.
**Fundstelle:** Schlüssel `customer_group_id` in `frontend/src/lib/stores/customerGroupStore.ts:9`; wird nach Login gesetzt (`sessionService.ts:107`) und an die Preisabfrage gehängt (`productStore.ts:732`, Auswahl der Sonderpreise `:511-521`).
**Ablauf (ohne Request-Eingriff):**
1. DevTools → **Application → Local Storage** → Shop-Domain.
2. Neuen Eintrag anlegen: Schlüssel `customer_group_id`, Wert = die ID einer Gruppe mit Sonderkonditionen (eine gültige ID aus euren Testdaten; in der Demo z. B. die B2B-Gruppe).
3. Seite neu laden, dasselbe Produkt ansehen – der (niedrigere) Sonderpreis erscheint. Optional: Produktpreisabfrage im Network zeigen, dass jetzt `customer_group_id` mitgeht.
**Kernsatz:** „Diese eine Zeile im Browser macht jeden Besucher zum Großkunden.“

---

### Station 6 – Bezahlen ist optional *(K-01, der Höhepunkt)*

**Was es zeigt:** Der Endpunkt, der eine Bestellung als „bezahlt“ markiert, ist ohne Anmeldung erreichbar und gleicht nichts mit Stripe ab. Die Bestellung geht danach als „bezahlt“ ins ERP.
**Fundstelle:** `backend/business/src/business_api/v1/endpoints/order.py:110-138` – `PATCH /{order_id}/mark_paid`, keine Auth, `status` default „paid“, setzt `StillToPay = 0`, ruft `send_order_to_systems(order)`. Erreichbar über Traefik unter `/api`.
**Ablauf (nur mit deiner eigenen Testbestellung):**
1. Neue Testbestellung anlegen (wie Station 0), aber **nicht** bezahlen. Die eigene `order_id` notieren.
2. Einen **PATCH auf `/api/order/<deine-order-id>/mark_paid`** schicken (per Copy-as-fetch von einem bestehenden Order-Request abgeleitet, oder per Postman). Body enthält `status: "paid"` und ein `intent_id` (das Handler-Feld ist Pflicht, beliebiger Testwert genügt).
3. Bestellung öffnen: Status „bezahlt“, offener Betrag 0. Im (Test-)ERP erscheint die Bestellung als bezahlt.
**Kernsatz:** „Die Ware wird versandt. Bezahlt wurde nie. Und im JTL steht trotzdem eine Zahlung.“
**Guardrail laut sagen:** „Ausschließlich meine eigene Testbestellung, Testsystem.“

**Nebenbefund (bestätigt im Test, 07.10.2026): `mark_paid` zerstört die echten Zahlungsdaten.**
`mark_paid` lädt die Bestellung, ändert die Zahlungsfelder und schreibt sie per `insert_or_replace_one(order, id_field="_id")` komplett zurück ([order.py:132](../../../webshop/backend/business/src/business_api/v1/endpoints/order.py:132)). Das zurückgeschriebene Dict enthält das Feld der Zahlungsmethode nicht mehr — die **tatsächliche Methode geht verloren** (im Test beobachtet: „PayPal“ → „Unbekannt“, Status → „Unbekannt“). Der ungeschützte Endpunkt fälscht also nicht nur den Zahlstatus, er **korrumpiert** die Bestelldaten. Für die Demo: zeigt zusätzlich zur Zahlungs-Fälschung einen unwiederbringlichen Datenverlust. Hinweis: Eine beliebige Zahlungsmethode lässt sich separat bereits **bei der Bestellanlage** über den Client-Payload (`checkout_data.paymentMethod` / `paymentMethodType`) setzen; dieser Nebenbefund betrifft die Zerstörung beim nachträglichen „Bezahlt“-Setzen.

---

### Station 7 – Auch ehrliche Kunden kosten Geld *(K-02)*

**Was es zeigt:** Der Stripe-Zahlvorgang wird beim ersten Mal erzeugt und wiederverwendet; die Bestellung wird aber bei jedem Checkout-Schritt neu berechnet. Wer nach dem Erzeugen des Zahlvorgangs den Versand oder den Warenkorb ändert, zahlt den alten Betrag – das ERP bucht den neuen als bezahlt.
**Fundstelle:** `backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33`; Bewertung 02, K-02.
**Ablauf:** Checkout starten (Zahlvorgang entsteht), dann Versandart/Warenkorb ändern, zahlen. Differenz zwischen gezahltem Betrag und ERP-Betrag zeigen. **Keine Manipulation nötig.**
**Kernsatz:** „Selbst ohne Angreifer stimmt die Buchhaltung nicht – deshalb die Differenzen der letzten Monate.“

---

## Schluss-Folie (nach Station 7)

Drei Zeilen, dann Schnitt:

- **Was:** Preise falsch · Zahlung umgehbar · Kundendaten offen *(S-01/S-03 nur benennen, nicht live mit echten Daten zeigen)*.
- **Warum:** keine zentrale Preisberechnung · keine Berechtigungsprüfung · Geschäftslogik im Browser statt im Backend.
- **Was wir brauchen:** Sofortmaßnahmen in 24–72 h + 4 Wochen Stabilisierung (`00_gesamtbericht.md`, Abschnitt 4 und 5).

## Bewusst NICHT live zeigen

- **Fremde Kundendaten (S-01).** Nur benennen. Höchstens an der eigenen Testbestellung andeuten. Fremde Bestellnummern durchzuprobieren ist ein echter DSGVO-Vorfall – auch im eigenen Haus.
- **Den PDF-Dienst (S-03)** nicht zur Codeausführung bringen. Benennen genügt.
- **Keine Aufzeichnung mit echten Daten** herumreichen.
