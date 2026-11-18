# 03 – Backend-Analyse & JTL-Integration (Webshop SN24)

Stand: 2026-10-07 · Grundlage: lokales Git-Repo `webshop/` (308 Commits, letzter Commit `9cc4a13` vom 2026-10-05). Rein statische Analyse: kein Netzwerkzugriff, keine Ausführung, keine Änderungen am Repo.
Alle Fundstellen sind als `Pfad:Zeile` relativ zum Repo-Root angegeben. Secrets sind maskiert (die ersten 4 Zeichen, dann „…“).

---

## 1. Zusammenfassung

**Kernaussage zur Behauptung „falsche/fehlende Daten kommen von undokumentierten JTL-API-Änderungen“:**
Die Behauptung lässt sich **nur in einem Fall belegen**, und auch dieser Fall hat eine hausgemachte Ursache. Die meisten Datenfehler lassen sich mit Fundstellen im eigenen Code erklären:

1. **Verschluckte Fehler auf allen Ebenen.** `fetch()` prüft keine HTTP-Statuscodes. `fetch_as_pydantic*()` geben bei Validierungsfehlern still `None` oder `[]` zurück. `handle_extra_data()` läuft unter `suppress(Exception)`. Beide Worker-Loops laufen unter `suppress(Exception)`, und das ohne jedes Logging. Antwortet JTL also mit 401, 404, 500 oder einer abweichenden Struktur, schreibt der Shop einen Artikel **ohne Bilder, Varianten, Sonderpreise, Merkmale oder Lieferanten in die DB und überschreibt dabei die vorher korrekten Daten**. Fundstellen: `backend/items_svc/src/jtl/jtl_helper/client.py:63-111`, `backend/items_svc/src/jtl/jtl_helper/build_api_items.py:707-715`, `backend/items_svc/src/jtl/worker.py:71-75`.
2. **Konkrete Mapping-Bugs**, die *immer* falsche Daten erzeugen, ganz ohne API-Änderung:
   - Merkmalwert-Beschreibungen gehen verloren: Ein Dict-Comprehension überschreibt Einträge, und die Lookup-Keys sind vertauscht.
   - Sonderpreise ohne Enddatum werden verworfen.
   - Fällt der Steuer-Abruf aus, gilt still ein Steuersatz von 1 %.
   - Artikel ohne Kategorie werden nie abgerufen.
   - Kategorien und Konfiguratordaten sind zwischen zwei Schreibvorgängen leer.
   - Seit 2026-06-05 werden Kundengruppen- und persönliche Rabatte in der Artikelliste nicht mehr angewendet.

   Details in den Befunden B-03 bis B-12.
3. **Abgeschaltete Synchronisation.** Seit Commit `ef0fe86` (2026-08-17, „create a new checkout logic“) ist der Abgleich von Aufträgen und Kunden **aus JTL in den Shop deaktiviert**: `"update_runner": False`, an seiner Stelle steht Debug-Code mit `print`. Versand- und Stornostatus und Kundenänderungen erreichen den Shop deshalb nicht mehr. Fundstelle: `backend/business/src/business_api/jtl_helper/worker.py:128,150-163`.
4. **Fehlende Version im API-Pfad.** Seit Commit `32709a1` (2025-07-25) werden alle Artikel-Unterressourcen (`/items/{id}/images|variations|specialprices|properties|…`) **ohne `/v1`-Präfix** aufgerufen, ebenso der komplette Auftrags- und Kundenpfad im business-Service. Im selben Commit änderte sich die erwartete Struktur von `/items/{id}/properties` (Liste → Objekt mit `Properties`). Ruft man ohne Versionspfad auf, bekommt man mutmaßlich das Verhalten der jeweils neuesten Version des JTL-REST-Servers. Dann **ändert jedes Wawi-Update potenziell das Antwortschema**. Das ist wahrscheinlich der reale Kern der „undokumentierten Änderungen“, aber selbst verschuldet, weil die Version nicht festgeschrieben ist. Das muss gegen die OpenAPI-Beschreibung der eingesetzten Wawi-Version verifiziert werden.
   > **Live-Prüfung 08.10.2026 (auf dem JTL-Server gegen `localhost`):** Alle 12 geprüften Abfragen (Artikelliste, Artikel, 8 Unterressourcen, Kunden, Aufträge) liefern **mit und ohne `/v1` denselben Status (200) und dieselben Felder**. Die fehlende Versionsangabe erklärt die Datenprobleme beim aktuellen JTL-Stand also **nicht**; sie bleibt ein Zukunftsrisiko (neue JTL-Version). Stichproben: Artikel-Id 7 und 71679 (beide identisch), Feldnamen des jeweils ersten Datensatzes; `variations/{vid}/values` noch ungeprüft (Artikel 71679 ohne Variationen). Die Vermutung „realer Kern der undokumentierten Änderungen“ ist damit für den heutigen Stand **widerlegt**.
5. **Einziger expliziter Hinweis auf eine JTL-Änderung:** Commit `1ba1491` (2026-06-09) mit der Nachricht „Fix fetching all items from JTL after there breaking update“. Als Workaround wird seitdem pro Kategorie abgerufen (`backend/items_svc/src/jtl/jtl_helper/raw_items.py:61-89`). Folge: Artikel ohne Kategoriezuordnung fehlen dauerhaft. Was sich bei JTL konkret geändert hat, ist nirgends dokumentiert.

**Weitere kritische Befunde (Auswahl):**

- **Öffentliche, nicht authentifizierte Endpunkte:**
  - `PATCH /api/order/{id}/mark_paid` markiert Bestellungen ohne Prüfung als bezahlt und bucht sie in JTL.
  - IDOR-Lücken für Bestellungen, Mail-Daten mit Rechnungs-PDF, Warenkörbe und Konfigurationen.
  - Unsandboxed Jinja im `pdf_svc` (`/api/pdf/v1/generate/template_string`), also ein mögliches RCE.
- **Bestellungen, die in JTL nicht angelegt wurden, gelten trotzdem als „in ERP angelegt“** (`backend/business/src/business_api/utils/order.py:607-611`). Sie werden nie erneut versucht und fehlen in JTL.
- **Serverseitige Validierung:**
  - Preise werden serverseitig neu berechnet (positiv), aber mit fest kodierten 19 % MwSt., ohne Kundengruppe und auf Basis eines Mongo-Caches.
  - **Bestände werden nicht geprüft und für normale Artikel nicht einmal aus JTL gelesen.**
- **Secrets im Klartext im Repo:** `Readme.md:18-30` enthält JTL-Token, Custom-API-Token, Stripe-Secret, Webhook-Secret und SMTP-Passwort. Dazu kommen JTL-Shop-Blowfish-Key, Strapi-Secrets und ein fest kodierter JWT-Key (`foobar`).
- **Tests:** für items_svc, mail_svc und pdf_svc keine. Insgesamt geschätzte Testabdeckung des Backends unter 10 %, für die JTL-Integration 0 %. Keine Backend-CI.

**Empfehlung für die .NET-Ablösung:**
- Den POC ausschließlich auf **versionierte** JTL-Endpunkte (`/api/eazybusiness/v1/...`) aufsetzen.
- Rohantworten persistieren (für Vergleich und Beweisführung).
- Streng typisierte DTOs mit lautem Fehlerverhalten (Validation → Log/Metrik, kein Überschreiben guter Daten).
- Die „Custom API“ (direkter SQL-Zugriff auf die eazybusiness-DB) als eigenständiges Risiko behandeln und schrittweise ersetzen.

---

## 2. Architektur

### 2.1 Überblick

```
                Traefik (/api/*)
                     │
 ┌──────────┬────────┼──────────┬───────────┬─────────────┐
 │auth_svc  │business│items_svc │payment_svc│ pdf_svc     │  (FastAPI, Python 3.13, je 4 uvicorn-Worker im Stack)
 │/api/auth │/api    │/api/items│/api/payment│/api/pdf    │
 └────┬─────┴───┬────┴────┬─────┴─────┬─────┴─────────────┘
      │ MQ      │ HTTP    │           │ HTTP (mark_paid)
      ▼         ▼         ▼           ▼
   RabbitMQ  business_worker   items_svc_worker      mail_svc_worker (MQ "send_mail")
      │         │   │              │   │
   Mongo(auth) Mongo(business)   Mongo(items)  Mongo(payment)
                │   │              │   │
                ▼   ▼              ▼   ▼
         JTL-Wawi REST API      "CUSTOM_API" (eigener Dienst, direkter SQL-Zugriff auf eazybusiness)
         {JTL__DOMAIN}/api/eazybusiness
```

- Deployment-Varianten:
  - `docker-compose.yml` (Dev) und `docker-compose_deploy.yml`. **Achtung:** `docker-compose_deploy.yml` bindet ebenfalls `./compose/services/*` (Dev-Services mit `--reload` und Quellcode-Mounts) ein, nicht `compose/services_deploy/*`.
  - `docker-stack.yml` (Swarm, 3 Nodes, `env_file: .env`).
- Gemeinsame interne Bibliothek **`svc-helper`**: öffentliches Repo in der GitLab-Gruppe `vitandi1` eines Ex-Entwicklers (`gitlab.com/vitandi1/svc_helper.git`), Rev `5e65dee9` (`backend/*/uv.lock`). *Korrektur 07.10.2026: Das Repo ist öffentlich, nicht privat; Kopie unter `referenz/svc_helper`.* Es stellt Mongo-Wrapper, Query-Builder, MQ (pika), JWT-Prüfung (`get_current_user_id`) und `parallel_execute` bereit. **Der Code ist nicht im Shop-Repo.** Das ist ein Single-Point-of-Knowledge (Konto einer Person). Teile des Verhaltens (Auth, Mongo-Upsert, Query-Builder) wurden in dieser Analyse nicht geprüft; der Code liegt jetzt gesichert vor.
- **Valkey** (`compose/services/backend/cache.yml`, `docker-stack.yml:386-419`) wird betrieben, aber **von keinem Service genutzt**: kein Redis/Valkey-Client im Code, `backend/valkey/valkey.conf` ist leer (0 Zeilen). In Dev ist er auf Host-Port 11020 ohne Passwort freigegeben. Gecacht wird nur in Mongo (items als vorberechnete Dokumente) und im Prozess (Sitemap, `backend/business/src/business_api/v1/endpoints/sitemap.py:114-228`).

### 2.2 Python-Version und Bibliotheken

Alle Services: `.python-version` = **3.13**, `requires-python >=3.13`. Aufgelöste Versionen aus `uv.lock`:

| Service | fastapi | pydantic | pydantic-settings | httpx | pymongo | pika | uvicorn | weitere |
|---|---|---|---|---|---|---|---|---|
| auth_svc | 0.115.8 | 2.10.4 | 2.8.0 | 0.28.1 | 4.11.1 | 1.3.2 | 0.34.0 | pyjwt 2.10.1, argon2-cffi 23.1.0, bcrypt 4.2.1 |
| business | 0.116.1 | 2.11.9 | 2.10.1 | 0.28.1 | 4.15.0 | 1.3.2 | 0.35.0 | jinja2 3.1.6, python-barcode 0.16.1, filelock 3.32.2 |
| items_svc | 0.115.14 | 2.11.7 | 2.10.1 | 0.28.1 | 4.13.2 | 1.3.2 | 0.35.0 | nh3 0.2.21, beautifulsoup4 4.14.2 |
| mail_svc | 0.121.0 | 2.12.4 | 2.11.0 | 0.28.1 | 4.15.3 | 1.3.2 | 0.52.3 | – |
| payment_svc | 0.136.1 | 2.13.3 | 2.14.0 | 0.28.1 | 4.17.0 | 1.3.2 | 0.46.0 | stripe 15.1.0, starlette 1.0.0 |
| pdf_svc | 0.115.11 | 2.10.4 | 2.8.1 | 0.28.1 | 4.11.2 | 1.3.2 | 0.34.0 | weasyprint 69.0, mistletoe 1.4.0 |

Alle Services: `svc-helper` (git, Rev 5e65dee9). Die Versionen driften zwischen den Services stark (FastAPI 0.115 bis 0.136, Starlette 0.41 bis 1.0).

### 2.3 Services im Detail

#### items_svc (`backend/items_svc`, Paket `jtl_data_collector`)
- **Zweck:** Artikel-Read-Model. Ein Worker zieht Artikel und Stammdaten aus JTL und der Custom API, berechnet Ableitungen und legt alles in Mongo ab. Die API liest nur aus Mongo.
- **DB:** Mongo `jtl.items` (Indizes `backend/items_svc/src/jtl/core/data_stores.py:14-47`) und `jtl.item_globals` (ein Dokument `_id: "main"`).
- **Endpunkte** (alle **ohne Auth**, Root-Path `/api/items`):

