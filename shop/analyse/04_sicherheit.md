# 04 – Sicherheitsaudit Webshop SN24 (statische Analyse)

| | |
|---|---|
| **Gegenstand** | Git-Repository `webshop/` (Branch `main`, HEAD `9cc4a13`, 365 Commits, 28 Remote-Branches) |
| **Datum** | 07.10.2026 |
| **Methode** | Rein statische Code- und Konfigurationsanalyse inkl. vollständiger Git-Historie (`git log --all -p`). Keine Netzwerkzugriffe, keine Ausnutzung, keine Änderungen am Repo. |
| **Orientierung** | OWASP Top 10 (2021), OWASP ASVS 4.0.3, CWE |
| **Secrets** | In diesem Bericht ausschließlich maskiert (erste 4 Zeichen + „…“). |

> **Hinweis zur Aussagekraft:** Welche Compose-/Stack-Datei produktiv verwendet wird und welche Werte die produktive `.env` enthält, ist aus dem Repo nicht eindeutig ableitbar (es gibt `docker-compose_deploy.yml`, `compose/services_deploy/*` und `docker-stack.yml`). Befunde, die davon abhängen, sind mit **„zu verifizieren“** gekennzeichnet. Die Code-Befunde (Autorisierung, Injection, Preislogik) gelten unabhängig davon.

---

## 1. Management-Zusammenfassung

Der seit Juni 2026 produktive Shop weist **mehrere kritische Schwachstellen** auf, die ohne Anmeldung und mit trivialen HTTP-Anfragen ausnutzbar sind. Die Ursache ist fast immer dieselbe: **Die Backend-Services prüfen weder Authentifizierung noch Autorisierung**, und Traefik leitet den kompletten Pfad `/api/*` des Business-API (inklusive rein interner Endpunkte) sowie den PDF-Service ins Internet durch.

Die gravierendsten Punkte:

1. **Massenhafter Abfluss von Kundendaten (DSGVO-meldepflichtig)** – Bestellungen inkl. Name, Anschrift, E-Mail, Telefon, Positionen und Auftragsbestätigungs-PDF sind über fortlaufende Bestellnummern bzw. Kundennummern ohne Login abrufbar (`/api/order/single_order`, `/api/order/?user_id=`, `/api/maildata/*`). Die E-Mail-Prüfung der „Bestellung finden“-Seite findet **nur im Browser** statt.
2. **Bestellungen ohne Bezahlung als „bezahlt“ markierbar** – `PATCH /api/order/{id}/mark_paid` ist öffentlich und ohne Authentifizierung; die Bestellung wird danach als bezahlt an JTL-Wawi übertragen.
3. **Preismanipulation** – Versandkosten kommen vom Client, Mengen dürfen negativ sein, bestehende Bestellungen können per `order_id` überschrieben werden, während der alte (günstigere) Stripe-PaymentIntent wiederverwendet wird.
4. **Remote Code Execution** – Der PDF-Service ist öffentlich unter `/api/pdf` erreichbar und rendert beliebige Jinja2-Template-Strings ohne Sandbox (Server-Side Template Injection) sowie beliebiges HTML mit WeasyPrint (SSRF/lokales Datei-Lesen).
5. **Produktive Zugangsdaten im Git** – JTL-Wawi-API-Token, Custom-API-Token (JTL-SQL-Zugriff), SMTP-Passwort des Shop-Postfachs, Stripe-Schlüssel (Test), sämtliche Strapi-Secrets, der Blowfish-Schlüssel des Alt-Shops sowie ein schwacher JWT-Schlüssel (`foob…`) liegen im Klartext im Repository bzw. in der Historie.
6. **Offene Infrastruktur-Dienste** – Mailpit-Weboberfläche (alle Mails inkl. Passwort-Reset-Links), RabbitMQ-Management mit `user`/`pw12…`, Valkey ohne Passwort und – über `docker-compose_deploy.yml` eingebunden – mongo-express **ohne Login** werden auf Host-Ports veröffentlicht (zu verifizieren, ob von außen erreichbar).
7. **Unsicherer Passwort-Reset** – Reset-Tokens laufen faktisch nie ab, jeder gespeicherte Token (auch Refresh-Token) wird akzeptiert, und der Reset-Token ist gleichzeitig ein gültiges Login-JWT.

**Gesamtbewertung:** Kritisch. Es ist davon auszugehen, dass ein Angreifer mit geringem Aufwand den gesamten Kundenbestand auslesen, Waren ohne Zahlung bestellen und Server übernehmen kann. Wir empfehlen, die Sofortmaßnahmen (Abschnitt 2) **innerhalb von 24–72 Stunden** umzusetzen, die Zugriffsprotokolle (Traefik-Access-Log, Loki/Promtail) auf bereits erfolgte Ausnutzung zu prüfen und den Datenschutzbeauftragten einzubinden (Art. 33 DSGVO – 72-Stunden-Frist ab Kenntnis einer Verletzung).

**Positiv** fällt auf: Argon2-Passwort-Hashing mit Bcrypt-Upgrade-Pfad, timing-sichere Login-Logik mit Fallback-User, HttpOnly/Secure/SameSite-Cookies, korrekte Stripe-Webhook-Signaturprüfung, serverseitige Ermittlung der Artikelpreise, Jinja2-Autoescape in Mail- und Bestell-PDF-Templates, interne Docker-Netze (`internal: true`) für die Datenbanken, GTM erst nach Consent.

### Statistik

| Schweregrad | Anzahl |
|---|---|
| Kritisch | 7 |
| Hoch | 9 |
| Mittel | 9 |
| Niedrig | 4 |
| **Summe** | **29** |

---

## 2. Sofortmaßnahmen (priorisiert)

| Prio | Maßnahme | Befund |
|---|---|---|
| **P0 – sofort (Stunden)** | Traefik: öffentliche Routen auf eine **Allowlist** reduzieren. Sofort sperren: `/api/pdf/*`, `/api/order/*/mark_paid`, `/api/order/send_order_to_system/*`, **`/api/order/send/*`, `/api/order/send_payed/*`** (im Repo auskommentiert, live laut Test vom 08.10.2026 aber offenbar aktiv – Produktionsstand weicht vom Repo ab), `/api/order/send_mail/*`, `/api/maildata/*`, `/api/sitemap/refresh`, `/api/user/?identifier=`, `/api/*/docs`, `/api/*/openapi.json`, `/api/auth/v1/demo/`. Übergangsweise per Traefik-Middleware (z. B. `IPAllowList` nur Docker-Netz) | S-01, S-02, S-03, S-15, S-26, S-29 |
| P0 | `GET /api/order/single_order` und `GET /api/order/?user_id=` serverseitig an Login bzw. an E-Mail + Bestellnummer + Rate-Limit binden (Hotfix: Endpunkt nur noch mit gültigem Access-Token + Eigentumsprüfung) | S-01 |
| P0 | Host-Port-Freigaben entfernen bzw. per Firewall (Achtung: Docker umgeht `ufw`/iptables-INPUT) sperren: 1025, 8025 (Mailpit), 11010/11012 (RabbitMQ), 11020 (Valkey), 11021–11025 (Redis-Commander/mongo-express), 10002 (Frontend direkt). `db_viewer.yml` aus dem Deploy entfernen | S-07 |
| **P1 – innerhalb 24 h** | **Rotation aller kompromittierten Secrets** (Reihenfolge): 1. JTL-Wawi-API-Token, 2. Custom-API-Token, 3. SMTP-Passwort `info@…`, 4. JWT-Signaturschlüssel `SECURITY__KEY` (≥ 256 Bit, zufällig; invalidiert alle Sessions), 5. Strapi `APP_KEYS`, `ADMIN_JWT_SECRET`, `JWT_SECRET`, `API_TOKEN_SALT`, `TRANSFER_TOKEN_SALT`, 6. alle DB-/MQ-Passwörter (`pw12…`), 7. Strapi-Admin-Passwörter + API-Tokens neu erzeugen, 8. Stripe-Test-Keys + Webhook-Secret rollen; prüfen, ob produktiv Live-Keys aktiv sind | S-04, S-05, S-11 |
| P1 | Prüfen, ob `SECURITY__KEY` produktiv `foob…` oder Default `abc1…` ist. Falls ja: sofort ersetzen – sonst kann jeder ein JWT für jede E-Mail-Adresse fälschen | S-05 |
| P1 | Log-Forensik: Traefik-Access-Log/Loki nach Zugriffen auf `/api/order/single_order`, `/api/order/?user_id`, `/api/maildata`, `/mark_paid`, `/api/pdf/v1/generate/template_string`, `/api/pdf/v1/generate/html` durchsuchen; JTL-Aufträge mit Zahlungsstatus „bezahlt“ gegen Stripe-Auszahlungen abgleichen | S-01–S-03, S-06 |
| P1 | Datenschutzvorfall bewerten (Art. 33/34 DSGVO) | S-01, S-09 |
| **P2 – innerhalb 1 Woche** | `mark_paid` nur noch intern mit Service-Token; Zahlungsbestätigung ausschließlich über Stripe-Webhook mit Betragsabgleich; Bestellung nach Intent-Erstellung einfrieren; `quantity ≥ 1`; Versandkosten serverseitig | S-02, S-06 |
| P2 | Passwort-Reset neu implementieren (zufälliger, gehashter Einmal-Token mit Ablauf und Typ-Prüfung); `/login/with-jwt` und `/login/with-token` entfernen | S-08 |
| P2 | PDF-Service: Endpunkte `template_string`, `html`, `markdown` entfernen oder nur intern; Jinja2 `SandboxedEnvironment`; WeasyPrint mit restriktivem `url_fetcher` | S-03 |
| P2 | Rate-Limiting (Traefik `RateLimit`-Middleware) für Login, Registrierung, Passwort-vergessen, Order-Lookup, Kontakt, Upload | S-10 |
| **P3 – innerhalb 1 Monat** | Git-Historie bereinigen (`git filter-repo`) **nach** Rotation; Secret-Scanning (gitleaks/trufflehog) in pre-commit und CI; Security-Header/CSP; Dependency-Updates; Container-Härtung; Strapi im Production-Modus | S-04, S-18, S-23, S-22, S-11 |

---

## 3. Befunde-Übersicht

