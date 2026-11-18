# Zusammenfassung: Webshop SN24 und POC (Stand 09.10.2026)

> **Für neue Sitzungen zuerst lesen.** Details stehen in `analyse/` (00–15; 15 = Lehren aus dem Trello-Board), offene Aufgaben in `TODO.md`. **Vertraulich.**

## Begriffe (ab 27.10.2026)

- **JTL-Shop** / **JTL-Shop-Backend**: JTL-Shop 5.5.2 bei Timme (live, rechnet korrekt, Referenz).
- **Stripe-Shop** / **Python-Backend**: SN24 auf node1 (offline), wird ersetzt.
- **.NET-Shop** / **.NET-Backend**: unsere Migration (dieses Repo).

## Ausgangslage
- Der Webshop „Sicherheit und Service“ (SN24, `sicherheit.meesenburg.com`) ist seit Juni 2026 live: SvelteKit-SPA, 6 Python/FastAPI-Dienste, 4× MongoDB, Strapi, JTL-Wawi als ERP.
- Gebaut hat ihn die ICS Sustainable Solutions GmbH, die inzwischen **insolvent** ist. Weiter betreut wird er von zwei Ex-Entwicklern als Freelancer: **Christopher Seidel** (neogris, DK) und **Dominic Bräuer**.
- Seit dem Go-Live gibt es laufend falsche Preise und Konfigurationen. Die beiden Konfiguratoren (`/shop`, `/configurator`) rechnen unterschiedlich. Die Geschäftslogik ist auf Frontend, Backend und JTL verteilt (7 Preisberechnungen).

## Warum das Backend ausgetauscht werden muss
1. **Sicherheit:** Es gibt kritische Lücken.
   - `mark_paid` und die Auftragsübertragung an JTL sind öffentlich erreichbar.
   - Kundendaten und Auftrags-PDFs sind ohne Login abrufbar.
   - Der PDF-Dienst ist für Codeausführung missbrauchbar.
   - **Live wird der Login-JWT mit dem Repo-Standardschlüssel signiert, Anmeldungen sind also fälschbar.** Die Datenbankpasswörter sind ebenfalls Standardwerte.
2. **Zahlungen:** Der Stripe-Betrag wird nicht gegen die Bestellung geprüft. Fehler bei der JTL-Übergabe werden still verschluckt (`suppress(Exception)`), die Bestätigungsmail geht trotzdem raus.
3. **Betrieb:** Es gibt keine Tests, kein CI/CD und keine Backups. Der Live-Code weicht vom Repo ab (62 nie committete Änderungen). Das Backend hängt an einer Bibliothek aus der GitLab-Gruppe eines Ex-Entwicklers. Seit Juli fließen Logs mit personenbezogenen Daten an einen netcup-Server der Freelancer.
4. **Abhängigkeit:** Code, Server und Wissen liegen bei den Freelancern, und Gegenmaßnahmen auf Code-Ebene werden nicht umgesetzt.
5. **Behauptung „JTL ändert die API undokumentiert“: widerlegt.** Mit und ohne `/v1` sind die Antworten identisch, die Fehler liegen im eigenen Code (Bericht 03, POC-Test 09.10.).

**Entscheidung (bestätigt 09.10.2026):** Neues Backend in C#, Umsetzung ohne Zeitdruck nach **`analyse/14_plan_backend.md`**. Erster Schritt: alle Grundfunktionen **ohne Konfigurator** (Artikel sichtbar, bestellbar, bezahlbar, Aufträge in JTL). **Rückfall:** JTL-Shop läuft auf anderem Server weiter, Domain wird umgeleitet, **beide schreiben in dieselbe JTL-Wawi** (eigener Nummernkreis nötig). ERP nur über `IErp…Broker` (später JTL v2 oder esERP).