| Methode | Pfad | Request | Response |
|---|---|---|---|
| GET | `/health` | – | `{health}` |
| POST | `/items/` | Body: `api_query: APIQuery` (filters/sort/pagination, svc_helper), `excluded_fields: list`, `user_id: str` (Default `"userID"`) | `PaginatedResponse[Item]` (JSON) |
| POST | `/items/count` | `list[int]` | `{amount}` |
| GET | `/items/all-urls` | – | `list[str]` |
| POST | `/catagories/` (sic, Tippfehler) | `ids: list[int]`, `must_be_in_a_store: bool` | `Categories{main_ids, by_ids, missing}` |
| GET | `/catagories/` | – | `dict[int,str]` |
| GET | `/catagories/all-urls` | – | `list[str]` |
| GET | `/attribute_collections/custom_fields` | – | `dict[int, CustomField]` |
| GET | `/attribute_collections/used_properties` | – | `list[str]` (lädt **alle** Artikel in den Speicher) |
| POST | `/manufacturers/` | – | `dict[int, Manufacturer]` |
| POST | `/shipping/` | `item_classes: list[ShippingClass]`, `country_code` | `list[ShippingMethod]` (Versandlogik fest kodiert, `backend/items_svc/src/jtl/v1/utils/shipping.py`) |
| GET | `/google_merchant/`, `/google_merchant/csv`, `/google_merchant/csv-stream` | – | XML/CSV-Feed |

- **Abhängigkeit:** `POST /items/` ruft für Rabatte `GET http://business_api:11000/user/?identifier=…` und `/user/customer_groups` auf (`backend/items_svc/src/jtl/v1/utils/discount_logic.py:93-115`).
- **Hintergrundjob** `items_svc_worker` (`backend/items_svc/src/jtl/worker.py`), Ablauf:
  1. Beim Start: hart kodierter Debug-Artikel `41003` (`backend/items_svc/src/jtl/worker.py:103-105,123-125`).
  2. Danach zwei Endlosschleifen über `parallel_execute`:
     - `updater`: alle 5 min (`refresh_intervall=300`, `backend/items_svc/src/jtl/core/config.py:32`) erst Global-Daten, dann inkrementelle Artikel (`changedSince`). Zwischen 0 und 3 Uhr UTC stattdessen ein Vollabgleich.
     - `update_all`: zwischen 0 und 3 Uhr UTC alle 5 min ein Vollabgleich.
   - Zwischen 0 und 3 Uhr laufen also **zwei Vollabgleiche parallel und wiederholt**. Ob `parallel_execute` blockiert und damit `update_all` nie startet, ist ohne den svc_helper-Code nicht feststellbar.

#### business (`backend/business`, Paket `business_api`)
- **Zweck:** Checkout/Bestellungen, Nutzer-Mapping Shop ↔ JTL, Warenkorb, Konfigurator-Speicher, Mail-Daten, Sitemap, Seitenprüfung für Traefik.
- **DB:** Mongo `business.*`, nämlich `user`, `user_erp`, `order`, `order_erp`, `globaluser_erp`, `payment_log_erp`, `configurations`, `carts` (`backend/business/src/business_api/core/data_stores.py:8-63`). Dazu Dateiablage `resources/order_attachments` mit `filelock`.
- **Endpunkte** (Root-Path `/api`). Auth heißt hier: Cookie `access_token`, JWT-Prüfung über svc_helper.

| Methode | Pfad | Auth | Zweck / Modelle |
|---|---|---|---|
| GET | `/health`, `/health_auth` | – | |
| POST | `/order/` | optional (Cookie) | Form `payload` = `OrderPayload` (JSON), `files[]` → `MsgCreated{id}` |
| GET | `/order/?user_id=` | **keine** wirksame (Fallback auf Query-`user_id`) | Liste der Bestellungen eines Nutzers |
| GET | `/order/single_order?number=` | **keine** | Bestellung inkl. Adressen |
| PATCH | `/order/{order_id}/mark_paid` | **keine** | Body `{status, intent_id}` → Bestellung bezahlt + ERP-Übergabe |
| GET | `/order/payment_methods`, `/order/shipping_methods` | – | aus `globaluser_erp` |
| GET | `/order/send_order_to_system/{order_id}` | **keine** | stößt ERP-Übergabe und Mail an |
| GET | `/user/?identifier=` | JWT (identifier wird ignoriert) | `FrontendUser` |
| GET | `/user/customer_groups` | – | `list[CustomerGroup]` |
| POST/GET/DELETE | `/cart/{user_id}` | **keine** | `Cart`/`StoredCart` |
| POST/GET | `/configurator/` | **keine** | `StoredConfig` |
| GET | `/maildata/order/{nr}`, `/order_pdf/{nr}`, `/shipping/{nr}`, `/cancel/{nr}`, `/payed/{nr}` | **keine** | Mail-Kontext inkl. Name, Adresse, Positionen bzw. JTL-Auftrags-PDF als Base64 |
| POST | `/contact/` | – | `ContactFormRequest` → MQ `send_mail` |
| POST | `/upload/security_card` | – | Formular + Dateien → MQ `send_mail` (keine Größen- oder Typprüfung) |
| GET | `/sitemap.xml`, POST `/sitemap/refresh` | – | |
| GET | `/_check_page` | – | Forward-Auth für Traefik (200/404/410) |

- **Abhängigkeiten:**
  - `items_svc` (`http://items_svc:11000/items/`, `backend/business/src/business_api/utils/items.py:9`)
  - `pdf_svc` (`backend/business/src/business_api/utils/pdf.py:316`)
  - RabbitMQ (`send_mail`, `user_registered`, `user_updated`, `payment_intent_updated`)
  - JTL REST und Custom API
- **Hintergrundjob** `business_worker` (`backend/business/src/business_api/worker.py`):
  - MQ-Consumer: Registrierung → Kunde in JTL anlegen; User-Update; Payment-Intent-Update (der zugehörige Push im payment_svc ist toter Code, siehe B-31).
  - Danach `jtl_worker.run()`. **Seit 2026-08-17 ist der Updater-Loop deaktiviert** und gibt nur offene Bestellungen per `print` aus.

#### payment_svc (`backend/payment_svc`)
- Stripe-PaymentIntents. Mongo `payment` (Zugangsdaten über `MONGO_PASSWORD`).
- Endpunkte (Root `/api/payment`, ohne Auth):
  - `POST /intents/`: `PaymentIntentRequest{order_id}`. Holt den Betrag (`StillToPay`) über `GET business_api/order/single_order` und erzeugt den Intent; ein vorhandener Intent zur Order wird wiederverwendet.
  - `GET /status/?intent_id=`
  - `POST /webhook/`: Stripe-Signaturprüfung, danach `PATCH business_api/order/{order_id}/mark_paid`.

#### auth_svc (`backend/auth_svc`)
- Registrierung, Login (Passwort, JWT, Token), Refresh, Logout, Passwort-Reset, Aktivierung, Status, Update. Mongo `auth.user`.
- Router mit Prefix `/{MAIN_VERSION}` (`backend/auth_svc/src/auth/main.py:23-25`).
- Push nach MQ `user_registered`/`user_updated` → business_worker legt den Kunden in JTL an.
- Dazu das Skript `backend/auth_svc/src/auth/migrate_jtl_data.py`: Migration aus einem Dump des alten JTL-Shops (`tkunde.sql`), XTEA/Blowfish-Entschlüsselung von Namen, Übernahme von bcrypt-Hashes.

#### mail_svc (`backend/mail_svc`)
- Worker: MQ `send_mail` → Jinja-Templates → SMTP (`backend/mail_svc/src/mail/utils/mail.py`).
- Zusätzlich eine Test-FastAPI `GET /test_mail`, die **Produktions-URLs** `https://sicherheit.meesenburg.com/api/maildata/...` abruft und an fest kodierte Mitarbeiteradressen sendet (`backend/mail_svc/src/mail/main.py:20-24,48-60,164-181`).

#### pdf_svc (`backend/pdf_svc`)
- WeasyPrint. Endpunkte unter `/api/pdf/v1/generate/`: `html`, `markdown`, `template_string`, `template`. Ohne Auth.

---

## 3. JTL-Integration (Schwerpunkt)

### 3.1 Verbindungsaufbau

**JTL-Wawi REST (eazybusiness)**, identisch in `backend/items_svc/src/jtl/jtl_helper/client.py:24-42` und `backend/business/src/business_api/jtl_helper/client.py:105-126`:

```
Base-URL : urljoin(JTL__DOMAIN, "/api/eazybusiness")   → z.B. http(s)://<wawi-host>:<port>/api/eazybusiness
Header   : Accept: application/json
           X-AppId: <JTL__APP_ID>          (Default "icsTest/v3", .env-Beispiel: "icsW…")
           X-AppVersion: <JTL__APP_VERSION> (Default "1.0.3")
           Authorization: <JTL__API_TOKEN>  (kompletter Wert inkl. Präfix, Format "Wawi <uuid>", Readme: "Wawi dbb0…")
Timeout  : 20 s (items) / 10 s (business), einzelne Calls 15–45 s
```

Konfiguration über pydantic-settings mit `env_nested_delimiter="__"`, also `JTL__DOMAIN`, `JTL__API_TOKEN`, `JTL__APP_ID`, `JTL__APP_VERSION` (`backend/items_svc/src/jtl/core/config.py:12-17`, `backend/business/src/business_api/core/config.py:33-39`).

**„CUSTOM_API“**, `backend/items_svc/src/jtl/jtl_helper/client.py:45-60` bzw. `backend/business/src/business_api/jtl_helper/client.py:129-148`:

```
Base-URL : CUSTOM_API__DOMAIN (ohne Pfad-Präfix)
Header   : Accept: application/json
           x-token: <CUSTOM_API__API_TOKEN>   (Readme: "15e1…")
```

> **Konfigurationsfehler:** Die Compose-Dateien setzen `CUSTOM_API__TOKEN` (`compose/services/backend/items_svc.yml:72`, `compose/services/backend/business_api.yml:36,83`, `compose/services_deploy/backend/*.yml`). Das Settings-Feld heißt aber `api_token`, gelesen wird also `CUSTOM_API__API_TOKEN`. Bei Compose-Betrieb bleibt deshalb der Default `"abc"`. Nur im Swarm-Stack funktioniert es, weil dort `.env` per `env_file` mit `CUSTOM_API__API_TOKEN` geladen wird (laut `Readme.md:22`).

**Was ist die Custom API?** Ihr Quellcode ist **nicht im Repo**. Aus den Aufrufen ergibt sich:
- Sie liefert Rohdaten mit **JTL-DB-Spaltennamen**: `kKonfiggruppe`, `kKonfigitem`, `kArtikel`, `kEigenschaftWert`, `fAufpreisNetto`, `fLagerbestand`, `cArtNr`, `kKundengruppe`, `kSteuerklasse` (`backend/items_svc/src/jtl/v1/utils/global_data_logic.py:310-417`).
- Sie nimmt Zahlungen mit den Spalten der Tabelle `tZahlung` entgegen: `cName`, `dDatum`, `fBetrag`, `kBestellung`, `kBenutzer=5`, `kZahlungsart`, `nZuweisungstyp` … (`backend/business/src/business_api/utils/order.py:567-582`).
- Sie ändert Auftragspositionen (Preise, Konfig-Zuordnungen, Variationen, Versandtext).

→ Es handelt sich um **einen eigenen Dienst mit direktem SQL-Lese- und Schreibzugriff auf die JTL-eazybusiness-Datenbank** (MS SQL). Er umgeht die JTL-Geschäftslogik und ist ein eigenständiges Risiko: Schemaänderungen bei Wawi-Updates schlagen hier direkt durch, und es fehlen JTL-Validierungen und Workflows.