| ID | Schwere | CWE | Fundstelle (Auszug) | Kurzbeschreibung |
|---|---|---|---|---|
| S-01 | **Kritisch** | CWE-639, CWE-862 | `backend/business/src/business_api/v1/endpoints/order.py:88-107`, `.../v1/endpoints/mail_data.py:8-32`, `frontend/src/routes/(store)/order-lookup/+page.svelte:60-95` | IDOR: Bestellungen, Kundendaten und Bestell-PDFs aller Kunden ohne Login abrufbar; E-Mail-Prüfung nur clientseitig |
| S-02 | **Kritisch** | CWE-306, CWE-840 | `backend/business/src/business_api/v1/endpoints/order.py:110-163` | `mark_paid` und `send_order_to_system` öffentlich ohne Auth → Bestellung ohne Zahlung als bezahlt an JTL |
| S-03 | **Kritisch** | CWE-1336, CWE-94, CWE-918 | `backend/pdf_svc/src/pdf/v1/endpoints/pdf.py:14-88`, `.../utils/template_handler.py:57-74`, `docker-stack.yml:655`, `compose/services_deploy/backend/pdf_svc.yml:34` | PDF-Service öffentlich; SSTI (unsandboxed Jinja2) → RCE; WeasyPrint-HTML → SSRF / lokale Dateien |
| S-04 | **Kritisch** | CWE-798, CWE-540 | `Readme.md:17-30`, `cms/strapi_cms/.env:7-21`, `docker-stack.yml:569-570`, `compose/*/frontend/cms.yml`, `backend/auth_svc/src/auth/migrate_jtl_data.py:313` u. a. | Produktive Zugangsdaten (JTL, Custom-API, SMTP, Strapi, Blowfish, Stripe-Test) im Repo/Historie |
| S-05 | **Kritisch** (zu verifizieren) | CWE-321, CWE-1391 | `compose/services_deploy/backend/auth.yml:29`, `compose/services/backend/auth.yml:30`, `compose/services/backend/business_api.yml:38`, `backend/auth_svc/src/auth/core/config.py:12` | JWT-HMAC-Schlüssel hartcodiert/trivial (`foob…`, Default `abc1…`) → Token-Fälschung für beliebige Konten |
| S-06 | **Kritisch** | CWE-602, CWE-639, CWE-20 | `backend/business/src/business_api/v1/endpoints/order.py:43-85`, `.../utils/order.py:185-203,393-411`, `.../schemas/orders_schemas.py:33,41,69`, `backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33` | Preis-/Bestellmanipulation: Versandkosten vom Client, negative Mengen, Überschreiben fremder/eigener Bestellungen nach Intent-Erstellung, Intent-Wiederverwendung |
| S-07 | **Kritisch** (zu verifizieren) | CWE-306, CWE-1188, CWE-668 | `compose/services*/backend/mail_svc.yml:41-43`, `docker-stack.yml:664-666`, `compose/services*/backend/message_queue.yml:7-9`, `compose/services*/backend/cache.yml:7`, `compose/services/backend/db_viewer.yml:11-76`, `docker-compose_deploy.yml:8-10`, `traefik/traefik.yml:2` | Mailpit-UI, RabbitMQ-Management (Default-Creds), Valkey ohne Passwort, mongo-express ohne Login auf Host-Ports; Traefik `api.insecure` |
| S-08 | **Hoch** | CWE-640, CWE-613, CWE-287 | `backend/auth_svc/src/auth/v1/endpoints/account.py:59-96`, `.../utils/auth.py:205-237,297-368`, `.../v1/endpoints/session.py:36-77` | Passwort-Reset: kein Ablauf, kein Token-Typ, Refresh-Token als Reset-Token, Reset-JWT als Login nutzbar; Session-Kill per „Passwort vergessen“ |
| S-09 | **Hoch** | CWE-639 | `backend/business/src/business_api/v1/endpoints/configurator.py:15-64`, `.../v1/endpoints/cart.py:12-62` | Schließanlagen-Konfigurationen und Warenkörbe beliebiger Kunden lesen/überschreiben/löschen |
| S-10 | **Hoch** | CWE-307, CWE-770 | alle Auth-/Formular-Endpunkte; `backend/auth_svc/src/auth/utils/auth.py:36-66` | Kein Brute-Force-Schutz / Rate-Limiting (Login, Reset, Order-Lookup, Kontakt, Upload) |
| S-11 | **Hoch** | CWE-1392, CWE-489, CWE-200 | `Readme.md:141`, `compose/services_deploy/frontend/cms.yml:65-66`, Git-Historie `strapi-current.sql` (f8131dc), `cms/db/init/*.sql`, `cms/strapi_cms/Dockerfile:41` | Strapi: dokumentierte Default-Admin-Credentials, DB-Dumps mit Admin-Hashes/Token-Hashes in Historie, `strapi develop` im Image, Admin-Panel öffentlich |
| S-12 | **Hoch** | CWE-434, CWE-400, CWE-306 | `backend/business/src/business_api/v1/endpoints/upload_security_card.py:17-53`, `.../utils/file.py:45-82`, `backend/mail_svc/src/mail/utils/mail_templates.py:130-148` | Upload ohne Auth, ohne Größen-/Typ-/Anzahlprüfung; Dateien als „PDF“ an internes Postfach (Malware-Vektor) |
| S-13 | **Hoch** | CWE-319 | `Readme.md:17,21`; `backend/business/src/business_api/jtl_helper/client.py:105-148` | JTL-Wawi-REST-API und Custom-API über **HTTP** (Klartext) inkl. API-Token und Kundendaten über das Internet |
| S-14 | **Hoch** | CWE-532 | `traefik/traefik.yml:38-53`, `backend/payment_svc/src/payment/main.py:38-141`, `.../v1/endpoints/webhook.py:44-46`, `backend/auth_svc/src/auth/utils/auth.py:227,294`, diverse `print()` | Personenbezogene Daten und Session-Cookies in Logs; Klartext-Passwort in RabbitMQ-Nachricht |
| S-15 | **Hoch** | CWE-284, CWE-923 | `docker-stack.yml` (alle Router), `backend/payment_svc/src/payment/utils/webhook.py:95-99`, `backend/items_svc/src/jtl/v1/utils/discount_logic.py:94-104` | Keine Service-zu-Service-Authentifizierung; interne Endpunkte durch `/api`-Präfix-Routing öffentlich; flaches `web`-Netz |
| S-16 | **Hoch** | CWE-345 | `backend/payment_svc/src/payment/utils/webhook.py:63-105`, `backend/business/src/business_api/v1/endpoints/order.py:120-135`, `backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33` | Stripe: kein Betrags-/Währungsabgleich bei Webhook, auch `payment_failed` löst ERP-Übertragung aus, `client_secret` an jeden mit Order-ID |
| S-17 | Mittel | CWE-79 | `frontend/src/lib/utils/strapiRichText.ts:3-44`, `frontend/src/lib/components/RichTextRenderer.svelte:18-54`, `frontend/src/routes/(singlepages)/faq/+page.svelte:99-112`, `frontend/src/lib/utils/sanitizeHtml.ts` | Stored XSS über CMS-Richtext (kein Escaping, `javascript:`-Links); schwacher eigener Sanitizer |
| S-18 | Mittel | CWE-693, CWE-829 | `frontend/web_server/nginx.conf`, `traefik/*`, `frontend/src/app.html:13-73` | Keine Security-Header/CSP/HSTS; Drittanbieter-Skripte ohne SRI, teils vor Consent |
| S-19 | Mittel | CWE-613 | `backend/auth_svc/src/auth/v1/endpoints/session.py:80-141`, `.../core/config.py:13-19` | Access-JWT bis 7 Tage gültig, keine Revocation, Logout invalidiert JWT nicht, `stay_logged_in`-Cookie clientseitig steuerbar |
| S-20 | Mittel | CWE-915 | `backend/auth_svc/src/auth/models/user.py:21-22`, `backend/business/src/business_api/worker.py:31-71` | Mass Assignment: `UserIn` mit `extra="allow"` → beliebige Felder im Business-User-Datensatz |
| S-21 | Mittel | CWE-200, CWE-770 | `backend/items_svc/src/jtl/v1/endpoints/items.py:174-209`, `backend/items_svc/src/jtl/schemas/item_schemas.py:87,191-207` | Items-API: Einkaufspreise/Lieferantendaten öffentlich, unbegrenzte `page_size`, Regex-/Operator-Filter vom Client |
| S-22 | Mittel | CWE-250, CWE-1104 | `cms/strapi_cms/Dockerfile:21,41`, `frontend/Dockerfile:1`, `compose/services*/traefik.yml:14`, `docker-stack.yml:661`, Backend-Dockerfiles | Container-Härtung: EOL-Node, `latest`-Images, docker.sock in Traefik, kein `USER` in Backend-Images, `--forwarded-allow-ips *` |
| S-23 | Mittel | CWE-1395 | `frontend/package-lock.json`, `cms/strapi_cms/package-lock.json`, `backend/*/uv.lock` | Abhängigkeiten mit bekannten CVEs (h11, starlette, urllib3, requests, jinja2, koa, axios, undici, tar-fs, vite …); Strapi-Lockfile inkonsistent |
| S-24 | Mittel | – (DSGVO) | `frontend/src/app.html:29,46-73`, `frontend/src/lib/utils/tracking.ts:291-308`, `frontend/src/lib/tracking-params.ts:152-159` | Chat-Widget (Userlike) & Trust-Widgets ohne Consent; gehashte PII im dataLayer; Consent-Check auf nicht existierendes Cookie |
| S-25 | Mittel | CWE-1357 | `backend/*/pyproject.toml` (`svc-helper` von `gitlab.com/vitandi1`), `.gitea/workflows/frontend_check.yml:17` | Sicherheitskritische Shared-Library (JWT-Prüfung, Mongo, MQ) aus dem Repo eines Ex-Entwicklers (öffentlich, außerhalb eurer Kontrolle); `curl | bash` in CI |
| S-26 | Niedrig | CWE-200 | alle FastAPI-Apps (`/docs`, `/openapi.json`), `backend/auth_svc/src/auth/v1/endpoints/demo.py`, `backend/business/src/business_api/main.py:39-49` | OpenAPI-Doku, Demo-Login-Seite und Debug-Endpunkte öffentlich |
| S-27 | Niedrig | CWE-204, CWE-79 | `backend/auth_svc/src/auth/v1/endpoints/account.py:52-66` | Konto-Enumeration bei Registrierung; Token-Reflexion in `/activate` |
| S-28 | Niedrig | CWE-321 | `traefik/data/*.key` (seit `88c8ccf`, 29.12.2024) | Private TLS-Schlüssel (self-signed, localhost) im Repo |
| S-29 | Niedrig | CWE-400 | `backend/business/src/business_api/v1/endpoints/sitemap.py:242-247` | Unauthentifiziertes Cache-Invalidieren der Sitemap (DoS-Verstärker) |

---

## 4. Secrets-Inventar (Aufgabe 1)

Durchsucht wurden der aktuelle Stand und alle 365 Commits aller Branches (Prefix-Muster `sk_live/sk_test/pk_/rk_/whsec_`, `-----BEGIN … PRIVATE KEY-----`, JWT-, AWS-, GitHub-, Slack-, Google-Muster sowie Zuweisungen auf `*PASS*|*SECRET*|*TOKEN*|*KEY*|*SALT*`). **Keine** Stripe-Live-Keys (`sk_live_`/`rk_live_`) gefunden. Keine `.env` außer `cms/strapi_cms/.env` (per `!cms/strapi_cms/.env` in `.gitignore` **bewusst** eingecheckt). Keine SSH-Schlüssel.

