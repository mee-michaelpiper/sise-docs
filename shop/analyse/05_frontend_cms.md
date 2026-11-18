# 05 – Analyse Frontend (SvelteKit) & CMS (Strapi)

Stand: 2026-10-07, Repo-HEAD `9cc4a13` (main, 2026-10-05). Alle Fundstellen relativ zum Repo-Root `webshop/`.
Analyse rein lesend: kein `npm install`, kein Netzwerk. Secrets nur gekürzt (erste 4 Zeichen + „…“).

---

## 1. Zusammenfassung

- **Architektur:** Das Frontend ist eine reine **Client-SPA**: SvelteKit 2.17 / Svelte 5.28 mit `adapter-static` und `fallback: index.html`, ausgeliefert über nginx. Ohne explizites `prerender` wird nur `/410` vorgerendert. SSR läuft nur im Dev-Server. Darum läuft `frontend/src/hooks.server.ts` (Tracking-Attributions-Cookies) in Produktion **nie**. SEO hängt an clientseitigem Rendering, an Traefik-ForwardAuth (`/_check_page`) und an der `sitemap.xml` im Python-Backend.
- **API-Schicht:** Es gibt keine zentrale API-Schicht. rund 50 direkte `fetch`-Aufrufe sind über 25 Dateien verteilt (dazu 17 Aufrufe über `fetchAllConfiguratorItems`) (Services, Stores, Svelte-Seiten, Komponenten). Base-URL: **relative Pfade `/api/...` auf derselben Domain**, das Routing macht Traefik per `PathPrefix` (`/api/items`, `/api/auth`, `/api/payment` und Rest `/api` → business_api). Strapi läuft per Konvention unter `cms.<host>` (Subdomain-Ableitung aus `window.location`), optional per `VITE_STRAPI_URL`. **Die .NET-API muss 26 Endpunkte (inkl. Strapi-unabhängiger Infrastruktur-Endpunkte) mit exakt den heutigen Pfaden, Feldnamen und teilweise PascalCase-JTL-Strukturen bedienen** oder das Frontend muss angepasst werden.
- **Kernproblem des Vertrags:** Den Katalog liefert ein **generischer Abfrage-Endpunkt** (`POST /api/items/items/` mit frei wählbaren Filtern, Feldern und Sortierung, `excluded_fields`). Das Frontend baut an 30+ Stellen eigene Queries und bekommt **Rohdaten des ERP** (inkl. `purchase_price_net` = Einkaufspreis, Sonderpreislisten aller Kundengruppen, Lieferanten). Das 1:1 nachzubauen wäre ein Risiko. Empfehlung: fachliche Endpunkte (Produktliste, Detail, Suche, Konfigurator-Daten) plus Fassade für die Übergangszeit.
- **Businesslogik im Frontend:** Brutto-Preise, MwSt (Fallback fest **1,19**, 15+ Stellen, keine Länderlogik), Kundengruppen-Sonderpreise (ohne Datumsprüfung), Warenkorb- und Zwischensummen, Versandkosten- und Gratisversand-Schwelle (zwei widersprüchliche Quellen: Konstante 99 € vs. API `free_shipping_at`), Gratisgeschenk-Schwelle (200 €, nur Client), Lieferzeit, Verfügbarkeit (`inStock` faktisch immer `true`), Treuepunkte, Bestellstatistiken. Die **Kundengruppe** kommt aus `localStorage` und geht als `customer_group_id` im Request-Body mit, ist also manipulierbar.
- **Code-Qualität:** Acht Dateien haben über 1.000 Zeilen, Spitzenreiter sind Konfigurator-Seite (2.618), `/shop` (2.512) und `productStore.ts` (2.059). Fünf verschiedene Mapper von API-Item zu Produkt sind kopiert, `ApiItem`/`Product`/`Category` mehrfach definiert. Mock-Daten stecken im Produktivcode (`/configurator/results`, Footer). Es gibt nur **1 Dummy-Test**. Svelte 5 ist überwiegend mit Runes umgesetzt, aber gemischt mit 117 `svelte/store`-Stellen, 11× deprecated `$app/stores` und 90 `$effect` (teils asynchrones Daten-Laden ohne Abbruch).
- **Strapi:** Strapi 5 (package.json 5.30.1, **Lockfile 5.6.0, also nicht synchron**), 41 Single-Types (je Seite ein eigener Content-Type), 5 Collection-Types, Plugin `populate-deep`. **`.env` mit Secrets ist eingecheckt** und dieselben Werte stehen als Default in `docker-stack.yml`.
- **Branches:** 18 Remote-Branches sind formal nicht gemergt. Alle stammen von „DominicB“ (April bis Juni 2026) und sind laut Commit-Historie fast vollständig über **Squash-PRs** (Azure DevOps „Merged PR …“) in `main` gelandet. Die Branches sind Altlasten: nach Stichprobe löschen.

---

## 2. Technik-Inventar

### 2.1 Frameworks & Bibliotheken (`frontend/package.json`, aufgelöst aus `package-lock.json`)

| Paket | Deklariert | Installiert (Lock) | Bemerkung |
|---|---|---|---|
| svelte | ^5.28.2 | 5.28.2 | Runes global aktiv (`frontend/svelte.config.js:8`) |
| @sveltejs/kit | ^2.16.0 | 2.17.1 | |
| @sveltejs/adapter-static | ^3.0.8 | 3.0.8 | SPA-Fallback `index.html`, `precompress: true` |
| @sveltejs/vite-plugin-svelte | ^5.0.0 | 5.0.3 | |
| vite | ^6.0.0 | 6.0.11 | |
| typescript | ^5.5.0 | 5.7.3 | `strict: true` (`frontend/tsconfig.json`) |
| @inlang/paraglide-sveltekit | ^0.15.5 | 0.15.5 (paraglide-js 1.11.8) | Adapter gilt als Auslaufmodell (Paraglide 2.x ohne Adapter) |
| @stripe/stripe-js | ^8.4.0 | 8.4.0 | Payment Element |
| vitest | ^3.0.0 | 3.0.5 | |
| eslint / typescript-eslint / eslint-plugin-svelte | ^9.25 / ^8.32 / ^3.6 | 9.25.1 / 8.32.1 / 3.6.0 | Flat-Config |
| prettier (+ svelte, sort-imports) | ^3.4.2 | 3.4.2 | |
| svelte-check | ^4.0.0 | 4.1.4 | |

Weitere Laufzeit-Abhängigkeiten gibt es nicht (kein UI-Kit, keine Datums- oder HTTP-Lib, kein State-Lib). Externe Skripte kommen über `frontend/src/app.html`: CookieFirst (Consent), Google Tag Manager `GTM-K49H…`, eTrusted, Trusted Shops, Userlike (Chat) und Händlerbund (Widerrufsbutton).

**Build/Runtime:** `frontend/Dockerfile` baut mit `node:23.5` (ungerade Node-Version, kein LTS) und `npm install` (nicht `npm ci`). Danach `compile:messages` und `build`, Auslieferung mit `nginx:1.27` (`frontend/web_server/nginx.conf`: nur `try_files … /index.html`, keine Security-Header, kein Caching, keine gzip-Regeln, obwohl `precompress` aktiv ist). `VITE_STRIPE_PUBLISHABLE_KEY` wird als Build-Arg eingebrannt.

### 2.2 Rendering-Modus

- `adapter-static` mit `fallback: 'index.html'` (`frontend/svelte.config.js:13-20`). `prerender.entries: ['*','/410']`, aber nur `/410` hat `prerender = true` (`frontend/src/routes/(singlepages)/410/+page.ts:1`). Alle anderen Seiten sind SPA.
- `ssr = false` explizit nur in `(authenticated)` (`frontend/src/routes/(authenticated)/+layout.ts:6`). In Prod spielt das keine Rolle, weil ohnehin kein Node-Server läuft.
- `frontend/src/hooks.server.ts` (Paraglide-Handle und Attributions-Cookies `sn24_attribution_first_touch/last_touch`) **läuft in Produktion nicht** (nginx-Static). Wirksam ist nur clientseitige Param-Weitergabe (`navigationUtils`).
- 404/410/Redirects übernimmt **Traefik-ForwardAuth** gegen `business_api:/_check_page` (`compose/services/frontend/main.yml:20-27`, `backend/business/src/business_api/v1/endpoints/check_page.py:338`). Die `sitemap.xml` kommt vom Backend (`backend/business/src/business_api/v1/endpoints/sitemap.py:231`). **Beides muss die .NET-API mit übernehmen.**

### 2.3 Routing-Struktur (34 Seiten)