**`trigger_up.sql`** legt in der JTL-DB (Default-Schema, i. d. R. `dbo`) zwei Tabellen an:
- `SalesOrderChanges(id, kSalesOrder, changedAt, processing)` und `CustomerChanges(id, kCustomer, changedAt, processing)`.
- Dazu **16 AFTER-INSERT/UPDATE-Trigger** auf `Verkauf.tAuftrag`, `tAuftragAdresse`, `tAuftragAttribut`, `tAuftragAttributSprache`, `tAuftragEckdaten`, `tAuftragFeatures`, `tAuftragFile`, `tAuftragKampagne`, `tAuftragPosition`, `tAuftragPositionEckdaten`, `tAuftragPositionEigenschaft`, `tAuftragRechnung`, `tAuftragStorno`, `tAuftragText`, `tAuftragZahlungsinfo` und `dbo.tkunde`. Sie schreiben die geänderte Auftrags- bzw. Kunden-ID in die Change-Tabelle, wenn dafür noch kein unverarbeiteter Eintrag existiert (`trigger_up.sql:5-363`).
- `trigger_down.sql` entfernt Trigger und Tabellen wieder.
- Die Custom API stellt diese Tabellen als `/sales_order_changes` und `/customer_changes` bereit (GET/DELETE).
- **Für Artikel gibt es keinen Trigger.** Artikeländerungen werden ausschließlich über `changedSince` der REST-API erkannt.
- Risiken:
  - Fremd-Trigger in der Wawi-DB sind von JTL nicht unterstützt und können Wawi-Updates oder Performance beeinträchtigen.
  - `processing` wird im Shop-Code nie gesetzt.
  - Konsumiert werden die Tabellen seit 2026-08-17 ohnehin nicht mehr (B-02). Sie wachsen also unbegrenzt, gebremst nur durch die NOT-EXISTS-Deduplizierung.

### 3.2 Gelesene und geschriebene Daten – Ablauf

**Artikel (items_svc), ein Sync-Lauf** (`backend/items_svc/src/jtl/worker.py:25-62`):

1. **Global-Daten aus REST** (`update_global_data`, `backend/items_svc/src/jtl/v1/utils/global_data_logic.py:439-461`): Steuerklassen und Steuersätze, Item-Overview, Custom-Field-Definitionen, Hersteller, Warengruppen, Versandklassen, Versandarten, Merkmale mit Werten und Wertbeschreibungen, Lieferanten, Zustände.
   → Danach wird das Dokument `item_globals/main` **mit `categories={}` und `shipping_methods={}` überschrieben** (`backend/items_svc/src/jtl/v1/utils/global_data_logic.py:452,456,460`). Variationsdaten, Konfiggruppen, Mappings und Einheiten fallen dabei auf ihre Defaults zurück und sind leer.
2. **Global-Daten aus der Custom API** (`update_global_data__custom_api`, `backend/items_svc/src/jtl/v1/utils/global_data_logic.py:483-519`): Konfiggruppen, Konfigitems, Versandarten, Variationswert-Zusatzdaten (Aufpreis, Gewicht, Lagerbestand, Barcode, ArtNr), Kategorien inkl. Rabatte und SEO, Kind-Artikel-Variations-Mapping, Artikeleinheiten. Danach ein zweiter Schreibvorgang.
3. **Artikel:** `GET /v1/categories` (paginiert), dann **pro Kategorie** `GET /v1/items?categoryId=…&pageSize=100&pageNumber=…`, inkrementell zusätzlich mit `changedSince` (`backend/items_svc/src/jtl/jtl_helper/raw_items.py:61-129`).
4. **Pro Artikel** (`handle_extra_data`, `backend/items_svc/src/jtl/jtl_helper/build_api_items.py:337-375`), sequenziell und **ohne `/v1`**: images (und je Bild ein Custom-API-Call für die Sortierung), Bild-Binärdaten (nur wenn die Datei lokal noch fehlt), customfields, descriptions, variations plus values je Variation, specialprices, salesChannelPrices, properties, suppliers.
   → Pro Artikel ergeben sich **mindestens 9 + #Variationen + 2 × #Bilder HTTP-Calls** (N+1-Muster). Parallelisierung oder Rate-Limit-Handling gibt es nicht.
5. **Ableitungen** (`backend/items_svc/src/jtl/jtl_helper/evaluated_data.py`):
   - `in_configurator` und `in_shop` hängen am **Kategoriepfad-Namen** „Schließanlagen“ › „mechanisch|mechatronisch|elektrisch“ (Zeilen 17-32).
   - Zylinderlängen werden per Regex aus **fest kodierten Variationsnamen** gelesen (Zeilen 48-240).
   - Express-Kennzeichen über Herstellernamen „Keso“/„Dormakaba“ (Zeilen 399-411).
   - Kundengruppenpreise = Normalpreis × (100 − höchster Kategorierabatt) (`backend/items_svc/src/jtl/jtl_helper/build_api_items.py:468-523`).
6. **Upsert** in `jtl.items` nach `id`. Der Datensatz wird **zweimal pro Artikel** geschrieben (`backend/items_svc/src/jtl/jtl_helper/build_api_items.py:714-715`).
- **Bestände:** Für Standardartikel wird **kein Bestand** aus JTL gelesen; es gibt keinen `stocks`-Endpunkt. Vorhanden sind nur `fLagerbestand` je Variationswert (Custom API) und `Stocklevel` je Lieferant. Der Google-Feed meldet immer „in stock“ (`backend/items_svc/src/jtl/v1/utils/google_merchant.py:125-137`).

**Kunden (business):**
- **Lesen:** `GET /customers/{id}` sequenziell aufsteigend ab der höchsten bekannten ID, bis eine ID keine gültige Antwort liefert (`backend/business/src/business_api/jtl_helper/raw_users.py:45-71`). Eine Lücke (gelöschter Kunde) stoppt den Abgleich.
- **Schreiben:** `POST /customers` bei Registrierung oder Gastbestellung (`backend/business/src/business_api/jtl_helper/user_handling.py:55-106`). Schlägt das fehl, wird eine **zufällige negative ID** als Platzhalter vergeben.

**Aufträge (business):** Schreiben siehe Kapitel 4. Lesen:
- `GET /salesOrders/{id}` (Timeout 45 s) und Positionen über die Custom API (`/order/line_items`).
- Der Change-Tabellen-Abgleich ist deaktiviert.
- `fetch_newest_sales_orders` (sequenziell bis zur ersten Lücke) wird nicht mehr aufgerufen.

**Business-Stammdaten** (`globaluser_erp`): salesChannels, customerGroups, customerCategories, paymentMethods, shippingMethods (REST) und companies (Custom API) über `fetch_global_data` (`backend/business/src/business_api/jtl_helper/raw_users.py:74-132`). Der Aufruf ist **nur hinter dem Flag `"test_fetch_global_data": False`** erreichbar (`backend/business/src/business_api/jtl_helper/worker.py:127,141`). Die Daten werden also **nie automatisch aktualisiert**.

### 3.3 Fehlerbehandlung bei fehlenden oder abweichenden Feldern

| Stelle | Verhalten | Folge |
|---|---|---|
| `fetch()` items (`backend/items_svc/src/jtl/jtl_helper/client.py:63-81`) | kein `raise_for_status`; JSON-Fehlerkörper (401/404/500) wird als Daten zurückgegeben; nur Timeout/HTTPError wird geloggt | Fehlerantworten werden als Daten weiterverarbeitet |
| `fetch_as_pydantic` (`backend/items_svc/src/jtl/jtl_helper/client.py:84-96`) | `ValidationError` → `None`, **ohne Log** | Sonderpreise/Merkmale fallen still weg |
| `fetch_as_pydantic_list` (`backend/items_svc/src/jtl/jtl_helper/client.py:99-111`) | jede Exception → `[]`, **ohne Log**; ein ungültiger Eintrag verwirft die **ganze** Liste | z. B. alle Kategorien, Hersteller oder Bilder weg |
| Raw-Modelle `RawItem` u. a. (`backend/items_svc/src/jtl/schemas/item_raw_schemas.py:89-118`) | Pflichtfelder ohne Default (z. B. `Gtin`, `ReleasedOnDate`, `ConditionId`); zusätzliche Felder werden ignoriert | ein Artikel mit `null` in einem Pflichtfeld bricht **die ganze Seite und damit den ganzen Sync-Lauf** ab (Exception im Generator) |
| `handle_extra_data` unter `suppress(Exception)` (`backend/items_svc/src/jtl/jtl_helper/build_api_items.py:707-708`) | Abbruch beim ersten Fehler; die übrigen Unterressourcen bleiben leer; der Artikel wird trotzdem gespeichert | **gute Bestandsdaten werden mit leeren Listen überschrieben** |
| Worker-Loop `suppress(Exception)` (`backend/items_svc/src/jtl/worker.py:71-75`) | kein Log | Sync-Ausfälle bleiben unsichtbar; `cached_last_time` ist schon vorgerückt (`backend/items_svc/src/jtl/jtl_helper/raw_items.py:102`), **Änderungen aus dem Fenster gehen verloren** |
| Custom-API-Global-Daten (`backend/items_svc/src/jtl/v1/utils/global_data_logic.py:486-516`) | keine Prüfung; `None` aus `fetch` → `TypeError` | Global-Dokument bleibt mit leeren Kategorien und Variationsdaten stehen |
| business `Base` mit `extra="forbid"` (`backend/business/src/business_api/schemas/base.py:4-9`), gilt u. a. für `Address`, `SalesOrderShippingDetail`, `SalesOrderPaymentDetails`, `CustomerGroup`, `PaymentMethod`, `Company` | **jedes neue Feld, das JTL zusätzlich liefert, führt zum ValidationError** | das ist die einzige Stelle, an der *additive* JTL-Änderungen Fehler auslösen würden – selbst verschuldet durch `forbid` auf Eingangs-DTOs |
| Change-Handling (`backend/business/src/business_api/jtl_helper/change_handling.py:17-47`) | Fehler beim Update wird geloggt, die Change-ID aber **trotzdem gelöscht** | Änderung geht dauerhaft verloren |

### 3.4 Endpunkt-Tabelle für den .NET-POC

Base: `{JTL__DOMAIN}/api/eazybusiness`. Alle Requests tragen die Header `Accept: application/json`, `X-AppId`, `X-AppVersion` und `Authorization: Wawi <API-Key>`.

**Empfehlung für den POC:** Für alle Pfade die Variante **mit `/v1`** verwenden. Die unversionierten Varianten aus dem Bestandscode nur parallel abfragen, um Abweichungen zu dokumentieren.

Paging-Antwort (genutzte Felder): `Items[]`, `TotalItems`, `PageNumber`, `PageSize`, `TotalPages`, `HasPreviousPage`, `HasNextPage`, `NextPageNumber`, `PreviousPageNumber` (`backend/items_svc/src/jtl/schemas/item_raw_schemas.py:121-129`). Paging-Parameter: `pageNumber`, `pageSize`.

#### A. JTL-REST – Artikeldaten (POC-relevant)