**Ursprüngliche Entscheidung:** Eine **.NET-10-API** (Stil wie Meesenburg.ES: Apis → Services → Brokers) ersetzt FastAPI und rechnet den Zahlbetrag serverseitig. Das Frontend bleibt vorerst und wird auf die neue API umgestellt. Betrieb in Containern (AKS).
- **Phase 1:** 2 Wochen + Testphase (Plan: `analyse/08_plan_phase1.md`).
- **Danach:** Konfigurationsregeln aus einem Fachworkshop in die API übernehmen.
- **Später JTL → esERP** (`analyse/07_eserp_bewertung.md`): Shop-Artikel als esERP-eigene 500er, SAP bleibt außen vor.
- **Neu zur Entscheidung (04.11.2026, Bericht 20):** Statt Svelte/Strapi umzustellen, wird der .NET-Shop der ganze Shop, mit eigenem Inhaltsmodul im Admin (Option B). Grundlage: Das SN24-Team nutzt im JTL-Shop 8 OPC-Bausteine (98 % der Inhalte), Inhalte auf 113 Artikel- und 68 Kategorieseiten, 9.046 Weiterleitungen. Aufwand bis Livegang ca. 70–110 PT. Erst Prototyp „Inhalte“ für das Team. AKS-Speicher ist voll → eigener Knotenpool. **Prototyp „Inhalte“ steht (04.11.):** Bausteine, Vorschau, Zeitsteuerung, Versionen; 237 Inhalte aus dem JTL-Shop übernommen; Admin nach Rechten (`Meesenburg.Authorization`, Datenmodell wie SiSe-Portal). Design B (Blau-Knöpfe) entschieden; alle übernommenen Inhalte überarbeitet (Text statt Text-Bilder, oben kompakt), Prüfliste für die Fachabteilung: `analyse/workshop/Pruefliste_Inhalte.md`. **05.11.:** Adressen und SEO-Kopf wie im JTL-Shop (`/Kaba-penta-cross-Doppelzylinder`, 301 für alte Adressen), Angebote rot; Inhalts-Fragen F-37–F-46 in Bericht 16.

## Neues Backend und POC (`src/`, Solution `Meesenburg.SiseShop.slnx`)

**Start (alles):** `aspire run` bzw. `dotnet run --project src/Meesenburg.SiseShop.AppHost --launch-profile http` (Docker muss laufen; MongoDB als Container mit Volume `siseshop-mongo-data`). Dashboard https://localhost:17217 (lokal ohne Anmeldung). JTL-Zugang als AppHost-Parameter in dessen User-Secrets (`Parameters:jtl-base-url`, `jtl-api-token`, `jtl-app-id`); **Schreibsperre** `jtl-allow-writes` = `false` (nur GET an JTL).

| Projekt | Zweck | Port |
|---|---|---|
| `Meesenburg.SiseShop.AppHost` | Aspire: Mongo, API, Worker, Portal, JTL-POC; Publish nach Docker Compose | Dashboard https://localhost:17217 |
| `Meesenburg.SiseShop.Api` | .NET-Backend (Shop-API) (`/api/v1/…`, Scalar unter `/scalar`) | http://localhost:5072 |
| `Meesenburg.SiseShop.Worker` | Abgleich JTL → Lesemodell, Auftrags-Outbox (S2/S8) | – |
| `Meesenburg.SiseShop.Core` / `.Contracts` / `.ServiceDefaults` | Brokers + Services / DTOs / Aspire-Grundlagen | – |
| `tests/Meesenburg.SiseShop.Tests` | Unit-Tests (`dotnet test`) | – |
| `Meesenburg.Shop.JtlApi` | Liest JTL-REST (`/v1`) und Custom-API, Vertragsprüfung mit Pydantic-artigen Regeln, Snapshots für Änderungserkennung | http://localhost:5279 (Scalar unter `/scalar`) |
| `Meesenburg.Shop.Portal` | **.NET-Test-Shop** (Aufbau wie sicherheit.meesenburg.com, SiSe-Farben) unter `/`, **Admin** (System, Abgleich, JTL-Diagnose) unter `/admin` (Blazor Server, MudBlazor) | http://localhost:5036 |