| Route | Datei | Zweck |
|---|---|---|
| `/` | `frontend/src/routes/+page.svelte`, `+page.js` | Startseite aus Strapi-Single-Type `home` (Dynamic Zone → `ComponentResolver`) |
| `/[singlepage]` | `(singlepages)/[singlepage]/+page.ts` | Generische CMS-Seite: Slug = Strapi-API-Name (`agb`, `impressum`, …) |
| `/faq` | `(singlepages)/faq/` | FAQ (Strapi `faqs`) |
| `/news`, `/news/[slug]` | `(singlepages)/news/…` | News-Liste mit Filtern, Artikel (Strapi `news-articles`) |
| `/410` | `(singlepages)/410/` | „Gone“-Seite, prerendered, von Traefik bei 410 genutzt |
| `/login` | `(login)/login/` | Login (+ `redirectTo`) |
| `/register` | `(register)/register/` | Registrierung |
| `/confirm` | `(confirm)/confirm/` | Konto-Aktivierung per Token |
| `/forgotpass` | `(forgotpass)/forgotpass/` | Passwort vergessen und Reset (Token) |
| `/dashboard/overview` | `(authenticated)/dashboard/overview/` | Konto-Übersicht, Statistiken, letzte Bestellungen |
| `/dashboard/orders`, `/dashboard/orders/[id]` | `(authenticated)/dashboard/orders/…` | Bestellhistorie und -detail (JTL-Bestellungen) |
| `/dashboard/addresses` | `(authenticated)/dashboard/addresses/` | Adresse (nur 1 Adresse im User-Datensatz `additionalProp1`) |
| `/dashboard/payments` | `(authenticated)/dashboard/payments/` | Zahlarten, abgeleitet aus Bestellungen |
| `/dashboard/settings` | `(authenticated)/dashboard/settings/` | Profil/Passwort |
| `/shop` | `(store)/shop/+page.svelte` | Produktliste mit Kategorien, Hersteller- und Preisfiltern, Paginierung |
| `/shop/[slug]` | `(store)/shop/[slug]/` | Produktdetail (Varianten, Konfig-Gruppen, Lieferzeit, Kontaktformular) |
| `/shop/categories`, `/shop/categories/[slug]` | `(store)/shop/categories/…` | Kategorienübersicht und -seite |
| `/shop/cart` | `(store)/shop/cart/` | Warenkorb, Versandrechner, Gratisgeschenke |
| `/shop/checkout`, `/shop/checkout/success` | `(store)/shop/checkout/…` | Mehrstufiger Checkout mit Stripe, Erfolgsseite |
| `/payment/[orderId]` | `(store)/payment/[orderId]/` | (Nach-)Zahlung einer Bestellung, Redirect-Polling |
| `/order-lookup` | `(store)/order-lookup/` | Gast-Bestellabfrage (Nr. + E-Mail) |
| `/upload-formular` | `(store)/upload-formular/` | Upload Sicherungskarte (Multipart) |
| `/configurator` | `(configurator)/configurator/+page.svelte` (+ Desktop-/MobileView) | Einstieg Schließanlagen-Konfigurator |
| `/configurator/brands`, `/configurator/brands/[brand]` | `(configurator)/configurator/brands/…` | Markenauswahl, Produkte je Marke |
| `/configurator/questionnaire` | `(configurator)/configurator/questionnaire/` | Fragebogen |
| `/configurator/results` | `(configurator)/configurator/results/` | **Ergebnisse aus Mock-Daten** |
| `/configurator/configure/[productId]` | `(configurator)/configurator/configure/[productId]/` | Schließplan/Matrix-Konfiguration (2.618 Zeilen) |
| `/design-language`, `/components-showcase` | `routes/design-language/`, `routes/components-showcase/` | Interne Styleguide-Seiten, **öffentlich im Prod-Build** |
| `/api/hello.json` | `frontend/src/routes/api/hello.json.js` | Leere Datei (0 Byte), Überbleibsel |

i18n-Routing: `reroute` über Paraglide (`frontend/src/hooks.ts`), Präfix `/en/…` für Englisch, Default `de` ohne Präfix.

### 2.4 State-Management

| Art | Datei | Inhalt |
|---|---|---|
| svelte/store (writable/derived) | `frontend/src/lib/stores/productStore.ts` | `products` (Katalog, Paginierung, Fetch-Funktionen) **und** `cart` (Warenkorb, Persistenz localStorage/DB, Merge bei Login), `cartTotal`, `cartItemCount` |
| svelte/store | `frontend/src/lib/services/sessionService.ts` | `session`, `isAuthenticated`, `user`, Refresh-Timer |
| svelte/store | `frontend/src/lib/stores/customerGroupStore.ts` | Kundengruppe (persistiert in `localStorage`) |
| svelte/store | `priceToggleStore.ts`, `toastStore.ts`, `cartLoginPromptStore.ts`, `securityCardUploadStore.ts` | Netto/Brutto-Umschalter, Toasts, Login-Hinweis, Upload-Dateien |
| Runes-Klasse/Objekt | `frontend/src/lib/stores/checkoutStore.svelte.ts` (450 Z.) | Checkout-Formularzustand, Payment-Intent, Pending-Order-ID |
| Runes (Modul-`$state`) | `frontend/src/lib/states/questionnaireResponses.svelte.ts` | Konfigurator-Fragebogen |
| „Context“ (eigentlich Stores) | `frontend/src/lib/contexts/configurator.ts` | `isMobileView` (globaler resize-Listener ohne Cleanup), `configuratorState` |
| Browser-Storage | 26× `localStorage`, 38× `sessionStorage` | `cart_items`, `customer_group_id`, `user_addresses_*`, `user_payment_methods_*`, Checkout-Session (30 min), Gast-Cart-ID, Tracking-Flags |

### 2.5 i18n

Paraglide JS 1 mit sveltekit-Adapter (`frontend/src/lib/i18n.ts`, `frontend/project.inlang/settings.json`). Quellsprache `de`, dazu `en`. `frontend/messages/de.json` hat 1.096 Keys, `en.json` 961: **169 Keys fehlen in EN**, 34 nur in EN. Lint-Module werden zur Build-Zeit **von jsdelivr geladen** (Netzwerkabhängigkeit im Build). Hartkodierte Texte gibt es trotzdem (z. B. `upload-formular/+page.svelte:43,74` deutsch, `ContactForm.svelte:108` englisch, `configurator/results/+page.svelte:19-20` englisch, `<title>Kasse</title>` in `shop/checkout/+page.svelte:655`). Preis- und Datumsformat über `Intl` (`i18n.ts:27-120`), Währung fix EUR. Strapi-Locale wird aus dem URL-Präfix ergänzt (`frontend/src/lib/api/StrapiManager.ts:27-35,60-63`).

### 2.6 Tracking & Consent

- **Consent:** CookieFirst (`frontend/src/app.html:13`). Nur GTM ist per `type="text/plain" data-cookiefirst-category="advertising"` gegated (`app.html:15-26`). **Ungegated** laden: GTM-noscript-iframe (`app.html:36-43`), eTrusted (`:29`), Userlike-Chat (`:45-49`), Trusted Shops (`:50-70`) und Händlerbund (`:72`). Die DSGVO-Konformität sollte juristisch geprüft werden.
- **Tracking:** `frontend/src/lib/utils/tracking.ts` (553 Z.) pusht GA4-E-Commerce-Events (`view_item`, `begin_checkout`, `purchase`) in den `dataLayer`, mit SHA-256-gehashten Nutzerdaten (Enhanced Conversions, `tracking.ts:60-75`). Deduplizierung über `localStorage`-Keys. Für `purchase` lädt es Items, Versand- und Zahlarten nach (`tracking.ts:145-149`) und ruft zur Neukunden-Erkennung **`GET /api/order/?user_id=<CustomerId>`** auf (`tracking.ts:277`). Versandkosten-Artikel ist hart `ItemId 144` (`tracking.ts:8`).
- **Attribution:** `frontend/src/lib/tracking-params.ts` (Allowlist gclid/utm_* usw.), Cookies nur via `hooks.server.ts`, also in Prod wirkungslos (siehe 2.2).

### 2.7 Styling

Eigenes CSS ohne Framework: Design-Tokens als Custom Properties (`frontend/src/lib/styles/colors.css`, `sizing.css`, `design-language.css` mit 474 Z.), Reset, globale `components.css`, sonst scoped `<style>` pro Komponente mit BEM-Klassen. Eigenes Tool `frontend/tools/check-css-values` prüft Token-Nutzung. Icons werden **zur Laufzeit einzeln per fetch** geladen (`frontend/src/lib/components/Icon.svelte:63`, `/assets/icons/<name>.svg`) und per `{@html}` eingefügt. Assets (Icons, Bilder, Shop-Bilder) liegen **nicht im Repo**, sondern in einem Volume (`static/frontend/assets`, gitignored).

### 2.8 Tests

- **1 Testdatei**, `frontend/src/demo.spec.ts`: `expect(1 + 2).toBe(3)`. **Keine fachliche Testabdeckung** (0 % für Preise, Warenkorb, Checkout, Konfigurator).
- CI (`.gitea/workflows/frontend_check.yml`) führt `check`, `lint` und `test` bei PRs aus. Der Test-Job ist damit faktisch wirkungslos. Im Pre-Commit ist der Test-Hook auskommentiert (`.pre-commit-config.yaml`).
- Keine E2E-Tests (Playwright o. ä.), keine Komponenten-Tests.

### 2.9 Linting

ESLint 9 Flat Config (`frontend/eslint.config.js`: js/ts recommended, svelte recommended, prettier), Prettier mit Import-Sortierung, `svelte-check` (strict TS), Pre-Commit-Hooks in Docker. Dazu 47 `eslint-disable` (46× `svelte/no-at-html-tags`) und 4 `-svelte-ignore` (wegen Tippfehler mit führendem `-` wirkungslos: `frontend/src/lib/components/Page.svelte:28` u. a.).

---

## 3. API-Vertrag (Ist-Zustand, den die .NET-API erfüllen muss)

### 3.1 Base-URL-Konfiguration

| Ziel | Mechanismus | Fundstelle |
|---|---|---|
| Backend-APIs | **Relative Pfade** `/api/...` (gleiche Origin) in den meisten Aufrufen | z. B. `frontend/src/lib/services/authService.ts:8`, `cartService.ts:6`, `configuratorService.ts:1` |
| Backend-APIs (Variante) | `getUrl() + '/api'`: absolute Origin aus `window.location` | `frontend/src/lib/services/domainService.ts:4-23`, genutzt in `orderService.ts:38,282`, `paymentService.ts:255` |
| Routing | Traefik-`PathPrefix`: `/api/items` → items_svc, `/api/auth` → auth, `/api/payment` → payment_svc, `/api/pdf` → pdf_svc, Rest `/api` → business_api | `docker-stack.yml:210,304,458,523,655` |
| Strapi | `import.meta.env.VITE_STRAPI_URL` oder `https://cms.<hostname>[:port]/api` | `frontend/src/lib/api/StrapiManager.ts:51`, `frontend/src/lib/utils/getCmsUrl.ts:1-9`, `helpContentService.ts:98` (eigene Ableitung via `getUrl('cms')`) |
| Strapi-Medien | `getCmsUrl() + url` | `frontend/src/lib/utils/cmsMedia.ts:15-20`, diverse `<img src="{getCmsUrl()}…">` |
| Produktbilder | Statisch `/assets/img/shop_items/<name>` (Volume, nicht API) | `productStore.ts:489`, `itemsService.ts:152`, `freeGiftService.ts:16`, `SearchBar.svelte:78`, `ProductCarousel.svelte:87` |
| Auth | **Cookies** (`credentials: 'include'`), kein Authorization-Header. Kein CSRF-Token. | durchgängig |