| # | Methode | Pfad (Bestandscode) | Query | Fundstelle | Tatsächlich genutzte Felder |
|---|---|---|---|---|---|
| A1 | GET | `/v1/items` | `pageSize=0&pageNumber=1` | `raw_items.py:35-44` | `TotalItems` (Overview) |
| A2 | GET | `/v1/categories` | `pageSize=100&pageNumber=n` | `raw_items.py:9-32` | `Id`, `Name`, `Description`, `ParentCategoryId`, `Level`, `SortNumber`, `ActiveSalesChannels` (nur die IDs werden fürs Paging der Artikel verwendet) |
| A3 | GET | `/v1/items` | `pageSize=100&pageNumber=n&categoryId=c` (voll) bzw. zusätzlich `changedSince=<Zeitstempel>` (inkrementell) | `raw_items.py:61-129` | siehe **Feldliste RawItem** unten |
| A4 | GET | `/items/{itemId}/images` (**ohne v1**) | – | `build_api_items.py:39-83` | `ItemId`, `ImageId`, `ImageDataType`, `SalesChannelId`, `SortNumber`, `Size`, `Width`, `Height` |
| A5 | GET | `/v1/items/imagedata/{imageId}` | – | `raw_items.py:132-135` | Binär-Body |
| A6 | GET | `/items/{itemId}/customfields` (**ohne v1**) | – | `build_api_items.py:86-103` | `CustomFieldId`, `Value` |
| A7 | GET | `/items/{itemId}/descriptions` (**ohne v1**) | – | `build_api_items.py:106-130` | `ItemId`, `Name`, `Description`, `ShortDescription`, `SeoPath`, `SeoMetaDescription`, `SeoTitleTag`, `SeoMetaKeywords`, `LanguageIso`, `SalesChannelId` |
| A8 | GET | `/items/{itemId}/variations` (**ohne v1**) | – | `build_api_items.py:133-150` | `Id`, `ItemId`, `Name`, `Type`, `Translations[]{Name, LanguageIso}` |
| A9 | GET | `/items/{itemId}/variations/{variationId}/values` (**ohne v1**) | – | `build_api_items.py:142-150` | `Id`, `Name`, `Translations[]{Name, LanguageIso}` |
| A10 | GET | `/items/{itemId}/specialprices` (**ohne v1**), **Objekt** | – | `build_api_items.py:219-274` | `ItemId`, `IsActive`, `StartDate`, `EndDateActive`, `EndDate`, `TillAmountActive`, `TillAmountInStockSmallerThan`, `SpecialPrice[]{IsActive, CustomerGroupId, NetPrice, SalesChannelId}` |
| A11 | GET | `/items/{itemId}/salesChannelPrices` (**ohne v1**) | – | `build_api_items.py:228-233` | `ItemId`, `SalesChannelId`, `CustomerGroupId`, `FromQuantity`, `NetPrice`, `ReduceStandardPriceByPercent` |
| A12 | GET | `/items/{itemId}/properties` (**ohne v1**), **Objekt** | – | `build_api_items.py:277-293` | `ItemId`, `Properties[]{PropertyId, PropertyValueId}` (unter `/v1` war es bis 2025-07-25 eine Liste, Commit `32709a1`) |
| A13 | GET | `/items/{itemId}/suppliers` (**ohne v1**) | – | `build_api_items.py:296-323` | `SupplierId`, `IsStandard`, `PurchasePriceNet`, `TaxRate`, `MinimumPurchaseQuantity`, `PermissibleOrderQuantity`, `DeliveryTime`, `UseSupplierDeliveryTime`, `Stocklevel`, `IsDropshipping`, `MergeStockLevel`, `Comment`, `PackagingUnit`, `AmountPackagingUnit`, `PackagingUnitPurchasePriceNet` (`ScalePrices` wird geparst, aber verworfen) |
| A14 | GET | `/v1/properties` | `pageNumber=n` | `raw_items.py:138-152` | `Id`, `Name`, `Translations[]`, `PropertyGroups[]` |
| A15 | GET | `/v1/properties/{propertyId}/values` | – | `raw_items.py:154-162` | `Id`, `Name`, `Description` |
| A16 | GET | `/v1/properties/{propertyId}/values/{valueId}/descriptions` | – | `raw_items.py:164-175` | `PropertyValueId`, `Name`, `Description`, `LanguageIso` (SEO-Felder optional) |
| A17 | GET | `/v1/items/customfields` | – | `global_data_logic.py:38-40` | `CustomFieldId`, `Name`, `Attribut` (sic), `GroupName`, `Type`, `PossibleValues[]` |
| A18 | GET | `/v1/manufacturers` | – | `global_data_logic.py:41-43` | `Id`, `Name`, `Homepage` |
| A19 | GET | `/v1/productGroups` | – | `global_data_logic.py:44-46` | `Id`, `Name` |
| A20 | GET | `/v1/shippingClasses` | – | `global_data_logic.py:47-49` | `Id`, `Name` |
| A21 | GET | `/v1/shippingMethods` | – | `global_data_logic.py:50-52` | `Id`, `Name`, `Priority`, `GrossPrice`, `ExtraWeight` |
| A22 | GET | `/v1/taxClasses` | – | `global_data_logic.py:22-24` | `Id`, `Name`, `IsDefault`, `Type` |
| A23 | GET | `/v1/tax/taxclass/{taxClassId}/{companyId}/{departureIso}/{shipmentIso}` | fest: `companyId=6`, `DE/DE` | `global_data_logic.py:25-35` | `Rate`, `TaxClassId`, `DepartureCountryISO`, `ShipmentCountryISO` |
| A24 | GET | `/v1/suppliers` | – | `global_data_logic.py:56-58` | Stammdaten (`Id`, `Name`, `Type`, `LanguageISO`, `Company`, `Street`, `PostalCode`, `City`, `CountryIso` …) |
| A25 | GET | `/v1/conditions` | – | `global_data_logic.py:60-62` | `Id`, `Names[0].Name` |

Pfade in der Tabelle relativ zu `backend/items_svc/src/jtl/jtl_helper/` bzw. `backend/items_svc/src/jtl/v1/utils/`.

**Feldliste `RawItem`** (aus A3, `backend/items_svc/src/jtl/schemas/item_raw_schemas.py:89-118`). *Pflicht* bedeutet: kein Default im Bestandscode.

```
Id:int*, Name:str="", SKU:str*, ManufacturerId:int=-1, IsActive:bool*,
Categories[]{CategoryId:int*, Name:str}            -- Name enthält Pfad "A -> B -> C"
Description:str*, ShortDescription:str*,
Identifiers{Gtin:str*, ManufacturerNumber:str*, Asins:list*},
Components[]{ItemId, Quantity, SortNumber}*, ChildItems:int[]*, ParentItemId:int*,
ItemPriceData{SalesPriceNet*, SuggestedRetailPrice*, PurchasePriceNet*, EbayPrice*, AmazonPrice*},
ActiveSalesChannels:str[]*, SortNumber:int*, Added*, Changed*, ReleasedOnDate*  (datetime),
StorageOptions{InventoryManagementActive, SplitQuantity, GlobalMinimumStockLevel, Buffer,
               SerialNumberItem, SerialNumberTracking, SubjectToShelfLifeExpirationDate,
               SubjectToBatchItem, ProcurementTime, DetermineProcurementTimeAutomatically,
               AdditionalHandlingTime}*,
ConditionId:int*, ShippingClassId:int=-1, ProductGroupId:int*, TaxClassId:int*,
Dimensions{Length, Width, Height}*, Weights{ItemWeigth (sic, JTL-Tippfehler), ShippingWeight}*,
AllowNegativeStock:bool*,
Quantities{MinimumOrderQuantity, PermissibleOrderQuantity,
           MinimumPurchaseQuantityForCustomerGroup[]{CustomerGroupId, PermissibleOrderQuantity,
                                                     MinimumPurchaseQuantity, IsActive}}*,
DangerousGoods:{UnNumber, HazardNo}*, SearchTerms:str*, PriceListActive:bool*, IgnoreDiscounts:bool*
```

**Beispiel-Requests für den POC** (Platzhalter; Werte aus der `.env`, nicht aus dem Readme übernehmen):

```http
GET {JTL__DOMAIN}/api/eazybusiness/v1/categories?pageSize=100&pageNumber=1
Accept: application/json
X-AppId: {JTL__APP_ID}
X-AppVersion: {JTL__APP_VERSION}
Authorization: Wawi {API-KEY}
```
```http
GET {JTL__DOMAIN}/api/eazybusiness/v1/items?pageSize=100&pageNumber=1&changedSince=2026-10-01T00:00:00Z
(gleiche Header)
```
```http
GET {JTL__DOMAIN}/api/eazybusiness/v1/items/{itemId}/specialprices
GET {JTL__DOMAIN}/api/eazybusiness/items/{itemId}/specialprices     ← Vergleichsaufruf "wie Bestand"
```

Hinweis zu `changedSince`: Der Bestandscode übergibt ein Python-`datetime` direkt an httpx. Serialisiert wird es per `str()`, also als `2026-10-01 12:34:56.123456+00:00`: mit Leerzeichen, Mikrosekunden und `+00:00` (URL-kodiert als `+` bzw. `%2B`). Ob JTL das korrekt parst, ist unklar. **Im POC ISO-8601 (`yyyy-MM-ddTHH:mm:ssZ`) verwenden und beide Varianten vergleichen.**

**C#-Skizze für den POC-Client** (laut fehlschlagend statt still):

```csharp
var http = new HttpClient { BaseAddress = new Uri($"{cfg.Domain.TrimEnd('/')}/api/eazybusiness/") };
http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
http.DefaultRequestHeaders.Add("X-AppId", cfg.AppId);
http.DefaultRequestHeaders.Add("X-AppVersion", cfg.AppVersion);
http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", cfg.ApiToken); // "Wawi <key>"

async Task<JtlPage<T>> GetPage<T>(string path, int page, IDictionary<string,string>? q = null) {
    var url = $"{path}?pageSize=100&pageNumber={page}" + (q is null ? "" : "&" + string.Join("&", q.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}")));
    using var resp = await http.GetAsync(url);
    var raw = await resp.Content.ReadAsStringAsync();
    await rawStore.SaveAsync(url, (int)resp.StatusCode, raw);   // Beweissicherung
    resp.EnsureSuccessStatusCode();                              // kein stilles Weiterverarbeiten
    return JsonSerializer.Deserialize<JtlPage<T>>(raw, strictOptions)!; // Unbekannte Felder loggen, nicht verbieten
}
```

#### B. Custom API (eigener Dienst, direkter DB-Zugriff)

Header `x-token` (items) bzw. `X-token` (business). Base `CUSTOM_API__DOMAIN`.

| # | Methode | Pfad | Parameter / Body | Antwortfelder (genutzt) | Fundstelle |
|---|---|---|---|---|---|
| C1 | GET | `item-image-info` (relativ, ohne `/`) | `?image_id=&item_id=` | `sort_number` | `backend/items_svc/src/jtl/jtl_helper/build_api_items.py:56-67` |
| C2 | GET | `/config_groups` | – | `kKonfiggruppe`, `name{*:{sprach_name,text}}`, `beschreibung{…}`, `cKommentar`, `kShop`, `items[]`, `used_by[]`, `sort_per_article{kArtikel:sort}`, `has_image`, `nMin`, `nMax`, `nTyp`, `nSort`, `cInet`, `cDelInet` | `backend/items_svc/src/jtl/v1/utils/global_data_logic.py:310-342,486` |
| C3 | GET | `/config_items` | – | `kKonfigitem`, `kKonfiggruppe`, `kArtikel`, `name`, `beschreibung`, `bEmpfohlen`, `bName`, `bPreis`, `bRabatt`, `bZuschlag`, `fMin`, `fMax`, `fInitial`, `fStandardpreis`, `bIgnoreMultiplier`, `price_by_customer_group{*:{kKundengruppe,kSteuerklasse,fPreis,kShop,nTyp}}`, `nPosTyp`, `bSelektiert`, `nSort` | `global_data_logic.py:345-395,489` |
| C4 | GET | `/shipping_methods` | – | `Id`, `Name`, `Priority`, `GrossPrice`, `ExtraWeight`, `FreeShippingAt` | `global_data_logic.py:420-436,495` |
| C5 | GET | `/property_values` (= Variationswerte) | – | `kEigenschaft`, `kEigenschaftWert`, `fAufpreis`, `fAufpreisNetto`, `fGewichtDiff`, `fLagerbestand`, `fPackeinheit`, `cBarcode`, `cArtNr` | `global_data_logic.py:398-417,499` |
| C6 | GET | `/categories` | – | `id`, `name`, `description`, `parent_category_id`, `sort_number`, `level`, `shop_id`, `active_sales_channels[]`, `children_ids[]`, `discounts[]{customer_group_id,discount}`, `seo_*` | `global_data_logic.py:505-508` |
| C7 | GET | `/child_item_mappings` | – | `parents{parentId:{childs{childId:{combinations{varId:valueId}}}}}` | `global_data_logic.py:510-513` |
| C8 | GET | `/item-units` | – | `{itemId:{item_id, unit_weight, unit_label}}` | `global_data_logic.py:515-516` |
| C9 | GET / DELETE | `/sales_order_changes` | DELETE-Body `{"updated_ids":[…]}` | `changes[]{id, entity_id, changed_at}` | `backend/business/src/business_api/jtl_helper/worker.py:61-77`, `backend/business/src/business_api/jtl_helper/change_handling.py:50-53` |
| C10 | GET / DELETE | `/customer_changes` | wie C9 | wie C9 | `backend/business/src/business_api/jtl_helper/worker.py:80-91` |
| C11 | GET | `/order/line_items` | `?order_id=` | Liste von `LineItem` (JTL-REST-Format) | `backend/business/src/business_api/jtl_helper/raw_order.py:77,99` |
| C12 | POST | `/order/set_item_prices` | `?order_id=`, Body `[{position_id,total_netto,total_gross}]` | Status 200 | `backend/business/src/business_api/jtl_helper/raw_order.py:239-259` |
| C13 | POST | `/order/adjust_config_item` | `?main_item_id=`, Body `[childLineIds…]` | 200/201 | `backend/business/src/business_api/jtl_helper/raw_order.py:262-274` |
| C14 | POST | `/order/adjust_variation_item` | `?main_item_id=`, Body `{varId:[dataId,name,netto]}` | 200/201 | `backend/business/src/business_api/jtl_helper/raw_order.py:277-289` |
| C15 | POST | `/order/adjust_variation_inputs` | `?main_item_id=`, Body `{varId:[varId,value]}` | 200/201 | `backend/business/src/business_api/jtl_helper/raw_order.py:292-302` |
| C16 | POST | `/order/adjust_shipping_text` | `?order_id=&text=` | 200 | `backend/business/src/business_api/jtl_helper/raw_order.py:221-236` |
| C17 | POST | `/payment` | Body = `tZahlung`-Spalten (`cName`, `dDatum`, `fBetrag`, `kBestellung`, `kBenutzer`, `nAnzahlung`, `kZahlungsart`, `nKeinExport`, `cExternalTransactionId`, `nZuweisungstyp`, `nZahlungstyp`, `kRechnung`) | `JTLPaymentResponse` | `backend/business/src/business_api/jtl_helper/raw_order.py:165-181`, `backend/business/src/business_api/utils/order.py:566-583` |
| C18 | GET | `/order/{orderId}/ident_code` | – | `indent_code` (sic) | `backend/business/src/business_api/jtl_helper/mail_data.py:87-90` |
| C19 | GET | `/companies` | – | Firmenstamm (`Company`, `active`) | `backend/business/src/business_api/jtl_helper/raw_users.py:105-110` |