POC-Projekte einzeln: `dotnet run --launch-profile http` im Projektordner. Ohne Zugangsdaten laufen Beispieldaten (`Jtl:UseSampleData=true`). Das Portal wird zur Test-Oberfläche des .NET-Shops: **Farben/Grunddesign SiSe-Portal, Aufbau wie sicherheit.meesenburg.com** (Navigation oben).

**Zugang zu JTL:** Die API-Ports 50443 und 58080 sowie der Wawi-SQL-Port 50000 (seit 10.10.2026, vorher offen für alle, J-21) sind per IP-Liste (Windows-Firewall auf dem JTL-Server) gesperrt. Bis zur Freigabe läuft der Zugriff über einen SSH-Tunnel über den Shop-Server node1, der bei JTL freigegeben ist (reine Portweiterleitung, auf node1 wird nichts geändert; darf jederzeit aufgebaut werden):
```bash
ssh -f -N -o ExitOnForwardFailure=yes -L 50443:c10330.servers.ecomdata.de:50443 -L 58080:c10330.servers.ecomdata.de:58080 -L 14330:c10330.servers.ecomdata.de:50000 -i ~/.ssh/hetzner_shop mpiper@136.243.107.86
```
SQL (Lese-Login `siseshop_reader`, voller Lesezugriff nur in der Entwicklungszeit) über `localhost,14330`; Verbindung in den AppHost-User-Secrets `Parameters:jtl-sql`. SSH auf c10330 (Port 50022, `tools/jtl-ssh`) **funktioniert seit 12.10.2026 über node1** (ecomdata-Portal: eingehend TCP 50022 nur von node1):
```bash
ssh -o IdentitiesOnly=yes -J mpiper@136.243.107.86 -i ~/.ssh/jtl_admin -p 50022 mpiper@c10330.servers.ecomdata.de
```
Host-Schlüssel `SHA256:OyMfxZ5/JvNVXZO6Sm7D9yrHeI+B+46GrEmWcBtpx2A`; `jtl_admin` hat eine Passphrase → vorher `ssh-add --apple-use-keychain ~/.ssh/jtl_admin`.
Die User-Secrets zeigen auf `http://localhost:50443` bzw. `:58080`. Die Tokens stammen aus der gesicherten Live-`.env` und gelten als kompromittiert, nach der Rotation kommen sie aus dem Key Vault.

**Versionen:** JTL-Wawi **1.11.9**, REST-API nur **Version 1** → offizielle Spezifikation **1.1** (`referenz/jtl-wawi-api/jtl-wawi-api-1.1_wawi-1.11.0.0_openapi.json`). Firma 6, Shop-Kanal `2-2-5`. **Alle JTL-Erkenntnisse für das C#-Backend sammeln wir in `analyse/13_jtl_schnittstelle_referenz.md`.**

**Ergebnis des ersten Live-Tests (09.10.2026):**
- Verbindung OK: 23.324 Artikel, Steuerklassen, Kategorien und Custom-API antworten.
- **JTL lehnt eine ganze Artikelseite mit HTTP 500 ab**, sobald ein Artikel ohne Kategorie darin vorkommt („Bitte geben Sie mindestens eine Kategorie vor!“). Bei solchen Artikeln (z. B. ID 122, 131, alte Abus-Kameras) scheitert sogar die Einzelabfrage `GET /v1/items/{id}`. **Das ist ein Verhalten der JTL-REST-API (v1)**: Laut offizieller Doku ist `categoryId` ein optionaler Filter, keine Pflicht. Der Server unterstützt **nur API-Version 1** (`/v2/items` → `UnsupportedApiVersion`).
- **Lösung:** Mit dem offiziellen Filter `isActiveOnSalesChannelId=2-2-5` (Kanal „JTL-Shop: sicherheit.meesenburg.com“) laufen die Abfragen fehlerfrei (10.052 Shop-Artikel von 23.324; alle 51 Seiten à 200 ohne Fehler). Das alte Backend fragt dagegen jede Kategorie einzeln ab (`items_svc/.../raw_items.py`), mit vielen Anfragen, und bricht bei Fehlern still ab (`HasNextPage` fehlt → Ende).
- Offen im POC: Fehlerseite statt 502 weiterreichen, Snapshot speichern (`POST /jtl/contract/snapshots`).