Es gibt **keinen zentralen HTTP-Client** (keine Interceptors, kein einheitliches Fehlerformat, keine Timeouts außer `paymentService`, kein Retry). Uneinheitlich: Manche Item-Aufrufe senden `credentials: 'include'` (`itemsService.ts`), andere nicht (`productStore.ts`).

### 3.2 Backend-Aufrufe nach Domäne

Legende Response: Felder, die das Frontend tatsächlich liest. „FE→Backend-Service“ = heutiges Ziel laut Traefik.

#### Auth (`/api/auth/v1/*` → auth_svc)

| # | Datei:Zeile | Methode | Pfad | Payload | Erwartete Response |
|---|---|---|---|---|---|
| A1 | `frontend/src/lib/services/authService.ts:60` | POST | `/api/auth/v1/register` | `{email, name, first_name, last_name, password, password_repeat, additionalProp1:{companyName,address,additionalAddress,postalCode,city,country,phone,birthdate,title,additionalInfo,newsletter,terms}}` | `detail` \| `msg` (Text), Fehler `detail`/`msg` |
| A2 | `authService.ts:98` | POST | `/api/auth/v1/login` | `{identifier, password, stay_logged_in}` | Setzt Cookies, `detail`/`msg` |
| A3 | `authService.ts:143` | POST | `/api/auth/v1/forgot-password` | `{email}` | `detail`/`msg`, Fehler `errors` |
| A4 | `authService.ts:163` | POST | `/api/auth/v1/reset-password` | `{token, password, password_confirm}` | `detail`/`msg`, Fehler `errors`\|`detail` |
| A5 | `authService.ts:199` | PUT | `/api/auth/v1/update` | `{first_name?, last_name?, email?, phone?, newsletter?, company_name?, birthdate?, address?, additional_address?, city?, postal_code?, country?, current_password?, new_password?, new_password_repeat?}` | beliebiges JSON (Erfolg), Fehler `errors`\|`detail` |
| A6 | `authService.ts:300` | GET | `/api/auth/v1/activate?token=` | – | `detail`/`msg` |
| A7 | `frontend/src/lib/services/sessionService.ts:83` | GET | `/api/auth/v1/status` | – | **`access_token`** (wird nur auf Truthiness geprüft, landet aber im JS), `user_ok`, `user{id,email,name,first_name,last_name}` |
| A8 | `sessionService.ts:158` | POST | `/api/auth/v1/refresh` | – | nur Status (Cookies) |
| A9 | `sessionService.ts:227` | POST | `/api/auth/v1/logout` | – | nur Status |

Aufrufer: `login/+page.svelte:65`, `register/+page.svelte:148`, `forgotpass/+page.svelte:75,133`, `confirm/+page.svelte:41`, `profileService.ts:92`, `dashboard/addresses/+page.svelte:161`.
Session-Konstanten: Token-Lebensdauer **15 min** clientseitig angenommen, Refresh alle 13 min, aber **nur bei `stayLoggedIn`** (`sessionService.ts:23-25,59-63,191`).

#### Artikel / Katalog (`/api/items/*` → items_svc)

| # | Datei:Zeile | Methode | Pfad | Payload | Erwartete Response |
|---|---|---|---|---|---|
| K1 | `frontend/src/lib/stores/productStore.ts:742` (`fetchProducts`) | POST | `/api/items/items/` | `{api_query:{filters:{operator:'AND',conditions:[…]},sort?,pagination:{page,page_size}}, customer_group_id?}`. Pflichtfilter: `evaluated_data.in_shop=true`, `active_sales_channels ne []`, `parent_item_id=0` | `items[]` (ApiItem, s. u.), `total_items`, `total_pages`, `current_page`, `page_size` |
| K2 | `productStore.ts:845` (`fetchProductById`) | POST | `/api/items/items/` | Filter `id eq`, `in_shop`, `active_sales_channels`, page_size 1 | `items[0]` |
| K3 | `productStore.ts:919` (`fetchProductByIdAny`) | POST | `/api/items/items/` | **nur** `id eq` (ohne in_shop/aktiv-Filter). Für Warenkorb-Hydrierung und Konfigurator | `items[0]` |
| K4 | `productStore.ts:1008` (`fetchProductBySlug`) | POST | `/api/items/items/` | Fallback `name contains <slug→text>` | `items[]` |
| K5 | `productStore.ts:1227` (`executeProductQuery`: Related nach Kategorie, Hersteller, „random“) | POST | `/api/items/items/` | `categories.id eq` / `manufacturer.id eq` / `id ne` | `items[]` |
| K6 | `productStore.ts:1295` (`fetchConfigItems`) | POST | `/api/items/items/` | `operator:'OR'`, bis zu 100× `id eq`, Batches | `items[]` inkl. `config_group_data` |
| K7 | `frontend/src/lib/services/itemsService.ts:9,79` | POST | `/api/items/items/` | `id eq` / `id in [..]`, `is_active`, `excluded_fields[…]` | `items[]` (Dashboard, Tracking) |
| K8 | `frontend/src/lib/components/Header/SearchBar.svelte:137` | POST | `/api/items/items/` | Freitext-Filter (verschachtelt), `sort sku`, `customer_group_id?` | `items[]`; **Suche wird clientseitig nachgefiltert** (`SearchBar.svelte:157-175`) |
| K9 | `frontend/src/lib/components/ProductCarousel.svelte:36` | POST | `/api/items/items/` | `sku in [..]` aus CMS, **`excluded_fields: []`** (Vollobjekt) | `items[]` |
| K10 | `frontend/src/lib/components/shop/ProductConfigGroups.svelte:97` | POST | `/api/items/items/` | `OR`-Liste `id eq` | `items[].active_sales_channels`, `id` |
| K11 | `frontend/src/lib/services/freeGiftService.ts:83` | POST | `/api/items/items/` | `active_sales_channels contains '2-2-5'`, `variations eq []`, `categories.name endswith 'Gratisgeschenke'` | `items[]` |
| K12 | `frontend/src/routes/(store)/shop/+page.svelte:339,394` | POST | `/api/items/items/` | Min/Max-Preis: sort `price_data.normal_price.sales_price_net` asc/desc, page_size 1 | `items[0].price_data`, `tax_class` |
| K13 | `shop/+page.svelte:134` | POST | `/api/items/catagories/` | – (kein Body) | **Ergebnis wird verworfen** (`await res.json()` ohne Nutzung) |
| K14 | `shop/+page.svelte:151`; `shop/categories/+page.svelte:40`; `shop/categories/[slug]/+page.svelte:47` | POST | `/api/items/catagories` (Tippfehler „catagories“ ist Vertragsbestandteil!) | `[]` | `{main_ids:{id:name}, by_ids:{id:Category{id,name,description,parent_category_id,level,sort_number,active_sales_channels,children_ids,seo_path,seo_meta_description,seo_title_tag,seo_meta_keywords}}, missing[]}` |
| K15 | `shop/+page.svelte:315` | POST | `/api/items/manufacturers/` | `{}` | Objekt `{<key>:{id,name}}` |
| K16 | `frontend/src/lib/services/orderService.ts:282` | POST | `/api/items/shipping/?country_code=XX` | `[{id,name}]` (Versandklassen der Warenkorbartikel) | `[{Id\|id, Name\|name, Priority, GrossPrice\|gross_price, ExtraWeight, free_shipping_at, description, min_delivery_days, max_delivery_days, delivery_time_text, picture_url}]` |

**Konfigurator-Katalogabfragen** (alle über `fetchAllConfiguratorItems`, `frontend/src/lib/utils/apiHelpers.ts:107`, POST `/api/items/items/`, automatische Paginierung à 100, Default-Filter `is_active`, `evaluated_data.in_configurator`, Default-`excluded_fields` `apiHelpers.ts:38-60`): `configuratorBrands.ts:6`, `configuratorBrandProducts.ts:39,71,87`, `configuratorPopularProducts.ts:56,90,102`, `configuratorFilteredProducts.ts:49`, `configuratorConfigurations.ts:17,35`, `configuratorKeys.ts:157,384,550`, `configuratorCylinders.ts:332,342` (Fallback: **alle** Konfigurator-Items), `frontend/src/lib/utils/urlUtils.ts:29` (**alle** Items, um Slug→Kategorie zu mappen).

**ApiItem-Felder, die das Frontend liest** (`productStore.ts:51-232`, `frontend/src/lib/types/items.ts:50`): `id, sku, name, is_active, categories[{id,name,raw_path,path}], manufacturer{id,name,homepage}, descriptions{description,description_short,…}, tax_class{rate,rate_factor,…}, price_data{normal_price{sales_price_net,suggested_retail_price,purchase_price_net}, special_prices[{net_price,reduce_standard_price_by_percent,customer_group_id,sales_channel_id,is_active,start_date,end_date,…}], customer_group_prices}, storage_options{inventory_management_active,procurement_time,additional_handling_time,…}, images[{name,image_id,sort_number}], variations[{id,name,type,translations,data[{id,name,translations,additional_charge{price,netto},…}]}], custom_fields[], evaluated_data{in_shop,in_configurator,is_express,zylinder_length{inside,outside,single}}, child_items, suppliers[{supplier_id,is_standard,delivery_time,use_supplier_delivery_time}], parent_item_id, child_variation_mapping, config_groups[], config_group_data{…}, shipping_class{id,name}, unit{amount,label}, active_sales_channels`.

**Filter-DSL (Vertrag):** `operator: AND|OR` (verschachtelbar), Bedingungen `{field, operator, value}` mit Operatoren `eq, ne, in, contains, endswith, lt` (Backend-Beispiel). Feldpfade in Punktnotation (`evaluated_data.in_shop`, `categories.name`, `manufacturer.id`, `price_data.normal_price.sales_price_net`), `sort[{field,direction}]`, `excluded_fields[]` (Top-Level).