#### C. JTL-REST – Kunden, Aufträge, Stammdaten (business)

| Methode | Pfad (ohne v1, außer Dateien) | Query/Body | Fundstelle |
|---|---|---|---|
| GET | `/customers` | `pageSize=500&pageNumber=n` | `backend/business/src/business_api/jtl_helper/raw_users.py:21-35`, `backend/business/src/business_api/jtl_helper/client.py:67-102` |
| GET | `/customers/{id}` | – | `backend/business/src/business_api/jtl_helper/raw_users.py:38-42` |
| POST | `/customers` | `User` (by_alias, ohne `Number`) | `backend/business/src/business_api/jtl_helper/raw_users.py:135-141` |
| GET | `/salesOrders` (paginiert), `/salesOrders/{id}`, `/salesOrders/{id}/lineitems` | – | `backend/business/src/business_api/jtl_helper/raw_order.py:22-144` |
| POST | `/salesOrders` | `?renameSalesOrderNumberWhenAlreadyExists=true&disableAutomaticWorkflows=true`, Body = Shop-Order-Dict (inkl. interner `_…`-Felder) | `backend/business/src/business_api/jtl_helper/raw_order.py:147-162` |
| POST | `/salesOrders/{id}/lineitems` | `?salesOrderId=`, Body `[LineItem…]` | `backend/business/src/business_api/jtl_helper/raw_order.py:184-196` |
| PATCH | `/salesOrders/{id}/lineitems/{lineId}` | `?salesOrderId=&salesOrderLineItemId=`, Body `{ItemId, SalesPriceNet, SalesPriceGross, PurchasePriceNet}` → 204 | `backend/business/src/business_api/jtl_helper/raw_order.py:199-218` |
| POST | `/salesOrders/{id}/output/pdf` | – → PDF | `backend/business/src/business_api/jtl_helper/raw_order.py:117-126` |
| POST | `/v1/salesOrders/{id}/files` | `{FileName, FileDataType, FileData(base64)}` | `backend/business/src/business_api/utils/file.py:120-123` |
| GET | `/salesChannels`, `/customerGroups`, `/customerCategories`, `/paymentMethods`, `/shippingMethods` | – | `backend/business/src/business_api/jtl_helper/raw_users.py:77-117` |

### 3.5 Synchronisationsfrequenz

| Datenart | Mechanismus | Frequenz | Status |
|---|---|---|---|
| Artikel-Stammdaten (global) | REST + Custom API | alle 5 min (`backend/items_svc/src/jtl/core/config.py:32`) | aktiv |
| Artikel inkrementell | `changedSince` = letzter Lauf (nur im Prozessspeicher), beim Start 10 Tage zurück (`backend/items_svc/src/jtl/core/config.py:33`) | alle 5 min | aktiv, Fenster geht bei Fehlern verloren |
| Artikel voll | pro Kategorie | 0–3 Uhr UTC alle 5 min, zweifach parallel | aktiv |
| Aufträge/Kunden JTL → Shop | Trigger-Tabellen → Custom API → REST | war alle 300 s | **deaktiviert seit 2026-08-17** |
| Business-Stammdaten (Kundengruppen, Zahlarten, Firmen) | REST | nie (nur Test-Flag) | **deaktiviert** |
| Neue Kunden JTL → Shop | sequenzieller ID-Scan | nur bei Registrierung | stoppt bei ID-Lücken |

---

## 4. Bestellfluss Backend

### 4.1 Ablauf

1. **`POST /api/order/`** (`backend/business/src/business_api/v1/endpoints/order.py:43-85`)
   - Formfeld `payload` (`OrderPayload`), optional mit Dateien.
   - Der Nutzer wird aus dem JWT-Cookie ermittelt, sonst per **E-Mail-Adresse aus der Rechnungsadresse**. Existiert unter dieser Adresse ein Shop-Kunde, wird ihm die Bestellung ohne Authentifizierung zugeordnet; andernfalls wird ein neuer JTL-Kunde angelegt (`backend/business/src/business_api/utils/user.py:96-124`).
   - `checkout_data.order_id` aus dem Client entscheidet, ob eine bestehende Order aktualisiert wird (`backend/business/src/business_api/v1/endpoints/order.py:64-73`). **Es wird nicht geprüft, ob sie dem Nutzer gehört.**
   - Bestellnummer = höchste lokale `Number` + 1, Start bei 20 000 (`backend/business/src/business_api/utils/order.py:185-203`). **Race Condition**: Parallele Checkouts bekommen dieselbe Nummer; JTL benennt per `renameSalesOrderNumberWhenAlreadyExists` um, die Shop-Nummer weicht dann ab.
2. **Preisbildung** (`backend/business/src/business_api/utils/order.py:206-250`, `backend/business/src/business_api/utils/items.py`)
   - **Serverseitig:** Die Preise kommen aus `items_svc` (Mongo-Cache), nicht aus dem Client. Positiv.
   - Aber:
     - `POST /items/` wird **ohne `user_id`** aufgerufen (`backend/business/src/business_api/utils/items.py:10-18`). Es gilt also stets der Preis der Default-Kundengruppe, auch für B2B-Kunden.
     - Brutto = Netto × 1,19, **fest kodiert** (`backend/business/src/business_api/utils/items.py:40`, `backend/business/src/business_api/utils/order.py:213,400`). Die Steuerklasse des Artikels und das Lieferland werden ignoriert.
     - Variationsaufpreise fehlen in den Positionen. Sie werden erst nachträglich per `PATCH` auf die JTL-Positionen addiert (`backend/business/src/business_api/jtl_helper/order_handling.py:95-116`).
     - Versandkosten: `gross_price` der vom Client gesendeten `shipping_method` (`backend/business/src/business_api/utils/order.py:392-399`). **Der Versandpreis kommt also vom Client** und wird nicht gegen die Stammdaten geprüft. Zusätzlich gibt es eine Sonderliste mit IDs 22/43.
   - **Bestand: keine Prüfung.** Bestände für Standardartikel werden nicht einmal geladen (siehe 3.2).
3. **Zahlung**
   - Vorkasse (`BANK_TRANSFER`, JTL-Zahlart 23): direkte Übergabe über `send_order_to_systems` (`backend/business/src/business_api/v1/endpoints/order.py:80-81`).
   - Stripe:
     - Das Frontend ruft `POST /api/payment/intents/` auf. Der Betrag ist `StillToPay` der Shop-Order.
     - **Existiert schon ein Intent für die Order, wird er wiederverwendet, auch wenn sich der Betrag inzwischen geändert hat** (`backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33`).
     - Stripe-Webhook → Signaturprüfung (`backend/payment_svc/src/payment/utils/webhook.py:16-33`) → `PATCH /order/{id}/mark_paid` (`backend/payment_svc/src/payment/utils/webhook.py:88-103`). Fehler beim Aufruf werden nur per `print` ausgegeben, an Stripe geht trotzdem ein 200 → **kein Retry**.
     - `mark_paid` (`backend/business/src/business_api/v1/endpoints/order.py:110-138`) prüft weder Betrag noch Intent-Status bei Stripe und ist **öffentlich ohne Auth erreichbar**.
     - Auch `payment_failed`/`canceled` lösen `send_order_to_systems` aus (Statuszweig `else`). Die Zahlungsbuchung scheitert dann nur zufällig an `fromisoformat(None)`.
4. **`send_order_to_systems`** (`backend/business/src/business_api/utils/order.py:602-637`) mit Flags `_in_erp_created`, `_in_erp_payed`, `_in_erp_file`, `_mail_send`:
   - **ERP-Anlage** `_make_order_with_jtl` (`backend/business/src/business_api/jtl_helper/order_handling.py:127-193`):
     - `POST /salesOrders` mit Customer-ID-Fallbacks und gegebenenfalls Kunden-Neuanlage.
     - `POST …/lineitems`.
     - Speichern der lokalen Order.
     - Custom API: Positionspreise setzen, Konfig-Zuordnung, Variationen und Varianten-Eingaben, Versandtext. Die Konfig-Zuordnung wird zweimal angewendet, „since JTL overrides them“.
     - **Gibt `{}` zurück, wenn JTL keine `Id` liefert. `send_to_erp` wertet das nicht aus, `_in_erp_created` wird trotzdem `True`** (`backend/business/src/business_api/utils/order.py:607-611`) → die Bestellung fehlt in JTL und wird nie erneut versucht.
     - Rückgabewerte der Custom-API-Workarounds (`bool`) werden ignoriert.
   - **Zahlung**: Custom API `POST /payment` (direkt in `tZahlung`). `kZahlungsart` stammt aus dem vom Client angegebenen `payment_method_type`, nicht aus der tatsächlichen Stripe-Methode des Webhooks. `kBenutzer=5` ist fest kodiert.
   - **Dateien**: `POST /v1/salesOrders/{id}/files`.
   - **Mail**: lokale Jinja-HTML → `pdf_svc` → PDF → MQ `send_mail` (`backend/business/src/business_api/utils/order.py:586-599`, `backend/business/src/business_api/utils/pdf.py:290-317`).
   - Alles läuft unter `suppress(Exception)` ohne Log.
   - **Keine Transaktion und keine Idempotenz**: Doppelte Webhooks, Nutzer-Reload und `GET /order/send_order_to_system/{id}` können parallel laufen und zu Doppelanlagen in JTL führen (Flags werden erst *nach* dem Erfolg gesetzt, ohne Lock).
5. **Mails**
   - `mail_svc` quittiert jede MQ-Nachricht, auch bei Fehlern (`backend/mail_svc/src/mail/worker.py:22-26`).
   - `send_mail` gibt **immer `True`** zurück; der `else`-Zweig ist unerreichbar, nach `except` folgt „Email sent successfully“ (`backend/mail_svc/src/mail/utils/mail.py:61-85`).
   - Fehlgeschlagene Mails gehen still verloren.
   - Versand-, Storno- und Bezahlt-Mails existieren nur als Datenendpunkte (`/maildata/*`) und Test-API. Ein automatischer Auslöser fehlt, und weil der Auftrags-Sync deaktiviert ist, gibt es ohnehin keinen Statusabgleich.
6. **PDF**
   - Eigene Bestellbestätigung über Jinja + `pdf_svc`; zusätzlich JTL-Auftrags-PDF über `POST /salesOrders/{id}/output/pdf`.
   - `pdf_svc` ist öffentlich und rendert über `/template_string` **beliebige Jinja-Templates ohne Sandbox** (`backend/pdf_svc/src/pdf/utils/template_handler.py:5,69`).

### 4.2 Antwort auf „Wird Preis/Bestand serverseitig validiert?“

- **Preis: teilweise.** Artikelpreise werden serverseitig aus dem Cache übernommen. Kundengruppe, persönlicher Rabatt, Steuerklasse und Lieferland werden **nicht** berücksichtigt. Versandkosten kommen vom Client. Der Stripe-Betrag wird beim Bezahlen **nicht** mit der Order verglichen.
- **Bestand: nein.**

---

## 5. Bewertung der Behauptung „undokumentierte JTL-API-Änderungen“

### 5.1 Hinweise, die die Behauptung stützen