| # | Typ | Variable | Datei:Zeile | seit Commit | Live / Test | Wert (maskiert) | Rotation |
|---|---|---|---|---|---|---|---|
| 1 | JTL-Wawi-REST-API-Token | `JTL__API_TOKEN` | `Readme.md:18` | `7130753` (05.03.2026) | **Live** (Prod-Host, App-ID `icsWebshop/v1`) | `Wawi…` | **Sofort** – voller ERP-Zugriff |
| 2 | Custom-API-Token (JTL-DB-Zugriff) | `CUSTOM_API__API_TOKEN` | `Readme.md:22` | `7130753` | **Live** | `15e1…` | **Sofort** |
| 3 | SMTP-Passwort Shop-Postfach | `MAIL_SMTP_PASSWORD` | `Readme.md:30` | `7130753` | **Live** (Host `k69g…`, Port 465) | `rcaT…` | **Sofort** – Phishing im Namen des Shops möglich |
| 4 | Stripe Secret Key | `STRIPE_SECRET_KEY` | `Readme.md:24` | `7130753` | Test (`sk_test_`) | `sk_t…` | Ja (rollen); prüfen, welcher Key produktiv aktiv ist |
| 5 | Stripe Webhook Signing Secret | `STRIPE_SIGNING_SECRET` | `Readme.md:25` | `7130753` | vermutlich Test-Endpoint | `whse…` | Ja |
| 6 | Stripe Publishable Key | `VITE_STRIPE_PUBLISHABLE_KEY` | `Readme.md:26` | `7130753` | Test (`pk_test_`) | `pk_t…` | Nein (öffentlich), aber Hinweis auf Testmodus |
| 7 | Strapi APP_KEYS (Session-Signatur) | `APP_KEYS` | `cms/strapi_cms/.env:7`; Default in `compose/services_deploy/frontend/cms.yml:25`, `compose/services/frontend/cms.yml:27` | `38b280c` (01.01.2025) | vermutlich **Live** (als Fallback-Default in Deploy-Compose) | `BmF1…` | **Sofort** |
| 8 | Strapi API-Token-Salt | `API_TOKEN_SALT` | `cms/strapi_cms/.env:8`; `compose/services_deploy/frontend/cms.yml:26` | `38b280c` | vermutlich Live | `w4Pp…` | Ja (danach API-Tokens neu erzeugen) |
| 9 | Strapi Admin-JWT-Secret | `ADMIN_JWT_SECRET` | `cms/strapi_cms/.env:9`; `docker-stack.yml:570`; `compose/services_deploy/frontend/cms.yml:24` | `38b280c` | vermutlich **Live** – erlaubt Fälschung von Admin-Sessions | `o+Df…` | **Sofort** |
| 10 | Strapi Transfer-Token-Salt | `TRANSFER_TOKEN_SALT` | `cms/strapi_cms/.env:10`; `compose/services_deploy/frontend/cms.yml:27` | `38b280c` | vermutlich Live | `8RSY…` | Ja |
| 11 | Strapi Users-Permissions-JWT | `JWT_SECRET` | `cms/strapi_cms/.env:21`; `docker-stack.yml:569`; `compose/services_deploy/frontend/cms.yml:23` | `38b280c` | vermutlich Live | `FabD…` | **Sofort** |
| 12 | Strapi-DB-Passwort (MariaDB) | `DATABASE_PASSWORD`, `MYSQL_ROOT_PASSWORD`, `MYSQL_PASSWORD` | `cms/strapi_cms/.env:18`; `docker-stack.yml:567,605-606`; `compose/*/frontend/cms.yml` | `38b280c` | Default `pw12…`, Live falls nicht überschrieben | `pw12…` | Ja |
| 13 | Strapi-Admin-Login | (Doku) | `Readme.md:141`; `compose/services_deploy/frontend/cms.yml:65-66`; `compose/services/frontend/cms.yml:66-67` | `38b280c` | Dev-Default, **zu verifizieren** ob in Prod gesetzt | `admi…` / `Pw12…` | Ja |
| 14 | JWT-Signaturschlüssel Shop-Login | `SECURITY__KEY` | `compose/services_deploy/backend/auth.yml:29`; `compose/services/backend/auth.yml:30`; `compose/services/backend/business_api.yml:38` | `15a53c6` (17.03.2025) | **zu verifizieren** (in Deploy-Compose hart gesetzt) | `foob…` | **Sofort** |
| 15 | JWT-Default im Code | `Security.key` | `backend/auth_svc/src/auth/core/config.py:12`; `backend/business/src/business_api/core/config.py:12` | Initial | greift, wenn Env fehlt (z. B. `docker-stack.yml` setzt keinen Key) | `abc1…` | Default entfernen (Fail-closed) |
| 16 | Blowfish/XTEA-Schlüssel Alt-JTL-Shop | `blowfish_key` | `backend/auth_svc/src/auth/migrate_jtl_data.py:313` | `82aea72` (19.05.2026) | **Live** (entschlüsselt Kundendaten des Alt-Shops) | `16e9…` | Alt-Shop-DB/Dump sichern bzw. löschen; Schlüssel aus Repo |
| 17 | RabbitMQ-Passwort | `MESSAGE_QUEUE__PASSWORD`, `RABBITMQ_DEFAULT_PASS` | `compose/services_deploy/backend/auth.yml:31`; `compose/services_deploy/backend/business_api.yml:75`; `compose/services/backend/message_queue.yml:15`; `docker-stack.yml:369` | `15a53c6` | Default `pw12…`, hart gesetzt für auth/business | `pw12…` | Ja |
| 18 | MongoDB-Root-Passwörter (auth, business, items, payment) | `*__MONGO__USER_PW`, `MONGO_PASSWORD` | `docker-stack.yml:254,347,476,541`; `compose/services*/backend/*.yml`; `backend/*/core/config.py` (z. B. `backend/auth_svc/src/auth/core/config.py:31`) | Initial | Default `pw12…` | `pw12…` | Ja |
| 19 | Fehlerhafte Zuweisung | `MONGO_PASSWORD=${PAYMENT__MONGO__USER_NAME:-pw12…}` | `compose/services_deploy/backend/payment_svc.yml:31`, `compose/services/backend/payment_svc.yml:32` | – | Bug: Passwort = Benutzername | – | Korrigieren |
| 20 | Strapi-DB-Dump mit Admin-Bcrypt-Hashes, Full-Access-API-Token-Hash, Transfer-Token-Hash, Admin-E-Mails | – | Historie: `strapi-current.sql` (`f8131dc` 08.10.2025 hinzugefügt, `443f8d9` gelöscht), `cms/db/init/db_dump.sql` (`d410386`), `cms/db/init/strapi.sql` (`4167a31`/`036a2ef`) | 02.01.2025–14.10.2025 | Dev-Stand; **zu verifizieren**, ob Prod-DB aus Dump initialisiert wurde | `$2…` (bcrypt) | Admin-Passwörter & Tokens neu setzen |
| 21 | TLS-Private-Keys | – | `traefik/data/cert.key`, `cert_cms.key`, `cert_traefik.key` | `88c8ccf` (29.12.2024) | Test (self-signed `CN=localhost`) | `----…` | Nein (nur Dev), aus Repo entfernen |

Weitere Platzhalter ohne Geheimnischarakter: `api_token = "Wawi <UUID_TOKEN>"` (`backend/business/src/business_api/core/config.py:38`), `CUSTOM_API` Default `abc` (`:44`), `.env.example`.

**Wichtig:** Die Repository-Kopien liegen mindestens auf Gitea (`gitea.sustainable-solutions.it`), bei allen Entwicklern und in Build-Caches. Ein reines Löschen im HEAD reicht nicht – **erst rotieren, dann Historie bereinigen** (`git filter-repo --replace-text`), danach alle Clones neu ziehen lassen.

---

## 5. Details je Befund

### S-01 – IDOR: Bestellungen, Kundendaten und Bestell-PDFs ohne Anmeldung abrufbar (Kritisch)

**Fundstellen**
- `backend/business/src/business_api/v1/endpoints/order.py:102-107` – `GET /order/single_order?number=…` ohne jede Authentifizierung.
- `backend/business/src/business_api/v1/endpoints/order.py:88-99` – `GET /order/?user_id=…`: Der Wert aus dem Access-Token wird nur verwendet, wenn er numerisch ist (`_user_id.isnumeric()`); das JWT-`sub` ist jedoch die **E-Mail-Adresse** (`backend/auth_svc/src/auth/utils/crypto.py:52`) → es wird **immer** der vom Client übergebene `user_id`-Parameter verwendet.
- `backend/business/src/business_api/v1/endpoints/mail_data.py:8-32` – `/maildata/order/{nr}`, `/maildata/order_pdf/{nr}`, `/maildata/shipping/{nr}`, `/maildata/cancel/{nr}`, `/maildata/payed/{nr}` öffentlich.
- `backend/business/src/business_api/utils/order.py:36,194-197` – Bestellnummern fortlaufend ab 20 000 → trivial enumerierbar; ERP-Kundennummern ebenfalls numerisch.
- `frontend/src/routes/(store)/order-lookup/+page.svelte:60-95` – „Bestellung finden“: Server liefert die komplette Bestellung, der **Abgleich der E-Mail erfolgt nur im Browser**.
- Routing: `docker-stack.yml:304` bzw. `compose/services_deploy/backend/business_api.yml:42` – `PathPrefix(/api)` leitet alle Business-Endpunkte nach außen.

**Beschreibung / Auswirkung:** Ein anonymer Angreifer kann durch Hochzählen der Bestellnummer bzw. Kundennummer den gesamten Bestellbestand abrufen: Rechnungs- und Lieferadresse, E-Mail, Telefon, Firma, Positionen, Preise, Zahlungsstatus sowie die Auftragsbestätigung als PDF. Bei einem Anbieter von Schließanlagen ist zusätzlich die Information „wer hat welches Schließsystem an welcher Adresse“ sicherheitsrelevant (Einbruchsplanung). Dies ist eine meldepflichtige Datenschutzverletzung (Art. 33 DSGVO), sofern ausgenutzt, und ein Verstoß gegen Art. 32 DSGVO.

**Empfehlung**
- Autorisierung serverseitig: `user_id` **ausschließlich** aus dem verifizierten Token ableiten (z. B. JWT-`sub` → Business-User → `erp_id`) und gegen `CustomerId/_shop_user_id` der Bestellung prüfen.
- Gast-Lookup als `POST /order/lookup {number, email}` mit serverseitigem Vergleich, generischer Fehlermeldung und Rate-Limit (z. B. 5/min/IP); besser zusätzlich ein nicht ratbarer Lookup-Token aus der Bestellbestätigungs-Mail.
- `/maildata/*` nur intern (eigenes Netz, Service-Token).
- Für die Erfolgs-/Zahlungsseite (`frontend/src/routes/(store)/shop/checkout/success/+page.svelte:28`, `.../payment/[orderId]/+page.svelte:57,133`) ein kurzlebiges, an die Session gebundenes Order-Token verwenden.

---

### S-02 – Öffentlicher Endpunkt markiert Bestellungen als bezahlt (Kritisch)

**Fundstellen:** `backend/business/src/business_api/v1/endpoints/order.py:110-138` (`PATCH /order/{order_id}/mark_paid`), `:153-163` (`GET /order/send_order_to_system/{order_id}`); `backend/business/src/business_api/utils/order.py:566-583,602-623`.

**Beschreibung:** Der Endpunkt ist für den internen Aufruf durch den Payment-Service gedacht (`backend/payment_svc/src/payment/utils/webhook.py:95`), ist aber über `/api/order/…/mark_paid` ohne Authentifizierung öffentlich. Ein Body `{"intent_id":"x"}` (Default-Status `paid`) setzt `StillToPay=0`, `PaymentStatus=1` und überträgt die Bestellung inkl. Zahlungseingang „Stripe“ mit beliebiger Transaktions-ID an JTL (`send_payed_to_erp`). Die `order_id` (UUID) wird u. a. über S-01 bzw. die Antwort von `POST /order` geliefert.

**Auswirkung:** Warenbezug ohne Bezahlung; Manipulation der Buchhaltung in JTL; über `send_order_to_system` zusätzlich Auslösen von ERP-Übertragungen und Bestellmails für fremde Bestellungen.

**Empfehlung:** Endpunkt aus dem öffentlichen Routing entfernen; Service-zu-Service-Authentifizierung (mTLS oder signiertes Service-Token); Statusübergang „bezahlt“ nur, wenn der zugehörige PaymentIntent bei Stripe (`stripe.PaymentIntent.retrieve`) `succeeded` ist und `amount`/`currency` exakt dem Bestellbetrag entsprechen.

---

### S-03 – PDF-Service öffentlich: Server-Side Template Injection (RCE) und SSRF (Kritisch)

**Fundstellen:** `backend/pdf_svc/src/pdf/v1/endpoints/pdf.py:14-88`; `backend/pdf_svc/src/pdf/utils/template_handler.py:57-74` (`Template(template_string)` – nicht sandboxed); Routing `docker-stack.yml:655`, `compose/services_deploy/backend/pdf_svc.yml:34`, `compose/persistence_files/dev_reverse_proxy/default.conf:53-62`.

**Beschreibung:**
1. `POST /api/pdf/v1/generate/template_string` rendert einen **vom Client gelieferten** Jinja2-Template-String mit `jinja2.Template`. Ohne `SandboxedEnvironment` ist über bekannte Objekt-Traversals Codeausführung im Container möglich (CWE-1336). Zusätzlich ist Jinja2 im PDF-Service in Version 3.1.5 (CVE-2025-27516, Sandbox-Ausbruch via `|attr`) – selbst eine Sandbox wäre damit unzureichend.
2. `POST /api/pdf/v1/generate/html` / `markdown` übergibt beliebiges HTML an WeasyPrint. WeasyPrint lädt referenzierte Ressourcen (`<img>`, `<link>`, `@import`, Attachments) – inkl. `file://` und interner HTTP-Ziele (`http://business_api:11000/…`, `http://auth_mongo…`, Cloud-Metadata) – und bettet sie in das PDF ein (SSRF, lokales Datei-Lesen, CWE-918/CWE-73).
3. Endpunkte sind zudem ein CPU-DoS-Vektor (PDF-Rendering ohne Limits).

**Auswirkung:** Übernahme des pdf_svc-Containers; von dort Zugriff auf `web`- und `business_api`-Netz (MongoDB `business_mongo`, Valkey, RabbitMQ – Default-Credentials), Lesen der per `env_file: .env` injizierten **gesamten Produktions-.env** (alle Services erhalten in `docker-stack.yml` dieselbe `.env` → JTL-Token, Stripe-Secret, SMTP …).

**Empfehlung:** Router `pdf_svc` aus Traefik entfernen (`traefik.enable=false`); nur aus dem internen Netz erreichbar machen; `template_string`-Endpunkt löschen; `SandboxedEnvironment` + Jinja2 ≥ 3.1.6; WeasyPrint mit eigenem `url_fetcher`, der nur `data:` und eine Allowlist erlaubt; Request-Größen- und Zeitlimits; `.env` pro Service minimal halten.

---

### S-04 – Produktive Zugangsdaten im Repository und in der Git-Historie (Kritisch)

Siehe vollständiges Inventar in Abschnitt 4.