#### Konfigurator (gespeicherte Konfiguration, `/api/configurator/` → business_api)

| # | Datei:Zeile | Methode | Pfad | Payload | Erwartete Response |
|---|---|---|---|---|---|
| C1 | `frontend/src/lib/services/configuratorService.ts:96` (Aufruf `configure/[productId]/+page.svelte:972`) | POST | `/api/configurator/` | `{config_id\|null, user_id?, category, cylinderKeyMatrix{cyl:{key:bool}}, cylinders[], keys[], config_name, address{address,apartment,zipCode,city}, date_created, date_modified}` | `config_id` (Pflicht), sonst gleiche Felder; akzeptiert auch `cylinder_key_matrix`, `zip_code` |
| C2 | `configuratorService.ts:115` (Aufruf `+page.svelte:2253`) | GET | `/api/configurator/?config_id=` | – | wie C1 |

#### Warenkorb (`/api/cart/{id}` → business_api)

| # | Datei:Zeile | Methode | Pfad | Payload | Erwartete Response |
|---|---|---|---|---|---|
| W1 | `frontend/src/lib/services/cartService.ts:97` (Aufrufer `productStore.ts:1721`, `shop/checkout/+page.svelte:272,405`) | POST | `/api/cart/{identifier}` (`identifier` = `session.user.id` = ERP-ID **oder** clientseitig generierte `guest_<uuid>`) | `{items[{id,quantity,variation_ids[{id,data_id}]?,variation_inputs[{id,value}]?,config_group_items[]?,matrix_config_id?,matrix_config_url?}], timestamp, lastUpdated}` | `{cart_id}` |
| W2 | `cartService.ts:142` (Aufrufer `productStore.ts:1742,1831`) | GET | `/api/cart/{identifier}` | – | `{cart:{items[…]}}`, 404 = kein Cart |
| W3 | `cartService.ts:199` | DELETE | `/api/cart/{identifier}` | – | Status, **wird nirgends aufgerufen** |

#### Checkout / Bestellungen (`/api/order/*`, `/api/user/` → business_api)

| # | Datei:Zeile | Methode | Pfad | Payload | Erwartete Response |
|---|---|---|---|---|---|
| O1 | `frontend/src/lib/services/orderService.ts:117` (Aufruf `shop/checkout/+page.svelte:588`) | POST multipart | `/api/order/?user_identifier=<erpId\|email>` | Feld `payload` = JSON `{checkout_data:{order_id?, billing_address{firstName,lastName,email,address,addressNumber,apartment,city,zipCode,country,phone,company}, shipping_address?, newsletter, saveInfo, useSameAddressForBilling, discountCode, customerComment, agbAccepted, revocationAccepted, shippingMethod{**komplettes Objekt inkl. GrossPrice, free_shipping_at**}, paymentMethod:'bank_transfer'\|'online', paymentMethodType, timestamp}, checkout_items[{id,quantity,variation_ids,variation_inputs,config_group_items}]}` plus `files[]` (Sicherungskarten) | `{id, detail}` (Upsert: wiederholter Aufruf mit `order_id` aktualisiert) |
| O2 | `orderService.ts:142` (Aufrufer `sessionService.ts:104` mit **E-Mail**, `apiUserDataService.ts:16`, `shop/checkout/+page.svelte:123` mit **ERP-ID**) | GET | `/api/user/?identifier=` (Backend ignoriert `identifier` und nutzt das Cookie) | – | `UserData{erp_id, email, name, first_name, last_name, customer_group_id, created_at, additionalProp1{companyName,address,additionalAddress,postalCode,city,country,phone,birthdate,title,additionalInfo,newsletter,terms}}` |
| O3 | `orderService.ts:189` (Aufrufer `apiUserDataService.ts:31`, `dashboard/orders/+page.svelte:29`, `tracking.ts:277`) | GET | `/api/order/?user_id=<erpId>` | – | `Order[]` im **JTL-PascalCase-Format** (s. u.) |
| O4 | `orderService.ts:214` (Aufrufer `apiUserDataService.ts:88`, `tracking.ts:149`) | GET | `/api/order/payment_methods` | – | `[{Id, Name, IsStandard, IsActive}]` |
| O5 | `orderService.ts:240` (Aufrufer `shop/checkout/+page.svelte:504`, `tracking.ts:147`) | GET | `/api/order/shipping_methods` | – | wie K16 |
| O6 | `frontend/src/routes/(store)/payment/[orderId]/+page.svelte:57,133,232`; `shop/checkout/success/+page.svelte:28` | GET | `/api/order/single_order?number=<id>` | – | `Order` (u. a. `SalesOrderPaymentDetails.StillToPay`, `Number`, `Id`), Polling alle 1,5 s |
| O7 | `frontend/src/routes/(store)/order-lookup/+page.svelte:60` | GET | `/api/order/single_order?number=<nr>&is_jtl_number=true` | – | `Order`, 404, **429** (Lockout). **E-Mail-Abgleich erfolgt clientseitig** (`:89-97`) |

**Order-Struktur (JTL)** `frontend/src/lib/types/order.ts:78-200`: `Id, Number, ExternalNumber, BillingNumber, CompanyId, CustomerId, CurrencyIso, BillingAddress{FirstName,LastName,Street,PostalCode,City,CountryIso,EmailAddress,PhoneNumber,Company,…}, Shipmentaddress{…}, SalesOrderDate, SalesOrderPaymentDetails{PaymentMethodId,PaymentStatus,TotalGrossAmount,StillToPay,DateOfPayment,…}, SalesOrderShippingDetail{ShippingMethodId,DeliveryCompleteStatus(0/1/2),ShippingDate,EstimatedDeliveryDate,…}, IsCancelled, CancellationDetails, lineitems[{Id,ItemId,SKU,Name,Type,Quantity,QuantityDelivered,SalesPriceNet,SalesPriceGross,Discount,PurchasePriceNet,TaxRate,notes[]}]`.

#### Zahlung (`/api/payment/*` → payment_svc, Stripe)

| # | Datei:Zeile | Methode | Pfad | Payload | Erwartete Response |
|---|---|---|---|---|---|
| P1 | `frontend/src/lib/services/paymentService.ts:93` (Aufrufer `shop/checkout/+page.svelte:620`, `payment/[orderId]/+page.svelte:203`) | POST | `/api/payment/intents/` | `{order_id}` | `{intent:{id, client_secret, status, amount, currency, payment_method_types[]}}` |
| P2 | `paymentService.ts:117` | GET | `/api/payment/status/?intent_id=` | – | `{intent_id, status:'pending'\|'paid'\|'failed'\|'canceled'}`, **wird nicht genutzt** |
| P3 | `frontend/src/lib/services/stripeService.ts:91,264` | – | Stripe.js (`loadStripe`, `confirmPayment`) | Publishable Key aus `VITE_STRIPE_PUBLISHABLE_KEY` | Redirect auf `returnUrl` mit `payment_intent`, `redirect_status` |

Nicht vom Frontend, aber für den Ablauf nötig: Stripe-Webhook (`backend/payment_svc/.../webhook.py`) und `PATCH /api/order/{id}/mark_paid`.

#### Konto

Läuft vollständig über A5, A7, O2 und O3. **Adressen und Zahlarten im Dashboard werden aus der Bestellhistorie abgeleitet** (`frontend/src/lib/services/apiUserDataService.ts:46-84`). Zusätzlich gibt es einen **parallelen localStorage-„Service“** für Adressen, Zahlarten (inkl. Feldern `iban`, `bic`, `last4`) und Bestellungen (`frontend/src/lib/services/userDataService.ts:55-329`). `dashboard/orders/+page.svelte:25-26` nutzt ihn noch.

#### Sonstiges (business_api)

| # | Datei:Zeile | Methode | Pfad | Payload | Erwartete Response |
|---|---|---|---|---|---|
| S1 | `frontend/src/lib/components/ContactForm.svelte:70` | POST | `/api/contact` | `{item_sku:'', name, email, message, phone_number}` | nur Status |
| S2 | `frontend/src/lib/components/shop/ProductContactForm.svelte:86` | POST | `/api/contact/` (mit Slash!) | `{item_sku, name, email, message, phone_number}` | nur Status |
| S3 | `frontend/src/routes/(store)/upload-formular/+page.svelte:63` | POST multipart | `/api/upload/security_card` | `salutation, name, email, street, postalCodeCity, phone, lockInfo, additionalInfo, files[]` | nur Status |
| S4 | (Traefik) `compose/services/frontend/main.yml:20` | GET | `/_check_page` (ForwardAuth, Header `X-Forwarded-Uri`) | – | 200 / 404 / 410 / Redirect |
| S5 | (SEO) `backend/business/.../sitemap.py:231` | GET | `/api/sitemap.xml` bzw. Routing laut Traefik | – | XML |

Nicht vom Frontend genutzt, aber vorhanden: `/api/items/google_merchant/*` (Merchant-Feed), `/api/maildata/*`, `/api/order/send_*`, `/api/pdf/*`, `/api/items/items/count`, `/all-urls`.

> **Hinweis „Captcha“:** Kontakt- und Produktkontaktformular nutzen ein **rein clientseitiges** Rechen-Captcha (`frontend/src/lib/utils/useCaptcha.svelte.ts`). Das Backend bekommt davon nichts mit, Spam-Schutz muss also in der neuen API liegen.

### 3.3 Strapi-Aufrufe (alle GET, öffentlich, ohne Token)

