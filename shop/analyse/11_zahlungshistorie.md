# 11 – Zahlungshistorie: Wann konnten Bestellungen fälschlich als bezahlt gelten?

Stand: 07.10.2026 · Grundlage: reine Git-Historie (`main`, 308 Commits, inkl. Remote-Branches), keine Server-, Stripe- oder JTL-Daten.
Repo: `scratch-2026-10-07-807c11/webshop`. Secrets nur maskiert (erste 4 Zeichen + „…“).

> **Wichtig:** Der Deploy-Zeitpunkt ist nicht der Commit-Zeitpunkt. Ab März 2026 laufen Änderungen per PR (Squash-Merge) in `main`. Angegeben ist jeweils das **Merge-Datum in `main`** (UTC). Wann tatsächlich deployt wurde, steht nicht im Repo. Das muss mit Server-/Container-Logs abgeglichen werden.

---

## 1. Zusammenfassung

1. **Go-Live/Relaunch: zwischen 17.06. und 23.06.2026** (sicher eingegrenzt). Der letzte PR vor dem Relaunch ist PR 707 vom 17.06.2026, der erste PR danach heißt „First Fixes and Changes after Relaunch“ (PR 713, 23.06.2026). Vorbereitungen (Produktions-Domain, Tracking, Consent, Merchant-Feed, Trusted Shops, Händlerbund) liefen vom 13.05. bis 13.06.2026.
2. **Vor `mark_paid` gab es keinen öffentlichen, unauthentifizierten Weg, eine Bestellung als bezahlt zu setzen.** Zahlungen wurden nur über den Stripe-Webhook gebucht, und der prüft die Signatur. Danach liefen sie über RabbitMQ und den Business-Worker per Custom-API `POST /payment` nach JTL.
3. **Trotzdem gab es auch vor dem 17./20.08.2026 Lücken:**
   - **(c) Doppelbuchungen bzw. Doppelzahlungen ohne jede Manipulation.** Bis 17.08.2026 entstand bei jedem Aufruf der Zahlungsseite ein *neuer* PaymentIntent. Bis 19.07.2026 sah ein Kunde nach Klarna-, Sofort- oder anderen Redirect-Zahlungen oft erneut das Zahlungsformular, solange der Webhook noch nicht verarbeitet war. Keine Ebene war idempotent: weder payment_svc noch Worker noch, soweit sichtbar, die Custom-API.
   - **(a) Der Worker bucht unabhängig vom Status.** `update_payment_details` prüft `payment_status` nicht. Jede MQ-Nachricht, also auch `failed` oder `canceled` mit gesetzter `payment_method`, wird als volle Zahlung nach JTL gebucht. Ob Stripe solche Events liefert, ist unsicher (siehe 3.3), deshalb als Vermutung mit geringer Häufigkeit eingestuft.
   - **(a) Stripe-Testmodus gegen Live-Wawi (Vermutung, hohe Relevanz).** Die dokumentierte Deploy-`.env` (Readme, seit 05.03.2026) nutzt Stripe-**Test**keys (`sk_t…`, `pk_t…`, `whse…`). Laut Commit 8fbc713 (09.02.2026) bestellte Meesenburg schon damals gegen das „Live-WaWi-System“. Testzahlungen, z. B. mit Testkarte, wären damit als echte „Stripe“-Zahlungen in JTL gebucht worden. Wann prod auf Live-Keys umgestellt wurde, steht nicht im Repo.
   - **(a) Signatur-Fallback `no_token`.** Fehlt `STRIPE_SIGNING_SECRET` in der Umgebung, gilt das öffentlich bekannte Secret `no_token`. Dann lassen sich Webhooks fälschen. Das geht nur mit Manipulation und nur bei fehlerhafter Konfiguration (Vermutung).