| Beleg | Bewertung |
|---|---|
| Commit `1ba1491` (2026-06-09): „Fix fetching all items from JTL after there breaking update“ → Umstellung auf Abruf pro Kategorie (`backend/items_svc/src/jtl/jtl_helper/raw_items.py:61-89`) | Einziger expliziter Hinweis. Was sich geändert hat (Paging? Limit?), ist nicht dokumentiert. Der Workaround hat eine Lücke: Artikel ohne Kategorie fehlen; außerdem wird `items` statt `new_items` gespeichert (Duplikate) (`raw_items.py:85,125`). |
| Commit `32709a1` (2025-07-25): Wechsel von `/v1/items/{id}/…` auf `/items/{id}/…`; `properties` jetzt als Objekt mit `Properties[]` | Zeigt, dass die unversionierte Route ein **anderes Schema** liefert als `/v1`. Die Strukturänderung hat sich das Team mit dem Wechsel selbst eingehandelt, eine Änderung von JTL-Seite belegt sie nicht. |
| Kommentar „Typo is from the JTL API“ (`ItemWeigth`) (`backend/items_svc/src/jtl/schemas/item_raw_schemas.py:70`) | Bekannte, stabile JTL-Eigenheit, keine Änderung. |
| Kommentare „JTL overrides them“, „trigger variation api-fix“ (`backend/business/src/business_api/jtl_helper/order_handling.py:84,186`) | Workarounds für JTL-**Verhalten** beim Anlegen von Konfig- und Variationspositionen über REST, nicht für Feldänderungen. Die Lösung schreibt per Custom API direkt in die DB. |

### 5.2 Hinweise, die gegen die Behauptung sprechen (Ursache im eigenen Code)

- **Die Raw-Schemas für Artikel wurden seit 2025-11-19 nicht mehr angepasst** (`git log -- backend/items_svc/src/jtl/schemas/item_raw_schemas.py`: zuletzt `9936ca9`, nur `FreeShippingAt` ergänzt). Echte Feldumbenennungen bei JTL hätten zwingend Schema-Anpassungen erfordert, denn `RawItem` hat fast nur Pflichtfelder. Solche Anpassungen gibt es in der Historie nicht.
- **Die Commits seit Juni 2026 sind fast ausschließlich eigene Logik-Fixes**: „fix cart price calculation“, „fix shop product price“, „Fix: Shop configurator price calculation“, „fix lock plan type codes“, „fix gift quantity“, „Fix shipping-costs/methods selection“ (`git log --since=2026-06-01`). Die Preisprobleme lagen also nachweislich im eigenen Mapping und in der eigenen Berechnung.
- **Mapping-Bugs, die unabhängig von JTL falsche oder fehlende Daten erzeugen**: B-03 bis B-12 (Befundtabelle).
- **Abgeschaltete Synchronisation** (B-02) erklärt veraltete Auftrags- und Kundendaten vollständig.
- **Fehlende Beobachtbarkeit**: Weil Fehler verschluckt werden (B-01) und Rohantworten nicht gespeichert werden, lässt sich die Behauptung mit dem heutigen System **weder belegen noch widerlegen**. Das spricht für Monitoring statt Schuldzuweisung.

### 5.3 Fazit und Vorschlag zur Verifikation

Wahrscheinlich ist eine Mischung. **(a)** Die eigentliche Ursache der meisten Fehler sind stilles Fehlerverhalten und konkrete Mapping-Bugs. **(b)** Die Nutzung unversionierter Endpunkte (seit 2025-07) macht das System anfällig für Schemaänderungen bei Wawi-Updates. Das ist ein Designfehler, kein Versäumnis von JTL.

Zur Verifikation:
1. Wawi-Version und REST-Server-Version zum Zeitpunkt der Fehler ermitteln (JTL-Changelog prüfen).
2. Mit dem .NET-POC je Artikel parallel `/v1/...` und unversioniert abfragen und die Rohantworten speichern und vergleichen.
3. Kontrollgruppe: Artikel mit bekannt falschen Shopdaten. Den Mongo-Datensatz gegen die Roh-JSONs halten; dabei prüfen, ob die Felder in JTL vorhanden waren, aber durch B-01, B-03 … verloren gingen.

---

## 6. Befunde

Schweregrade: **Kritisch** (Sicherheit, Geld- oder Datenverlust in Produktion), **Hoch** (systematisch falsche Daten oder schwere Betriebsrisiken), **Mittel**, **Niedrig**.