| # | Datei:Zeile | Endpoint | Zweck |
|---|---|---|---|
| ST1 | `frontend/src/lib/api/StrapiLoader.ts:29` (via `routes/+page.js`, `[singlepage]/+page.ts`, `faq/+page.ts`, `news/+page.ts`) | `GET /api/{slug}?pLevel=5[&locale=en]` | Home und alle CMS-Einzelseiten (Slug = Single-Type-Name) |
| ST2 | `StrapiLoader.ts:22`; `routes/(singlepages)/news/[slug]/+page.svelte:70` | `GET /api/news-articles?filters[slug][$eq]=<slug>&pLevel=5` | News-Artikel (Slug **unescaped** in URL) |
| ST3 | `routes/(singlepages)/news/+page.svelte:189,258` | `GET /api/news-articles?pLevel=2&sort=…&filters[newsCategory][slug][$eq]=…&filters[date][$gte/$lte]=…&pagination[…]` | News-Liste (Sortierung `titleAsc`↔`title:desc` vertauscht, `StrapiManager.ts:96-101`) |
| ST4 | `routes/(singlepages)/faq/+page.svelte:55` | `GET /api/faqs?pLevel=5` | FAQ |
| ST5 | `frontend/src/lib/components/Header/Header.svelte:48`, `Header/TopBar.svelte:20`, `Footer/Footer.svelte:40`, `PromoBanner.svelte:79` | `GET /api/settings?pLevel=5` | Navigation, Top-Bar, Footer, Promo-Banner: **4× derselbe Request pro Seitenaufruf** |
| ST6 | `frontend/src/lib/services/helpContentService.ts:103` | `GET /api/configurator-contextual-helps?filters[name][$eq]=<name>&populate=*` | Kontexthilfe im Konfigurator |
| ST7 | diverse `<img>` | `https://cms.<host>/uploads/...` | Medien |

Strapi bleibt vom Backend-Wechsel unberührt, ist aber über den Produkt-Carousel (`sku`-Listen aus CMS → K9) mit dem Katalog verknüpft.

---

## 4. Businesslogik im Frontend (gehört ins Backend)

| # | Thema | Fundstelle(n) | Was passiert im Frontend | Risiko |
|---|---|---|---|---|
| B1 | **Bruttopreis / MwSt** | `productStore.ts:508,528-532,1318-1320`; `cartCalculations.ts:25,62`; `priceCalculations.ts:47,71,144`; `ProductCard.svelte:44`; `ProductPrice.svelte:145,204`; `ProductConfigGroups.svelte:189`; `ProductCarousel.svelte:115`; `SearchBar.svelte:57,85`; `shop/+page.svelte:454,462,736`; `shop/[slug]/+page.svelte:275-276,570,583`; `cart/+page.svelte:367,406`; `configure/[productId]/+page.svelte:2194,2228`; `ItemList.svelte:40` | `net × (tax_class.rate_factor \|\| 1.19)`. Keine Abhängigkeit vom Lieferland (CH/NO/GB stehen in `countries.ts` mit 81 Ländern), kein Reverse-Charge/B2B, Rundung pro Position uneinheitlich | Falsche Preisanzeige, Abweichung zur Rechnung |
| B2 | **UVP netto/brutto widersprüchlich** | `productStore.ts:530-532` (UVP × Steuer) vs. `priceCalculations.ts:63-76` („UVP ist brutto“) | Zwei Annahmen zur gleichen Größe | Falsche Streichpreise |
| B3 | **Kundengruppen-Sonderpreise** | `productStore.ts:513-525`; `priceCalculations.ts:88-175`; Kundengruppe aus `customerGroupStore.ts` (localStorage), wird als `customer_group_id` im Body gesendet (`productStore.ts:739,842,916,1005,1224,1292`, `SearchBar.svelte:133`) | Client wählt aktiven Sonderpreis; `start_date`/`end_date`/`till_amount_*` werden **nicht** geprüft | Manipulierbarer Preis-Kontext, abgelaufene Aktionen sichtbar |
| B4 | **Warenkorbsummen / Zwischensumme** | `cartCalculations.ts:20-136`; `shop/checkout/+page.svelte:192-200`; `CartSummary.svelte:62`; `productStore.ts:2053` (`cartTotal`, ohne Varianten/Konfig-Aufschläge, also anders als die Cart-Seite) | Heuristik „Basispreis ≈ Summe Config-Items → nicht doppelt zählen“ (`cartCalculations.ts:88-96`), Matrix-Divisor (`:82`) | Fehlerträchtig (siehe viele „fix price calculation“-PRs) |
| B5 | **Versandkosten / Gratisversand** | `frontend/src/lib/constants/shipping.ts:1-18` (99 € / 7,50 €, `calculateShippingCost` ungenutzt); `CartSummary.svelte:69-72` (Schwelle 99 €); `shop/checkout/+page.svelte:203-214`, `CheckoutShippingMethod.svelte:92-105`, `CartShippingCalculator.svelte:90-102` (Schwelle `free_shipping_at` aus API) | Zwei Quellen für dieselbe Regel; Checkout schickt das ganze `shippingMethod`-Objekt inkl. `GrossPrice` an das Backend (`orderService.ts:77`) | Inkonsistente Anzeige; Backend darf Client-Preis nicht vertrauen |
| B6 | **Gratisgeschenke** | `cart/+page.svelte:31-58` (Schwelle **200 €** hart), `freeGiftService.ts:46-67` (Kanal `2-2-5`, Kategorie „Gratisgeschenke“, Preis 0 lokal gesetzt `:26-27`), `FreeGiftCarousel.svelte:17-31,67`; `isGift` geht beim Persistieren verloren (`productStore.ts:1582-1600`) und **nicht** an die Bestellung (`shop/checkout/+page.svelte:537-577`) | Anspruch, Auswahl und Preis 0 nur im Client | Geschenk ohne Prüfung bestellbar, nach Reload ggf. mit Normalpreis |
| B7 | **Lieferzeit** | `shop/[slug]/+page.svelte:624-637` | Lieferant-Standard → `delivery_time` + `additional_handling_time`, sonst 1 Tag | Fachregel im View |
| B8 | **Verfügbarkeit** | `productStore.ts:543` `!inventory_management_active \|\| true` (immer `true`); `freeGiftService.ts:32` | Lagerbestand wird ignoriert | Bestellungen ohne Bestand |
| B9 | **Sichtbarkeitsregeln** | Pflichtfilter `in_shop`, `active_sales_channels`, `parent_item_id=0`, `is_active` werden vom Client gesetzt (`productStore.ts:665-723`, `SearchBar.svelte:108-112`) und an anderer Stelle bewusst weggelassen (`productStore.ts:885-910`) | Client entscheidet, was sichtbar ist | Nicht freigegebene Artikel abrufbar |
| B10 | **Rabatt-%** | `productStore.ts:535-541`, `priceCalculations.ts:88-112` | aus `reduce_standard_price_by_percent` | ok, aber Client-seitig |
| B11 | **Konfigurator-Preise/Regeln** | `configure/[productId]/+page.svelte` (2.618 Z.), `configurator*.ts`, `determineLockPlanType.ts` (295 Z.), `keyQuantityLabel.ts` | Preis- und Schließplanlogik (separat analysiert) | – |
| B12 | **Validierung** | `frontend/src/lib/utils/validate*.ts`, `checkoutValidation.ts` (225 Z.), Passwortregeln `validateRegistration.ts:31-50` | Muss im Backend identisch existieren | Umgehbar |
| B13 | **Gast-Bestellabfrage** | `order-lookup/+page.svelte:89-97` | E-Mail-Vergleich nach Abruf der kompletten Bestellung | Datenleck (Security separat) |
| B14 | **Kontostatistik/Treuepunkte** | `apiUserDataService.ts:101-150` (Treuepunkte = Umsatz × 10, Fallback „Jan 2023“), Status-Mapping `DeliveryCompleteStatus` 0/1/2 (`dashboard/orders/+page.svelte:48-53`) | Fachlogik und Fake-Werte im Client | Falsche Angaben |
| B15 | **Adressen/Zahlarten aus Bestellungen** | `apiUserDataService.ts:46-84` | Aggregation im Client | Gehört als Endpunkt ins Backend |
| B16 | **Neukunden-Erkennung (Tracking)** | `tracking.ts:274-290` | lädt alle Bestellungen eines Kunden | Gehört in Backend/Server-Side-Tagging |
| B17 | **Warenkorb-Merge bei Login** | `productStore.ts:1683-1714,1809-1843` | Summierung identischer Positionen im Client | Gehört in Cart-Service |
| B18 | **Captcha** | `useCaptcha.svelte.ts` | Rechenaufgabe rein clientseitig | Kein Schutz |

---

## 5. Befunde