**Stripe im POC (10.10.2026):** Testschlüssel des Live-Kontos (Testmodus, `acct_…vnpQU`) als optionale AppHost-Parameter `stripe-secret-key`, `stripe-publishable-key`, `stripe-webhook-secret` (User-Secrets des AppHost) → Umgebungsvariablen `Stripe__*` für die `shopapi`. Ein Zahlungsdienst, der sie liest, fehlt noch. Schlüssel gilt als kompromittiert und wird mit dem Stripe-Zugang (nächste Woche) gerollt (Bericht 12, 5.1).

## Infrastruktur (Kurz)
| System | Ort | Zugang Meesenburg |
|---|---|---|
| Shop `node1` 136.243.107.86 | Hetzner (Docker Compose `/home/admin/SN24`) | `mpiper` + Schlüssel `hetzner_shop`, sudo |
| `node2` 136.243.110.39 | Hetzner, Testinstanz `sn24_test` der Freelancer (spricht mit **Produktiv-JTL**) | wie node1 |
| `node3` 136.243.110.31 | Hetzner, Projekt `configurators` (Univelop) | wie node1 |
| JTL-Wawi `c10330` | ecomdata (Windows-VM, RDP, Custom-API + JTL-REST) | `nord4` (ohne Admin) |
| Repo | Azure DevOps `Meesenburg-Group/Sicherheit und Service/WebshopSicherheitUndService` | vorhanden |
| **JTL-Shop** (JTL-Shop 5.5.2, Template NOVA/NOVAChild) | **Timme Hosting GmbH & Co. KG** (Lüneburg), `k69g81.meinserver.io` = 178.63.45.204, physisch **Hetzner Falkenstein fsn1, DC14** (Wirt vermutlich `h3775.timmehosting.de`); verwalteter Server, ISPConfig; derselbe Host wie SMTP; Test-Domain `sicherheit.meesenburg-service.de` (12.10.: PHP-Seiten HTTP 500, statische Dateien OK → Webauftritt/Rechte prüfen); Docroot `/var/www/sicherheit-meeseburg.com/web/`; Stand 12.10.: im Wartungsmodus (503), Zertifikat (30.10. geprüft): **ein** Let’s-Encrypt-Zertifikat (14.10.–12.01.2027) mit CN `shz24.de` und SAN `shz24.de`, `www.shz24.de`, `sicherheit.meesenburg.com`, `www.sicherheit.meesenburg.com` – gültig für den Shop, Browser zeigen keinen Fehler; CN kommt aus der ISPConfig-Webseite (shz24.de als Haupt- oder erste Domain, sicherheit.meesenburg.com als Alias); `shz24.de` leitet per 302 auf den Shop um. Chrome-Prüfung 30.10.: `sicherheit.meesenburg.com` sicher (Kette bis ISRG Root X1 ok, HTTP→HTTPS 301, keine HTTP-Ressourcen); **`sicherheit.meesenburg-service.de` liefert das Server-Zertifikat `k69g81.meinserver.io` → „Nicht sicher“**; kein HSTS-Header. Schöner: eigene Domain als Hauptdomain bzw. getrenntes Zertifikat (Timme) | Webverwaltung (M. Piper) |
| Log-Abfluss | netcup `85.235.65.67` (Loki/Prometheus, wahrscheinlich Seidel/neogris) – **seit 09.10. 12:20 gestoppt** (Alloy aus) | keiner |

**Sicherungen** (Image „Backup Shop“, `SN24_20261009_0545`): node1 vollständig, node2/node3 schlank, JTL-Server (`Sicherung_SN24_2026-10-08_1142.zip`). Protokoll: `_PROTOKOLL.md` im Image.

## Sofortmaßnahmen (umgesetzt)
- 09.10. 12:20: Log-Versand an netcup gestoppt (Alloy aus).
- 09.10. 13:49: `/api/pdf`, `/api/maildata`, `/api/order/send*`, `mark_paid` von außen gesperrt (Traefik-Datei `zz-meesenburg-sperre-intern.yml`, Rücknahme durch Löschen).
- 09.10. 14:08: Mailpit (1025/8025) und Frontend-Direktport 10002 von außen gesperrt (Firewall, Dienst `mee-ports-sperre.service`).