| ID | Schwere | Fundstelle | Beschreibung | Auswirkung | Empfehlung |
|---|---|---|---|---|---|
| B-01 | Kritisch | `backend/items_svc/src/jtl/jtl_helper/client.py:63-111`; `backend/items_svc/src/jtl/jtl_helper/build_api_items.py:707-715`; `backend/items_svc/src/jtl/worker.py:71-75` | Kein Statuscode-Check; ValidationError/Exception → `None`/`[]` ohne Log; `handle_extra_data` und Worker-Loop unter `suppress(Exception)`; der Artikel wird danach trotzdem gespeichert | Bilder, Varianten, Sonderpreise, Merkmale und Lieferanten verschwinden still und **überschreiben gute Daten**; Ausfälle bleiben unsichtbar | Fail-loud: HTTP-Fehler und Validierungsfehler loggen und messen; einen Artikel nur bei vollständigem Abruf ersetzen (sonst den letzten guten Stand behalten); Rohantworten speichern |
| B-02 | Kritisch | `backend/business/src/business_api/jtl_helper/worker.py:128,150-163` (Commit `ef0fe86`, 2026-08-17) | `"update_runner": False`; Abgleich von Aufträgen und Kunden aus JTL abgeschaltet, ersetzt durch Debug-`print` | Versand- und Stornostatus, Positionsänderungen und Kundenänderungen (Kundengruppe, Rabatt) kommen nicht mehr im Shop an | Sofort reaktivieren (nach Fix von B-14) oder im .NET-Nachfolger als Erstes umsetzen |
| B-03 | Hoch | `backend/items_svc/src/jtl/jtl_helper/raw_items.py:164-175` | Dict-Comprehension `{prop_id: {value.Id: …} for … for value in …}` überschreibt pro Property alle Werte bis auf den letzten | Beschreibungen von Merkmalwerten fehlen (nur der letzte Wert je Merkmal bleibt) | Verschachtelt aufbauen: `{prop_id: {v.Id: … for v in values}}` |
| B-04 | Hoch | `backend/items_svc/src/jtl/v1/utils/global_data_logic.py:172-174` | Lookup `descriptions.get(raw_prop.Id).get(prop_id)` nutzt die Property-ID statt der Wert-ID (`value_data.Id`) | Wertbeschreibungen werden nahezu nie zugeordnet (zusammen mit B-03 praktisch immer leer) | Key korrigieren und testen |
| B-05 | Hoch | `backend/items_svc/src/jtl/jtl_helper/build_api_items.py:250-254` | Sonderpreise werden nur übernommen, wenn `EndDateActive and EndDate >= today`; `StartDate` wird nicht geprüft | Sonderpreise **ohne Enddatum fehlen**; zukünftige Sonderpreise gelten sofort | Logik: aktiv ∧ (Start ≤ jetzt) ∧ (¬EndDateActive ∨ Ende ≥ jetzt) |
| B-06 | Hoch | `backend/items_svc/src/jtl/v1/utils/global_data_logic.py:123-127`; `backend/items_svc/src/jtl/schemas/global_data_schemas.py:59-60` | Fehlt der Steuersatz (Fetch-Fehler), gilt `rate=1` → `rate_factor=1.01` | Bruttopreise im Feed bzw. in der Anzeige mit 1 % statt 19 % bzw. 7 % | Fehlenden Steuersatz als Fehler behandeln, keinen Default verwenden |
| B-07 | Hoch | `backend/items_svc/src/jtl/v1/utils/global_data_logic.py:452,456,460,483-518` | Global-Daten werden in zwei Schritten geschrieben; Schritt 1 setzt Kategorien, Versandarten, Variationsdaten und Konfiggruppen leer; Schritt 2 (Custom API) ohne Fehlerprüfung | Bei Custom-API-Fehlern oder zwischen den Schritten: leere Kategoriepfade → `in_configurator` falsch, Kategorie-Rabatte fehlen, `KeyError` bei Variationen (→ B-01) | Global-Daten vollständig im Speicher aufbauen und erst dann atomar schreiben (Version/Swap) |
| B-08 | Hoch | `backend/items_svc/src/jtl/jtl_helper/raw_items.py:61-129` | Abruf nur über Kategorien (Workaround zu `1ba1491`) | **Artikel ohne Kategoriezuordnung werden nie synchronisiert**; Kind-Artikel ohne eigene Kategorie fehlen | Unabhängig von Kategorien paginieren (`/v1/items`) und die Ursache des „breaking update“ mit dem POC klären |
| B-09 | Hoch | `backend/items_svc/src/jtl/jtl_helper/raw_items.py:97-102` | `cached_last_time` wird **vor** dem Abruf auf „jetzt“ gesetzt, nur im Speicher; `changedSince` als `str(datetime)` | Bei Fehlern oder Neustart gehen Änderungen verloren oder werden erst nach 10 Tagen bzw. nachts nachgeholt | Wasserstand persistent und erst nach Erfolg setzen; ISO-8601 mit Überlappung |
| B-10 | Hoch | `backend/items_svc/src/jtl/v1/utils/discount_logic.py:93-103` vs. `backend/business/src/business_api/v1/endpoints/user.py:21-27` (Commit `cb05fbe`, 2026-06-05) | Der business-Endpunkt `/user/` verlangt seit Juni 2026 das JWT-Cookie und ignoriert `identifier`; items_svc ruft ihn ohne Cookie nur mit `identifier` auf | Kundengruppen- und persönliche Rabatte werden in der Artikelliste nicht mehr angewendet → B2B-Kunden sehen falsche Preise | Kundenkontext explizit übergeben (Kundengruppen-ID aus Token) oder Preise im Gateway berechnen |
| B-11 | Hoch | `backend/items_svc/src/jtl/jtl_helper/build_api_items.py:42-47` u. a.; `backend/business/src/business_api/jtl_helper/raw_order.py:26,91,148,187,204,118`; `backend/business/src/business_api/jtl_helper/raw_users.py:22,39,136` | Unversionierte JTL-Pfade gemischt mit `/v1` | Antwortschema hängt von der Wawi-Serverversion ab; „undokumentierte Änderungen“ werden dadurch erst möglich | Alle Aufrufe auf eine feste Version (`/v1`) festschreiben; Version im Client-Config-Objekt pflegen |
| B-12 | Hoch | `backend/items_svc/src/jtl/schemas/item_raw_schemas.py:89-118`; `backend/business/src/business_api/schemas/base.py:4-9` | Items: fast nur Pflichtfelder; ein `null` bricht die ganze Seite und den Lauf ab. Business: `extra="forbid"` auf Eingangs-DTOs → jedes neue JTL-Feld wird zum Fehler | Ein einzelner Datensatz stoppt den Sync, additive JTL-Erweiterungen brechen Aufträge und Kunden | Tolerant lesen (unbekannte Felder loggen), nullable modellieren, Validierung pro Datensatz isolieren |
| B-13 | Kritisch | `backend/business/src/business_api/utils/order.py:519-564,606-611`; `backend/business/src/business_api/jtl_helper/order_handling.py:154-155` | `send_to_erp` meldet keinen Misserfolg; `_in_erp_created=True` wird auch gesetzt, wenn JTL keine Order-ID geliefert hat | Bestellungen (auch bezahlte) fehlen in JTL, werden nie erneut gesendet und fallen nicht auf | Ergebnis prüfen und Status `failed` setzen; Retry über Outbox/Queue (der Consumer `consume__retry_order_sending_to_jtl` existiert, ist aber kaputt und auskommentiert) |
| B-14 | Hoch | `backend/business/src/business_api/jtl_helper/change_handling.py:17-47`; `backend/business/src/business_api/jtl_helper/raw_order.py:104-114`, `backend/business/src/business_api/jtl_helper/raw_users.py:45-71`, `backend/business/src/business_api/jtl_helper/raw_order.py:129-144`, `backend/business/src/business_api/jtl_helper/raw_users.py:21-35` | Change-IDs werden auch bei fehlgeschlagenem Update gelöscht; der sequenzielle ID-Scan stoppt an der ersten Lücke; in `fetch_all_*` wird bei ValidationError das vorige bzw. ein ungebundenes `order`/`user` gespeichert | Dauerhafter Datenverlust, nach Lücken keine neuen Datensätze, Duplikate bzw. NameError | Nur erfolgreiche Changes löschen; paginiert mit `changedSince` lesen; `continue` nach Fehler |
| B-15 | Kritisch | `backend/business/src/business_api/v1/endpoints/order.py:110-138` | `PATCH /order/{id}/mark_paid` ohne Auth und ohne Prüfung bei Stripe; über Traefik `/api` öffentlich geroutet (`docker-stack.yml:304`) | Jeder kann Bestellungen als bezahlt markieren und an JTL übergeben lassen | Nur intern erreichbar machen (eigenes Netz oder Signatur); im Webhook den Intent-Status und den Betrag gegen die Order prüfen |
| B-16 | Kritisch | `backend/business/src/business_api/v1/endpoints/order.py:88-107,153-163`; `backend/business/src/business_api/v1/endpoints/mail_data.py:8-32`; `backend/business/src/business_api/v1/endpoints/cart.py:12-62`; `backend/business/src/business_api/v1/endpoints/configurator.py:42-64` | IDOR: Bestellungen per `user_id` oder fortlaufender Nummer, Mail-Daten inkl. JTL-Auftrags-PDF, Warenkörbe und Konfigurationen ohne Auth abrufbar oder änderbar; `mail_svc/main.py` nutzt dies bereits gegen die Produktions-URL | DSGVO-relevanter Abfluss von Namen, Adressen und Bestellungen | Auth-Pflicht und Besitzprüfung; interne Endpunkte nicht über Traefik routen |
| B-17 | Kritisch | `backend/pdf_svc/src/pdf/utils/template_handler.py:5,13,69`; `backend/pdf_svc/src/pdf/v1/endpoints/pdf.py:64-88`; `backend/pdf_svc/src/pdf/utils/html_handler.py:15` | Öffentliches `/api/pdf/v1/generate/template_string` rendert beliebige Jinja-Templates ohne `SandboxedEnvironment`; WeasyPrint mit Default-URL-Fetcher | Server-Side Template Injection (Codeausführung), SSRF und Auslesen lokaler Dateien | Service nur intern; Sandbox; eigener `url_fetcher` ohne `file://`/intern |
| B-18 | Kritisch | `Readme.md:18-30`; `backend/auth_svc/src/auth/migrate_jtl_data.py:313`; `docker-stack.yml:569-570`; `cms/strapi_cms/.env` (versioniert, `.gitignore`-Ausnahme `!cms/strapi_cms/.env`) | Secrets im Klartext: `JTL__API_TOKEN` („Wawi dbb0…“), `CUSTOM_API__API_TOKEN` („15e1…“), `STRIPE_SECRET_KEY` („sk_t…“), `STRIPE_SIGNING_SECRET` („whse…“), `MAIL_SMTP_PASSWORD` („rcaT…“), Blowfish-Key des JTL-Shops („16e9…“), Strapi `JWT_SECRET`/`ADMIN_JWT_SECRET` („FabD…“/„o+Df…“), `APP_KEYS`/`API_TOKEN_SALT`/`TRANSFER_TOKEN_SALT` („BmF1…“/„w4Pp…“/„8RSY…“); zusätzlich PII (E-Mail einer Privatperson als `debug_email`, `backend/auth_svc/src/auth/migrate_jtl_data.py:343`; Mitarbeiteradressen in `backend/mail_svc/src/mail/main.py:20-24`) | Vollzugriff auf Wawi-API und DB-Dienst, Stripe (Testmodus) und Mailkonto; Tokens stecken in der Git-Historie (`7130753`, 2026-03-05) | **Alle Secrets rotieren**, Historie bereinigen, Secret-Store verwenden, Secret-Scanning (gitleaks) in CI |
| B-19 | Kritisch | `compose/services/backend/business_api.yml:38`; `compose/services/backend/auth.yml:30`; `compose/services_deploy/backend/auth.yml:29`; `backend/auth_svc/src/auth/core/config.py:12`; `backend/business/src/business_api/core/config.py:12`; `docker-compose_deploy.yml:9-12` | JWT-Signaturschlüssel fest `SECURITY__KEY=foob…` bzw. Default `abc1…`; `SECURITY__KEY` fehlt in der Readme-Liste der `.env`; Deploy-Compose bindet Dev-Services ein | Ist der Schlüssel bekannt, lassen sich Tokens fälschen → Zugriff auf beliebige Konten (abhängig vom svc_helper-Verhalten) | Zufälligen Schlüssel aus dem Secret-Store; Start verweigern, wenn der Default aktiv ist |
| B-20 | Hoch | `backend/business/src/business_api/utils/items.py:10-18,40`; `backend/business/src/business_api/utils/order.py:213,392-401` | Bestellpreise: Default-Kundengruppe, 19 % fest kodiert, Versandpreis vom Client | Falsche Rechnungsbeträge (B2B, 7 %-Artikel, Ausland), manipulierbare Versandkosten | Preise und Versand serverseitig vollständig aus den Stammdaten berechnen; Steuer pro Steuerklasse und Land |
| B-21 | Hoch | `backend/items_svc/src/jtl/v1/utils/google_merchant.py:125-137`; keine Bestandsabfrage im Code | Bestände für Standardartikel werden nicht gelesen; keine Prüfung im Checkout; Google-Feed immer „in stock“ | Überverkauf, fehlerhafte Lieferzeiten, Feed verstößt gegen Merchant-Richtlinien | Bestand (`/v1/stocks` bzw. passende JTL-Ressource) synchronisieren und im Checkout prüfen |
| B-22 | Hoch | `backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33`; `backend/payment_svc/src/payment/utils/webhook.py:88-105` | Intent wird trotz geänderter Order-Summe wiederverwendet; `mark_paid`-Fehler werden geschluckt, Stripe erhält trotzdem 200; failed/canceled lösen ERP-Übergabe aus | Zahlbetrag ≠ Auftragssumme; bezahlte Orders bleiben unbestätigt; unbezahlte landen in JTL | Intent bei Betragsänderung aktualisieren; Webhook idempotent und mit Retry (Fehler → 5xx); nur `succeeded` → ERP |
| B-23 | Hoch | `backend/business/src/business_api/utils/order.py:602-637`; `backend/business/src/business_api/v1/endpoints/order.py:153-163` | Keine Idempotenz und kein Locking bei der ERP-Übergabe; Flags erst nach Erfolg gesetzt | Doppelte Aufträge oder Zahlungen in JTL bei parallelen Webhooks oder Aufrufen | Outbox-Pattern mit Status-Maschine und atomarem Claim (findOneAndUpdate) |
| B-24 | Hoch | `backend/business/src/business_api/utils/order.py:185-203` | Bestellnummer = max(lokal) + 1, nicht atomar; `Number` als String sortiert | Doppelte Nummern; JTL benennt um → Shop- und JTL-Nummer laufen auseinander | Nummernvergabe atomar (Counter-Dokument) oder JTL als führend |
| B-25 | Hoch | `backend/business/src/business_api/v1/endpoints/order.py:64-73`; `backend/business/src/business_api/utils/user.py:96-124` | Vom Client geliefertes `order_id` überschreibt eine bestehende Order ohne Besitzprüfung; Gastbestellung wird per E-Mail an ein bestehendes Konto gehängt | Fremde Bestellungen manipulierbar; Bestellungen landen beim falschen Kunden | Besitzprüfung; Gastbestellungen nicht automatisch mergen |
| B-26 | Hoch | `backend/mail_svc/src/mail/utils/mail.py:61-85`; `backend/mail_svc/src/mail/worker.py:22-26` | `send_mail` liefert immer `True`; die Nachricht wird auch bei Fehler quittiert | Mails (Bestellbestätigung, Passwort-Reset) gehen still verloren | Ergebnis korrekt zurückgeben; bei Fehler nack mit Dead-Letter-Queue |
| B-27 | Hoch | `backend/auth_svc/src/auth/v1/endpoints/account.py:82-96`; `backend/auth_svc/src/auth/core/data_stores.py:67-71`; `backend/auth_svc/src/auth/utils/auth.py:221` | Reset sucht *irgendeinen* Token (auch Refresh-Token) ohne Typ- und Ablaufprüfung; `expires_at` wird mit TTL-Sekunden (3600) statt Zeitpunkt befüllt | Reset-Links verfallen nicht; ein Refresh-Token erlaubt Passwortänderung | Token-Typ und Ablauf prüfen; Einmal-Token nach Gebrauch löschen |
| B-28 | Mittel | `trigger_up.sql:5-363` | 16 Fremd-Trigger und zwei Tabellen in der JTL-Produktiv-DB; `processing` ungenutzt; Konsum deaktiviert (B-02) | Wawi-Update- und Supportrisiko, wachsende Tabellen, Schreiblast auf Auftragstabellen | Durch REST-`changedSince` bzw. JTL-eigene Mechanismen ersetzen; bis dahin Monitoring |
| B-29 | Mittel | Custom API (Code nicht im Repo); `backend/business/src/business_api/utils/order.py:566-583`; `backend/business/src/business_api/jtl_helper/raw_order.py:221-302` | Direkte DB-Schreibzugriffe (`tZahlung`, Positionen, Konfig, Variationen), Rückgabewerte ignoriert | Inkonsistenzen in der Wawi, umgangene Workflows; nicht prüfbar | Quellcode und Betreiber klären; schrittweise auf offizielle REST-Endpunkte migrieren |
| B-30 | Mittel | `compose/services/backend/items_svc.yml:72`, `compose/services/backend/business_api.yml:36,83`, `compose/services_deploy/backend/*.yml`; `compose/services/backend/payment_svc.yml:32` | Env-Name `CUSTOM_API__TOKEN` ≠ Feld `api_token`; `MONGO_PASSWORD=${PAYMENT__MONGO__USER_NAME…}` (Username statt Passwort) | Custom-API-Token bleibt `"abc"` (Compose); Mongo-Login schlägt fehl oder ist schwach | Variablennamen korrigieren; Settings beim Start validieren |
| B-31 | Mittel | `backend/business/src/business_api/worker.py:127-179,199-203`; `backend/payment_svc/src/payment/utils/webhook.py:105-118`; `backend/business/src/business_api/jtl_helper/payment.py` | Toter bzw. kaputter Code: Retry-Consumer referenziert nicht importierte Namen (`_make_order_with_jtl`, `ShippingMehod`, `_finalize_order`); MQ-Push nach `return`; Payment-Consumer ohne Producer | Irreführend; Retry-Mechanismus existiert faktisch nicht | Entfernen oder reparieren |
| B-32 | Mittel | `backend/items_svc/src/jtl/worker.py:65-142` | Zwei parallele Endlosschleifen machen nachts doppelte Vollabgleiche alle 5 min; hart kodierter Debug-Artikel `41003` | Hohe Last auf der Wawi, Race beim Schreiben | Ein Scheduler mit Locking; Debug-Code entfernen |
| B-33 | Mittel | `backend/items_svc/src/jtl/jtl_helper/build_api_items.py:337-375,705-715` | N+1-Abruf (≥ 9 Calls pro Artikel, je Bild ein Custom-API-Call), sequenziell, doppelter Upsert | Vollabgleich dauert lange, Rate-Limits, Timeouts (→ B-01) | Parallelisierung mit Limit, Batch-Endpunkte, Change-Detection |
| B-34 | Mittel | `backend/items_svc/src/jtl/jtl_helper/evaluated_data.py:17-32,48-240,399-411`; `backend/items_svc/src/jtl/v1/utils/shipping.py` | Geschäftslogik hängt an fest kodierten Kategorie-, Variations- und Herstellernamen sowie fest kodierten Versand-Mappings; Parse-Fehler (`split("-")`, `config_group.name[de]`) brechen den Lauf ab | Umbenennung in der Wawi → Artikel im falschen Kanal (Shop/Konfigurator), fehlende Zylinderlängen | In JTL-Attribute bzw. Custom Fields verlagern; Parser pro Artikel absichern |
| B-35 | Mittel | `backend/items_svc/src/jtl/v1/endpoints/items.py:174-209` | `user_id` frei im Body; beliebige Mongo-Filter über `APIQuery` (svc_helper); `page_size` unbegrenzt | Preisabfragen für fremde Kundengruppen (sobald B-10 behoben ist), DoS | Kundenkontext aus Token; Filter-Whitelist; Limits |
| B-36 | Mittel | `backend/business/src/business_api/jtl_helper/user_handling.py:89-105`; `backend/business/src/business_api/jtl_helper/raw_users.py:135-141` | Kunden-Neuanlage: bei Fehler zufällige negative ID; `except:` (bare) | Bestellungen mit Platzhalter-Kunden-IDs, Dubletten | Fehler propagieren und über die Queue erneut versuchen |
| B-37 | Mittel | `backend/business/src/business_api/jtl_helper/worker.py:127,141`; `backend/business/src/business_api/schemas/global_data_raw_schemas.py:78-107` | Business-Stammdaten (Kundengruppen, Zahlarten, Firmen, Versandarten) werden nie aktualisiert | Veraltete Zahl- und Versandarten oder Kundengruppen | Regelmäßig synchronisieren |
| B-38 | Mittel | `backend/business/src/business_api/v1/endpoints/upload_security_card.py:17-53`; `backend/business/src/business_api/v1/endpoints/contact.py:11-17` | Uploads ohne Größen- oder Typlimit, komplett in den Speicher bzw. in die MQ-Nachricht (Base64); keine Rate-Limits | DoS, Mail-Spam-Relay; im Mail-Dienst werden alle Anhänge als PDF deklariert | Limits, Captcha/Rate-Limit, Dateien außerhalb der MQ ablegen |
| B-39 | Mittel | Valkey (`compose/services/backend/cache.yml`, `docker-stack.yml:386-419`, `backend/valkey/valkey.conf`) | Läuft, ungenutzt, ohne Konfiguration und Passwort; in Dev auf Host-Port 11020 | Unnötige Angriffsfläche und Kosten | Entfernen oder gezielt einsetzen (z. B. Preis- und Session-Cache im .NET-Nachfolger) |
| B-40 | Mittel | `backend/*/uv.lock` (svc-helper `gitlab.com/vitandi1/svc_helper.git@5e65dee9`) | Kernfunktionen (Auth, DB, MQ, Query-Builder) in einem fremden Repo eines Einzelaccounts (öffentlich, jederzeit entziehbar) | Nicht auditierbar; Lieferkettenrisiko; Wissensinsel | Code übernehmen/spiegeln; im .NET-Nachbau durch Standardbibliotheken ersetzen |
| B-41 | Niedrig | `backend/auth_svc/src/auth/utils/hash_algorithms.py:26-35` | `Bcrypt.generate`: bei übergebenem Salt wird `salt_enc` nie gesetzt (UnboundLocalError) | Latenter Fehler | Fix und Test |
| B-42 | Niedrig | `backend/items_svc/src/jtl/v1/endpoints/categories.py:9`; `backend/business/src/business_api/jtl_helper/mail_data.py:90` | Tippfehler in API-Pfad (`/catagories`) und Feld (`indent_code`) | Verwirrung beim Nachbau | Im .NET-Nachbau korrigieren und kompatible Aliase vorsehen |
| B-43 | Niedrig | durchgängig, z. B. `backend/business/src/business_api/utils/order.py:37-38,192,198,421-428,583`, `backend/payment_svc/src/payment/utils/webhook.py:94-103` | Logging per `print` (teils mit Zahlungsdaten); `log_request`-Hooks sind No-Ops (`backend/items_svc/src/jtl/jtl_helper/client.py:17-21`) | Keine Korrelation, keine Level, Leaks in Container-Logs | Strukturiertes Logging mit Korrelations-ID |
| B-44 | Niedrig | `compose/services/backend/*.yml` (`--reload`, Quellcode-Mounts) via `docker-compose_deploy.yml` | Deploy-Compose nutzt die Dev-Konfiguration | Instabilität, Codeänderung zur Laufzeit | Getrennte Prod-Konfiguration |
| B-45 | Hoch | Tests: `backend/items_svc/src/tests`, `backend/mail_svc/src/tests`, `backend/pdf_svc/src/tests` (jeweils 0 Tests); business 11 Tests (nur Payment-Mapping); payment 1 Test; auth 44 Tests; CI nur Frontend (`.gitea/workflows/frontend_check.yml`) | Keine Tests für die JTL-Integration, das Mapping und den Bestellfluss; keine Backend-CI | Regressionsfehler wie B-03 bis B-10 bleiben unentdeckt; Abdeckung insgesamt schätzungsweise unter 10 % | Contract-Tests mit aufgezeichneten JTL-Antworten (Golden Files) als Basis für den .NET-POC |