| ID | Schweregrad | Fundstelle | Beschreibung | Auswirkung | Empfehlung |
|---|---|---|---|---|---|
| F-01 | Kritisch | `frontend/src/lib/stores/productStore.ts:76-80`, `ProductCarousel.svelte:62` (`excluded_fields: []`), `frontend/src/lib/types/order.ts` (`PurchasePriceNet`) | Generischer Item-Endpunkt liefert ERP-Rohdaten inkl. **Einkaufspreis** (`purchase_price_net`), Sonderpreise aller Kundengruppen, Lieferanten | Offenlegung von Margen/Konditionen | In .NET-API DTOs je Use-Case. Einkaufspreise, fremde Kundengruppen und Lieferantendaten nie ausliefern |
| F-02 | Kritisch | `customerGroupStore.ts:9-55`, `productStore.ts:739` ff. | Kundengruppe liegt in `localStorage` und wird vom Client als `customer_group_id` gesendet | Preisgruppe manipulierbar (B2B-Preise für jeden) | Kundengruppe ausschließlich serverseitig aus der Session ableiten; Feld aus Vertrag streichen |
| F-03 | Kritisch | `order-lookup/+page.svelte:60-97`, `payment/[orderId]/+page.svelte:57,133,232`, `checkout/success/+page.svelte:28`; Backend `order.py:102-107` | `single_order` liefert komplette Bestellung (Adresse, E-Mail, Positionen) **ohne Auth** nur per Nummer; E-Mail-Prüfung clientseitig | Personen- und Bestelldaten per Enumeration abrufbar | Lookup-Endpunkt mit Nummer + E-Mail serverseitig, Rate-Limit, signierter Zahlungslink statt Nummer |
| F-04 | Hoch | `orderService.ts:183-208`, `tracking.ts:277`, `cartService.ts:97-205`; Backend `order.py:88-99`, `cart.py` | Bestellungen (`?user_id=`) und Warenkörbe (`/cart/{id}`) per Client-ID adressiert; Backend fällt ohne Token auf den Parameter zurück | IDOR: fremde Bestellungen/Warenkörbe lesen und überschreiben | `/me/orders`, `/me/cart` aus Session; Gast-Cart über serverseitig ausgestelltes Cart-Token |
| F-05 | Hoch | `shop/checkout/+page.svelte:537-577`, `orderService.ts:77`, `cart/+page.svelte:31-58` | Gratisgeschenk-Anspruch und Versandpreis nur clientseitig; Bestellung enthält `shippingMethod` mit Preis, Geschenk ohne Kennzeichnung | Preis-/Geschenkmanipulation bzw. falsche Berechnung | Server berechnet Versand, Geschenk-Berechtigung und Summen; Client zeigt nur an (Quote-/Pricing-Endpunkt) |
| F-06 | Hoch | Abschnitt 4 B1–B4, ≥ 15 Stellen `|| 1.19` | Preis- und Steuerberechnung verteilt und dupliziert, kein Lieferlandbezug | Abweichende Preise zwischen Liste, Detail, Warenkorb, Checkout, Rechnung | Ein Pricing-Endpunkt (`/cart/quote`) liefert netto, brutto, MwSt und Versand pro Position; Frontend-Rechnen entfernen |
| F-07 | Hoch | `frontend/src/demo.spec.ts` | Keine Tests (1 Dummy) bei hochkomplexer Preis- und Warenkorblogik | Regressionen (Historie: >10 „fix price calculation“-PRs) | Vor dem Umbau Charakterisierungstests (Vitest) für `cartCalculations`, `priceCalculations`, `configSelections`, `productStore.cart`; Playwright-E2E für Checkout gegen Mock-API |
| F-08 | Hoch | `routes/(configurator)/configurator/configure/[productId]/+page.svelte` (2.618 Z., 2.338 Z. Script, 21 `$effect`, 54 `$state`), `routes/(store)/shop/+page.svelte` (2.512), `lib/stores/productStore.ts` (2.059), `shop/checkout/+page.svelte` (1.351), `shop/[slug]/+page.svelte` (1.300), `shop/cart/+page.svelte` (1.076); insgesamt 26 Svelte-Dateien > 500 Z. | Riesen-Komponenten mit Datenzugriff, Fachlogik und UI vermischt | Kaum wartbar, testbar oder migrierbar | Fachlogik in TS-Module/Services, Daten-Loading in `+page.ts`-`load`, Komponenten < 300 Z. |
| F-09 | Hoch | `frontend/src/hooks.server.ts`, `svelte.config.js:13` | `hooks.server.ts` wird mit `adapter-static` in Prod nie ausgeführt; Attributions-Cookies entstehen nur im Dev | Tracking-Attribution greift nicht (Annahme der Entwickler falsch) | Entweder clientseitig umsetzen oder bewusst auf SSR (adapter-node) wechseln |
| F-10 | Hoch | `cms/strapi_cms/.env` (eingecheckt, `.gitignore`-Ausnahme `!cms/strapi_cms/.env`), `docker-stack.yml:567-570,604-606` | Strapi-Secrets im Repo (`APP_KEYS=BmF1…`, `ADMIN_JWT_SECRET=o+Df…`, `JWT_SECRET=FabD…`, `API_TOKEN_SALT=w4Pp…`, `TRANSFER_TOKEN_SALT=8RSY…`, DB-PW `pw12…`) und identisch als Prod-Defaults | Kompromittierbare Admin-Tokens | Secrets rotieren, aus Git entfernen, Docker-Secrets/Vault, keine Defaults |
| F-11 | Hoch | Abschnitt 3.2 (~50 fetch-Aufrufe in 25 Dateien) | Kein zentraler API-Client; Pfade, Header, Fehler-Handling und `credentials` uneinheitlich; zwei Base-URL-Varianten (`/api` vs. `getUrl()+/api`) | Umstellung auf neue API teuer und fehleranfällig | `lib/api/client.ts` mit Base-URL aus Env, typisierten Endpunkten (OpenAPI-generiert aus .NET), einheitlichem Fehlerformat (ProblemDetails) |
| F-12 | Mittel | `productStore.ts:478`, `ProductCarousel.svelte:78`, `SearchBar.svelte:60-100`, `itemsService.ts:143`, `freeGiftService.ts:9` | 5 duplizierte Mapper ApiItem→Product; `ApiItem` 3× definiert (`productStore.ts:51`, `types/items.ts:50`, `SearchBar.svelte:17`), `Category` 5×, `CartItem` 2×, `PaymentMethod` 2× | Inkonsistente Preise/Bilder je Ansicht | Ein Domain-Modell und ein Mapper; Typen aus OpenAPI generieren |
| F-13 | Mittel | `routes/(store)/shop/*` vs. `lib/services/configurator*.ts` | Shop und Konfigurator nutzen getrennte Lade-, Preis- und Mapping-Wege (`fetchAllConfiguratorItems` vs. `products.*`), eigene Category-Typen, eigene Steuerberechnung (`configure/+page.svelte:2194`) | Doppelte Wartung, abweichende Preise | Gemeinsame Katalog- und Pricing-Services |
| F-14 | Mittel | `routes/(configurator)/configurator/results/+page.svelte:6-10`, `Footer.svelte:5` (`footerMocks`: Versand, Zahlarten, Siegel, WhatsApp-Nr.), `lib/mocks/cylinderMockData.ts` (1.474 Z., ungenutzt), `navigationItems.ts`, `topBarMocks.ts` | Mock-Daten im Produktivcode, Ergebnisseite zeigt Fake-Produkte | Falsche Inhalte live, Bundle-Ballast | Mocks entfernen bzw. nach `tests/fixtures`; Footer-Daten nach Strapi |
| F-15 | Mittel | `utils/urlUtils.ts:29`, `configuratorCylinders.ts:342`, `configuratorBrands.ts:6`, `productStore.ts:1602-1632`, `apiUserDataService.ts:131-150` | N+1 und Overfetching: alle Konfigurator-Items für Slug-Mapping/Markenliste; Warenkorb-Hydrierung 1 Request pro Position; Dashboard ruft `getOrders` bis zu 4× | Lange Ladezeiten, Last auf API | Fachliche Endpunkte (`/brands`, `/configurator/series/{slug}`, `/cart` mit aufgelösten Positionen) |
| F-16 | Mittel | `Header.svelte:48`, `TopBar.svelte:20`, `Footer.svelte:40`, `PromoBanner.svelte:79` | `settings` 4× pro Seitenaufruf mit `pLevel=5` | Unnötige CMS-Last, Flackern | Einmal im Root-Layout laden, per Context teilen; Caching |
| F-17 | Mittel | `sanitizeHtml.ts:11-33`, 46× `{@html}` mit `formatRichTextChild` (`strapiRichText.ts:3-17`, kein Escaping), `renderRichTextParagraphs` (`href` ungeprüft) | Rich-Text aus Strapi/ERP ohne Escaping; Sanitizer entfernt nur `<script>`/`on*`, nicht `javascript:`-URLs, `iframe`, `style` | XSS über CMS- oder ERP-Inhalte | Text escapen, Links whitelisten, DOMPurify; Security-Review |
| F-18 | Mittel | `session`-Init an 5 Stellen (`routes/+layout.ts:7`, `+layout.svelte:19`, `Header.svelte:44`, `(authenticated)/+layout.ts:10`, `(authenticated)/+layout.svelte:39`); `sessionService.ts:97` | Redundante Initialisierung; `status` gibt `access_token` an JS; Refresh nur bei „angemeldet bleiben“ | Token im JS-Kontext, Sessions laufen nach 15 min still ab | Ein Init im Root-`load`; `/auth/status` ohne Token; Refresh serverseitig per Sliding-Cookie oder 401-Interceptor |
| F-19 | Mittel | `userDataService.ts:55-329`, `dashboard/orders/+page.svelte:25-26` | Parallelwelt im localStorage für Adressen, Zahlarten (`iban`, `bic`, `last4`) und Bestellungen | Verwirrend, potenziell sensible Daten im Browser | Entfernen; Adressbuch-Endpunkte in .NET-API |
| F-20 | Mittel | `productStore.ts:1716-1726` | Cart-Speicherung als Promise-Queue mit Vollersetzung (Kommentar: „Overwrite newer state with stale data.“), kein Versions-/ETag-Konzept | Lost Updates bei mehreren Tabs/Geräten | Positionsbasierte Cart-API (PATCH/Items) mit Version/ETag |
| F-21 | Mittel | 90× `$effect`, z. B. `configure/[productId]/+page.svelte:131-478` (17 Effects hintereinander, async IIFEs ohne Abbruch), `cart/+page.svelte:53-61,103-108` | Effects für Daten-Loading und Ableitungen statt `load`/`$derived`; Race Conditions | Inkonsistente UI, doppelte Requests | Daten in `load`/Resources, Ableitungen mit `$derived`, AbortController |
| F-22 | Mittel | 117 `writable/derived` neben Runes; 11 Dateien `$app/stores` (deprecated, z. B. `routes/+layout.svelte:2`); `createEventDispatcher` (`RangeSlider.svelte:3,48`) | Gemischte Svelte-4/5-Muster | Uneinheitlich, künftige Deprecations | Schrittweise auf `$app/state`, Runes-Klassen (`*.svelte.ts`) und Callback-Props |
| F-23 | Mittel | `routes/+layout.svelte:25` + 25 Seiten mit `<link rel="canonical" href={window.location.href}>` | Doppelte Canonicals, inkl. Query-Parametern; SPA ohne SSR; nur 4 `meta description`; kein JSON-LD; `og:image` relativ (`shop/[slug]/+page.svelte:704`) | Schwaches SEO für einen Shop | SSR/Prerender für Katalog und CMS (adapter-node oder Prerender + ISR), ein Canonical, strukturierte Daten (Product, Breadcrumb) |
| F-24 | Mittel | `app.html:29,36-72` | Drittanbieter (Userlike, Trusted Shops, eTrusted, Händlerbund, GTM-noscript) laden vor Consent | DSGVO-/TTDSG-Risiko | Alle nicht notwendigen Skripte über CookieFirst-Kategorien gaten |
| F-25 | Mittel | `cms/strapi_cms/package.json` (5.30.1, populate-deep 4.0.5) vs. `package-lock.json` (5.6.0, populate-deep fehlt); `Dockerfile.prod` nutzt `npm install` | Lockfile nicht synchron | Nicht reproduzierbare Builds | `npm install` einmal lokal, Lock committen, Build mit `npm ci` |
| F-26 | Mittel | `messages/de.json` (1.096) vs. `en.json` (961) + hartkodierte Strings | 169 fehlende EN-Übersetzungen, gemischte Sprachen im Code | Halb-englische Seiten | i18n-Lint im CI, Strings extrahieren |
| F-27 | Niedrig | `frontend/src/lib/constants/shipping.ts:3-18` (`calculateShippingCost`), `paymentService.ts:111` (`getPaymentIntentStatus`), `cartService.ts:181` (`deleteCart`), `routes/api/hello.json.js` (0 Byte), `shop/+page.svelte:132-147` (Antwort verworfen), `userDataService.ts` | Toter Code | Verwirrung | Entfernen (knip/ts-prune) |
| F-28 | Niedrig | 11× `console.log` (u. a. `helpContentService.ts:104-129` loggt URL und komplette Strapi-Antworten, `RelatedProducts.svelte:26,43`), 142× `console.error`, 21× `console.warn` | Debug-Ausgaben im Prod-Bundle | Rauschen, Infos im Browser | Logger-Wrapper mit Level, `drop` in Vite-Prod-Build |
| F-29 | Niedrig | `routes/design-language/+page.svelte` (1.958 Z.), `routes/components-showcase/` | Interne Seiten öffentlich | Bundle-/SEO-Ballast | Nur im Dev-Build routen |
| F-30 | Niedrig | `Icon.svelte:63`; 51 `<img>`, davon 6 mit `loading`, 0 `srcset`, 0 `width/height` | Icons per Einzel-fetch, Bilder ohne responsive Varianten, Layout-Shift | Performance (LCP/CLS) | SVG-Sprite/Import zur Build-Zeit, `srcset` (Strapi-Formats, Bild-Service für Shop-Bilder), Dimensionen setzen |
| F-31 | Niedrig | `frontend/web_server/nginx.conf` | Keine Cache-Header, kein `gzip_static` trotz `precompress`, keine Security-Header (CSP, HSTS…) | Performance/Sicherheit | `gzip_static on`, immutable Cache für `/_app/immutable`, Security-Header (ggf. in Traefik) |
| F-32 | Niedrig | `frontend/Dockerfile:1` (`node:23.5`), `npm install` | Nicht-LTS-Node, nicht reproduzierbar | Build-Brüche | `node:22-alpine`, `npm ci` |
| F-33 | Niedrig | `StrapiManager.ts:96-101` | Sortierung `titleDesc`→`title:asc` / `titleAsc`→`title:desc` vertauscht | Falsche Sortierung | Korrigieren |
| F-34 | Niedrig | `StrapiManager.ts:135`, `helpContentService.ts:103`, `order-lookup/+page.svelte:61`, `payment/[orderId]/+page.svelte:57` | Query-Parameter ohne `encodeURIComponent` | Fehlerhafte Requests bei Sonderzeichen, Filter-Injection | `URLSearchParams` nutzen |
| F-35 | Niedrig | `Page.svelte:28` u. a. (`<!-- -svelte-ignore …-->`) | Wirkungslose Ignore-Kommentare (Tippfehler) | – | korrigieren/entfernen |
| F-36 | Niedrig | `contexts/configurator.ts:15-22` | Globaler `resize`-Listener auf Modulebene ohne Cleanup, `isMobileView` als Store | Leaks/HMR-Effekte | `MediaQuery` aus `svelte/reactivity` |
| F-37 | Niedrig | A11y: 92 `aria-*`, 21 `role`, alle `<img>` mit `alt` (5× leer), 1 klickbares `div` (Konfigurator) | Grundsätzlich bemüht; Fokus-Management vorhanden (`focusFirstError`), aber ohne Tests | Unklarer WCAG-Stand | axe-Checks in E2E, BFSG-Prüfung (seit 06/2025 Pflicht für Shops) |