4. **(b) Abweichende Beträge vor dem 17.08.2026:** Gebucht wurde `intent_amount`, also der Betrag beim Anlegen des Intents (= `StillToPay` aus dem JTL-Abbild), nicht `amount_received` aus Stripe. Weil Intents nie nachträglich geändert wurden, entspricht das im Normalfall dem gezahlten Betrag. Abweichungen gibt es praktisch nur bei Fehlbuchungen nach (a) (gebucht ohne Geldeingang) und bei Doppelbuchungen nach (c).
5. **Ab 17.08. (Commit) bzw. 20.08.2026 (Merge PR 808)** kommen neue Risiken hinzu: der öffentliche `mark_paid`, die Wiederverwendung eines PaymentIntent pro Bestellung (Betrag ≠ Bestellung möglich) und der Buchungsbetrag aus der Bestellsumme statt aus Stripe.
6. **Gegenrichtung (bezahlt, aber nicht gebucht):** Bis 22.07.2026 kannte der Code die Methode `link` nicht, bis 20.08.2026 auch `amazon_pay` und `revolut_pay` nicht. Solche erfolgreichen Zahlungen wurden **nie nach JTL gebucht**. Folge: Der Kunde sieht weiter einen offenen Betrag und zahlt eventuell ein zweites Mal.

---

## 2. Zeitleiste Go-Live / Softstart