**Beschreibung:** `Readme.md:15-35` enthält einen kompletten Produktions-`.env`-Block (JTL-Wawi-Token, Custom-API-Token, SMTP-Zugang des Shop-Postfachs, Stripe-Keys). `cms/strapi_cms/.env` ist per Negativ-Regel in `.gitignore:16` explizit eingecheckt, und dieselben Strapi-Secrets stehen als **Fallback-Defaults** in `docker-stack.yml:569-570` und `compose/services_deploy/frontend/cms.yml:23-27` – fehlt die Variable in der Prod-`.env`, laufen die öffentlich bekannten Werte. Der XTEA/Blowfish-Schlüssel des Alt-Shops (`backend/auth_svc/src/auth/migrate_jtl_data.py:313`) entschlüsselt Kundendaten des Alt-Systems. Zusätzlich lag zwischen Okt. 2025 und Okt. 2025 ein vollständiger Strapi-Dump (`strapi-current.sql`, ~790 KB) mit Admin-Bcrypt-Hashes und Token-Hashes im Repo.

**Auswirkung:** Jeder mit Lesezugriff auf das Repo (Entwickler, Dienstleister, kompromittierter Gitea/Laptop) hat Vollzugriff auf ERP (Kunden, Aufträge, Preise), das Mail-Konto (Phishing mit echter Absenderadresse, Lesen von Kundenkorrespondenz) und kann Strapi-Admin-Sessions fälschen. Der JTL-Token wird zudem über HTTP übertragen (S-13).

**Empfehlung:** Rotation gemäß Abschnitt 2; Secrets ausschließlich über Docker-Swarm-Secrets oder einen Secret-Store (Vault, SOPS/age); **keine Default-Werte** für Secrets in Compose-Dateien (`${VAR:?missing}` statt `${VAR:-default}`); `Readme.md` bereinigen; `!cms/strapi_cms/.env` aus `.gitignore` entfernen; gitleaks als pre-commit-Hook (`.pre-commit-config.yaml` existiert bereits) und als CI-Job; Historie bereinigen.

---

### S-05 – Schwacher/hartcodierter JWT-Signaturschlüssel (Kritisch, zu verifizieren)

**Fundstellen:** `compose/services_deploy/backend/auth.yml:29` und `compose/services/backend/auth.yml:30` (`SECURITY__KEY=foob…`), `compose/services/backend/business_api.yml:38`; Defaults `backend/auth_svc/src/auth/core/config.py:12`, `backend/business/src/business_api/core/config.py:12` (`abc1…`); `docker-stack.yml:424-461` setzt keinen Key (→ `.env` oder Default). Algorithmus HS256 (`backend/auth_svc/src/auth/utils/crypto.py:44`).

**Beschreibung:** Das Access-JWT enthält nur `sub` (= E-Mail), `iss`, `aud`, Zeitstempel. Issuer `Meesenburg` und Audience `["Meesenburg","ICS"]` stehen im Code. Mit bekanntem Schlüssel kann jeder ein gültiges Token für **jede** E-Mail-Adresse erzeugen. Über `POST /api/auth/v1/login/with-jwt` (`backend/auth_svc/src/auth/v1/endpoints/session.py:36-55`) wird ein solches Token sogar in eine vollwertige Session mit Refresh-Token umgewandelt. Auch `PUT /api/auth/v1/update` (Name/Adresse ändern) akzeptiert es.

**Auswirkung:** Vollständige Kontoübernahme beliebiger Kunden (insb. B2B-Konten mit Sonderkonditionen), Zugriff auf Profil, Adressen, Bestellhistorie.

**Empfehlung:** Zufälligen Schlüssel ≥ 32 Byte (oder asymmetrisch RS256/EdDSA, damit Business-Services nur den Public Key kennen); Service soll ohne gesetzten Schlüssel **nicht starten**; `jti` + Revocation-Liste; `/login/with-jwt` entfernen.

---

### S-06 – Preis- und Bestellmanipulation im Checkout (Kritisch)

**Fundstellen**
- Versandkosten vom Client: `backend/business/src/business_api/schemas/orders_schemas.py:41` (`shipping_method: ShippingMehod` aus dem Request) → `backend/business/src/business_api/utils/order.py:393-411` verwendet `checkout_data.shipping_method.gross_price` und `free_shipping_at` ungeprüft.
- Negative Mengen: `backend/business/src/business_api/schemas/orders_schemas.py:67-69` (`quantity: int` ohne `ge=1`) → `backend/business/src/business_api/utils/items.py:95-115` multipliziert ungeprüft; Gesamtsumme sinkt.
- Überschreiben bestehender Bestellungen: `backend/business/src/business_api/v1/endpoints/order.py:64-75` lädt die Bestellung zur client-gelieferten `order_id` und ersetzt Positionen, Adressen, Beträge **ohne Eigentumsprüfung**; `_in_erp_created` etc. werden zurückgesetzt (`utils/order.py:510-513`) → Doppelübertragung an JTL.
- Intent-Wiederverwendung: `backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33` gibt einen vorhandenen Intent zur `order_id` zurück, **ohne den Betrag neu zu prüfen**.

**Angriffsszenario (logisch abgeleitet, nicht ausgeführt):** Bestellung mit günstigem Artikel anlegen → PaymentIntent erzeugen (kleiner Betrag) → `POST /order` mit gleicher `order_id` und teuren Artikeln → kleinen Intent bezahlen → Webhook ruft `mark_paid` → teure Bestellung wird als bezahlt an JTL übertragen. Alternativ: `gross_price: 0` für Spedition oder Positionen mit Menge −1.

**Auswirkung:** Direkter finanzieller Schaden; Integrität der ERP-Daten.

**Empfehlung:** Versandmethode nur per ID akzeptieren, Preis/Freigrenze serverseitig aus Stammdaten; `quantity: conint(ge=1, le=…)`; bestehende Bestellungen nur durch den Eigentümer und nur im Status „pending, kein Intent“ änderbar – nach Intent-Erstellung Bestellung einfrieren oder Intent bei Änderung via `PaymentIntent.modify` neu bepreisen; im Webhook `amount_received == order.total` prüfen; Idempotenzschlüssel (`idempotency_key=order_id`) bei `PaymentIntent.create`.

---

### S-07 – Infrastruktur-Dienste ohne/mit Default-Authentifizierung auf Host-Ports (Kritisch, zu verifizieren)

**Fundstellen**
- `docker-compose_deploy.yml:8-10` (laut `Readme.md:5-9` der Produktionsstart) inkludiert **`compose/services/*`** (die Dev-Variante!), inklusive `compose/services/backend/db_viewer.yml` (mongo-express mit `ME_CONFIG_BASICAUTH=false` auf 11022–11025, Redis-Commander 11021) und `--reload`-Uvicorn.
- Mailpit: `compose/services*/backend/mail_svc.yml:41-43`, `docker-stack.yml:664-666` – Web-UI 8025 und SMTP 1025 veröffentlicht, ohne Auth.
- RabbitMQ Management 11010 und AMQP 11012: `compose/services*/backend/message_queue.yml:6-9`; Credentials `user`/`pw12…`, in `compose/services_deploy/backend/auth.yml:31` und `business_api.yml:75` hart verdrahtet.
- Valkey 11020: `compose/services*/backend/cache.yml:6-7`; `backend/valkey/valkey.conf` ist leer → kein `requirepass`.
- Traefik: `traefik/traefik.yml:1-3` `api.insecure: true` (Dashboard/API ohne Auth auf Port 8080 des Containers); in `compose/services/traefik.yml:21-25` zusätzlich ein Router `traefik.<DOMAIN>` ohne Auth-Middleware.
- Frontend-Container direkt auf 10002 (`compose/services/frontend/main.yml:10`, `compose/services_deploy/frontend/main.yml:10`) – umgeht Traefik inkl. der Middleware `auth`.

**Auswirkung:** Mailpit zeigt sämtliche über den Mail-Service versendeten Nachrichten (falls produktiv `MAIL_SMTP_HOST` nicht gesetzt ist, landen **alle** Kundenmails inkl. Passwort-Reset-Links und Sicherungskarten dort); RabbitMQ erlaubt Mitlesen/Einschleusen von Nachrichten (`send_mail` → Spam/Phishing über das Shop-Postfach; `user_registered` enthält Klartext-Passwörter, S-14); Valkey ohne Passwort ermöglicht über `CONFIG SET`/`MODULE LOAD` typischerweise Codeausführung; mongo-express gibt Lese-/Schreibzugriff auf alle Datenbanken (Benutzer, Bestellungen, Payment-Intents). Docker-veröffentlichte Ports umgehen in der Regel Host-Firewalls wie `ufw`.

**Empfehlung:** Ports nicht publizieren (oder `127.0.0.1:` binden); `db_viewer.yml` und Mailpit aus jedem Produktions-Deploy entfernen; `docker-compose_deploy.yml` auf `services_deploy` umstellen oder löschen (nur ein maßgeblicher Deploy-Pfad); RabbitMQ/Valkey/Mongo mit starken, per Secret gesetzten Passwörtern und Valkey-ACLs; Traefik `api.insecure: false`, Dashboard nur mit BasicAuth/IP-Allowlist. Von außen mit Portscan verifizieren.

---

### S-08 – Unsicherer Passwort-Reset- und Token-Flow (Hoch)

**Fundstellen:** `backend/auth_svc/src/auth/v1/endpoints/account.py:82-96`, `backend/auth_svc/src/auth/utils/auth.py:205-237`, `:297-368`, `backend/auth_svc/src/auth/core/data_stores.py:69-73`, `backend/auth_svc/src/auth/v1/endpoints/session.py:36-77`, `backend/auth_svc/src/auth/core/config.py:20-21,25`.

**Einzelprobleme**
1. `reset-password` sucht den Benutzer per `tokens.token == request.token` – **jeder** gespeicherte Token passt, auch der Refresh-Token; der Token-Typ (`ONE_TIME`) und `expires_at` werden **nicht geprüft**.
2. `expires_at=settings.security.password_reset_ttl` (Integer 3600) wird von Pydantic als Unix-Zeitstempel 1970 interpretiert – die Ablaufzeit ist damit ohnehin wertlos; der Token gilt bis zur Benutzung.
3. Der Reset-Token ist ein reguläres, mit dem Login-Schlüssel signiertes Access-JWT. Wer den Link (Mail, Mailpit, Logs, Referrer) sieht, kann sich über `/login/with-jwt` oder `/login/with-token` **ohne Passwortänderung** anmelden; der Token wird dabei nicht verbraucht.
4. `forgot_password` ersetzt `user.tokens` vollständig → jeder Anonyme kann durch „Passwort vergessen“ alle Sessions eines Kunden beenden (DoS).
5. `get_user_by_token` wirft bei unbekanntem Token `UserNotFoundError` → unbehandelt (HTTP 500), der `MsgFailed`-Zweig ist toter Code.
6. Registrierung ohne E-Mail-Bestätigung (`auto_login_after_registration = True`): Konten mit fremden E-Mail-Adressen möglich; der Business-Service verknüpft Gast-Bestellungen per E-Mail (`backend/business/src/business_api/utils/user.py:32-37,110-111`).
7. `/activate` aktiviert mit jedem Token und reflektiert den Token in der Antwort.

**Empfehlung:** Reset-Token = 32 Byte Zufall, nur Hash speichern, Typ `password_reset`, Ablauf 15–60 min als `datetime`, einmalig; nach Reset alle Sessions invalidieren und Benachrichtigungsmail; `/login/with-jwt` und `/login/with-token` entfernen; Double-Opt-In bei Registrierung; eigene Tokens nicht per Mongo-Volltextfeld, sondern pro Typ suchen.

---

### S-09 – IDOR auf Schließanlagen-Konfigurationen und Warenkörbe (Hoch)

**Fundstellen:** `backend/business/src/business_api/v1/endpoints/configurator.py:15-64`; `backend/business/src/business_api/v1/endpoints/cart.py:12-62`; Schema `backend/business/src/business_api/schemas/configurator.py:30-64` (Zylinder, Schlüssel, Schließberechtigungen, Adresse).

**Beschreibung:** `GET /api/configurator/?user_id=<int>` liefert alle gespeicherten Konfigurationen eines Kunden; `POST /api/configurator/` mit fremder `config_id` überschreibt diese (inkl. `user_id`). `GET/POST/DELETE /api/cart/{user_id}` liest, ersetzt oder löscht Warenkörbe beliebiger Kunden. Keine Authentifizierung.