- 19.10. 08:44: **JTL-Shop 5 wieder live** (DNS auf 178.63.45.204); **Stripe-Shop (Python-Backend) auf node1 von außen gesperrt** (80/443, `mee-shop-offline.service`, Container laufen weiter, rücknehmbar).

## Nächste Schritte
0. **Backend nach Bericht 14:** S0 Grundgerüst (09.10.), S1 JTL-Lesebroker, S2 Abgleich (Voll/Delta/Stammdaten) und S3 Katalog-API + .NET-Test-Shop (10.10.) erledigt. .NET-Test-Shop http://localhost:5036, Admin `/admin`. S4 (Prüfungen), S5 (Warenkorb/Preise/Versand) und Vergleich Stripe-Shop ebenfalls erledigt (10.10.). S6 (Bestellung mit Live-Prüfung) erledigt. Wawi-SQL angebunden (Lese-Login, 10.10.): Variationsaufpreise und Versandkosten kommen aus der Wawi, alle Variationsartikel bestellbar. S7 Zahlung mit Stripe im Testmodus erledigt (12.10.: Testkarte bezahlt, Ablehnung geprüft; Webhook gebaut, live noch ohne Stripe-CLI). S8 Trockenlauf erledigt (12.10.: Admin → Bestellungen → „Trockenlauf JTL“ zeigt Prüfungen und die exakten JTL-Anfragen, gesendet wird nichts). Erster Testauftrag im Produktiv-JTL am 12.10. erfolgreich (S100005 → Auftrag 91401, Kontrolle grün, storniert, J-26); Schreibsperre wieder aktiv. S8-Automatik gebaut und mit Schreibsperre getestet (Worker überträgt bezahlte Bestellungen/Vorkasse, Wiederholung, Klärungsfälle im Admin) – **ausgeschaltet**, Einschalten nur nach OK. JTL-Shop analysiert (Bericht 17) und Preis-/Sichtbarkeitsregeln im .NET-Shop nachgebaut (27.10.: Kategorie-Rabatte, Kundengruppen-, Staffel- und Sonderpreise, Sichtbarkeit; 205/205 Rabatt-Artikel auf den Cent wie JTL-Shop). Parität P7/P8/P10 erledigt (27.10.: Grundpreis, Verfügbarkeits-Ampel, Gratisgeschenke ab 200 € – wie JTL-Shop; REST meldet die Lagerführung falsch, J-27). S9 Mails vorbereitet (27.10.: Exchange Online über Graph wie ES, lokal Mailpit, Outbox, Vorkasse-/Bestätigungs-/Klärungsmails; echte Mails erst nach App-Registrierung und Freigabe, vorher nur an meesenburg.de). Befund: ≈ 3.500 Artikel mit Pflicht-Konfiguration sind im .NET-Shop ohne Konfiguration bestellbar → sperren (F-34), kleiner Konfigurator als S9a vor dem Livegang; großer Konfigurator (Schließanlagen) Phase 2. Großer Konfigurator als POC fertig (27.10.: `/konfigurator`, alle 12 Systeme, Schließplan-Matrix, Anlagentyp nach JTL-Plugin mit Vergleich zur Stripe-Regel, Plan bis in Bestellung, Mail und Wawi-Notiz; Bericht 18). Befund R-1: Rundung bei Menge > 1 weicht um Cent vom JTL-Shop ab – Entscheidung offen. Kleiner Konfigurator (S9a) ebenfalls fertig (27.10.: 3.701 Artikel, Preise wie JTL-Shop, ohne Konfiguration nicht bestellbar – Livegang-Hindernis erledigt). Großer Konfigurator nach den Systemen des Stripe-Shops gebaut (27.10.: Konfigurator-Sortiment ohne Shop-Kanal, 18 Anlagen-Systeme, Code-Position nach Typ, fachliche Typregel, Golden-Tests bis auf Rundung R-1; JTL-Variante als Vergleich; Abgleich per Admin anstoßbar). Workshop-Dateien liegen unter `analyse/workshop/` (offene Fragen, Wawi-Datenfehler). Bilder (28.10.): Vorladen im Abgleich, dynamische WebP-Größen ohne Verzerrung (41001: 77 KB → 30 KB), Abgleich-Pause nach Fehlern. Nächste Schritte: Vorstellung, Workshop (F-36, R-1, K-2), P11 Gutscheine/P13 Zahlarten, S9-Graph nach App-Registrierung. Mongo-Cluster `Dev-Sise-Shop` vorbereitet (E-P, Anlage nach OK). Trello-Lehren (Bericht 15) sind Abnahmekriterien. **Custom-API wird nicht mehr genutzt**; Lücken der REST-API (Variationsaufpreise, Versandkosten) per eigenem SQL-Lese-Login → bei ecomdata anfragen. Offene Entscheidungen E-A bis E-J in Bericht 14.
0c. **Vorstellung (KW 46/2026, GF + SN24-Team + IT):** .NET-Shop als Nachfolger des Stripe-Shops (Option B). Foliensatz (Artifact „SN24 .NET-Shop – Vorstellung“, 12 Folien), Drehbuch und Screenshots in `analyse/vorstellung/`. V1–V7 erledigt; Rest bis Livegang ca. 26–43 PT (V8 Betrieb, V9 Abnahme, PayPal/Stripe live, Konten, Entra/Rechtstexte, Workshop-Rest), Termin hängt an IT (Knotenpool, Key Vault, Entra, Graph, ecomdata-IP).
0d. **Backend ersetzt das Python-Backend (10.11.2026, Bericht 14 §5c):** alle 70 Endpunkte des Stripe-Shops verglichen. Nachgebaut: Sicherungskarte hochladen (L1), Wawi-Stand mit Sendungsnummer und Storno (L3), Schließpläne im Konto mit Ausblenden/Löschen nach 30 Tagen (L5), Registrierung per Link bestätigen und Gastbestellungen zuordnen (L6), Warenkorb am Konto (L7). Offen: L4 (Mails Versand/Storno, F-49), L2 zurückgestellt. Bei Option A zusätzlich 10–17 PT für die Svelte-Anbindung (S1–S4). Dazu nach dem Probelauf behoben: Freifeld-Variationen (362, J-39) machten Artikel unbestellbar; Artikelseite mit Radio-Auswahl und „Schlüssel hinzufügen“; Folien jetzt auch in `analyse/vorstellung/folien/`.
0e. **Entscheidungshelfer im Konfigurator (10.11.2026, Bericht 14 §5d):** vor dem Livegang beschlossen, erste Fassung läuft unter `/konfigurator/entscheidungshelfer` – 6 Fragen, Vorschläge mit echtem Preis für den Bedarf und Begründung, Übergabe an den Planer, Pflege im Admin. Grundlage: Wawi-Merkmale Sicherheitslevel/Empfehlungsgrad/Zylindertyp. Offen: F-51 mit dem SN24-Team, Auswertung, optional KI-Freitext.
0f. **Testkonten im Shop (10.11.2026):** lokal zeigt `/konto/anmelden` die 4 Testkonten zum Anmelden ohne Passwort (nur Development, `Customers:TestAccountsEnabled`). Endkunde und Händler haben je 4 Beispielbestellungen (`T-…`, geliefert/unterwegs/offen, ohne Wawi-Auftrag – Übertragung und Statusabgleich lassen sie in Ruhe); Adresse und Telefon hinterlegt, Kasse damit vorbelegt.
0g. **Weiter mit dem .NET-Shop (17.11.2026, Bericht 22):** Testversion im AKS für das SN24-Team (eigener Namespace und Knotenpool, Zugang nur intern bzw. IP-Liste plus Entra-Anmeldung vor dem ganzen Shop, noindex/robots) und Shop-Admin bereinigt ins SiSe-Portal als NuGet-Paket (RCL) mit kleinem, abschaltbarem ES-Patch. ES wird aus der Arbeitskopie deployt (928 offene Änderungen, nur Prod-Cluster) → vorher Sicherungsstand im ES-Repo. Engpass: Freigabe der AKS-Ausgangs-IP bei ecomdata (V-2). Aufwand 5,5–6,5 PT (Test) + 7–8,5 PT (Portal). **Entschieden 17.11.:** nur Entra-Anmeldung vor dem Shop (abschaltbar, kein eigener Ingress), Team nutzt Testkonten, Portal-Rollen. **Erledigt:** T-1 Testzugang, T-4 Schlüssel/Tickets in Mongo, P-1 Pakete auf ES-Stand. App-Registrierung legt M. Piper an (`deploy/aks-test/APP_REGISTRIERUNG.md`).
0h. **AKS-Kapazität für den produktiven .NET-Shop (17.11.2026, Bericht 23):** Der .NET-Shop wird der produktive Shop (Entscheidung 17.11.). Bedarf ≈ 2,4 GiB (Shop 2×, API 2×, Worker 1×) plus Test ≈ 1,2 GiB → eigener Pool **2 × D2ds_v5**, grob 215–290 €/Monat. ES-Pool liegt heute auf einem Knoten bei 103 % Speicher → vor dem Admin-Umzug entlasten (Aspire-Dashboard 1,1 GiB). Risiko: Shop hängt am VPN zum Rechenzentrum (Mongo).
0b. **Selbstverwaltung gebaut (05.11.2026):** Rechtstexte aus Strapi, Kontaktformular, Admin „Menüs & Hinweise“ (inkl. Wartungsmodus), „Weiterleitungen“ mit 404-Liste, „Gutscheine“ (JTL-Kupons übernommen), Google-Shopping-Feed, Cookie-Einwilligung. V3 (Sitemap, robots.txt, strukturierte Daten) und **V4 statisches Rendering** erledigt: öffentliche Seiten ohne dauerhafte Verbindung je Besucher, Warenkorb im Cookie, interaktiv nur Warenkorb, Kasse, Konfigurator, Konto, Admin. Weiterleitungen wahlweise 301/302. Merkmal-/News-Umleitungen zurückgestellt (M. Piper). **V5 Bestellabschluss** erledigt: unbezahlte Online-Bestellungen nach 48 h automatisch storniert (Stripe-Zahlung abgebrochen), AGB und Widerrufsbelehrung als PDF an der Bestellmail. Nächstes Paket: V8 Betrieb.
0a. **Livegang Version 1 (05.11.2026):** Plan in Bericht 14, Abschnitt 5b (V1–V9 selbst, dazu Liste der Zulieferungen von IT, Buchhaltung, Marketing, Fachbereich). Workshop „Funktionen“ mit dem SN24-Team vorbereitet (Bericht 21): Merkmal-Seiten und News nicht nachbauen, sondern umleiten (F-47/F-48), Vorschläge für Selbstverwaltung (Menü, Hinweisleiste, Weiterleitungen, Gutscheine, Mailtexte).
1. GF-Entscheidung zum Vorgehen (09.10.) → **Rotation und Aussperrung nach `analyse/12_rotation_aussperrung.md`**.
2. Zusammenarbeit mit neogris wird gekündigt: Mail zur Datenherausgabe/Löschung (`korrespondenz/`), danach Rotation/Aussperrung (Bericht 12). Alloy ist bereits gestoppt.
3. Phase 1 starten (`analyse/08_plan_phase1.md`); Fachworkshop Konfigurationsregeln.

## Arbeitsregeln (vom Nutzer)
- Auf Servern **erst lesen, jede Änderung nur nach ausdrücklichem OK**; kein Neustart/Deploy ohne Freigabe.
- Sicherungen **vollständig** (inkl. `.env`); Secrets und Kundendaten nie im Chat ausgeben.
- Alle Zugangsdaten gelten als kompromittiert und werden getauscht.
- Stripe-Referenzen müssen durchgängig zuordenbar sein (Shop ↔ Stripe ↔ ERP ↔ SAP).