| Datum (Merge in main) | Commit | Indiz |
|---|---|---|
| 07.11.2025 | 3dca370 (PR #194) | Payment-Service (Stripe) und JTL-Zahlungsbuchung eingeführt |
| 07.11.2025 | 9062df5 (#213) | Google-Merchant-Route (erste Version) |
| 01.12.2025 | 2b10da9 (#237) | „update salesOrder after payment“: Zahlung → JTL produktiv verdrahtet |
| 09.02.2026 | 8fbc713 (#267) | „Add Stripe keys to live frontend“. Commit-Text: *„On the current Live-WaWi-System Meesenburg ran into this problem while ordering“*. **Testbestellungen liefen schon gegen die Live-Wawi.** |
| 05.03.2026 | 7130753 | Deploy-Compose-Dateien. Readme-`.env`: `DOMAIN=mb-sn24.meesenburg.de` (Staging) mit Stripe-**Test**keys `sk_t…`/`pk_t…`/`whse…` und JTL-Server `c10330…` |
| 13.05.2026 | 7071ea6 (PR 612; Einzelcommit 26b6c4b) | Google Tag Manager / GA-Tracking eingebaut |
| 15.05.2026 | a9f6ee4 (PR 613) | „add JTL migrate script“ (Kundendatenmigration) |
| 19.05.2026 | 1d8006d (PR 616; Einzelcommit 02ffd9a 18.05.) | CookieFirst-Consent, **erste Nennung von `sicherheit.meesenburg.com`** (Consent-Site-ID, `BASE_URL`, `CMS_URL`) |
| 20.05.2026 | 2a8d19a (PR 628) | Google-Merchant-CSV-Feed |
| 29.05.2026 | 3673a9a (PR 647) | Standard-Zahlungsart von 1 (Bar) auf 23 (Vorkasse), Tippfehler `PaymentMehodId` behoben |
| 30.05.–05.06.2026 | b80df7d, 277cf05, ddc1d75, 1f85bb0 | 200/404-Check, Title-Tags (SEO-Vorbereitung) |
| 05.06.2026 | 7e3d475 (PR 669; 3498431) | Trusted-Shops-Badge |
| 06.06.2026 | bad9e37 (PR 674) | Artikel-Update jede Nacht |
| 11.06.2026 | c2d304f (PR 690) | „Fix all the old category URL schema“ (Weiterleitung alter Shop-URLs, typisch für den Domain-Umzug) |
| 13.06.2026 | f516c8f (PR 694) | Händlerbund-Link im Footer |
| **17.06.2026 10:29 UTC** | dd2f58d (PR 707) | letzter PR **vor** dem Relaunch („Fix minor issues“) |
| **23.06.2026 07:43 UTC** | 56003f9 (PR 713) | **„First Fixes and Changes after Relaunch“** |
| 01.07.2026 | 100970c (PR 720) | „Second Fixes and Changes after Relaunch“ |
| 07.07.2026 | 35c6f7f (PR 727) | 410-Gone-Seite für alte URLs, Sitemap mit `BASE_URL=https://sicherheit.meesenburg.com` |

Weitere Befunde:
- Im Repo stehen nie Live-Keys (`pk_live`/`sk_live` kommen in keinem Commit vor). Die Umstellung test→live geschah nur in der Server-`.env` und ist **aus Git nicht datierbar**.
- Commits mit „go-live“, „golive“ oder „hotfix/prod“ gibt es nicht. Releases sind PR-Merges in `main`, Release-Tags fehlen.

**Einschätzung:** Produktiver Relaunch auf `sicherheit.meesenburg.com` **zwischen 17.06. und 23.06.2026**. Ein Softstart mit echten Kunden ist ab etwa Mitte Mai 2026 denkbar (Tracking und Consent mit Produktions-Domain ab 13./19.05.). Das ist eine Vermutung und nur über Webserver-Logs und den ersten echten JTL-Auftrag mit `cName='Stripe'` zu belegen. **Bestellungen gegen die Live-Wawi gab es schon ab spätestens 09.02.2026** (Test- oder Staging-Bestellungen).

---

## 3. Zeitleiste Zahlungsfluss

### Phase 0: bis 07.11.2025 (vor 3dca370)
Keine Online-Zahlung. Bestellungen gingen synchron per `POST /api/order/` nach JTL (Zahlungsart Bar). Es gab keinen Bezahlt-Status. Für den Live-Betrieb irrelevant.

### Phase 1: 07.11.2025 bis 20.08.2026 (Webhook → MQ → Worker → JTL)
Gilt bis einschließlich 48bf59c (17.08.2026, letzter Stand vor `ef0fe86` in `main`). PR 808 wurde am **20.08.2026 11:14 UTC** (285d351) gemergt.

**Ablauf**
1. Checkout `POST /api/order/` legt den Auftrag **sofort in JTL** an, noch vor der Zahlung. Bis 29.05.2026 effektiv mit Zahlungsart „Bar“ (Feld hieß fälschlich `PaymentMehodId`), danach Vorkasse (23). Code: `48bf59c:backend/business/src/business_api/v1/endpoints/order.py:260-396`.
2. Die Zahlungsseite `/payment/{orderId}` lädt den Auftrag (`GET /api/order/single_order?id=`). Ist `StillToPay > 0`, ruft sie `POST /api/payment/intents/` mit `{order_id}` auf (`3534860^:frontend/src/routes/(store)/payment/[orderId]/+page.svelte:59-80`).
3. payment_svc ermittelt den Betrag **serverseitig** aus `StillToPay` des JTL-Abbilds (`48bf59c:backend/payment_svc/src/payment/utils/pricing.py:40-62`). Dann `stripe.PaymentIntent.create(amount=total)` (`…/v1/endpoints/intents.py`). Bei **jedem Aufruf entsteht ein neuer Intent**, eine Wiederverwendung gibt es nicht.
4. Stripe-Webhook `POST /api/payment/webhook/`:
   - Die **Signatur wird geprüft** (`stripe.Webhook.construct_event`, `…/utils/webhook.py:13-30`).
   - Mapping `succeeded→paid`, `payment_failed→failed`, `canceled→canceled`. Bei `paid` muss eine `payment_method` vorhanden sein (`…/v1/endpoints/webhook.py:53-67`).
   - Der Status wird in die Payment-Mongo geschrieben. Danach MQ-Push `payment_intent_updated` mit `payment_status`, `payment_method` und **`amount=intent.intent_amount`** (`48bf59c:backend/payment_svc/src/payment/utils/webhook.py:85-97`).
   - **Betrag und Status werden nicht gegen das Event (`amount_received`, `status`) geprüft.**
   - Ab 22.07.2026 (abb1cc4): ohne `payment_method` kein MQ-Push (`…/utils/webhook.py:77-83`). Vorher führte das zu einem ValidationError, einer `MessageQueueError` und HTTP 409, worauf Stripe das Event erneut zustellte.
5. Der Business-Worker `consume_payment_intent_update` (`48bf59c:backend/business/src/business_api/worker.py:106-129`) ruft `update_payment_details` auf (`48bf59c:backend/business/src/business_api/jtl_helper/payment.py:89-113`). Das ist ein Custom-API-`POST /payment` (tZahlung) mit:
   - `fBetrag = message.amount/100` (Betrag aus dem Intent, nicht aus Stripe),
   - `kZahlungsart` per Mapping (card→13, sepa_debit→27, klarna→25, paypal→21 …),
   - `cExternalTransactionId = intent_id`.

   **`payment_status` wird nirgends ausgewertet.** `map_to_jtl_payment_status` (Z. 65-86) ist definiert, wird aber nur in Tests benutzt. Danach wird der Auftrag aus JTL neu geladen (`update_order_after_payment`) und erst dann `basic_ack`.
6. Shop-seitig gilt eine Bestellung als „bezahlt“, wenn `StillToPay <= 0` im JTL-Abbild steht. Das ist nur der Fall, wenn JTL die Zahlung verbucht hat.

**Eigenschaften der Phase 1**

| Prüfpunkt | Befund |
|---|---|
| Serverseitige Prüfung gegen Stripe | Nur über die Webhook-Signatur. Status kommt aus dem Event-Typ. Betrag und `amount_received` werden nicht geprüft. Keine Prüfung, ob der Intent bereits `paid` ist |
| Öffentlicher Weg zu „bezahlt“ | **Keiner** im Code. Business-API-Schreibrouten waren nur `POST /order/`, `/cart`, `/configurator`, `/contact`, `/sitemap/refresh`. Die MQ ist intern |
| Betragsquelle JTL-Zahlung | `intent_amount` = `StillToPay` bei Intent-Erzeugung. Entspricht dem belasteten Betrag, weil Intents nie geändert werden. Nicht `amount_received` |
| Intent-Wiederverwendung | Nein. **Aber mehrere Intents pro Bestellung möglich** (jeder Seitenaufruf, Reload, zweiter Tab, Rückkehr vom Redirect bis 19.07.) |
| Buchung bei failed/canceled | **Ja, sofern das Event eine `payment_method` enthält.** Der Worker bucht immer den vollen Betrag. Ohne `payment_method`: bis 22.07. HTTP 409 mit Retry, danach übersprungen |
| Idempotenz | Keine. Weder Event-ID- noch Intent-Status-Prüfung, kein Duplikat-Check auf `cExternalTransactionId` im Worker. Die Custom-API liegt nicht im Repo, ihr Verhalten ist unbekannt |

**Änderungen innerhalb von Phase 1**
- 27.11.2025, 2a16053: Parameter `order_id`→`id` beim Lesen der Bestellung für den Intent (vorher fehlerhaft, vor Go-Live).
- 01.12.2025, 2b10da9: Worker-Umbau. Ack erst nach Buchung **und** Reload des Auftrags. Wirft der Reload einen Fehler, wird die Zahlung trotzdem gebucht, aber nicht ge-ackt. Bei erneuter Zustellung droht dann eine Doppelbuchung (siehe 3.3).
- 04.05.2026, a7e5fde: `requests`→`httpx`, keine Logikänderung.
- 23.06.2026, 56003f9: Signaturänderung `get_single_salesorder(client, custom_api, id)`, keine Logikänderung.
- 19.07.2026, **3534860** (PR 764 „Fix broken payment…“): Nach Rückkehr vom Stripe-Redirect (`payment_intent`/`redirect_status` in der URL) wird nur noch gepollt, es entsteht kein neuer Intent und kein neues Formular. **Vorher** wurde bei einem Redirect-Rückkehrer mit noch offenem `StillToPay` sofort ein neuer Intent erzeugt und das Zahlungsformular erneut angezeigt.
- 22.07.2026, abb1cc4: `link` ergänzt (vorher wurden Link-Zahlungen nie gebucht), Skip ohne `payment_method`, Zeitzone Berlin.

### Phase 2: ab 17.08.2026 (Commit ef0fe86), in main ab 20.08.2026 (285d351)
- Stripe-Bestellungen werden **erst nach der Zahlung** nach JTL übertragen (`send_order_to_systems`). Vorkasse wird sofort übertragen.
- Der payment_svc-Webhook ruft für **jeden Status mit `payment_method`** `PATCH http://business_api:11000/order/{order_id}/mark_paid` auf (HEAD `backend/payment_svc/src/payment/utils/webhook.py:88-103`). Der MQ-Push danach ist toter Code (`return` in Z. 105).
- `PATCH /api/order/{order_id}/mark_paid` ist **öffentlich** (Traefik `PathPrefix(/api)`, keine Auth). Er setzt `StillToPay=0`, `PaymentStatus=1` und `_intent_id` aus dem Payload und ruft `send_order_to_systems` (HEAD `backend/business/src/business_api/v1/endpoints/order.py:110-138`).
- Die JTL-Zahlung `send_payed_to_erp` bucht **`fBetrag = TotalGrossAmount` der Shop-Bestellung**, nicht den Stripe-Betrag (HEAD `backend/business/src/business_api/utils/order.py:566-583`). Dedupe über das Flag `_in_erp`/`_in_erp_payed` (ab 25.08.2026 bbb8d79).
- `POST /intents` **verwendet einen vorhandenen Intent pro `order_id` wieder** (HEAD `backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33`), unabhängig von Status und Betrag. Die Shop-Bestellung kann aber über `checkout_data.order_id` mit gleicher `_id` und neuem Warenkorb neu aufgebaut werden (HEAD `utils/order.py:185-203`, „Update bestehender pending Order“). **Belasteter Betrag (alter Intent) ≠ gebuchter Betrag (neue `TotalGrossAmount`).**
- Weitere öffentliche Route: `GET /api/order/send_order_to_system/{order_id}` (HEAD `order.py:153-164`). Sie bucht nur, wenn bereits ein `_intent_id` gesetzt ist.
- Bei `failed`/`canceled` über `mark_paid`: Der Auftrag wird angelegt. Die Zahlungsbuchung scheitert vermutlich an `DateOfPayment=None` (`fromisoformat(None)` → `TypeError`, unterdrückt). `_intent_id` bleibt aber gesetzt.

### 3.3 Hinweis zu Stripe-Semantik (Vermutung)
- Bei `payment_intent.payment_failed` setzt Stripe `payment_method` in der Regel auf `null` (die Methode steht dann in `last_payment_error.payment_method`). Das Risiko „failed wird gebucht“ ist deshalb **wahrscheinlich selten**.
- `payment_intent.canceled` kann eine `payment_method` tragen, etwa bei Abbruch nach `requires_action` oder `requires_capture`.
- Stripe stellt Webhooks *at-least-once* zu: Retries bei Timeout oder Nicht-2xx, gelegentlich Duplikate. Der Webhook ruft vor der Antwort synchron die Stripe-API auf (`PaymentMethod.retrieve`) und pusht in die MQ. Langsame Antworten und damit Retries sind realistisch.
- Das Verhalten der externen Bibliothek `svc_helper.MessageQueueConsumer` bei Exceptions (nack, requeue oder Abbruch) liegt nicht im Repo. Nicht ge-ackte Nachrichten stellt RabbitMQ beim Reconnect bzw. Worker-Neustart erneut zu.

---

## 4. Risikozeiträume

Datumsangaben = Merge in `main`, tatsächliche Deploys können später liegen. Kategorien: **(a)** ohne echte Zahlung als bezahlt/gebucht, **(b)** gebuchter ≠ gezahlter Betrag, **(c)** Doppelbuchung bzw. Doppelzahlung, **(d)** gezahlt, aber nicht gebucht (Gegenrichtung, relevant für den Abgleich).

| # | Zeitraum | Kat. | Mechanismus (Beleg) | Ausnutzbarkeit | Abgleich |
|---|---|---|---|---|---|
| R1 | **mind. 09.02.2026 (8fbc713) bis zur Umstellung auf Live-Keys (unbekannt), ggf. bis heute für Staging** | a | Stripe-**Testmodus** gegen die **Live-Wawi**. Testzahlungen (z. B. Testkarte) lösen echte JTL-Buchungen `cName='Stripe'` aus. Readme-`.env` ab 7130753 (05.03.2026) mit `sk_t…`/`pk_t…`/`whse…`, `DOMAIN=mb-sn24.meesenburg.de`. Vermutung: Prod-`.env` ist nicht im Repo | **Ohne Manipulation**, jeder Besucher der Test-/Staging-Instanz mit Testkarte | Alle JTL-Zahlungen `cName='Stripe'`: `cExternalTransactionId` (pi_…) im **Live**-Stripe-Konto nachschlagen. Nicht gefunden oder nur im Testmodus vorhanden = Fehlbuchung |
| R2 | 07.11.2025 (3dca370) bis 20.08.2026 (Ende Phase 1) | a, b | Worker bucht **ohne Statusprüfung** (`48bf59c:…/jtl_helper/payment.py:89-113`). `failed`/`canceled`-Events mit `payment_method` werden als volle Zahlung gebucht. Vor 22.07. scheiterten Events ohne PM mit 409, danach wurden sie übersprungen | Ohne Manipulation, aber nur bei passenden Stripe-Events (Vermutung: selten, v. a. `canceled`) | Stripe-Events `payment_intent.payment_failed`/`.canceled` des Zeitraums: Gibt es zu deren `pi_…` eine JTL-Zahlung? Zusätzlich: jede JTL-Stripe-Zahlung muss zu einem PI mit `status=succeeded` gehören |
| R3 | 07.11.2025 bis 20.08.2026; **Schwerpunkt bis 19.07.2026 (3534860)** | c | Ein neuer PaymentIntent bei **jedem** Aufruf der Zahlungsseite. Bis 19.07. sah ein Redirect-Rückkehrer (Klarna, Sofort, PayPal …) bei noch offenem `StillToPay` erneut das Formular, mit neuem Intent. Beide Zahlungen sind echt, beide werden gebucht | **Ohne Manipulation**, Kunde zahlt versehentlich doppelt | Stripe: Bestellungen (Intent-Metadaten fehlen, daher über payment_svc-Mongo `payment.order_id`) mit **> 1 PI `succeeded`**. JTL: Aufträge mit **> 1 Stripe-Zahlung** oder Summe Zahlungen > Auftragssumme (Überzahlung). Rückerstattung prüfen |
| R4 | 07.11.2025 bis 20.08.2026 | c | Fehlende Idempotenz: Stripe-Retry oder Duplikat-Event erzeugt einen zweiten MQ-Push. Worker-Redelivery, wenn `update_order_after_payment` nach erfolgreicher Buchung scheitert (Ack erst danach, `worker.py:117-123`). Ob die Custom-API Duplikate auf `cExternalTransactionId` abweist, ist unbekannt | Ohne Manipulation (technische Fehler, Timeouts der JTL-API 45 s) | JTL: `cExternalTransactionId` **mehrfach** in tZahlung. Business-Mongo `payment_log` je `intent_id` zählen. Stripe-Dashboard: Webhook-Zustellversuche > 1 |
| R5 | 07.11.2025 bis 22.07.2026 (`link`); bis 20.08.2026 (`amazon_pay`, `revolut_pay`) | d (→ c) | Methode fehlt im `Literal` bzw. Mapping, MQ-Nachricht ungültig, **keine Buchung**. Kunde sieht weiter einen offenen Betrag und zahlt eventuell nochmals | Ohne Manipulation | Stripe: `succeeded`-PIs mit `payment_method_types` link/amazon_pay/revolut_pay ohne JTL-Zahlung. Gleiche Bestellung mit zweitem PI prüfen |
| R6 | ab 07.11.2025, sobald `STRIPE_SIGNING_SECRET` fehlt (Default `no_token`, `compose/services*/backend/payment_svc.yml:30-31`) oder ein bekanntes Secret genutzt wird (Readme `whse…`) | a | Gefälschte `succeeded`-Webhooks. Ein PI lässt sich öffentlich über `/api/payment/intents/` erzeugen, eine PaymentMethod-ID über den Publishable Key | **Nur mit Manipulation** und nur bei Fehlkonfiguration (Vermutung) | payment_svc-Logs/Mongo: PIs mit Status `paid`, deren Stripe-Status ≠ `succeeded`. Prod-`.env` prüfen |
| R7 | **ab 17.08.2026 (Commit) / 20.08.2026 (Merge) bis heute** | a | Öffentlicher `PATCH /api/order/{id}/mark_paid` mit beliebigem `intent_id`. Erzeugt JTL-Auftrag und JTL-Zahlung | **Mit Manipulation**, trivial (ein HTTP-Request, `order_id` bekannt bzw. eigene Bestellung) | Jede JTL-Zahlung ab 17.08. mit `cExternalTransactionId`: PI muss im Live-Stripe existieren, `succeeded` sein und zum Auftrag gehören. Leere bzw. fremde IDs = verdächtig |
| R8 | ab 20.08.2026 bis heute | b | Intent-Wiederverwendung pro `order_id` plus Neuaufbau der Pending-Order mit neuem Warenkorb. **Belastet wird der alte Intent-Betrag, gebucht wird die neue `TotalGrossAmount`** (`utils/order.py:570`) | Ohne Manipulation (Kunde ändert Warenkorb nach erstem Zahlungsversuch). Gezielt ausnutzbar (billig zahlen, teuer bestellen) | Je JTL-Auftrag ab 20.08.: `fBetrag` ≠ Stripe `amount_received` des PIs aus `cExternalTransactionId` |
| R9 | ab 20.08.2026 bis heute | a | Webhook ruft `mark_paid` auch bei `failed`/`canceled` mit PM auf. Der Auftrag wird nach JTL übertragen, die Zahlung scheitert vermutlich (`DateOfPayment=None`). `_intent_id` bleibt gesetzt, ein späterer `send_order_to_system`-Aufruf ist öffentlich | Gering, eher Fehlerzustand | JTL-Aufträge ab 20.08. ohne Zahlung, aber mit Shop-`_intent_id`, gegen den Stripe-Status prüfen |

**Nicht gefunden:** Vor dem 17.08.2026 gab es keinen öffentlichen bzw. unauthentifizierten API-Weg, eine Bestellung als bezahlt zu setzen oder eine Zahlung nach JTL zu buchen. Auch einen client-seitig gesteuerten Betrag gab es nicht: Der Intent-Betrag kam immer serverseitig aus JTL `StillToPay`.

---

## 5. Empfehlung für den Abgleich

**Datenquellen:** JTL tZahlung (`cName='Stripe'`, `cExternalTransactionId`, `fBetrag`, `kBestellung`, `dDatum`), Stripe **Live** und **Test** (PaymentIntents, Charges, Events, Webhook-Zustellprotokoll), payment_svc-Mongo `payment.payment` (`order_id`, `intent_id`, `intent_amount`, `status`), business-Mongo `payment_log` (Phase 1) und `order` (Phase 2: `_intent_id`, `_in_erp_payed`), Container-Logs (`[WEBHOOK-STUB]`, `mark_paid:`, `[INTENT] Wiederverwendung`).

1. **Vollabgleich aller JTL-Stripe-Zahlungen ab 01.12.2025:** für jede Zeile `cExternalTransactionId` in Stripe Live abfragen.
   - Nicht vorhanden: Testmodus-Buchung (R1) oder Fälschung (R6/R7).
   - `status ≠ succeeded`: R2 oder R9.
   - `amount_received/100 ≠ fBetrag`: R8 bzw. b.
2. **Duplikate:** `GROUP BY cExternalTransactionId HAVING COUNT(*)>1` (R4) und `GROUP BY kBestellung HAVING COUNT(Stripe-Zahlungen)>1` bzw. Summe > Auftragswert (R3).
3. **Gegenrichtung:** jeden Live-PI `succeeded` (ab Go-Live) einer JTL-Zahlung zuordnen. Fehlt sie: R5 bzw. Webhook-Fehler. Prüfen, ob der Kunde ein zweites Mal gezahlt hat.
4. **Intent pro Bestellung:** In der payment_svc-Mongo Bestellungen mit mehreren `intent_id` suchen (Phase 1) und deren Stripe-Status prüfen (Doppelzahlung R3).
5. **Ab 17.08.2026 gesondert:** alle `mark_paid`-Aufrufe aus den Business-API-Logs (Quell-IP ≠ payment_svc-Container) und Traefik-Access-Logs `PATCH /api/order/*/mark_paid` von extern (R7).
6. **Konfiguration:** Prod-`.env` einsehen. Wann wurde `STRIPE_SECRET_KEY` auf `sk_live…` umgestellt, ist `STRIPE_SIGNING_SECRET` gesetzt, nutzen Staging-Instanzen dieselbe JTL? Damit lässt sich R1 zeitlich schließen.
7. **Nebenbefund Sicherheit:** Die Readme (seit 7130753, 05.03.2026) enthält Klartext-Zugangsdaten (Stripe-Test-Keys, Webhook-Secret, JTL-API-Token, Custom-API-Token, SMTP-Passwort). Alle diese Secrets rotieren, auch die Git-Historie ist betroffen.

---

### Belegliste (Datei:Zeile)
- `48bf59c:backend/business/src/business_api/jtl_helper/payment.py:89-113`: Buchung ohne Statusprüfung, `fBetrag=message.amount/100`
- `48bf59c:backend/business/src/business_api/worker.py:106-129`: MQ-Consumer, Ack nach Buchung und Reload
- `48bf59c:backend/payment_svc/src/payment/utils/webhook.py:77-97`: Skip ohne PM (ab abb1cc4), `amount=intent.intent_amount`
- `48bf59c:backend/payment_svc/src/payment/v1/endpoints/webhook.py:42-67`: Signaturprüfung, Status-Mapping
- `48bf59c:backend/payment_svc/src/payment/utils/pricing.py:40-62`: Betrag = `StillToPay`
- `3534860^:frontend/src/routes/(store)/payment/[orderId]/+page.svelte:59-80`: neuer Intent bei jedem Aufruf, kein Redirect-Handling
- HEAD `backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33`: Intent-Wiederverwendung
- HEAD `backend/payment_svc/src/payment/utils/webhook.py:88-105`: Aufruf `mark_paid` für jeden Status mit PM
- HEAD `backend/business/src/business_api/v1/endpoints/order.py:110-138, 153-164`: öffentlicher `mark_paid` und `send_order_to_system`
- HEAD `backend/business/src/business_api/utils/order.py:185-203, 566-583, 602-616`: Pending-Order-Update, Buchung mit `TotalGrossAmount`, Dedupe-Flags
- `compose/services_deploy/backend/payment_svc.yml:29-30`, `business_api.yml:42-48`: Secret-Default `no_token`, `/api`-Routing öffentlich