**Auswirkung:** Schließpläne (welcher Schlüssel schließt welche Tür, Sicherheitsstufe, Adresse) sind hochsensibel; Manipulation einer gespeicherten Konfiguration vor der Bestellung kann zu falsch gefertigten Schließanlagen führen.

**Empfehlung:** Authentifizierung erzwingen und `user_id` aus dem Token ableiten; `config_id` als nicht ratbarer Wert für Gäste + Bindung an Session; Schreibzugriff nur für Eigentümer.

---

### S-10 – Kein Brute-Force-Schutz und kein Rate-Limiting (Hoch)

**Fundstellen:** `backend/auth_svc/src/auth/utils/auth.py:36-66,127-130` (Fehlversuche werden in `logins_failed` gezählt, aber nie ausgewertet; zusätzlich wird für unbekannte Benutzer ein Fallback-User `not_a_user` in die DB geschrieben); keine Rate-Limit-Middleware in Traefik oder FastAPI. Das Frontend erwartet bereits HTTP 429 (`order-lookup/+page.svelte:75-77`), das Backend liefert es nie.

**Auswirkung:** Credential-Stuffing/Brute-Force auf Kundenkonten, Enumeration von Bestellungen, Massenversand über Kontakt- und Upload-Formular (Mail-Bombing des Postfachs `sicherheit@…`), Ressourcen-Erschöpfung.

**Empfehlung:** Traefik `RateLimit`/`InFlightReq`-Middleware je Route; progressive Verzögerung bzw. temporäre Sperre pro Konto und IP (ASVS V2.2.1); CAPTCHA/Proof-of-Work für Kontakt/Upload; Fallback-User nicht persistieren.

---

### S-11 – Strapi CMS: Default-Credentials, Dumps in der Historie, Development-Modus (Hoch)

**Fundstellen:** `Readme.md:140-141` und `compose/services_deploy/frontend/cms.yml:65-66` (Admin `admi…` / `Pw12…`); Git-Historie `strapi-current.sql` (`f8131dc`, 08.10.2025; Tabellen `admin_users` mit Bcrypt-Hashes zweier Admins inkl. externer Mitarbeiter-E-Mail, `strapi_api_tokens` „test“/full-access, `strapi_transfer_tokens`), `cms/db/init/db_dump.sql`, `cms/db/init/strapi.sql`; `cms/strapi_cms/Dockerfile:41` (`CMD ["npm","run","develop","--debug"]`, von `compose/services*/frontend/cms.yml` genutzt); `cms/strapi_cms/config/middlewares.ts` (Default-CORS, `strapi::poweredBy`); Plugin `strapi-v5-plugin-populate-deep` (`cms/strapi_cms/package.json:18`); Admin-Panel unter `cms.<DOMAIN>` öffentlich (`docker-stack.yml:587`).

**Auswirkung:** Wurde die Prod-DB aus einem Dump initialisiert oder das Default-Passwort nie geändert, ist Admin-Login für jeden möglich → Stored XSS im Shop (S-17), Manipulation von Impressum/AGB/Preisen-Texten, Upload beliebiger Dateien. Der Development-Modus aktiviert den Content-Type-Builder (Schemaänderungen über die Oberfläche → Dateischreibzugriff) und den Vite-Dev-Server. `populate-deep` kann Relationen (z. B. `createdBy`-Admin-Daten) über die Public-API offenlegen.

**Empfehlung:** Admin-Passwörter und Tokens neu setzen, unbekannte Admin-Accounts entfernen, 2FA/SSO (Enterprise) oder Admin-Panel per IP-Allowlist/BasicAuth an Traefik; `Dockerfile.prod` (`npm run start`) verwenden; CORS auf Shop-Domain beschränken; Public-Role-Berechtigungen und `populate-deep`-Ausgaben prüfen.

---

### S-12 – Unkontrollierter Datei-Upload (Upload-Formular, Bestellanhänge) (Hoch)

**Fundstellen:** `backend/business/src/business_api/v1/endpoints/upload_security_card.py:17-53`; `backend/mail_svc/src/mail/utils/mail_templates.py:130-148` (Empfänger fest `sich…@meesenburg.com`); `backend/mail_svc/src/mail/utils/mail.py:21-25` (jede Datei als `application/pdf` deklariert); `backend/business/src/business_api/v1/endpoints/order.py:47,77-78` + `backend/business/src/business_api/utils/file.py:45-82`; Frontend-Allowlist nur clientseitig (`frontend/src/routes/(store)/upload-formular/+page.svelte:202`).

**Beschreibung:** Anonym, ohne Größen-, Anzahl- oder Typprüfung (kein Magic-Byte-Check, keine Virenprüfung). Upload-Dateien werden komplett in den Speicher gelesen, base64 in eine RabbitMQ-Nachricht gepackt und als Mail-Anhang mit irreführendem MIME-Typ intern zugestellt. Bestellanhänge werden unbegrenzt auf Platte geschrieben und an JTL hochgeladen; `original_filename` wird ungeprüft an JTL weitergegeben.

**Auswirkung:** Malware-/Phishing-Zustellung an interne Mitarbeiter (vertrauenswürdiger Absender `contact_form`), Speicher-/Platten-DoS, Mail-Bombing. Sicherungskarten (berechtigen zur Schlüssel-Nachbestellung!) werden unverschlüsselt per E-Mail übertragen.

**Empfehlung:** Max. Größe (z. B. 10 MB) und Anzahl in Traefik (`buffering.maxRequestBodyBytes`) und FastAPI; serverseitige Typprüfung (libmagic) mit Allowlist (PDF/JPG/PNG); echten MIME-Typ setzen; AV-Scan (ClamAV); Captcha/Rate-Limit; Sicherungskarten nicht per Mail, sondern in einem zugriffsgeschützten Ablagesystem.

---

### S-13 – Klartext-HTTP zur JTL-Wawi- und Custom-API (Hoch)

**Fundstellen:** `Readme.md:17` (`JTL__DOMAIN=http://…:50443`), `Readme.md:21` (`CUSTOM_API__DOMAIN=http://…:58080`); `backend/business/src/business_api/jtl_helper/client.py:105-148` (Token im `Authorization`- bzw. `X-token`-Header); analog `backend/items_svc/src/jtl/jtl_helper/client.py`.

**Beschreibung:** Trotz Port „50443“ ist das Schema `http://`. Die Verbindung zum externen Hoster läuft über das Internet. API-Token und sämtliche Kunden-/Auftragsdaten (Adressen, Zahlungen) werden unverschlüsselt übertragen.

**Auswirkung:** Mitschneiden des ERP-Tokens und personenbezogener Daten auf jedem Netzpfad (Art. 32 DSGVO); Manipulation von Antworten (Preise/Bestände) durch MitM.

**Empfehlung:** TLS am JTL-REST-Server aktivieren (JTL-Wawi REST-API unterstützt HTTPS) bzw. Site-to-Site-VPN/WireGuard; Zertifikatsprüfung erzwingen; Custom-API ebenfalls nur via TLS + IP-Allowlist.

---

### S-14 – Personenbezogene Daten und Session-Tokens in Logs bzw. Message-Queue (Hoch)

**Fundstellen**
- `traefik/traefik.yml:38-53`: Access-Log mit `fields.defaultMode: keep` und `headers.defaultMode: keep`; nur `Authorization` wird verworfen (`:52`) → **`Cookie`-Header mit `access_token`/`refresh_token`** und `Stripe-Signature` werden protokolliert; zusätzlich landen vollständige Request-Pfade inkl. Query-String im Log, also auch die Links aus Passwort-Reset- und Aktivierungsmails (`?token=<JWT>`, erzeugt in `backend/auth_svc/src/auth/utils/auth.py:227`) und Lookup-Parameter (`?user_id=`, `?number=`). Gefiltert werden nur Statuscodes (`:54-58`), praktisch also alles.
- `backend/payment_svc/src/payment/main.py:38-141`: Sämtliche Exception-Handler schreiben den **kompletten Request-Body** ins Log (`intent_logger`/`webhook_logger`/`mq_logger`).
- `backend/payment_svc/src/payment/v1/endpoints/webhook.py:44-46`: Jedes Stripe-Event wird mit vollständigem Body geloggt (Stripe-Events enthalten u. a. `billing_details`, E-Mail, Name, ggf. Adresse); `backend/payment_svc/src/payment/utils/webhook.py:94` gibt zusätzlich den Payload per `print()` aus.
- `backend/auth_svc/src/auth/utils/auth.py:294`: `mq_push("user_registered", user.model_dump_json())` – `user` ist ein `UserIn` mit den Feldern `password` und `password_repeat` (`backend/auth_svc/src/auth/models/user.py:21-25`) → das **Klartext-Passwort** jedes neu registrierten Kunden steht in der RabbitMQ-Nachricht. Erst der Konsument entfernt es (`backend/business/src/business_api/worker.py:42-49`).
- Diverse `print()`-Ausgaben mit Bestell- und Kundendaten auf stdout (→ Docker-Logs/Promtail/Loki): `backend/business/src/business_api/v1/endpoints/order.py:39-40,121`, `backend/business/src/business_api/utils/order.py:58,421-426,583` (vollständige JTL-Antwort), `backend/business/src/business_api/jtl_helper/user_handling.py:90`, `backend/business/src/business_api/jtl_helper/worker.py:163`, `backend/mail_svc/src/mail/main.py:109-111`, `backend/mail_svc/src/mail/utils/mail_templates.py:47-49`, `backend/auth_svc/src/auth/migrate_jtl_data.py:139,280-308` (E-Mail-Adressen, ERP-IDs).

**Beschreibung:** Logs werden als weniger schützenswert behandelt als die Datenbanken (Log-Dateien im Traefik-Volume `/etc/traefik/data/`, Weiterleitung an Promtail/Loki über Labels `logging=promtail` in `compose/services/*`). Sie enthalten aber Session-Cookies, gültige Reset-/Aktivierungs-JWTs, Stripe-Event-Daten und Kundendaten. Die RabbitMQ-Nachricht mit Klartext-Passwort ist für jeden mit Zugriff auf RabbitMQ (Management-UI mit Default-Credentials, S-07) bzw. bei persistenten Queues auf dem Datenträger einsehbar; kann der Konsument die Nachricht nicht verarbeiten, verbleibt sie in der Queue.

**Auswirkung:** Wer Lesezugriff auf Logs oder Log-Backend erhält (Administratoren, Dienstleister, kompromittierter Container, offenes Grafana/Loki), kann Sitzungen übernehmen (Session-Hijacking über geloggte Cookies), Passwörter zurücksetzen bzw. sich anmelden (Reset-JWT, vgl. S-08) und Kundendaten massenhaft auslesen. Klartext-Passwörter sind wegen Passwort-Wiederverwendung auch für Fremddienste der Kunden relevant. Verstoß gegen Datenminimierung und Speicherbegrenzung (Art. 5 Abs. 1 lit. c/e, Art. 32 DSGVO).

**Empfehlung**
- Traefik: `headers.defaultMode: drop`, nur benötigte Header (`User-Agent`, `Referer`, `Content-Type`) explizit auf `keep`; `Cookie`, `Set-Cookie`, `Stripe-Signature` mindestens `redact`; Query-Strings nicht loggen bzw. Token-Parameter maskieren; Reset-/Aktivierungs-Tokens künftig nicht im Query-String, sondern im URL-Fragment oder per POST übermitteln.
- Payment-Service: Request-Bodies nicht loggen; nur Event-ID, Event-Typ, Intent-ID und Order-ID protokollieren.
- `user_registered`: explizites Nachrichtenschema ohne Passwortfelder verwenden (`user.model_dump_json(exclude={"password","password_repeat"})` bzw. eigenes Pydantic-Modell); vorhandene Nachrichten/Queues leeren; RabbitMQ-Zugriff absichern (S-07).
- Alle `print()`-Debugausgaben entfernen und durch strukturiertes Logging mit PII-Filter ersetzen (Ruff-Regel `T20` aktivieren).
- Bestehende Logs (Traefik-`access.log`, Loki) als sensibel einstufen, Zugriff beschränken, Aufbewahrungsfrist festlegen; nach der Forensik (Abschnitt 2, P1) Altbestände mit Tokens löschen. Da geloggte Tokens weiterhin gültig sein können: Rotation von `SECURITY__KEY` (S-05) invalidiert sie.