### 6.1 Codequalität – Gesamteinschätzung

- **Typisierung:** Pydantic wird genutzt, aber inkonsistent: dicts statt Modellen im Bestellfluss (`order: dict`), `TYPE_CHECKING`-Import aus fremdem Paket (`backend/business/src/business_api/jtl_helper/order_handling.py:25-26` importiert `src.jtl…`, das im business-Service nicht existiert), `mypy` nur als Dev-Dependency, in keiner CI.
- **Duplikate:** JTL-Client, Logger, Config und Mongo-Setup sind pro Service kopiert (`client.py` zweimal mit unterschiedlichem Fehlerverhalten); `format_de` und Mail-Datenaufbau doppelt (`backend/mail_svc/src/mail/main.py` vs. `backend/business/src/business_api/jtl_helper/mail_data.py`); `_get_user_id__*` in drei Varianten.
- **Toter Code:** auskommentierte Endpunkte (`backend/business/src/business_api/v1/endpoints/order.py:166-204`), `create_intent2`, `get_items`, `get_order_line_items1`, Code nach `return` (`backend/business/src/business_api/utils/order.py:178-182`, `backend/business/src/business_api/jtl_helper/user_handling.py:31-33`, `backend/business/src/business_api/jtl_helper/client.py:21-24`), Flags-Dict mit ausschließlich `False`.
- **Transaktionen:** Keine; Mongo-Upserts einzeln, Global-Daten nicht atomar (B-07), Bestellstatus über Flags ohne Locking (B-23).
- **Race Conditions:** Nummernvergabe (B-24), parallele Worker (B-32), Webhook vs. manuelle Auslösung (B-23), `uvicorn --workers 4` mit Prozess-Cache (Sitemap) und `filelock` auf einem lokalen Volume (bei Multi-Node problematisch).
- **Konfiguration:** Unsichere Defaults (`pw12…`, `abc1…`, `foob…`), uneinheitliche Variablennamen (B-30), Konfiguration zur Laufzeit per `settings.jtl_updates.cached_last_time` mutiert.

---

## 7. Empfehlungen für die Ablösung durch .NET/C#

### 7.1 POC „Artikeldaten aus JTL lesen“ (Phase 0, Beweisführung)
1. **Read-only-Client** gegen `{JTL__DOMAIN}/api/eazybusiness/v1/…` mit den Headern `Authorization: Wawi <key>`, `X-AppId`, `X-AppVersion`, `Accept: application/json`, gekapselt als `IJtlClient` (Polly: Retry, Timeout, Circuit Breaker; Rate-Limit über SemaphoreSlim).
2. **Rohdaten-Archiv**: Jede Antwort (URL, Status, Header, Body, Zeitstempel, Wawi-Version) in einer Tabelle oder als Blob speichern. Das ist die Grundlage, um die Behauptung „JTL hat geändert“ objektiv zu prüfen (Diff zweier Tage oder vor und nach einem Wawi-Update).
3. **Parallelabfrage `/v1` vs. unversioniert** für A4 und A6 bis A13 und die Unterschiede protokollieren.
4. **DTOs streng, aber tolerant**: `JsonSerializerOptions` mit `UnmappedMemberHandling.Skip`, unbekannte Felder per `JsonExtensionData` erfassen und loggen; Pflichtfelder explizit validieren (FluentValidation) **pro Datensatz**, sodass ein fehlerhafter Datensatz den Lauf nicht abbricht und keine guten Daten überschreibt.
5. **Paging generisch** (`JtlPage<T>` mit `Items`, `HasNextPage`, `NextPageNumber`, `TotalItems`), ohne Umweg über Kategorien. Ein Plausibilitätscheck `Summe(Items) == TotalItems` deckt den „breaking update“-Effekt auf.
6. **`changedSince`** als ISO-8601 in UTC mit Überlappung (z. B. 10 min) und **persistentem Wasserstand**, der erst nach erfolgreichem Lauf fortgeschrieben wird.
7. **Golden-File-Tests**: Die gespeicherten Rohantworten werden zu xUnit-Testfällen für das Mapping. Die Bugs B-03 bis B-06 als Regressionstests nachbilden: Erwartung „korrekt“, d. h. die Bugs werden nicht übernommen.

### 7.2 Zielarchitektur
- **Ein Integrationsdienst** (Worker Service, `BackgroundService` + Quartz/Hangfire) für alle JTL-Syncs mit Outbox/Inbox-Tabellen, Idempotenz-Schlüsseln und Dead-Letter-Queue. Die API-Dienste lesen nur das Read-Model.
- **Preise und Steuern zentral** berechnen (Kundengruppe, Sonderpreise, Kategorie- und Kundenrabatt, Steuerklasse × Land) und im Checkout erneut serverseitig validieren, ebenso den **Bestand**.
- **Bestellfluss als Zustandsmaschine**: `Created → PaymentPending → Paid → ErpCreated → ErpPaid → FilesUploaded → Mailed`, jeder Übergang idempotent und atomar (z. B. EF Core + optimistische Concurrency); Stripe-Webhook mit Betragsabgleich und Event-ID-Deduplizierung.
- **Custom API und Trigger** ablösen: zuerst alle von ihr gelieferten Daten auf offizielle REST-Endpunkte abbilden (Konfiggruppen, Variationsaufpreise, Kategorierabatte, Zahlungen), sonst nur noch lesenden SQL-Zugriff über einen klar versionierten, dokumentierten Adapter. Danach Trigger entfernen (`trigger_down.sql`).
- **Sicherheit**: Interne Endpunkte (mark_paid, maildata, pdf, send_order_to_system) nicht öffentlich routen; durchgängige AuthN/AuthZ (ASP.NET Core Identity/JWT mit Rotation); Secrets in Azure Key Vault o. ä.; alle in B-18 genannten Secrets **vor** Projektstart rotieren.
- **Beobachtbarkeit**: OpenTelemetry (Traces über JTL-Calls), Metriken pro Endpunkt (Fehlerrate, Validierungsfehler, Anzahl leerer Unterressourcen), Alarm bei „Artikel verliert Bilder, Varianten oder Preise gegenüber dem Vortag“.
- **Kompatibilität**: Die bestehenden Frontend-Verträge (`POST /items/` mit `APIQuery`, `/catagories`, `/order/*`) in einem Adapter-Layer nachbilden, damit Frontend und Backend getrennt migriert werden können. Fachliche Ableitungen (Konfigurator-Zuordnung, Zylinderlängen) aus fest kodierten Namen in pflegbare JTL-Attribute überführen.

### 7.3 Sofortmaßnahmen im Bestand (vor der Ablösung)
1. Secrets rotieren (B-18), `SECURITY__KEY` setzen (B-19).
2. `mark_paid`, `maildata`, `pdf_svc`, `send_order_to_system` und `single_order` sperren oder nur intern verfügbar machen (B-15 bis B-17).
3. `_in_erp_created` nur bei Erfolg setzen; JTL-Auftragsliste gegen die Shop-Orders abgleichen, um fehlende Bestellungen nachzutragen (B-13).
4. Auftrags- und Kundensync reaktivieren (B-02) und Logging in `fetch_as_pydantic*` und den Worker-Loops ergänzen (B-01), damit die JTL-Behauptung ab sofort messbar wird.

## Nachtrag 09.10.2026: Artikel ohne Kategorie (Live-Test mit dem POC)
- **Befund:** Die JTL-REST-API (`JTL.Wawi.Rest`, nur API-Version 1 verfügbar) antwortet mit **HTTP 500** und „Bitte geben Sie mindestens eine Kategorie vor!“, sobald ein Artikel **ohne Kategorie** in der Antwort steckt. Das gilt für `GET /v1/items` (die ganze Seite fällt aus) und für `GET /v1/items/{id}` (z. B. ID 122, 131). Unterressourcen wie `/descriptions` funktionieren.
- **Doku:** In der offiziellen Spezifikation (developer.jtl-software.com, Versionen 1.0–1.3) ist `categoryId` ein **optionaler** Filter von `GET /v1/items`, neben `searchKeyWord`, `manufacturerId`, `parentItemId`, `changedSince`, `isActiveOnSalesChannelId`. Eine Pflicht-Kategorie ist nicht dokumentiert. Es handelt sich also um ein JTL-spezifisches Verhalten bzw. einen Fehler der REST-Implementierung, nicht um eine API-Änderung. Der neue Endpunkt `GET /v2/items` (ab API 2.0) steht auf diesem Server nicht zur Verfügung (`UnsupportedApiVersion`).
- **Altes Backend:** `items_svc/src/jtl/jtl_helper/raw_items.py` fragt jede Kategorie einzeln ab (`categoryId`). Das sind viele Anfragen, Artikel ohne Kategorie kommen nie an, und bei einem Fehler endet die Schleife still, weil `HasNextPage` fehlt (bestätigt B-Befunde zu verschluckten Fehlern).
- **Lösung für die neue API:** Abruf über den Verkaufskanal **`isActiveOnSalesChannelId=2-2-5`** („JTL-Shop: sicherheit.meesenburg.com“), live getestet: 10.052 Artikel, 200 je Seite. Für Deltas zusätzlich `changedSince`. Fehlerhafte Seiten werden gemeldet und halbiert, bis der auslösende Artikel feststeht. In JTL Artikel ohne Kategorie bereinigen (Archiv-Kategorie zuweisen oder vom Shop-Kanal nehmen).