---

## 6. Strapi

| Aspekt | Befund |
|---|---|
| Version | `@strapi/strapi` **5.30.1** laut `cms/strapi_cms/package.json`, Lockfile **5.6.0** (F-25). Node `>=18 <=22`, Prod-Image `node:22-alpine3.20` (`Dockerfile.prod`), in `docker-stack.yml:551` als Service `strapi_dev` mit `NODE_ENV=production` |
| DB | MariaDB 11.6 (mysql2 3.9.8); Fallback SQLite (`config/database.ts`) |
| Plugins | `@strapi/plugin-users-permissions`, `@strapi/plugin-cloud` (unnötig self-hosted), `strapi-v5-plugin-populate-deep` (liefert `pLevel`); `config/plugins.ts` leer; i18n (Core) aktiv |
| Content-Types | **41 Single-Types**: `home`, `settings` (PromoBanner, TopNavigation(+Right), MainNavigation, FooterNavigation1–3, FooterCompanyInfo), `global` (vom Frontend ungenutzt) und **38 Inhaltsseiten mit je eigenem Typ** (`agb`, `datenschutz`, `impressum`, `widerrufsrecht`, `kontakt`, `zahlung-und-versand`, `upload-formular`, diverse Ratgeber wie `schliesszylinder-richtig-messen`, `systembeschreibung-keso-*` …), alle mit `title` + Dynamic Zone `blocks`. **5 Collection-Types:** `news-article`, `news-category`, `faq`, `configurator-contextual-help`, `payment-method` (vom Frontend ungenutzt). Alle außer `payment-method` i18n-lokalisiert, Draft&Publish an (außer `settings`/`global`) |
| Komponenten | 83 (`src/components/{shared,page,product,footer}`): Hero, Slider, Testimonials, Product-Carousel (SKU-Liste → Katalog), Trust-Indicators, SEO, Navigation … `product/*` (price, sku, delivery …) wirkt wie ein früherer Ansatz, Produktdaten im CMS zu pflegen |
| Nutzung im Frontend | Seiten-Rendering via `ComponentResolver.svelte` (473 Z., Mapping `__component` → Svelte-Komponente), Navigation/Footer/Promo aus `settings`, News, FAQ, Konfigurator-Hilfe, Medien |
| Anpassungen | `src/utils/dynamic-zone-media-guard.ts` (Workaround für Strapi-5-Bug bei Single-Media in Dynamic Zones), registriert in `src/index.ts:12` |
| Konfiguration (kurz, Security separat) | `config/middlewares.ts` Default (CORS offen/Default, CSP Default); `config/api.ts` maxLimit 100; Admin-Flags NPS/promoteEE an; **Secrets eingecheckt** (F-10); Uploads lokal (`public/uploads`, Volume `cms_public`), `data/` mit 9,3 MB Seed (`data.json` + Uploads) und `scripts/seed.js` |
| Architekturbewertung | „Ein Content-Type pro Seite“ heißt: jede neue Seite braucht Schema-Änderung + Deployment, und `/[singlepage]` mappt den URL-Slug direkt auf den API-Namen. Empfehlung: Collection-Type `page` mit `slug` + Dynamic Zone. Unabhängig vom .NET-Umbau, aber für Redakteure wichtig |
| Kopplung an Backend | `_check_page` (Python) prüft CMS-Slugs für 404/410, `sitemap.py` liest CMS-Seiten. **Bei Migration nach .NET mit portieren** |

---

## 7. Offene Branches (`git branch -r --no-merged origin/main`)

Alle 18 Branches: Autor **DominicB**. `ahead`/`behind` relativ zu `origin/main`. „PR in main“ = gleichnamiger bzw. inhaltlich passender Squash-PR in der main-Historie gefunden. `git cherry`: „−“ = Patch bereits identisch in main.

| Branch | Letzter Commit | ahead/behind | Inhalt (git log origin/main..branch) | Status-Einschätzung |
|---|---|---|---|---|
| feature/cart_configure_button | 2026-04-16 | 1/113 | „add configure button in cart“ | PR 550, cherry „−“: **gemergt** |
| feature/home_page_redesign | 2026-04-04 | 1/113 | „home page strapi + frontend“ (51 Dateien) | PR 538, cherry „−“: **gemergt** |
| fix/configurator_cylinder_length | 2026-04-16 | 3/113 | fix items_svc, cylinder-length, lint | PR 549: gemergt (Squash) |
| feature/new_chatbot | 2026-04-23 | 1/110 | „chatbot update“ (app.html) | PR 567, cherry „−“: **gemergt** |
| feature/shop_api_connection_and_tracking | 2026-05-13 | 4/104 | AGB/Widerruf im Checkout, Varianten, Cart→API, Tracking | PR 612: gemergt (Squash) |
| fix/tracking_issues | 2026-05-15 | 5/102 | view_item-Dubletten, Preise, Kategorien, Netto-Preise, Konfig-Preise | PR 614: gemergt (Squash) |
| feature/consent_tool_integration | 2026-05-18 | 3/101 | Cookie-Banner, Tracking-Logging entfernt, Strapi-Icons | PR 616: gemergt (Squash) |
| feature/contact_form | 2026-05-19 | 1/99 | Kontaktformular (Frontend + Strapi) | PR 622, cherry „−“: **gemergt** |
| fix/prio_1_issues | 2026-05-19 | 1/99 | „fix dashboard price format“ | PR 621, cherry „−“: **gemergt** |
| feature/prio_1_issues | 2026-06-05 | 8/89 | Checkout mobil, Trusted-Shops-Badge/Widget, Schlüsselfarben, Purchase-Tracking, Stückpreis | PR 669: gemergt (Squash) |
| feature/prio_2_issues | 2026-06-05 | 8/84 | Gratisgeschenke, Dashboard-Positionen, Mobile-Preis, Spam-Schutz Kontaktformular | PR 670: gemergt (Squash) |
| fix/prio_1 | 2026-06-09 | 6/77 | Konfigurator/Cart-Fixes, productSeries, Checkout-Schlüssel | PR 679/675: gemergt (Squash) |
| fix/checkout_cylinders | 2026-06-09 | 1/75 | „removed old logic + fixed checkout items“ | PR 682, cherry „−“: **gemergt** |
| fix/prio_3_issues | 2026-06-11 | 1/74 | „fix prio 3 issues“ (inkl. `backend/auth_svc`) | PR 689, cherry „−“: **gemergt** |
| fix/delivery_time | 2026-06-15 | 4/66 | Lieferzeit auf PDP, Cart-Popup | PR 700: gemergt (Squash) |
| fix/cart_delivery | 2026-06-15 | 3/65 | Versand im Warenkorb, Versandtage, Gratisgeschenke | PR 702: gemergt (Squash) |
| fix/cart_gifts_configurator | 2026-06-16 | 4/63 | Gratisgeschenk-Kategorie, Konfigurator-Preise | PR 705: gemergt (Squash) |
| fix/minor_issues | 2026-06-17 | 2/62 | Config-Save, Übersetzungen | PR 707: gemergt (Squash) |