---

### S-15 – Keine Service-zu-Service-Authentifizierung; interne Endpunkte öffentlich geroutet (Hoch)

**Fundstellen**
- `docker-stack.yml:304`: Router `business_api` mit `PathPrefix(/api)` – fängt alle `/api/*`-Pfade ab, die nicht von spezifischeren Routern (`/api/items` `:210`, `/api/auth` `:458`, `/api/payment` `:523`, `/api/pdf` `:655`) bedient werden; damit sind sämtliche Business-Endpunkte inkl. rein interner (`/order/{id}/mark_paid`, `/order/send_order_to_system/…`, `/maildata/*`, `/user/customer_groups`, `/sitemap/refresh`) öffentlich. Analog `compose/services_deploy/backend/business_api.yml:42`.
- `backend/payment_svc/src/payment/utils/webhook.py:95-99`: Payment-Service ruft `PATCH {BUSINESS_API_URL}/order/{id}/mark_paid` per unauthentifiziertem `httpx.patch` auf – kein Token, keine Signatur.
- `backend/items_svc/src/jtl/v1/utils/discount_logic.py:94-104`, `:106-113`: Items-Service ruft `http://business_api:11000/user/` und `/user/customer_groups` ebenfalls ohne Authentifizierung auf.
- Netzwerk: In `docker-stack.yml` hängen `frontend`, `items_svc`, `business_api`, `business_worker`, `auth`, `payment_svc`, `strapi_dev`, `pdf_svc`, `mailpit` und `mail_svc_worker` gemeinsam im externen Netz `web` (`:8-9`, z. B. `:154,183,277,431,496,558,628,668,686`); `pdf_svc` und `mailpit` zusätzlich im Netz `business_api` (`:629,669`), in dem auch `business_mongo`, RabbitMQ und Valkey liegen.

**Beschreibung:** Die Architektur verlässt sich darauf, dass interne Endpunkte „nur intern“ aufgerufen werden, setzt dies aber weder per Routing noch per Authentifizierung durch. Jeder Container im flachen `web`-Netz kann jeden anderen Service direkt erreichen; ein kompromittierter Container (z. B. `pdf_svc`, S-03) erreicht ohne weitere Hürde Datenbanken und Queue des Business-Bereichs.

**Auswirkung:** Ursache bzw. Verstärker von S-01, S-02, S-09 und S-29; fehlende Segmentierung erhöht die Reichweite jeder Einzelkompromittierung (laterale Bewegung).

**Empfehlung**
- Traefik-Routing auf eine explizite **Allowlist** öffentlicher Pfade umstellen (eigene Router je öffentlichem Endpunkt statt `PathPrefix(/api)`); interne Pfade gar nicht routen.
- Interne Endpunkte in einen separaten Router/Port bzw. eine separate FastAPI-App auslagern, die nur im internen Netz lauscht.
- Service-zu-Service-Authentifizierung: signiertes Service-Token (z. B. kurzlebiges JWT mit eigener Audience und eigenem Schlüssel pro Service) oder mTLS; Prüfung als FastAPI-Dependency auf allen internen Routen.
- Netzsegmentierung: `web` nur für Dienste, die Traefik erreichen muss; Datenbank-/Queue-Netze nur für die jeweils berechtigten Services (`pdf_svc` und `mailpit` aus `business_api` entfernen).

---

### S-16 – Stripe-Integration: kein Betragsabgleich, Fehlschläge lösen ERP-Übertragung aus, `client_secret` frei abrufbar (Hoch)

**Fundstellen**
- `backend/payment_svc/src/payment/v1/endpoints/webhook.py:53-76` und `backend/payment_svc/src/payment/utils/webhook.py:63-105`: Der Webhook übernimmt nur Status und Zahlungsart; `amount_received`, `currency` und die Zuordnung Intent → Bestellbetrag werden **nicht** gegen die Bestellung geprüft.
- Auch `payment_intent.payment_failed` und `payment_intent.canceled` führen (sofern eine Zahlungsmethode vorhanden ist) zum Aufruf von `mark_paid` (`utils/webhook.py:88-99`); dort setzt der Business-Service zwar `PaymentStatus=-1`, ruft aber in jedem Fall `send_order_to_systems(order)` auf (`backend/business/src/business_api/v1/endpoints/order.py:120-135`) → die Bestellung wird auch bei fehlgeschlagener Zahlung an JTL übertragen.
- Fehler beim Aufruf von `mark_paid` werden nur per `print()` gemeldet (`utils/webhook.py:102-103`), der Webhook antwortet trotzdem mit Erfolg → Stripe wiederholt nicht, Zahlungen können verloren gehen. Der nachfolgende MQ-Code (`:106 ff.`) ist durch das vorgezogene `return` (`:105`) toter Code.
- `backend/payment_svc/src/payment/v1/endpoints/intents.py:30-33`: `POST /api/payment/intents/` liefert zu einer bekannten `order_id` den bestehenden Intent **inklusive `client_secret`** zurück – ohne Authentifizierung und ohne Betragsprüfung (vgl. S-06).

**Beschreibung:** Die Webhook-Signaturprüfung selbst ist korrekt umgesetzt. Die fachliche Validierung danach fehlt: Es wird weder geprüft, ob der bezahlte Betrag dem aktuellen Bestellwert entspricht, noch ob die Zahlung tatsächlich erfolgreich war, bevor die ERP-Übertragung ausgelöst wird.

**Auswirkung:** In Kombination mit S-06 können Bestellungen mit einem zu niedrigen Betrag als bezahlt in JTL landen; fehlgeschlagene Zahlungen erzeugen Aufträge im ERP (Risiko des Versands unbezahlter Ware, Buchhaltungsfehler). Wer eine Order-ID kennt, erhält den `client_secret` und damit Einblick in Zahlungsstatus und -betrag des Intents bzw. kann Zahlungsversuche auf fremden Intents anstoßen.

**Empfehlung**
- Im Webhook bei `payment_intent.succeeded`: Intent per `stripe.PaymentIntent.retrieve` laden und `status == "succeeded"`, `amount_received == order.total` (in Cent) und `currency == "eur"` prüfen; bei Abweichung Bestellung sperren und Alarm auslösen.
- ERP-Übertragung ausschließlich bei bestätigter Zahlung (bzw. bei Vorkasse durch gesonderten Prozess); `failed`/`canceled` nur als Status speichern.
- Fehler beim internen Aufruf als HTTP 5xx an Stripe zurückgeben (Retry) oder über eine persistente Queue mit Wiederholung entkoppeln; Idempotenz über die Event-ID.
- `client_secret` nur an den authentifizierten Eigentümer bzw. die Checkout-Session ausliefern; Intent bei Bestelländerung neu bepreisen oder neu anlegen.

---

### S-17 – Stored XSS über CMS-Richtext und schwacher eigener Sanitizer (Mittel)

**Fundstellen**
- `frontend/src/lib/utils/strapiRichText.ts:3-17` (`formatRichTextChild`): `child.text` wird **ohne HTML-Escaping** in Tags eingebettet; `:23` gibt String-Inhalte unverändert zurück; `:35-36` setzt `child.url` ungeprüft in `<a href="…">` (auch `javascript:`-Schema, Attribut-Ausbruch über Anführungszeichen möglich).
- Ausgabe per `{@html …}` in zahlreichen Komponenten, u. a. `frontend/src/lib/components/TextSection.svelte:25-78`, `ImageTextSection.svelte:44-97`, `Faq.svelte:58`, `home/ConfiguratorGuide.svelte:42,114`, `PromoBanner.svelte:131`.
- `frontend/src/lib/components/RichTextRenderer.svelte:18-54`: gleiches Muster (`renderTextNode`/`renderParagraph`/`renderList` ohne Escaping, Ausgabe via `{@html}` in `:51,54`).
- `frontend/src/routes/(singlepages)/faq/+page.svelte:99-112` (`formatText`, Kopie der Funktion) mit Ausgabe in `:147,155,167,182`.
- `frontend/src/lib/utils/sanitizeHtml.ts:11-34`: entfernt nur `<script>` und `on*`-Attribute; `javascript:`-URLs, `<iframe>`, `<object>`, `<svg>`/`<math>`-Konstrukte, `srcdoc`, `<form action>` u. Ä. bleiben erhalten. Zudem nutzt die Funktion `document` und ist bei SSR nicht anwendbar.

**Beschreibung:** Inhalte aus Strapi werden als vertrauenswürdiges HTML behandelt. Jeder, der Inhalte im CMS bearbeiten kann (Redakteure, kompromittierte Admin-Konten, vgl. S-11, oder ein gefälschter Admin-JWT über das bekannte `ADMIN_JWT_SECRET`, S-04), kann Skriptcode in Shop-Seiten einbringen, der bei allen Besuchern ausgeführt wird.

**Auswirkung:** Ausführung von Skripten im Kontext der Shop-Domain: Manipulation des Checkouts (z. B. Austausch von Bankverbindung bei Vorkasse, Umleitung auf Phishing-Seiten), Aktionen im Namen eingeloggter Kunden. Die Session-Cookies selbst sind HttpOnly und dadurch nicht direkt auslesbar. Fehlende CSP (S-18) verhindert keine Ausführung.

**Empfehlung:** Text grundsätzlich escapen und Markup aus der Blockstruktur aufbauen (Svelte-Komponenten statt String-Konkatenation, z. B. `@strapi/blocks-react-renderer`-Äquivalent für Svelte); Links nur mit Schema-Allowlist (`https:`, `mailto:`, `tel:`, relative Pfade); wo HTML unvermeidbar ist, eine etablierte Bibliothek (DOMPurify bzw. `isomorphic-dompurify` für SSR) statt des eigenen Sanitizers; doppelte Implementierungen (`faq/+page.svelte`, `RichTextRenderer.svelte`) auf eine zentrale, getestete Funktion zurückführen; CSP einführen (S-18).

---

### S-18 – Keine Security-Header, keine CSP, Drittanbieter-Skripte ohne SRI (Mittel)

**Fundstellen**
- `frontend/web_server/nginx.conf:17-27`: kein einziger Security-Header (`Content-Security-Policy`, `Strict-Transport-Security`, `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`/`frame-ancestors`, `Permissions-Policy`).
- `traefik/traefik.yml`, `traefik/dynamic/tls.yml`: keine `headers`-Middleware; die einzigen Treffer zu `headers` betreffen das Access-Log. Keine Middleware dieser Art in `docker-stack.yml` oder `compose/*`. `frontend/src/hooks.server.ts` setzt ebenfalls keine Security-Header.
- `frontend/src/app.html:13`: Consent-Skript von `consent.cookiefirst.com` ohne `integrity`; `:29` eTrusted-Widget, `:46-50` Userlike-Chat, `:51-71` Trusted-Shops-Widget (protokollrelative URL `//widgets…`), `:73` Händlerbund-Widget – alle ohne Subresource Integrity und (außer GTM `:15`) **ohne Consent-Steuerung**, d. h. sie laden vor jeder Einwilligung (siehe auch S-24).
- `traefik/traefik.yml:17-20`: TLS-Mindestversion auskommentiert (Traefik-Default TLS 1.2 ist akzeptabel, aber nicht explizit festgelegt).

**Beschreibung / Auswirkung:** Ohne CSP gibt es keine zweite Verteidigungslinie gegen XSS (S-17) und kompromittierte Drittanbieter-Skripte (Supply-Chain, Magecart-artige Skimmer im Checkout). Ohne HSTS ist der erste Aufruf per HTTP für SSL-Stripping anfällig; ohne `frame-ancestors` ist Clickjacking des Kundenkontos/Checkouts möglich; ohne `Referrer-Policy` können Token in URLs (Reset-Links, S-08/S-14) an Drittanbieter abfließen.

**Empfehlung:** Traefik-`headers`-Middleware zentral für alle Router: `stsSeconds` ≥ 31536000 inkl. `includeSubdomains`, `contentTypeNosniff`, `referrerPolicy: strict-origin-when-cross-origin` (Reset-Seite: `no-referrer`), `frameDeny`/`frame-ancestors 'none'`, `permissionsPolicy`; CSP zunächst im `Report-Only`-Modus mit Nonces (SvelteKit `kit.csp`) und expliziter Allowlist der benötigten Drittanbieter (Stripe, CookieFirst, GTM, Userlike, Trusted Shops), dann scharf schalten; SRI für versionierte Drittanbieter-Skripte, wo der Anbieter es zulässt; nicht notwendige Widgets erst nach Consent laden; TLS-Mindestversion explizit setzen.

---

### S-19 – Lange JWT-Laufzeit, keine Revocation, Logout invalidiert Access-Token nicht (Mittel)

**Fundstellen:** `backend/auth_svc/src/auth/core/config.py:13-19` (`expires_delta` 15 min × `stay_logged_in_factor` 672 = **7 Tage** Access-JWT-Laufzeit; `refresh_token_expire` 24 h); `backend/auth_svc/src/auth/utils/auth.py:164-190` (`refresh` stellt bei `stay_logged_in` ein 7-Tage-Access-JWT aus); `backend/auth_svc/src/auth/v1/endpoints/session.py:80-113` (`/refresh` liest `stay_logged_in` aus einem **nicht signierten, clientseitigen Cookie** und setzt es neu); `:116-141` (`/logout` entfernt nur den Refresh-Token und löscht die Cookies im Browser).

**Beschreibung:** Das Access-JWT ist zustandslos und wird von den Business-Services ohne Abgleich mit einer Sperrliste akzeptiert. Logout, Passwortänderung oder Kontosperre wirken nicht auf bereits ausgestellte Access-JWTs. Die Wahl der langen Laufzeit liegt beim Client (Cookie-Wert), nicht beim Server.

**Auswirkung:** Ein einmal abgegriffenes Access-JWT (Logs, S-14; geteilter Rechner; XSS-gestützte Aktionen) bleibt bis zu 7 Tage gültig, auch nach Logout und Passwort-Reset.

**Empfehlung:** Access-JWT kurz halten (5–15 min) und „angemeldet bleiben“ ausschließlich über einen rotierenden, serverseitig gespeicherten Refresh-Token mit langer Laufzeit abbilden; `stay_logged_in` serverseitig beim Refresh-Token speichern statt aus einem Cookie zu lesen; `jti` und Token-Version pro Benutzer im JWT, Prüfung in `svc_helper.security`; bei Logout, Passwortänderung und Reset alle Tokens des Benutzers invalidieren; Refresh-Token-Rotation mit Wiederverwendungs-Erkennung.

---

### S-20 – Mass Assignment über Registrierungsdaten (Mittel)

**Fundstellen:** `backend/auth_svc/src/auth/models/user.py:21-22` (`UserIn` mit `model_config = ConfigDict(extra="allow")`); `backend/auth_svc/src/auth/utils/auth.py:294` (der komplette `UserIn` inkl. aller Zusatzfelder geht in die MQ-Nachricht; durch Unit-Tests `backend/auth_svc/src/tests/unit/utils/test_auth.py:324-328` sogar als Verhalten festgeschrieben); `backend/business/src/business_api/worker.py:31-71` (`consume_user_registration`: entfernt nur `password`, `password_repeat`, `db_id`, übergibt den Rest an `create_user` für JTL und speichert ihn per `MONGO_USER.insert_or_replace_one(data, "id")` im Business-User-Datensatz).

**Beschreibung:** Beliebige, vom Client bei der Registrierung mitgeschickte Felder werden ungefiltert in den Business-User übernommen und teilweise an das ERP weitergereicht. Lediglich `erp_id` wird anschließend überschrieben. Felder, die die Geschäftslogik später auswertet (z. B. Kundengruppe, Rabatte, Firmen-/B2B-Kennzeichen, `additionalProp1` in `backend/business/src/business_api/jtl_helper/user_handling.py:61`), können so vom Kunden selbst gesetzt werden.

**Auswirkung:** Rechteausweitung auf Datenebene (z. B. Zuordnung zu einer Kundengruppe mit Sonderkonditionen, je nach Auswertung der Felder), Verunreinigung der Kundendaten im ERP, Speicherung beliebig großer Datenmengen pro Konto.

**Empfehlung:** `extra="forbid"` für `UserIn`; explizites Nachrichtenschema für `user_registered` mit Allowlist der Felder (vgl. S-14); im Konsumenten ebenfalls gegen ein festes Pydantic-Modell validieren; sicherheitsrelevante Felder (Kundengruppe, Rabatte, Rollen) ausschließlich serverseitig/aus dem ERP setzen; Tests entsprechend anpassen.

---

### S-21 – Items-API: Einkaufspreise und Lieferantendaten öffentlich, unbegrenzte Abfragen (Mittel)

**Fundstellen:** `backend/items_svc/src/jtl/v1/endpoints/items.py:174-209` (`POST /api/items/…` – öffentlich über `docker-stack.yml:210`); `backend/items_svc/src/jtl/schemas/item_schemas.py:87` (`purchase_price_net`), `:191-207` (`ItemSuppliers` mit Lieferanten-ID, Einkaufspreis, Mindestabnahme, Lieferzeit, Bestand, Dropshipping-Kennzeichen, Kommentar), `:254` (`suppliers` als Teil des Artikels); Ausschluss von Feldern nur über die **vom Client** gelieferte Liste `excluded_fields` (`items.py:176-178,206-207`); Abfrage-/Filterlogik (`MongoQueryBuilder`, `APIQuery` mit Operatoren wie `contains`, `in`, `lte`, sowie `pagination.page_size`) stammt aus `svc_helper` und wird ungeprüft übernommen (`items.py:184-192`); `user_id` für Kundengruppenpreise kommt aus dem Request-Body (`items.py:179-181`) statt aus dem Token.

**Beschreibung / Auswirkung:** Interne Kalkulationsdaten (Einkaufspreise, Margen, Lieferanten, Bestände) sind für Wettbewerber abrufbar. Da Seitengröße und Filter vom Client kommen, sind sehr große Abfragen bzw. teure Regex-/`contains`-Suchen auf MongoDB möglich (Ressourcen-Erschöpfung). Die Preisberechnung für Kundengruppen hängt an einem vom Client gelieferten Benutzerbezug.

**Empfehlung:** Separates öffentliches Antwortschema (Response-Model) ohne Einkaufs- und Lieferantendaten – serverseitig, nicht per Client-Ausschlussliste; `page_size` begrenzen (z. B. `le=100`); Filterfelder und Operatoren per Allowlist beschränken, Regex-Eingaben escapen bzw. nur Präfixsuche über Index zulassen; Kundengruppe ausschließlich aus dem verifizierten Token ableiten.

---

### S-22 – Container- und Laufzeit-Härtung (Mittel)

**Fundstellen**
- `cms/strapi_cms/Dockerfile:21` (`node:20-slim` – Node.js 20 ist seit 30.04.2026 End-of-Life), `:41` (`npm run develop --debug`, vgl. S-11).
- `frontend/Dockerfile:1` (`node:23.5` – ungerade Node-Version, seit 06/2025 EOL; nur Build-Stage, Laufzeit `nginx:1.27` mit `USER nginx` ist in Ordnung).
- `docker-stack.yml:661`, `compose/services*/backend/mail_svc.yml:39`: `axllent/mailpit:latest` – nicht reproduzierbar, ungeprüfte Updates.
- `compose/services/traefik.yml:14`, `compose/services_deploy/traefik.yml:14`: `/var/run/docker.sock` in Traefik eingebunden (auch `:ro` erlaubt vollen Zugriff auf die Docker-API → Root auf dem Host bei Kompromittierung von Traefik).
- `backend/*/Dockerfile` (auth_svc, business, items_svc, mail_svc, payment_svc, pdf_svc): kein `USER`-Statement → Prozesse laufen als **root** im Container (besonders kritisch für `pdf_svc`, S-03).
- `docker-stack.yml:194-195` (und analog `:288,442,507,639`): Uvicorn mit `--forwarded-allow-ips *` → `X-Forwarded-For`/`-Proto` jedes Absenders werden vertraut (IP-basierte Limits/Logs fälschbar).
- Dev-Compose mit `--reload` im Produktions-Pfad (`docker-compose_deploy.yml:8-10`, vgl. S-07).

**Auswirkung:** Höhere Reichweite jeder Container-Kompromittierung (root, Docker-Socket), fehlende Sicherheitsupdates der Laufzeit, unzuverlässige Client-IP für Rate-Limits (S-10) und Forensik.

**Empfehlung:** Unterstützte LTS-Versionen (Node 22/24) und feste Image-Tags bzw. Digests; nicht-privilegierten `USER` in allen Backend-Images; `read_only: true`, `cap_drop: [ALL]`, `no-new-privileges`; Docker-Socket über einen Socket-Proxy (z. B. `tecnativa/docker-socket-proxy` mit nur `CONTAINERS=1`) oder Swarm-Provider mit eingeschränktem Zugriff; `--forwarded-allow-ips` auf die Traefik-Netz-IPs beschränken; Image-Scanning (Trivy/Grype) in der CI.

---

### S-23 – Abhängigkeiten mit bekannten Schwachstellen; inkonsistentes Strapi-Lockfile (Mittel)

**Fundstellen und Beispiele (Stand Lockfiles im HEAD)**
- Python (`backend/*/uv.lock`): `h11` 0.14.0 in auth_svc und pdf_svc (CVE-2025-43859, Request Smuggling; behoben in 0.16.0); `starlette` 0.41.3 (auth_svc, pdf_svc) und 0.46.2 (items_svc) – CVE-2025-54121 (DoS bei Multipart-Uploads, behoben in 0.47.2) –, zusätzlich 0.47.3 (business) – CVE-2025-62727 (DoS über `Range`-Header, behoben in 0.49.1); `jinja2` 3.1.5 in auth_svc und pdf_svc (CVE-2025-27516, Sandbox-Ausbruch, relevant für S-03); `urllib3` 2.3.0 (pdf_svc) und 2.5.0 (business) mit mehreren 2025 veröffentlichten CVEs; `requests` 2.32.3 in pdf_svc (CVE-2024-47081, Credential-Leak über `.netrc`).
- Frontend (`frontend/package-lock.json`): `axios` 1.7.9, `vite` 6.0.11, `@sveltejs/kit` 2.17.1, `cookie` 0.6.0, `form-data` 4.0.1 – für alle sind seither Sicherheitsupdates erschienen (u. a. SSRF/DoS in axios, Dev-Server-Dateizugriff in vite, unsichere Zufallsgrenze in form-data < 4.0.4).
- CMS (`cms/strapi_cms/package-lock.json`): `@strapi/strapi` **5.6.0** im Lockfile, während `cms/strapi_cms/package.json:15-17` **5.30.1** verlangt → Lockfile veraltet, tatsächlich installierte Version beim Build unklar; zudem `koa` 2.15.2, `axios` 1.7.4, `undici` 6.19.2, `tar-fs` 2.1.1/3.0.6, `vite` 5.2.14/5.4.8 mit bekannten CVEs (u. a. Open Redirect/XSS in koa, Pfad-Traversal in tar-fs).
- Unterschiedliche Versionsstände derselben Bibliotheken über die sechs Backend-Services (z. B. FastAPI 0.115.8 bis 0.136.1).

**Auswirkung:** Teilweise direkt ausnutzbare Schwachstellen (Request Smuggling hinter Traefik, DoS, Verstärkung von S-03); fehlende Reproduzierbarkeit des CMS-Builds.

**Empfehlung:** Alle Lockfiles aktualisieren (`uv lock --upgrade`, `npm update`/`npm audit fix`), Strapi-Lockfile neu erzeugen und einchecken; einheitliche Versionen über die Services (gemeinsame Constraints-Datei); Renovate/Dependabot sowie `pip-audit`/`npm audit` bzw. OSV-Scanner als CI-Gate; vor Updates Regressionstests.

---

### S-24 – Datenschutz: Drittanbieter ohne Einwilligung, PII im dataLayer, fehlerhafte Consent-Prüfung (Mittel)

**Fundstellen**
- `frontend/src/app.html:29` (eTrusted), `:46-50` (Userlike-Chat, lädt von S3-CDN), `:51-71` (Trusted Shops), `:73` (Händlerbund): werden unabhängig von der CookieFirst-Einwilligung geladen; nur GTM ist per `type="text/plain"` + `data-cookiefirst-category` (`:15`) consent-gesteuert.
- `frontend/src/lib/utils/tracking.ts:291-309`: Beim `purchase`-Event werden SHA-256-Hashes von E-Mail, Vor-/Nachname, Telefon, Adresse sowie Klartext-Kundennummer und Firma in den `dataLayer` geschrieben.
- `frontend/src/lib/tracking-params.ts:152-159` (`shouldWriteAttributionCookies`): prüft das Cookie `cookie_consent_marketing`, das von CookieFirst nicht gesetzt wird → Ergebnis ist praktisch immer `true`; Attributions-Cookies werden ohne Einwilligung gesetzt.