**Fazit:** Es sind keine unvollendeten Features erkennbar. Die Branches sind Überbleibsel des Squash-Merge-Workflows (Azure DevOps). Aktiv entwickelt wird seither direkt über neue PR-Branches (zuletzt PR 938 vom 2026-10-05). Empfehlung: Bei Branches mit cherry „+“ kurz `git diff origin/main...<branch>` prüfen, dann alle 18 löschen und „Delete source branch after merge“ aktivieren.

---

## 8. Empfehlungen für den Umbau auf die neue .NET-API

### 8.1 Strategie

1. **Vertrag zuerst festschreiben.** Die Tabellen in Abschnitt 3 in eine OpenAPI-Spezifikation überführen (Ist-Vertrag, „v0“). Daraus mit z. B. `openapi-typescript` Frontend-Typen generieren und als Contract-Tests gegen die .NET-API (z. B. Schemathesis/Pact) laufen lassen.
2. **Zweistufig migrieren:**
   - **Stufe 1, kompatible Fassade:** Die .NET-API bedient die heutigen Pfade (inkl. Tippfehler `/api/items/catagories`, Trailing-Slash-Varianten `/api/contact` und `/api/contact/`, PascalCase-Order-Struktur, `additionalProp1`, `detail`/`msg`-Fehlertexte). Sie **entschärft aber sofort** F-01 bis F-04: Einkaufspreise und fremde Kundengruppen entfernen, `customer_group_id` aus dem Body ignorieren und aus der Session ableiten, `single_order`/`order?user_id`/`cart/{id}` nur mit Auth oder Lookup-Token. Der generische Item-Filter wird serverseitig auf eine **Whitelist** von Feldern/Operatoren beschränkt und erzwingt die Sichtbarkeitsfilter (`is_active`, `in_shop` bzw. `in_configurator`, `active_sales_channels`).
   - **Stufe 2, fachliche v1-Endpunkte:** schrittweise ablösen, Frontend pro Domäne umstellen:
     - Katalog: `GET /v1/products?category=&manufacturer=&priceMin=&q=&page=`, `GET /v1/products/{id|slug}`, `GET /v1/products/{id}/related`, `GET /v1/categories`, `GET /v1/manufacturers`, `GET /v1/search?q=` (Suche serverseitig inkl. Umlaut-Normalisierung, `germanNormalization.ts`), `GET /v1/free-gifts`.
     - Preise: Antworten enthalten **fertige** `priceNet/priceGross/vatRate/strikePrice/discountPercent` für den Kontext (Kundengruppe aus Session, Lieferland aus Header/Query) → B1–B4, B10 aus dem Frontend entfernen.
     - Warenkorb: `GET/PUT/PATCH /v1/cart` (Session oder Gast-Cart-Token), Merge bei Login serverseitig, Antwort mit aufgelösten Positionen und Summen (`/v1/cart/quote?country=`) inkl. Versandoptionen, Gratisversand-Rest, Geschenk-Berechtigung (B5, B6, B17, F-15, F-20).
     - Checkout: `POST /v1/orders` nur mit Cart-Referenz, Adressen, gewählter `shippingMethodId` und Zahlart. Preise ausschließlich serverseitig. Uploads separat (`POST /v1/orders/{id}/attachments`).
     - Zahlung: `POST /v1/payments/intents {orderId}` + Webhook; Status-Polling über `GET /v1/orders/{id}/payment-status` statt kompletter Bestellung.
     - Konto: `GET /v1/me`, `GET /v1/me/orders`, `GET /v1/me/orders/{no}`, `GET/PUT /v1/me/addresses`, `GET /v1/me/stats`, `PUT /v1/me`, `PUT /v1/me/password`.
     - Gast: `POST /v1/orders/lookup {number,email}` (rate-limitiert).
     - Konfigurator: `GET /v1/configurator/brands`, `…/series/{slug}`, `…/products?filters`, Speichern `POST/GET /v1/configurator/configurations` (user_id aus Session), Preisberechnung serverseitig (separate Analyse).
     - Infrastruktur: `/_check_page` (404/410/Redirects) und `/sitemap.xml` portieren, ebenso Google-Merchant-Feed, Kontakt (mit echtem Captcha/Turnstile + Rate-Limit), Security-Card-Upload.
3. **Auth:** Cookie-basiert beibehalten (HttpOnly, Secure, SameSite=Lax), damit das Frontend kaum Änderungen braucht. `/auth/status` liefert **kein** Token mehr, nur User-Profil + Kundengruppe. Refresh per Sliding-Session oder 401-Interceptor im neuen API-Client (nicht nur bei „angemeldet bleiben“). CSRF-Schutz für state-changing Requests (Double-Submit-Token oder SameSite=Strict + Origin-Check). Fehlerformat vereinheitlichen (RFC 7807 ProblemDetails, Feldfehler als `errors{field:msg}`, kompatibel zu `authService` Zeilen 220/248/276).

### 8.2 Frontend-Vorarbeiten (unabhängig vom Backend, vor bzw. parallel zur Migration)

1. **Zentraler API-Client** `frontend/src/lib/api/client.ts` (Base-URL aus `PUBLIC_API_BASE_URL`/`VITE_API_URL`, Default `/api`, `credentials: 'include'`, Timeout/Abort, ProblemDetails-Parsing, 401-Handling) plus **ein Modul pro Domäne** (`catalog.ts`, `cart.ts`, `orders.ts`, `auth.ts`, `payment.ts`, `configurator.ts`, `cms.ts`). Alle rund 50 `fetch`-Stellen darauf umziehen. Danach ist der Backend-Wechsel ein Austausch hinter dieser Schicht.
2. **Charakterisierungstests** für Preis- und Warenkorblogik **vor** dem Umbau schreiben (F-07). Sie dienen später als Abnahmekriterien für die .NET-Pricing-Endpunkte (gleiche Eingaben → gleiche Summen). Playwright-E2E für Kernflows: Suche → PDP → Warenkorb → Checkout (Vorkasse + Stripe-Testmodus) → Erfolgsseite, Login/Registrierung, Gast-Lookup, Konfigurator speichern → in den Warenkorb.
3. **Domain-Modell konsolidieren:** ein `Product`/`CartItem`/`Order`-Typ, ein Mapper, Typen aus OpenAPI (F-12, F-13).
4. **Riesen-Komponenten zerlegen** (F-08), zuerst `productStore.ts` (Katalog und Warenkorb trennen; als Runes-Klassen) und `shop/+page.svelte` (Filter-State, Kategorie-Baum, Liste). Daten über `+page.ts`-`load` statt `onMount`/`$effect`.
5. **Mocks und toten Code entfernen** (F-14, F-27, F-29). `/configurator/results` an echte Daten anbinden oder ausblenden.
6. **Rendering-Entscheidung treffen:** Für SEO (F-23) und Tracking (F-09) `adapter-node` mit SSR für Katalog und CMS prüfen, alternativ Prerender der CMS-Seiten. Wenn SSR, lässt sich ein BFF-Muster (SvelteKit-Server ruft .NET-API intern) nutzen. Das versteckt die interne API-Topologie und erlaubt Server-Side-Tagging.
7. **Strapi aufräumen** (unabhängig): Lockfile synchronisieren, Secrets rotieren und aus Git entfernen (F-10, F-25), `settings` nur einmal laden (F-16), mittelfristig generischen `page`-Collection-Type einführen, `plugin-cloud` entfernen, Footer-Mocks ins CMS.

### 8.3 Checkliste „Vertrag, den .NET in Stufe 1 bedienen muss“ (Kurzform)

`/api/auth/v1/{register,login,forgot-password,reset-password,update,activate,status,refresh,logout}` ·
`/api/items/items/` (POST, Filter-DSL) · `/api/items/catagories[/]` (POST) · `/api/items/manufacturers/` (POST) · `/api/items/shipping/?country_code=` (POST) ·
`/api/cart/{id}` (GET/POST/DELETE) · `/api/configurator/` (GET `?config_id`, POST) ·
`/api/order/` (POST multipart `payload`+`files`, GET `?user_id`) · `/api/order/single_order` (GET `?number[&is_jtl_number]`) · `/api/order/payment_methods` · `/api/order/shipping_methods` · `/api/user/?identifier=` ·
`/api/payment/intents/` (POST) · `/api/payment/status/` (GET, ungenutzt) · Stripe-Webhook ·
`/api/contact[/]` (POST) · `/api/upload/security_card` (POST multipart) ·
`/_check_page` (Traefik ForwardAuth) · `/sitemap.xml` · Google-Merchant-Feed · Mail-/PDF-Endpunkte (intern).