**Beschreibung / Auswirkung:** Verstoß gegen § 25 TDDDG (Zugriff auf Endgeräte nur mit Einwilligung, soweit nicht unbedingt erforderlich) und Art. 6 DSGVO; ungesalzene SHA-256-Hashes von E-Mail/Telefon gelten als personenbezogene Daten (pseudonym, leicht zuordenbar) und werden an Werbeplattformen weitergegeben. Abmahn- und Bußgeldrisiko; kein unmittelbares Sicherheitsrisiko für die Infrastruktur.

**Empfehlung:** Alle nicht unbedingt erforderlichen Widgets erst nach Einwilligung laden (CookieFirst-Kategorien bzw. Zwei-Klick-Lösung für den Chat); Enhanced-Conversions-Daten nur bei Marketing-Einwilligung und nur über GTM-Consent-Mode übergeben; Consent-Prüfung auf die tatsächliche CookieFirst-API (`window.CookieFirst.consent`) bzw. deren Cookie umstellen und standardmäßig „keine Einwilligung“ annehmen (fail-closed); Verzeichnis der Verarbeitungstätigkeiten und Datenschutzerklärung abgleichen.

---

### S-25 – Lieferkette: sicherheitskritische Shared Library aus dem Repo eines Ex-Entwicklers (öffentlich, außerhalb eurer Kontrolle); `curl | bash` in der CI (Mittel)

**Fundstellen:** `backend/auth_svc/pyproject.toml:27`, `backend/business/pyproject.toml:24`, `backend/items_svc/pyproject.toml:24`, `backend/mail_svc/pyproject.toml:21`, `backend/payment_svc/pyproject.toml:24`, `backend/pdf_svc/pyproject.toml:24` – `svc-helper` aus `https://gitlab.com/vitandi1/svc_helper.git` (Commit `5e65dee9`); die Bibliothek stellt u. a. die JWT-Prüfung (`svc_helper.security.get_current_user_id`, `verify_jwt_token`), den Mongo-Query-Builder und die MQ-Anbindung. `.gitea/workflows/frontend_check.yml:17` lädt das nvm-Installationsskript per `curl … | bash` aus dem Internet.

**Beschreibung:** Die zentrale Sicherheitslogik liegt außerhalb des Repos in einem persönlichen GitLab-Namespace (öffentlich lesbar, Kopie seit 07.10.2026 unter `referenz/svc_helper`), ist damit nicht Teil dieser Prüfung und nicht unter Kontrolle des Shop-Betreibers. Positiv: Die Abhängigkeit ist auf einen Commit gepinnt. Wird das Repo gelöscht, umgeschrieben oder übernommen, sind Builds nicht mehr reproduzierbar bzw. könnte bei einer Aktualisierung des Pins manipulierter Code übernommen werden. Das `curl | bash` in der CI führt ungeprüften Fremdcode mit den Rechten des Runners aus.

**Auswirkung:** Supply-Chain-Risiko für Authentifizierung und Datenzugriff aller Services; Kompromittierung des CI-Runners (und der dort verfügbaren Secrets/Registry-Zugänge) bei manipuliertem Skript.

**Empfehlung:** `svc_helper` in eine Organisation des Betreibers (eigener Gitea/GitLab) übernehmen, Code-Review und eigene Tests für die Sicherheitsfunktionen, Releases als signierte Pakete in einer eigenen Registry; in der CI `actions/setup-node` mit fester Version statt `curl | bash`; Actions auf Commit-SHA pinnen.

---

### S-26 – OpenAPI-Dokumentation, Demo-Seite und Debug-Endpunkte öffentlich (Niedrig)

**Fundstellen:** Alle FastAPI-Apps werden ohne `docs_url=None`/`openapi_url=None` erzeugt (kein Treffer für `docs_url`/`openapi_url` in `backend/*/src`) → `/docs`, `/redoc`, `/openapi.json` je Service über die `/api/*`-Router erreichbar; `backend/auth_svc/src/auth/v1/endpoints/demo.py:6-12` und `backend/auth_svc/src/auth/main.py:8-10,25` (HTML-Demo-Login unter `/api/auth/v1/demo/`); `backend/business/src/business_api/main.py:39-49` (`/health_auth` gibt die aus dem Cookie ermittelte Benutzer-ID zurück).

**Beschreibung / Auswirkung:** Die vollständige API-Beschreibung inkl. interner Endpunkte erleichtert das Auffinden der Schwachstellen S-01, S-02, S-09, S-15 erheblich. Die Demo-Seite ist eine zusätzliche, nicht für den Produktivbetrieb vorgesehene Angriffsfläche. Für sich genommen geringes Risiko.

**Empfehlung:** In Produktion `docs_url`, `redoc_url` und `openapi_url` deaktivieren (per Umgebungsvariable steuerbar) bzw. nur intern erreichbar machen; Demo-Router nur in Entwicklungsumgebungen einbinden; Debug-Endpunkte entfernen.

---

### S-27 – Konto-Enumeration bei der Registrierung; Token-Reflexion in `/activate` (Niedrig)

**Fundstellen:** `backend/auth_svc/src/auth/v1/endpoints/account.py:52-56` (Registrierung antwortet mit HTTP 409 „Ein Benutzer mit dieser E-Mail-Adresse existiert bereits.“); `:59-66` (`GET /activate` gibt den übergebenen Token unverändert in der Antwortnachricht zurück). Positiv: `forgot-password` (`:69-79`) antwortet bereits generisch.

**Beschreibung / Auswirkung:** Die Existenz eines Kundenkontos zu einer E-Mail-Adresse ist feststellbar (Vorbereitung von Phishing und Credential-Stuffing, S-10). Die Token-Reflexion ist wegen der JSON-Antwort nur gering XSS-relevant, gibt aber unnötig Eingaben zurück und verwischt die Unterscheidung von Erfolg/Fehler (beide Fälle liefern `MsgCreated`).

**Empfehlung:** Registrierung mit Double-Opt-In und generischer Antwort („Bitte prüfen Sie Ihr Postfach“), Hinweis auf bestehendes Konto nur per E-Mail an die Adresse; `/activate` ohne Echo des Tokens und mit korrektem Fehlerstatus (4xx); Rate-Limit (S-10).

---

### S-28 – Private TLS-Schlüssel im Repository (Niedrig)

**Fundstellen:** `traefik/data/cert.key`, `traefik/data/cert_cms.key`, `traefik/data/cert_traefik.key` (mit zugehörigen `.crt`), eingecheckt seit `88c8ccf` (29.12.2024).

**Beschreibung / Auswirkung:** Es handelt sich laut Analyse um selbstsignierte Zertifikate für `CN=localhost` (Entwicklung), produktiv werden Let's-Encrypt-Zertifikate über `traefik/traefik.yml:60-65` bezogen. Ein direktes Risiko besteht daher nur, wenn diese Schlüssel irgendwo als vertrauenswürdig installiert wurden (z. B. als lokal vertrauenswürdige CA auf Entwicklerrechnern). Positiv: `acme.json` (produktiver Let's-Encrypt-Account- und Zertifikatsschlüssel) ist per `.gitignore:18` ausgeschlossen und laut Git-Historie nie eingecheckt worden.

**Empfehlung:** Schlüssel aus dem Repo und der Historie entfernen (zusammen mit S-04), `traefik/data/*.key` in `.gitignore` aufnehmen, Entwicklungszertifikate lokal erzeugen (z. B. `mkcert`); auf dem Server Dateirechte 600 für `acme.json` sicherstellen.

---

### S-29 – Unauthentifiziertes Invalidieren des Sitemap-Caches (Niedrig)

**Fundstellen:** `backend/business/src/business_api/v1/endpoints/sitemap.py:242-247` (`POST /sitemap/refresh`, öffentlich über den `/api`-Präfix-Router, vgl. S-15).

**Beschreibung / Auswirkung:** Jeder kann den Cache leeren und eine vollständige Neuberechnung der Sitemap synchron im Request auslösen (Abfragen an Items-Service/CMS). Wiederholte Aufrufe erzeugen ohne Rate-Limit (S-10) unnötige Last auf Backend und Datenquellen (DoS-Verstärker). Keine Datenpreisgabe außer der Anzahl der URLs.

**Empfehlung:** Endpunkt nur intern bzw. mit Service-Token erreichbar machen (Cronjob/Deploy-Hook); Neuberechnung asynchron und mit Mindestabstand (z. B. höchstens einmal pro Minute).

---

## 6. Hinweis zur Live-Prüfung

Abgesehen von der statischen Analyse des Repositorys wurde am **07.10.2026** vom Analyse-Rechner aus lediglich die Erreichbarkeit einzelner Ports des JTL-Servers `c10330.servers.ecomdata.de` festgestellt:

- **Port 3389 (RDP) war öffentlich aus dem Internet erreichbar.** Ein offen erreichbarer Windows-Remotedesktop ist ein häufiges Einfallstor (Passwort-Angriffe, RDP-Schwachstellen) – und auf diesem Server liegen JTL-Wawi und damit sämtliche Kunden-, Auftrags- und Zahlungsdaten.
- Die JTL-API-Ports **50443** und **58080** (vgl. S-13) waren von diesem Rechner aus **nicht** erreichbar; vermutlich besteht dort bereits eine IP-Freigabe (Allowlist). Das sollte beim Hoster bestätigt werden, ersetzt aber nicht die Transportverschlüsselung (S-13).

**Empfehlung:** RDP nicht direkt ins Internet freigeben, sondern nur über VPN (bzw. ein RD-Gateway mit MFA) erreichbar machen; den Hoster (ecomdata) ansprechen, die Firewall-Regeln für 3389 entsprechend einzuschränken und die RDP-Anmeldeprotokolle auf auffällige Anmeldeversuche prüfen; Network Level Authentication, Kontosperr-Richtlinie und aktuelle Windows-Updates sicherstellen.

Darüber hinaus wurde **nichts weiter getestet**: keine Anmeldeversuche, keine Aufrufe von Shop- oder API-Endpunkten, keine Ausnutzung der in diesem Bericht beschriebenen Schwachstellen. Alle übrigen Aussagen beruhen ausschließlich auf der statischen Analyse; mit „zu verifizieren“ markierte Befunde sollten vom Betreiber bzw. in einem beauftragten Penetrationstest gegen die Live-Umgebung bestätigt werden.

### 6.1 Portprüfung Shop-Server (07.10.2026, von außen)

Ziel `sicherheit.meesenburg.com` = 136.243.107.86 (Hetzner Online GmbH, RZ Falkenstein fsn1-dc8). Nur Verbindungsaufbau geprüft, keine Anmeldung, keine Inhalte abgerufen.

| Port | Dienst (laut Repo) | Ergebnis |
|---|---|---|
| 22 | SSH | **offen** |
| 80, 443 | Traefik | offen (erwartet) |
| 1025 | Mailpit SMTP | **offen** |
| 8025 | Mailpit-Weboberfläche | **offen, ohne Anmeldung** (HTTP 200, Seitentitel „Mailpit“) |
| 10002 | Frontend direkt (nginx 1.27.5) | **offen** |
| 11010–11012, 5672, 15672 | RabbitMQ | nicht erreichbar |
| 11020, 6379 | Valkey | nicht erreichbar |
| 11021–11025 | mongo-express / Redis-Commander | nicht erreichbar |
| 27017, 3306 | MongoDB, MariaDB | nicht erreichbar |

**Folgen für S-07:** Der kritische Teil ist **bestätigt für Mailpit**. Ob Produktivmails dort landen, hängt von der Deploy-Variante ab (Swarm-Stack + `.env` mit `MAIL_SMTP_*` → Standard `mailpit`; Compose-Variante übersetzt korrekt nach `MAIL__SMTP_*`). Für RabbitMQ, Valkey, mongo-express und die Datenbanken ist S-07 von außen **nicht bestätigt** (Stand 07.10.2026; Firewall vermutlich beim Hoster oder Ports nicht veröffentlicht).

**Sofort (nach Sicherung):** Mailpit-Daten sichern, Inhalt durch eine befugte Person prüfen, Ports 8025/1025 und 10002 schließen; SSH auf Schlüssel und IP-Freigabe beschränken.
