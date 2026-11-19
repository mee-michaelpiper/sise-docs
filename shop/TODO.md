# TODO – Webshop Sicherheit und Service

> Offene Punkte aus Analyse und Planung. Details in `analyse/` (v. a. 00, 07, 08).
> Erledigtes abhaken und mit Datum versehen, nicht löschen.

## Sofort (Sicherheit)

- [ ] **Custom-API (Port 58080) ohne Token-Prüfung** (Bericht 13 J-18): schreibende Endpunkte (`/payment`, `/order/adjust_*`) für jede IP der Firewall-Liste offen → IP-Liste für 58080 auf node1 (Stripe-Shop) beschränken (Änderung am JTL-Server: mit ecomdata, nach OK); DSB-Bewertung ergänzen

## Neues Backend (Bericht 14)

- [x] Plan erstellt (09.10.2026): `analyse/14_plan_backend.md`
- [x] **S0 Grundgerüst** (09.10.2026): Solution `Meesenburg.SiseShop.slnx`, Aspire-AppHost (Mongo, API, Worker, Portal, JTL-POC, Compose-Publish), ServiceDefaults, Core, Contracts, Api (`/api/v1/system/status`), Worker (Lebenszeichen), Tests (Schreibsperre 6/6), Dockerfiles; Portal-Seite „System“; live gegen JTL 1.11.9 über Tunnel geprüft
- [ ] Entscheidungen E-A bis E-G (Bericht 14, Abschnitt 6) und Abweichungen von Bericht 08 (Abschnitt 2: saubere v1-API statt Fassade, kein Schattenbetrieb, Nummernkreis `S…`) bestätigen
- [x] **S1 ERP-Abstraktion + JTL-Lesebroker** (10.10.2026): 3 Broker-Schnittstellen + JTL-Implementierung, 22 Tests, live geprüft; Befunde J-10–J-16
- [x] Neuere JTL-API-Versionen geprüft (10.10.2026): Spezifikationen 1.0–2.1 in `referenz/jtl-wawi-api/`; Aufpreise + Konfig-Gruppen ab Wawi 2.1 (API v2), Versandkosten in keiner Version
- [ ] **Wawi-Update auf ≥ 2.1 mit ecomdata klären** (REST-API v2: Variationsaufpreise, Konfigurationsgruppen); SQL nur als Rückfall nach Prüfung und OK
- [ ] Versandregeln im Shop-Backend (S5, E-H): Versandarten, Länder, Gewicht/Versandklasse, Freigrenze – Ist-Werte des Stripe-Shops als Startwerte
- [ ] **Fragen an den Fachbereich gesammelt in `analyse/16_fragen_fachbereich.md`** (F-01 … F-27, mit Beispielen und Optionen) – Termin vereinbaren; neue Fragen dort ergänzen, Antworten hier und im Plan nachziehen
- [ ] **Mongo-Cluster (E-P) später:** vorerst bleibt der lokale Container (Entscheidung 10.10.2026). Offen: Benutzer `siseshop-dev` nur mit readWrite auf `Dev-Sise-Shop`, Key-Vault-Secret `SiseShop-Dev`, AppHost-Schalter `SiseShop:Mongo:Mode=KeyVault`
- [ ] Fachbereich (gesammelt, später): Kanal `1-1-6` und dessen Staffelpreise, Kundengruppen-/Sonderpreise (J-11, J-12) – bis dahin nur Standardpreise
- [x] 47 nicht gelistete Kategorien benannt (10.10.2026, `analyse/anhang/jtl_kategorien_nicht_gelistet_2026-10-10.csv`): alte Videoüberwachung + Keso 4000S unter Eyseo, widersprüchliche Ebenen
- [ ] Fachbereich: diese 47 Kategorien in der Wawi bereinigen (löschen oder Baum korrigieren), ebenso Artikel ohne Kategorie (J-01)
- [x] **S2 Abgleich ins Lesemodell** (10.10.2026): Voll/Delta/Stammdaten im Worker, Halbieren bei HTTP 500, weiches Entfernen nur nach vollständigem Lauf, Befunde je Artikel, Portal-Seite „Abgleich“, 35 Tests
- [ ] S2 offen: prüfen, ob neue Sonder-/Kundengruppenpreise das `Changed`-Datum des Artikels setzen (sonst eigener Preisabgleich); Bestandsabgleich `/v1/stocks/changes`; Durchsatz (≈ 280 Artikel/min, Vollabgleich ≈ 36 min) ggf. mit höherer Parallelität
- [x] **S3 Katalog-API + .NET-Test-Shop** (10.10.2026): `/api/v1/catalog/…`, Bilder aus JTL (GridFS), .NET-Test-Shop im Portal unter `/`, Admin unter `/admin`; URLs wie Stripe-Shop
- [x] **S4 Prüfungen** (10.10.2026): Admin-Seite „Prüfungen“ mit Befunden, Artikelliste, verwaisten Kategorien, CSV-Export
- [ ] S4 später: Pflege im Web (z. B. Kategorie zuweisen per `POST /v1/categories/{id}/items`) – schreibend, nur nach OK
- [x] **J-17 geklärt** (10.10.2026): JTL-Zählung enthält nicht abrufbare Datensätze (Stichprobe 300 Lücken-Ids → alle 404); die 6.323 Shop-Artikel sind vollständig
- [ ] Entscheidung E-M (Svelte durch Blazor ersetzen) vor S10; vorher Messversuch Static SSR
- [x] Admin aufgeräumt (10.10.2026): alter POC → `archiv/poc/`; neuer Bereich „Vergleich Stripe-Shop“
- [ ] **Entscheidung E-N Kategorie-Rabatte** (207 Artikel im Stripe-Shop 10–35 % günstiger; per REST in keiner Version): Rabatte in JTL-Preise überführen (empfohlen) oder SQL-Lesezugriff (nach OK)
- [ ] Fachbereich: 933 Artikel im Shop-Kanal aktiv, im Stripe-Shop aber ausgeblendet; 79 Artikel im Stripe-Shop sichtbar, in JTL ohne Preis (Liste: Admin → Vergleich Stripe-Shop)
- [x] **S5 Warenkorb, Preise, Versandregeln** (10.10.2026)
- [x] Trello-Board ausgewertet (10.10.2026): `analyse/15_trello_lehren.md`; #77 (deaktivierte Artikel) im neuen Backend behoben
- [x] SQL-Broker (lesend) + neutrale Modelle v2/esERP-fähig gebaut (10.10.2026)
- [x] **Lese-Login für die Wawi-DB** angelegt und getestet (10.10.2026, J-24): `tools/sql/01_shop_reader_login.sql` – Entwicklungszeit mit vollem Lesezugriff (db_datareader, Entscheidung m.piper 10.10.2026), nur über SSH-Tunnel; Admin, nach Freigabe
- [ ] **Vor dem Livegang:** Lese-Login auf `tools/sql/02_shop_reader_betrieb.sql` umstellen (SELECT nur auf die Tabellen aus `JtlSqlQueries`, neues Passwort, Zugang nur übers VPN)
- [x] **Wawi-SQL (Port 50000) war aus dem Internet erreichbar (J-21):** Regel „SQL“ am 10.10.2026 durch m.piper eingeschränkt – vom Mac aus jetzt zu, von node1 offen (geprüft)
- [ ] Prüfen, welche SQL-Logins (sa?) per SQL-Anmeldung möglich sind; Passwörter im Zuge der Rotation tauschen. **Achtung:** Packtisch+ (und ggf. weitere Wawi-Clients) melden sich als `sa` an → beim `sa`-Wechsel alle Clients umstellen; besser eigenes SQL-Login für den Packtisch statt `sa`
- [x] **SSH auf c10330 funktioniert** (12.10.2026): Portal-Regel eingehend TCP 50022 nur von 136.243.107.86 (Quell-Port leer), Zugang über node1 als Sprungserver, Konto `mpiper` mit `~/.ssh/jtl_admin` (Passphrase, im macOS-Schlüsselbund/Agent), Host-Schlüssel geprüft `SHA256:OyMfxZ5/JvNVXZO6Sm7D9yrHeI+B+46GrEmWcBtpx2A`; vitandi-Schlüssel aus dem lokalen Agent entfernt
- [ ] ~~SSH Port 22 kommt nicht an (J-21/J-22):~~ erledigt, siehe oben – 22 und 50022 werden auf Netzebene bei ecomdata gefiltert (node1 in deren IP-Liste und IPBan-Whitelist, trotzdem Zeitüberschreitung) → ecomdata bitten, TCP 50022 für 136.243.107.86 freizugeben. SQL braucht SSH nicht (50000 für node1 offen)
- [ ] **AnyDesk auf c10330 klären (J-22, Vorfall):** `C:\Users\useradmin\Downloads\AnyDesk-goe.exe` mit Firewall-Freigabe für alle – wer hat es installiert, wird es gebraucht?
- [ ] **Netzwerkweg zur Wawi (E-O):** Site-to-Site-VPN ecomdata ↔ Meesenburg anfragen; Übergang bevorzugt: OpenSSH auf dem JTL-Server (nur Schlüssel, nur Weiterleitung auf 127.0.0.1:1433, Port 22 nur für node1) – Skript `tools/jtl-ssh/01_openssh_einrichten.ps1`, richtet m.piper selbst ein (Server nur teil-managed), dazu eigener Admin-SSH-Zugang statt RDP; sonst SQL-Port nur für eine feste Meesenburg-IP, Verschlüsselung erzwingen
- [ ] Schema-Prüfung der 3 Tabellen + länderabhängige Versandkosten finden (einmalig VIEW DEFINITION)
- [x] **E-H Versandkosten aus der Wawi** (10.10.2026): Preis/Freigrenze/Aktiv aus `tversandart` im Referenzabgleich, Regeln im Shop
- [x] **E-I Variationsaufpreise aus der Wawi** (10.10.2026): alle 549 Variationsartikel bestellbar, Live-Prüfung bei der Bestellung
- [ ] Länderabhängige / gewichtsabhängige Versandkosten (Trello #90) in der Wawi-DB suchen – erst wenn Auslandsversand kommt (E-F)
- [ ] Aufpreise mit dem Stripe-Shop stichprobenartig vergleichen (Vergleichsseite um Variationen erweitern)
- [x] Grundpreis (PAngV, Trello #46) aus der Wawi-DB (P7); Lieferzeit (#59) teilweise (Bearbeitungszeit, Ampel)
- [ ] Fachbereich: Versandregeln prüfen (Startwerte Stripe-Shop: Abholung 5,90 €, „frei ab 10 €“?; Versandklasse „DHL Paket (ab 500)“ hat nur Abholung; Metabo-Spedition 0 €)
- [x] **S6 Bestellung** (10.10.2026): Live-Prüfung in JTL, eingefrorenes Angebot, Nummernkreis S…, Kasse/Bestätigung im .NET-Test-Shop
- [ ] **Mongo-Cluster (E-P):** eigener Benutzer `siseshop-dev` (nur readWrite auf `Dev-Sise-Shop`) + Key-Vault-Secret `SiseShop-Dev` (JSON) anlegen – **nach OK**; danach lokalen Container entfernen (Volume 235 MB)
- [ ] ES-Repo: `nuget.config` enthält das Feed-PAT im Klartext (zusätzlich zum Azure-Secret im Dockerfile)
- [ ] S7–S9 Zahlung, JTL-Übergabe, Mail (Stripe-Test-Keys: `Readme.md:24–26` im Shop-Repo, gelten als kompromittiert → nach der Rotation neue Test-Keys), Auftragsübergabe (Trockenlauf → 1 Testauftrag nach OK), Mail
- [ ] S10 Svelte-Frontend umstellen, S11 Anmeldung/Konto, S12 Betrieb
- [ ] Portal in `Meesenburg.SiseShop.Web` umbenennen (jetzt sinnvoll, da Shop + Admin)
- [ ] Nach dem ersten Vollabgleich: Worker neu starten und zweiten Vollabgleich anfordern (URLs im Stripe-Shop-Format `{name}-{id}`)
- [ ] Fachbereich: Artikel wie „AB-Dummy 81 A,B,C,D,E“ (0,41 €) sind im Shop-Kanal aktiv – interne Konfigurator-Dummys aus dem Shop-Kanal nehmen?

## Sobald der JTL-Zugang da ist (IP-Freigabe)

- [x] **Versionierung der JTL-API prüfen** (08.10.2026, auf dem JTL-Server gegen `localhost`): alle 12 Abfragen mit und ohne `/v1` identisch (Status 200, gleiche Felder), bestätigt mit zweitem Artikel 71679; offen nur `variations/{vid}/values` (Artikel mit Variationen nötig) – kein Erklärungsgrund für die Datenprobleme; neue API nutzt trotzdem durchgängig `/v1`. Ergebnis in Bericht 03, 00, 01 nachgetragen.
  - Betroffen sind die Abfragen **ohne** `/v1` (seit Commit `32709a1`, 25.07.2025):
    - items_svc: `/items/{id}/images`, `/customfields`, `/descriptions`, `/variations`, `/variations/{vid}/values`, `/specialprices`, `/salesChannelPrices`, `/properties`, `/suppliers`
    - business: `/customers`, `/customers/{id}`, `/salesOrders`, `/salesOrders/{id}`, `/salesOrders/{id}/lineitems` (siehe Bericht 03, Abschnitt C)
  - Je Pfad prüfen: Antwortet `/v1/...` überhaupt (Status)? Ist die Struktur gleich wie ohne `/v1`? Welche Version liefert der Pfad ohne `/v1` (Standard = neueste?)?
  - Werkzeuge:
    - Postman-Collection (`postman/`), mit Paaren mit und ohne Version bei Bildern und Kanalpreisen
    - POC-Seite **Schnittstellen-Prüfung**: vergleicht die 8 Artikel-Unterabfragen automatisch (`/v1` gegen den Alt-Pfad)
  - Ergebnis in Bericht 03 nachtragen. Davon hängt ab, ob die neue API durchgängig `/v1` nutzen kann (Plan: ja) und ob „undokumentierte Änderungen“ durch die fehlende Versionsangabe erklärbar sind.
- [ ] Mit dem POC einen **Schnittstellen-Stand speichern** (5–10 typische Artikel: Konfigurator-Grundartikel, Zylinder, Schlüssel)
- [x] Suchparameter für `GET /v1/items` prüfen: `searchKeyWord` funktioniert (10.10.2026, J-10)
- [x] `GET /v1/items/{id}` existiert (10.10.2026; scheitert nur bei Artikeln ohne Kategorie, J-01)
- [x] Steuersatz-Abruf `v1/tax/taxclass/1/6/DE/DE` → 19 % (10.10.2026, Firma 6 richtig)

## Forensik – zuerst sichern, dann sperren, dann Freelancer informieren (Bericht 09)

- [x] **JTL-Server gesichert** (08.10.2026 11:42): `jtl_db_reader` (Custom-API inkl. `logs`, `snapshots`, `.env`, `.bak`), geplante Aufgabe, Firewall-Export, Ereignisprotokolle (Application, Security, RDP), IPBan-Logs; 101 Dateien, 278 MB; ZIP SHA-256 `E579F8CA0FCA06A9AEACA2F43A66840C705F945126993A7FB3F95B972EFB295F`; Kopie verschlüsselt lokal ablegen und Prüfsumme vergleichen; danach Server-Kopie löschen (offen)
- [ ] Custom-API-Code auswerten (u. a. Unterschied `main.py` ↔ `main.py.bak` vom 29./30.09.: Änderung direkt auf dem Produktivserver)
- [ ] Freigegebene IPs der Firewall-Regeln „JTL REST API 50443“ / „ICS Python REST API“ zuordnen (Hetzner ×3, netcup, Telekom-DSL, dänische Netze) und nicht mehr benötigte entfernen

- [ ] **Wer nutzt das geteilte Konto `useradmin` auf dem JTL-Server?** Aktive Sitzung seit 08.10.2026 06:42. Nicht selbst als `useradmin` anmelden (würde die Sitzung übernehmen) – stattdessen eigenes Admin-Konto über ecomdata
- [ ] Mit Admin-Rechten: Pfad/Startbefehl der Python-Prozesse ermitteln, **Custom-API-Ordner als Archiv sichern** (+ SHA-256), auf `.git` und Remote prüfen; Konfiguration mit DB-Zugangsdaten verschlüsselt ablegen, danach Zugangsdaten tauschen

- [x] Zugang zu den Shop-Servern `node1`, `node2`, `node3` (09.10.2026): eigener Benutzer `mpiper` mit Schlüssel `hetzner_shop` und sudo
- [x] **Shop-Server gesichert** (09.10.2026, Stufe A + B, ≈ 18 GB, verschlüsseltes Image „Backup Shop“, `SN24_20261009_0545`, Prüfsummen + Protokoll); Server-Temp gelöscht
- [x] **Sperre interner Endpunkte von außen** (09.10.2026 11:49 UTC, node1): Traefik-Datei `/home/admin/traefik/dynamic/zz-meesenburg-sperre-intern.yml` → `/api/pdf`, `/api/maildata`, `/api/order/send…`, `/api/order/{id}/mark_paid` von außen **403**, intern (Docker-Netze, Server-IP) weiter erreichbar. Geprüft: 8/8 extern gesperrt; Shop, Zahlungs-/Versandarten, Auth OK; Mail-Dienst über öffentliche URL weiter 200. Rücknahme: Datei löschen
- [x] **Ports 1025/8025 (Mailpit) und 10002 (Frontend direkt) von außen gesperrt** (09.10.2026 12:08 UTC, node1): Firewall `DOCKER-USER` (IPv4) + `INPUT` (IPv6) auf `enp130s0`, Skript `/usr/local/sbin/mee-ports-sperre.sh`, Dienst `mee-ports-sperre.service` (aktiv nach Neustart). Geprüft IPv4+IPv6 zu, Shop 200, intern Mailpit erreichbar. Rücknahme: `systemctl disable --now mee-ports-sperre.service && /usr/local/sbin/mee-ports-sperre.sh --remove`. Logs: 10002 nur Scanner; Mailpit-Oberfläche ohne Zugriffslog → Einsicht nicht ausschließbar (DSB)
- [ ] **DSB-Mail zur PDF-/Auftragsdaten-Lücke versenden** (Entwurf `korrespondenz/2026-10-09_entwurf_mail_dsb_pdf_schnittstelle.md`): bis 43.787 Aufträge / ~29.000 Kunden potenziell abrufbar, keine fremden Zugriffe in den Logs 25.09.–09.10.; **72-h-Frist endet 10.10.2026**
- [ ] Google Search Console prüfen: keine `/api/`-URLs indexiert (Briefing, Nachtrag PDF-Risiko); Zugriffe vor 05.10. über netcup-Log-Export auswerten
- [ ] **DB-Dumps bei den Entwicklern** (node1 `db_backups`, zuletzt 01./02.10.2026, per `python3 -m http.server` heruntergeladen): Löschung aller lokalen Kopien schriftlich verlangen; an DSB (Kollege); `db_backups` auf node1 nach Sicherung entfernen (Briefing, Nachtrag 11.10.)
- [x] Offene Root-Sitzungen beendet und Historie gesichert (node1 seit 07.10., node3 seit 09.10.) – 12.10.2026
- [ ] Weiter offen: IDOR `GET /api/order/single_order` und `GET /api/order/?user_id=` (vom Frontend genutzt → nur im Code lösbar), Login-JWT-Schlüssel = Repo-Wert (Bericht 12)
- [x] **Datenabfluss an netcup `85.235.65.67` gestoppt** (09.10.2026 10:20:44 UTC): `monitoring-alloy-1` und `monitoring-cadvisor-1` auf node1 gestoppt, Neustart-Regel `no` (Konfiguration und Container unverändert); 60 s ohne Verbindung geprüft, Shop HTTP 200
- [x] Mail an Christopher Seidel (neogris), **nur Monitoring-Server**: Export Loki-Logs/Prometheus-Metriken/Grafana-Dashboards seit Beginn, danach Löschung inkl. Kopien und Bestätigung; Frist **12.10.**; Upload-Link bereitstellen. Stand 09.10.: **Export eingegangen** (3,1 GB, im Image `netcup_export_20261009/`); Loki nur 14 Tage (ab ~25.09.) → Zeitraum 17.08.–24.09. nicht mehr belegbar; Auswertung abgeschlossen (25.09.–09.10.: nur eigene Tests, kein fremder Zugriff). Erhalt bestätigt, **Löschbestätigung von neogris per Mail erhalten (09.10.2026)**; `~/Downloads/monitoring_export.zip` gelöscht; SharePoint-Kopie bleibt vorerst (nur M. Piper hat Zugriff) Erster Versuch per Wormhole-Link fehlgeschlagen → neue Übergabe per SharePoint-Dateianforderung (leerer Ordner, nur Upload, Ablaufdatum); nach Eingang SHA-256 bilden und ins Image „Backup Shop“ – Entwurf `korrespondenz/2026-10-09_entwurf_mail_neogris_datenherausgabe.md` (Versand durch Nutzer); DSB informieren
- [ ] Herausgabe weiterer Daten (Code-Stände, Custom-API-Quellcode, Strapi-Global, `erp-demo.meesenburg.neogris.com`) separat anfordern – nach Kündigungsentscheidung
- [ ] **Mail an beide Entwickler** (Rückkehr zum Altshop, Dienstleistungen ausgesetzt, Verträge laufen weiter, alle Zugänge werden erneuert, Abstimmung mit Frau Dethleffsen/GF): Entwurf `korrespondenz/2026-10-12_entwurf_mail_entwickler_aussetzung.md` – Versand zeitgleich mit der Aussperrung
- [x] **Hetzner node1–3 ausgesperrt** (12.10.2026): Entwickler-Schlüssel und private Schlüssel entfernt, sshd nur noch `mpiper`, geprüft
- [x] **Azure DevOps / Entra** (12.10.2026): Seidel und Bräuer aus der Organisation entfernt, Gastkonten gesperrt, Sitzungen widerrufen; Audit-Log gesichert (Seidels DevOps-SSH-Schlüssel wurde von 217.74.152.246 angelegt = IP der Root-Sitzungen)
- [x] **JTL-Server: Windows-Passwörter rolliert** (12.10.2026, durch M. Piper; geschützt: mpiper, cpsadmin, Administrator, IcingaUser); RDP-Sitzungen per `tsdiscon` getrennt
- [x] **JTL-Windows-Firewall** (12.10.2026): 50443/58080 nur noch node1; geprüft: node1 erreicht beide Ports, node2/node3/extern Timeout, Shop 200
- [ ] ecomdata-Kundenportal: Fremd-IPs entfernen (eigene IPs behalten)
- [x] Hetzner-Robot: Passwort geändert, SN24-Schlüssel entfernt (12.10.2026, M. Piper)
- [x] ecomdata-Kundenportal: Passwort geändert (12.10.2026); Mail an die Entwickler versendet (12.10.2026)
- [ ] ecomdata-Portal-Firewall c10330: Gruppen `remotedesktop`, `sql_ports` (50000 von node1 offen), `jtl_ports`, `ftp` öffnen und Fremd-IPs entfernen (DK-Netze, 79.200.133.76, 152.53.32.159, node2/3); `ftp` deaktivieren, falls ungenutzt; RDP bleibt bis S2S-VPN offen (nur eigene IPs)
- [x] Testserver `c22910` gesichtet (12.10.2026, ZIP im Image `c22910_20261012/`) – Befunde Briefing, Nachtrag 12.10.
- [x] c22910: Passwörter rolliert (12.10.2026, M. Piper) – Seidel war dort bis 12.10. 09:48 per RDP verbunden (217.74.152.246), Sitzung getrennt
- [x] Packtisch ohne Wawi-DB (12.10.): Ursache SQL-Regel seit 10.10. nur LocalSubnet+node1 → 91.190.224.34 ergänzt (10:45)
- [x] Packtisch+ läuft wieder (12.10.): SQL-Regel um 37.228.146.166 ergänzt, Restfehler war Packtisch+-Konfiguration
- [ ] Packtisch: tatsächliche Ausgangs-IP 37.228.146.166 (12:49 in SQL-Regel ergänzt; 91.190.224.34 = TKRZ-Web-Proxy, bleibt drin) – laut RDP-Verlauf 01.09.–12.10. durchgehend dieselbe IP (sehr wahrscheinlich fest), formal bestätigen; Firewall-Team klärt Weg über eigene Infrastruktur, sonst Site-to-Site-VPN vorziehen; prüfen, ob ecomdata-Portalgruppe `sql_ports` diese IP enthält; weitere Rechner mit direktem DB-Zugriff erfassen
- [ ] Klären (Nachfrage beim Kollegen läuft): `useradmin`-Wiederverbindung c10330 am 12.10. 09:26/09:27 von 37.228.146.166 (Sitzung 6 übernommen, dann 27) – Passwort war seit 08:38:07 neu → die Person kannte das **neue** Passwort; ebenso nord1/nord2 (neu 08:38, wieder verbunden 08:48/08:50). Falls nicht autorisiert: `tsdiscon 27`, `useradmin` erneut rollieren, Passwort-CSV `C:\tmp\pw_rotation_*` löschen. `nord4` 06:37/06:58 von 45.145.239.72 (NL)?
- [ ] RDP-Herkunft klären: `useradmin` von 87.132.78.136 (29.09.–07.10.) und 87.133.85.157 (02.–23.09., auch nord4) – Telekom-DSL, wer? `cpsadmin` von 81.189.65.47 (AT) bei ecomdata bestätigen
- [ ] Eigene Admin-Konten für Kollegen statt gemeinsamem `useradmin`; `useradmin` nur noch für JTL-Worker/Custom-API-Aufgabe
- [ ] **c22910 härten:** (0) ~~eigenes Admin-Konto `mpiper` anlegen~~ vorhanden (18.11.); ~~kein SSH auf dem Testserver~~ Entscheidung geändert 18.11.2026 (M. Piper): SSH wie c10330, nur von node1, siehe Abschnitt „Zugang ecomdata-Server“; (1) `jtl_db_reader`-Ordner sichern (`sichtung_windows_server.ps1 -CopyPaths 'C:\Users\useradmin\Documents\jtl_db_reader'`), (2) aus der mpiper-Sitzung `rotate_local_passwords.ps1 -IncludeInteractive -Apply -UpdateDependencies` (rolliert useradmin + meesenburggmbh1), später `useradmin` deaktivieren, (3) ~~Sitzung `useradmin` (ID 2) trennen~~ erledigt 12.10. (tsdiscon 2); Sichtung mit `jtl_db_reader` = `sichtung_C22910_20261012_093522.zip` (SHA-256 41CA8561…D9BFF) noch ins Image, (4) Portal-Firewall c22910 bereinigen (SQL/JTL-Gruppen: Fremd-IPs raus), Windows-Regeln SQL/50443/58080 einschränken, WinRM 5985 „Any“ → LocalSubnet, (5) Security-Log vergrößern; (6) klären, ob Produktivdaten in der Test-DB (ecomdata/sa) und ob der Server überhaupt noch gebraucht wird
- [ ] SSH auf ecomdata-Servern (Port 50022?): nur Schlüssel, eigenes Konto, nur eigene IPs – Entscheidung offen
- [x] JTL-Shop unter Test-Domain `sicherheit.meesenburg-service.de` erreichbar (12.10., `URL_SHOP` in `config.JTL-Shop.ini.php` angepasst); SQL-Regel c10330 um 178.63.45.204 ergänzt (14:00)
- [ ] Prüfen, ob der JTL-Shop SQL 50000 wirklich nutzt (Verbindungen von 178.63.45.204 beobachten), sonst wieder entfernen; ecomdata-Portalgruppe `sql_ports` muss die IP ggf. ebenfalls enthalten
- [ ] Vor dem Umschalten: `URL_SHOP` zurück auf `https://sicherheit.meesenburg.com`, Cache leeren; SFTP/FTP-Zugänge bei Timme ändern (gelten als kompromittiert)
- [x] **Shopwechsel zurück auf den JTL-Shop 5** (DNS umgestellt, JTL-Shop live; Stripe-Shop (node1) seit 19.10.2026 06:44 UTC von außen gesperrt: 80/443 per Firewall, `mee-shop-offline.service`, Container laufen weiter)
- [x] PHP2Shell (CVE-2026-109618/-109619, 21.10.2026): node1–3 geprüft (22.10.) – **kein PHP** auf Hosts und in allen Containern (Strapi = Node.js), kein Update nötig; JTL-Server laut M. Piper gepatcht
- [x] node2 (Testinstanz) von außen gesperrt (22.10.2026 09:24 UTC): 80/443/1025/8025/10002, `mee-node2-sperre.service`, Container laufen weiter; Rücknahme: `systemctl disable --now mee-node2-sperre.service && /usr/local/sbin/mee-node2-sperre.sh --remove`
- [ ] Nach dem Wechsel: Stripe-Webhook-Endpunkt des Stripe-Shops im Dashboard deaktivieren (sonst fehlschlagende Zustellungen); offene Bestellungen/Zahlungen des Stripe-Shops seit dem Wechsel prüfen; entscheiden, ob Worker (items/business) auf node1 gestoppt werden; DNS `mb-sn24.sustainable-solutions.it` zeigt noch auf node1 (Entwickler-Domain)
- [ ] **21.10.2026: JTL-Shop `sicherheit.meesenburg.com` nicht erreichbar – Ursache: Timme hat vorsorglich alle Websites + ISPConfig abgeschaltet** (Statusseite timmehosting-status.com, Update 13:31 UTC) wegen PHP-Lücken „PHP2Shell“ CVE-2026-109618/109619 (CVSS 9,2, aktiv ausgenutzt). Freischaltung nach Einspielen der PHP-Updates (8.5.12/8.4.27/8.3.36/8.2.35), Zeitraum bis spätestens 23.10. 07:00 UTC angekündigt, kein fester Termin. Nichts zu tun außer beobachten; nach Freischaltung prüfen: Shop erreichbar, PHP-Version gepatcht, Wawi-Abgleich, Bestellungen. Neues .NET-Backend nicht betroffen (kein PHP)
- [x] PHP-Version JTL-Shop (27.10.2026): JTL-Shop 5.5.2 auf PHP 8.2 (8.2.35 vom 21.10. → gegen PHP2Shell abgesichert), Bericht 17
- [x] **Sichtbarkeit je Kundengruppe** aus der Wawi in den .NET-Shop übernommen (27.10.2026, P2) (JTL-Shop blendet 1.041 Artikel für Endkunden aus, Bericht 17) – Wawi-Tabelle prüfen, SQL-Broker erweitern
- [x] Kategorie-Rabatt nach JTL-Regel umgesetzt (27.10.2026, P3; 205/205 wie JTL-Shop) (MAX über Kategorien, gilt auch für Aufpreise, MIN mit Sonderpreis; Gäste = Kundengruppe 1) – umsetzen nach Entscheidung F-01; Testfälle 41166/33835 aus Bericht 17
- [x] Parität P7 Grundpreis, P8 Verfügbarkeit, P10 Gratisgeschenke (27.10.2026, wie JTL-Shop; J-27 Lagerflags aus der Wawi-DB)
- [ ] Parität offen (Plan 5a): P11 Gutscheine (Daten im JTL-Shop `tkupon`), P13 Zahlarten, P14 Mail-Absender (mit S9), P12 Ausland (nach F-15), P9 „Angebot anfordern“
- [x] R-2: keine Rabatt-Prozentangabe mehr (Hersteller untersagen Rabattwerbung), durchgestrichener Normalpreis bleibt (30.10.2026)
- [x] S-1: alle Konfig-Positionen „nur Konfigurator“ (`CatalogSync:HideConfigPositions`, 1.477 statt 429 Artikel) (30.10.2026)
- [ ] **Grundsatzentscheidung Shop-Oberfläche und Inhaltspflege** (Bericht 20, 04.11.2026): Empfehlung Option B (.NET-Shop komplett mit eigenem Inhaltsmodul statt Svelte/Strapi). Vorher Prototyp „Inhalte“ (2–3 PT) für das SN24-Team, Fragen an das Team in Bericht 20 Abschnitt 8. Entscheidung: M. Piper
    - ✅ 04.11.2026 Prototyp „Inhalte“ gebaut (Bericht 20 Abschnitt 10) – nächster Schritt: SN24-Team ausprobieren lassen (Test-Personen „Rita Redaktion“ bzw. „Erik Entwurf“), Fragen aus Abschnitt 8 klären
    - ✅ Admin umgebaut (Bereiche, Rechte, Profil), Berechtigungen mit `src/Meesenburg.Authorization` (Vorstufe gemeinsames Paket mit dem SiSe-Portal, README)
    - [ ] Entra-App-Registrierung „Shop-Admin“ + Gruppen `grp_mee_sise_shop_content|sales|operations|admin` (IT)
    - [ ] Gemeinsames Paket `Meesenburg.Authorization` für SiSe-Portal und Shop (ES-Katalog als `IPermissionCatalog`, AuditLog übernehmen), dann eine Rechte-Datenbank
    - ✅ 04.11.2026 Bereich oben auf Artikelseiten (neben dem Bild), neue Bausteine Tabelle/Kennzahlen/Vorteile/Akkordeon
    - [ ] Inhalte: Produktliste nach Kategorie, Bilder in den Blob Storage, SEO-Adressen/Weiterleitungen (alte Links auf sicherheitstechnik-nord.de in den Inhalten)
    - ✅ 04.11.2026 Design-Richtlinie Shop (`src/Meesenburg.Shop.Portal/DESIGN_SHOP.md`), Knöpfe blau statt Orange-Rot, Banner in 3 Varianten, JTL-Inhalte beim Import bereinigt
    - ✅ 04.11.2026 Farbvariante **B** entschieden (M. Piper): Knöpfe Markenblau; Angebote ab 05.11. rot (Marketing)
    - ✅ 04.11.2026 Alle 237 übernommenen JTL-Inhalte überarbeitet (140 Seiten/Kategorien, 97 Artikel): oben kompakt, Details im Akkordeon/unten, ~110 Bilder mit eingebranntem Text als Tabelle/Kennzahlen/Banner; 17 veraltete aus dem Shop. Daten: `analyse/workshop/inhalte/`, Einspielen `tools/inhalte_einspielen.py`
    - [ ] Fachabteilung entscheidet die Inhalts-Fragen **F-37 bis F-46** (Bericht 16: Lieferzeiten/Express, Kontakt, Ansprechpartnerin mit Foto, Aussagen alter Shop, Farbnamen, Grafik-Fehler, Knopf-Ziele, Videos, leere Seiten); Details je Seite `analyse/workshop/Pruefliste_Inhalte.md`. Inhalte sind im POC eingespielt
    - ✅ 05.11.2026 Angebote in Rot statt Gelb (Marketing-Freigabe): Sonderpreis-Marke, Sonderpreis, Hervorhebung „Aktion“
    - ✅ 05.11.2026 SEO-Kopf wie JTL-Shop: Titel = SEO-Titel aus der Wawi bzw. Artikelname ohne Shopnamen, Meta-Beschreibung mit JTL-Präfix, Schlüsselwörter, canonical, Open Graph (J-35)
    - ✅ 05.11.2026 Adressen wie im JTL-Shop (J-36): flach unter `/` ohne `/shop/` und ohne Id (`/Kaba-penta-cross-Doppelzylinder`, `/DORMAKABA-penta-cross`, Hersteller `/Kaba`, Seiten `/schliessanlagen-information`); 6.804 JTL-Pfade und 8.916 Weiterleitungen übernommen (`POST /api/v1/admin/seo/jtl-import`, Export per `q.php`), alte .NET-/Stripe-Adressen → 301; Links in Inhalten auf den alten Shop werden zu eigenen Pfaden
    - ✅ 05.11.2026 Workshop „Funktionen“ mit dem SN24-Team vorbereitet: `analyse/21_workshop_funktionen_sn24.md` + `analyse/workshop/Workshop_Funktionen_SN24.xlsx` (38 Funktionen mit Nutzung im JTL-Shop, Vorschlag, Prio, PT; 10 Fragen). Neue Fragen F-47 (Merkmal-Seiten umleiten + Filter) und F-48 (News weglassen / Ratgeber) in Bericht 16
    - [ ] Workshop-Termin mit dem SN24-Team, Innendienst und Marketing vereinbaren; Ergebnis in Bericht 14 (5b), 16 und hier
    - [ ] SEO-Lücken: Merkmal-Filterseiten (686, z. B. `/24x2170x3-mm`), Suchseiten (895), News (86) und JTL-Sonderseiten (Kontakt, Zahlung-und-Versand, Upload-Formular) gibt es im .NET-Shop nicht → 404; leere Kategorien (70) ebenfalls. Entscheiden: nachbauen oder auf Kategorie/Startseite umleiten
    - [ ] Vor Go-live: JTL-Pfade und Weiterleitungen erneut exportieren und importieren (Stand 05.11.2026)
- [x] **V1 Pflichtseiten + Kontakt** (05.11.2026): Rechtstexte aus dem JTL-Shop unter den JTL-Adressen, gegliedert; Pflichtlinks im Fuß; Kontaktformular `/kontakt` + „Frage zum Artikel?“; Mail an `Mail:ContactRecipients` (im Betrieb sicherheit@meesenburg.de eintragen)
- [x] Rechtstexte stattdessen aus dem Strapi des Stripe-Shops übernommen (05.11.2026, Wunsch M. Piper: aktueller; nennen sicherheit.meesenburg.com): `tools/rechtstexte_strapi.py`. Strapi-Texte sind Händlerbund-Texte (AGB: „Händlerbund … garantiert für die Rechtssicherheit“), Datenschutz „letzte Aktualisierung 02.12.2022“ → vom Fachbereich/Rechtsbeistand prüfen lassen (Stripe, Google, Trusted Shops erwähnt?)
- [ ] Händlerbund-Abruf automatisch (C-4): Schnittstelle bekannt (`legaltext-cache.haendlerbund.de/cache/?did=…&mode=html`, Dokument-Ids AGB 12766C46A8A, Widerruf 12766C53647, Datenschutz 160DEDA9674, Zahlung/Versand 12766C58F26, Impressum 1293C20B491, Batterie 134CBB4D101; J-38). Braucht das Zugangstoken aus dem Händlerbund-Konto (Marketing) im Key Vault – dann täglicher Abruf statt Übernahme
- [ ] Kopfzeile (Standorte, Service, News, Kontakt, FAQ) prüfen: „News“ entfällt (F-48), Ziele mit V2 (Menü im Admin) pflegbar machen
- [x] **V2 + Selbstverwaltung** (05.11.2026, Bericht 21): Admin „Menüs & Hinweise“ (Kopfzeile, Hauptmenü, Fußzeile mit Pflichtlinks, Hinweisleiste mit Zeitraum, Wartungsmodus, Google-Ids), „Weiterleitungen“ (inkl. 404-Liste), „Gutscheine“ (8 aktive JTL-Kupons übernommen), Google-Shopping-Feed `/google-shopping.xml`, Cookie-Einwilligung; neue Berechtigungen `marketing.read/manage`, `shop.settings.manage` (Startrollen werden automatisch ergänzt); 286 Tests
- [x] **V3 SEO** (05.11.2026): `/sitemap.xml`, `/robots.txt`, schema.org Product/BreadcrumbList; Feed und Sitemap nehmen die Adresse, unter der das Portal aufgerufen wird (im Betrieb Ingress-Header prüfen: `Portal:TrustedProxies`)
- [ ] V3 Rest: Merkmal-Seiten (686) und News (86) – **zurückgestellt (05.11., M. Piper): vorerst nicht umleiten**; bei Unsicherheit erst 302. Sitemap in der Google Search Console einreichen
- [x] Weiterleitungen wahlweise 302 „vorübergehend“ (05.11.2026): Häkchen im Admin; 301 mit `Cache-Control: max-age=86400`, damit Browser eine Änderung nach einem Tag übernehmen; 302 mit `no-store`. JTL-Übernahme bleibt 301
- [x] **V5 Bestellabschluss** (05.11.2026): unbezahlte Online-Bestellungen nach 48 h storniert (Stripe-Zahlung abgebrochen, SEPA „in Bearbeitung“ nie), Klärungsmail bestand, AGB/Widerrufsbelehrung als PDF an Vorkasse-Mail und Bestellbestätigung, Gutschein-Nutzung bei Storno zurück
- [ ] V5 Nacharbeit: Frist 48 h vom Fachbereich bestätigen lassen (F-33/F-21); soll der Kunde beim Storno eine Mail bekommen? (heute nicht – er hat ja noch keine Bestätigung); Roboto-Schrift ins Worker-Image (PDFs nutzen sonst die Systemschrift, Überschriften dann nicht fett)
- [x] **V4 statisches Rendering** (05.11.2026): öffentliche Seiten ohne Blazor-Verbindung, Warenkorb im Cookie, Formulare statt Klick-Ereignissen, Insel nur für Variationen/Konfiguration (Bericht 14, 5b)
- [ ] V4 Nacharbeit: veralteter Artikelname leitet per 302 statt 301 (Sonderfall `CanonicalSlug`, selten); Seiten-Zwischenspeicher (Output-Cache) erst nach Lasttest V9 entscheiden – die Seiten tragen je Besucher ein Antiforgery-Token
- [ ] Gutscheine: Wawi-Position mit negativem Preis beim ersten Testauftrag prüfen (REST 1.1); Kundengruppen-Einschränkung erst mit K4; „Persönliches Angebot“-Kupons entstehen im JTL-Shop laufend (Plugin „Angebot anfragen“, B-7) – bis zur Umschaltung neue Kupons erneut übernehmen
- [ ] Google: Analytics-/Ads-Ids und Merchant-Center-Feed-Adresse eintragen (Marketing); Datenschutzerklärung um Google/Consent ergänzen; **Google Fonts lokal ausliefern** (heute von fonts.googleapis.com = Datenübertragung ohne Einwilligung)
- [ ] **Entscheidungshelfer im Konfigurator vor dem Livegang** (K-3, Entscheidung 10.11.2026, Bericht 14 §5d): 7–9 PT, optional KI-Freitext +2–3 PT. **Erste Fassung läuft (10.11.2026):** Fragebogen, Bewertung, Übergabe an den Planer, Admin-Pflege. Offen: F-51 mit dem SN24-Team (Fragen, Schwellen, Texte je System pflegen), Auswertung Antworten → Bestellungen, Feinschliff nach Probelauf, optional KI-Freitext
- [ ] **Backend deckt alles vom Python-Backend ab (10.11.2026, Bericht 14 §5c)** – erledigt L1, L3, L5, L6, L7; L2 zurückgestellt; L4 nach Prüfung F-49 beim Termin. Betrieb: Rechte aus `tools/sql/02_shop_reader_betrieb.sql` (neu: `Verkauf.tAuftrag`, `tLieferschein`, `tVersand`). Ursprünglich: L1 Sicherungskarte hochladen, L2 Bestellstatus ohne Konto (Nummer + E-Mail), L3 Wawi-Status-Abgleich, L4 Mail „Zahlung eingegangen“ (+ Versand/Storno nach F-49), L5 Schließpläne am Konto, L6 Registrierung bestätigen (F-50), L7 Warenkorb am Konto. Nur bei Option A zusätzlich S1–S4 (Svelte-Anbindung)
- [ ] **Vorstellung .NET-Shop diese Woche (GF, SN24-Team, IT):** Folien und Drehbuch fertig (10.11.2026, `analyse/vorstellung/`). Vorher: Demo-Gutschein anlegen, Mailpit leeren, einmal durchspielen; Demo nur mit Vorkasse, solange der fremde Stripe-Test-Webhook steht. Danach Entscheidung (Option B) und Ansprechpartner eintragen
- [ ] **Livegang Version 1 (Bericht 14, Abschnitt 5b):** selbst bauen V1 Pflichtseiten + Kontakt → V2 Hinweisleiste/Wartung/Menü → V3 SEO-Rest → V4 statisches Rendering → V5 Bestellabschluss → V6 Gutscheine → V7 Google → V8 Betrieb → V9 Abnahme; von anderen: Entra-App, Graph Mail.Send, Stripe live, PayPal-Zugang, Händlerbund/Trusted Shops/Google, AKS/Key Vault/ecomdata-IP, K2-Export, OK Schreibsperre, Fachfragen
- [ ] AKS: Speicher zu 76–99 % belegt – eigener Knotenpool für den Shop; ES: Aspire-Dashboard produktiv (1,8 GiB), Requests realistisch setzen (Hinweis an ES)
- [ ] node2 (Testinstanz der Freelancer, spricht mit Produktiv-JTL) abschalten – unabhängig von der Entscheidung
- [ ] **Neu aus dem Workshop (Bericht 16), in dieser Reihenfolge vorgeschlagen:**
    1. ~~K-3 Schließplan-Link~~ ✅ 30.10.2026: Plan speichern (Link `/konfigurator/plan/{id}`, 128 Bit, 180 Tage bzw. dauerhaft nach Bestellung), Link in Kunden- und **Innendienst-Mail** (neu `InternalLockPlanOrder`: Kunde, Telefon, Matrix, Positionen, PDF im Anhang), PDF-Download. Offen: Schrift Roboto (Apache 2.0) ins Container-Image (sonst Systemschrift), interne Empfänger festlegen (F-26/F-33: Adressen wie bisher)
    2. ~~F-21/F-20 Vorkasse~~ ✅ 30.10.2026: läuft ohne Stripe; stündlich Zahlstand/Storno aus dem Wawi-Auftrag (REST, J-32), **Zahlungserinnerung nach 7 Tagen**, **Hinweis an den Innendienst nach 14 Tagen** (`Prepayment:ReminderDays/OverdueDays`), Admin-Knopf „Zahlung eingegangen“, keine automatische Stornierung (F-21 B). Später: Erkennung direkt aus dem SAP-Kontoauszug mit ES (NOSPA, Konto 80060, Bestellnummer im Verwendungszweck) – würde K. Petersen das Buchen in der Wawi abnehmen; Fristen 7/14 Tage vom Fachbereich bestätigen lassen
    3. ~~F-24 Widerrufsformular~~ ✅ 30.10.2026: `/vertrag-widerrufen`, Link „Vertrag widerrufen“ im Fuß jeder Seite und an der Bestellung (Nummer vorbelegt); wie JTL-Plugin `css_WithdrawalForm` (Pflicht Name + E-Mail + Kenntnisnahme, Bestellnummer/Nachricht freiwillig, „Widerruf bestätigen“), Eingangsbestätigung mit Inhalt und Zeitpunkt an den Kunden, interne Mail an `Mail:WithdrawalRecipients` (JTL: sicherheit@meesenburg.de; „Antworten“ geht an den Kunden), Zuordnung zur Bestellung bei passender Nummer + E-Mail. Spam: Drosselung + Fangfeld statt Google reCAPTCHA. Offen: Seiten `/datenschutz`, `/widerruf`, `/agb` (F-25), Bestellauswahl für angemeldete Kunden (mit F-17), Text vom Rechtsbeistand prüfen lassen
    4. F-17/F-05 Kundenkonten: Anmeldung wie bisher (JTL-Passwort-Hashes), gleiche Kundennummern, Gegenprüfung mit Kundenübernahme des Stripe-Shops; Händlerpreise; KG-Sicht (Außendienst) später
       - Analyse ✅ 30.10.2026 (Bericht 19): 8.141 Konten, alle Hash-Verfahren prüfbar, Zuordnung über `tInetKundeShop`; Plan K1–K5
       - Entscheidungen 04.11.2026: D1 einmaliger Export durch Meesenburg selbst, D2 `BCrypt.Net-Next` ok, neue Hashes **Argon2id** (OWASP) statt PBKDF2 ✅
       - ✅ 04.11.2026 K1 + K3 gebaut und lokal getestet: Anmelden, Registrieren, Passwort vergessen/ändern, Mein Konto (Bestellungen, Daten, Adressen), Kasse vorbelegt, Bestellung am Konto, Widerruf mit Bestellauswahl, Admin „Kundenkonten“, 4 Testkonten (je Hash-Verfahren, Passwörter in `src/Meesenburg.SiseShop.Api/appsettings.Development.json`)
       - ✅ 10.11.2026 Schnellanmeldung mit Testkonten auf `/konto/anmelden` (nur lokal), Beispielbestellungen für Endkunde/Händler, Telefon hinterlegt
       - [ ] K2 Übernahme aus dem Export (Format mit uns abstimmen: kKunde, E-Mail, Hash, Gruppe, Datum; Wawi-Zuordnung über `tInetKundeShop` liest der Shop selbst)
       - [ ] K4 Preise je Kundengruppe (Händler netto, KG per Schalter aus)
       - [ ] K5 Gegenprüfung Stripe-Shop (`auth_mongo`, Lesezugang D5) – deren Argon2-Hashes sind jetzt direkt prüfbar
       - [ ] Wawi-Aufträge aus dem JTL-Shop in „Meine Bestellungen“ (nach K2)
       - [x] Drosselung je Kunden-IP (04.11.2026): Portal gibt die Browser-Adresse mit (`X-Client-IP`), die API glaubt ihr nur mit internem Schlüssel (`X-Internal-Key`, AppHost-Parameter `internal-api-key`); gilt für Bestellung, Widerruf und Konto. Konto-Endpunkte jetzt 20/min je Kunde. Prüfung: Admin → System „Drosselung je Kunde“
       - [ ] Betrieb: Schlüssel in den Key Vault (`InternalApi:Key` für die API, `ShopApi:InternalKey` für das Portal) und Adressen des Ingress in `Portal:TrustedProxies` eintragen
       - [ ] Login-Tickets liegen im Speicher des Portals → bei mehreren Portal-Instanzen verteilter Cache nötig
       - Sicherheitsbefund: XTEA-Schlüssel des JTL-Shops und eine Kunden-E-Mail im Stripe-Shop-Code (`auth_svc/migrate_jtl_data.py`)
    5. F-20 PayPal direkt (eigenes Konto, ohne Stripe) + „Sofort per PayPal“-Button
    6. F-15 weltweit liefern, Länder ausschließen (Wawi bzw. Shop-Admin), Versandkosten je Land; Umsatzsteuer Ausland klären
    7. K-3 smarter Konfigurator: Fragen (Sicherheit, Objekt, Nutzung, Sicherungskarte, Preisklasse) → Filter auf die Systeme
    8. F-35 neue Schließanlagen nach Omega³-Schema automatisch übernehmen (prüfen, was heute noch gepflegt werden muss: Gruppenrollen/Code-Artikel in `lockplan-anlagen.json`)
    9. F-30 Versandkosten als Wawi-Versandposition statt Artikelposition analysieren (Packtisch)
    10. F-36.1 „3 inklusive“ je Hersteller einstellbar (je Schließung / je Zylinder), Liste von Karina
- [ ] Artikel 6-16711 heißt nur „Größe 10-45 mm“ (normaler Artikel, keine Konfig-Position) – Fachbereich prüfen
- [x] **Großer Konfigurator – POC** (27.10.2026, Bericht 18 B): `/konfigurator`, 12 Systeme, Typ nach JTL-Plugin + Vergleich Stripe-Regel, Warenkorb/Bestellung/Mail/Wawi-Notiz mit Schließplan. Offen für Phase 2: PDF-Angebot, Teilen-Link (nicht erratbar, #89), „Konfiguration bearbeiten“ aus dem Warenkorb (#12), Fragebogen/Hersteller-Seiten wie Stripe-Shop, Regel F ohne Zentraltür (Fachworkshop)
- [x] **Großer Konfigurator nach Stripe-Systematik** (27.10.2026, Bericht 18 C): 18 Anlagen-Systeme, Golden-Tests O³/gemini pluS bis auf Rundung R-1, JTL-Variante als Vergleich
- [x] Konfigurator F-36 entschieden (Workshop 29.10.2026, Bericht 16): Schlüsselcode-Zuschlag je Schlüssel bei D/E/F eingebaut (nur Keso hat die Position in der Wawi → Liste), Typvergleich nicht mehr angezeigt (wird weiter berechnet), Schließfunktion weg, Zuschlag je Zylinder × Menge
- [x] **Workshop 29.10.2026** durchgeführt, Entscheidungen in Bericht 16 („Workshop 29.10.2026: Entscheidungen“); Listen für die Kollegen: `analyse/workshop/Listen_Fachbereich_2026-10-30.xlsx` (K-2 Kanal Konfigurator 243 Artikel, S-1 Konfig-Positionen, F-08, F-10a/b, F-36.2) – erzeugt mit `tools/fachbereich_listen.py`
- [x] **Bilder** (28.10.2026): Vorladen im Referenzabgleich (300/Lauf), dynamische Größen bis 1600 px (proportional, nie aufgefüllt; WebP Shop, JPEG Mail), Abgleich-Pause 1/5/15 min nach Fehlern
- [ ] **JTL-Shop ↔ Wawi-Abgleich gestört seit 27.10.2026 16:10** (28.10. geprüft, nur lesend): **Ursache: nginx-Umleitung für dbeS fehlt.** Die Wawi ruft `/dbeS/mytest.php` usw. auf; diese Dateien sind in JTL-Shop 5 leer und müssen per nginx auf `/dbeS/index.php?id=<name>` umgeleitet werden. Seit 16:10 liefert der Server die leeren Dateien direkt aus (Wawi-Logbuch: „Der Shop hat keine Antwort geliefert“, `DbeSEmptyResponseException`); `/dbeS/index.php?id=mytest` antwortet weiter korrekt. Zugangsdaten unverändert (Wawi `tShop` zuletzt 2025, Shop `tsynclogin` seit MySQL-Start 20.10. unverändert), Shop-Dateien unverändert, kein Zusammenhang mit den SSH-Tunneln. Vermutlich hat Timme die Webseiten-Konfiguration in ISPConfig neu geschrieben (SSH-Zugang/Jail für `c446308mpiper` am selben Nachmittag, 16:42) und dabei eine von Hand eingetragene Umleitung verloren. **Zu tun (Timme):** in den nginx-Direktiven von web33 `location ^~ /dbeS/ { rewrite ^/dbeS/([a-zA-Z_]+)\.php$ /dbeS/index.php?id=$1 last; }` wieder eintragen (dauerhaft in ISPConfig, nicht nur im vhost); danach Abgleich prüfen, 3 offene Bestellungen kommen nach
- [ ] **Rundung R-1 entschieden: B, brutto je Stück** (Wawi rechnet laut Fachbereich genauso) – mit Trockenlauf prüfen, dass Wawi-Auftragssumme = Shop-Summe (Brutto-Positionspreise an REST?)
- [x] **Kleiner Konfigurator (S9a)** gebaut (27.10.2026, Bericht 18 A): 3.701 Artikel konfigurierbar, ohne Konfiguration nicht bestellbar; Beispiel 30558 = 100,33 € wie JTL-Shop
- [ ] Kleiner Konfigurator, offen: „Konfiguration bearbeiten“ aus dem Warenkorb (#12), Gruppenbilder, Mengen-Endrundung (R-1), Vaterartikel → Kind auflösen (3.320 Hauptartikel sind Variantenkinder), Spezialpositionen ohne Artikel (heute keine), Umstellung auf REST v2 `parentSalesOrderLineItemId` mit Wawi 2.1
- [ ] **S9 Mails – für Graph benötigt (IT):** App-Registrierung in Entra ID mit Anwendungsberechtigung `Mail.Send` (oder vorhandene ES-Graph-App), **auf das Shop-Postfach beschränkt** (Exchange „RBAC for Applications“ bzw. Application Access Policy); Shop-Postfach (Shared Mailbox, Absender F-26); Client-Secret in den Key Vault; SPF/DKIM/DMARC der Absenderdomäne prüfen. Danach Testmail nur an `meesenburg.de` (AppHost: `SiseShop:Mail:Mode = Graph`)
- [ ] S9 Mails: Absender sicherheit@meesenburg.de gesetzt (F-26); AGB + Widerrufsbelehrung als **PDF-Anhang** (F-33 A); Versandmail bleibt in der Wawi; Gestaltung am Ende. IT: geteiltes Postfach so einrichten, dass alle Kollegen die gesamte Kommunikation sehen (Exchange Online? gesendete Elemente der anderen)
- [ ] Artikel 71529: Wawi erlaubt Überverkauf, JTL-Shop nicht (J-27) – nach dem nächsten Wawi-Abgleich des JTL-Shops erneut vergleichen
- [ ] Gratisgeschenk-Position in der Wawi-Übergabe beim nächsten Testauftrag mit prüfen (0-€-Position)
- [ ] Artikel 48518 (BKS Janus): JTL-Shop zeigte am 27.10. kurzzeitig einen anderen Grundpreis als die Wawi – beim nächsten Abgleich erneut vergleichen
- [ ] Versandarten: Wawi- oder Shop-Konfiguration maßgeblich? (JTL-Shop: eigene Ids, EU/CH/Welt) – mit F-15 klären
- [ ] SSH-Benutzer `c446308mpiper` (Timme, web33) nach Abschluss der Analyse wieder löschen oder deaktivieren
- [x] **Azure auf PHP2Shell geprüft (21.10.2026, nur lesend):** Web-Apps Linux = Node/Python, ITMX-ASE-Apps Node 22, AKS `aks-meesenburg-prod` nur .NET-/nginx-/Infrastruktur-Images (eigene Images ohne PHP), VMs = AVD-Desktops bzw. ausgeschaltet. Einziger Fund: Windows-Apps `MeeBot` und `MeeTeamsTestApp` (Abo „Nösse Datentechnik“, RG `MP-Test`) mit **PHP 5.6 aktiviert** – um 18:03 samt Ressourcengruppe gelöscht. Nebenbefund: `frontend-identity-portal` läuft auf .NET 8.0.3 (2024), `MeeId-Oxomi-intern` auf Node 14 (EOL)
- [ ] (alt) Shopwechsel-Schritte: (178.63.45.204, Timme): DNS `sicherheit.meesenburg.com` (+ www, cms?) umstellen → direkt danach Let's-Encrypt-Zertifikat im ISPConfig, Wartungsmodus aus, Wawi-Abgleich testen, Bestellungen im Stripe-Shop bis zur Umschaltung abarbeiten; danach Stripe-Shop (node1) vom Netz bzw. auf Wartung
- [ ] **Secrets vor dem Shopwechsel:** `SECURITY__KEY` und Strapi-Admin-Secrets sind von außen ausnutzbar → sofort tauschen oder Wechsel binnen 1–2 Tagen (alternativ CMS per Traefik sperren); SMTP bald tauschen (genutzt für Bestellbestätigungen); Stripe live sobald Dashboard-Zugriff. JTL-/Custom-API-Token und DB-Passwörter beim Shopwechsel (nur von node1 erreichbar)
- [ ] Hetzner offen: Swarm auflösen/Tokens rotieren, node2-Testinstanz und node3-`configurators` stoppen (Entscheidung), `db_backups` auf node1 entfernen
- [ ] **Rotation und Aussperrung nach `analyse/12_rotation_aussperrung.md`** (ein Zeitfenster, nach GF-Freigabe): DevOps/Entra (Seidel, Bräuer), Hetzner node1–3 (vitandi-/m.schulz-Schlüssel, Swarm, sshd), JTL/ecomdata, Shop-Secrets, Stripe live, Mail, Strapi
- [ ] **Kritisch, live bestätigt 09.10.:** `SECURITY__KEY` (Login-JWT) = Repo-Standardwert (6 Zeichen), Mongo/RabbitMQ/Strapi-DB = `pw12…`, Strapi-Secrets = Repo-Werte; Stripe läuft mit `sk_live_`
- [ ] POC: JTL-REST v1 liefert für Artikel **ohne Kategorie** HTTP 500 (Liste und Einzelabruf, z. B. ID 122, 131). → Neue API ruft Artikel über `isActiveOnSalesChannelId=2-2-5` ab (getestet), nicht über Kategorien; Artikel ohne Kategorie in JTL bereinigen (Archiv-Kategorie oder deaktivieren); ~~POC-Broker umstellen~~ (erledigt 10.10.2026: POC-Artikelliste mit Kanalfilter, `Jtl:SalesChannelId`, 10.052 Artikel) (Referenz: `analyse/13_jtl_schnittstelle_referenz.md`, J-01)
- [ ] Custom-API ablösen (Bericht 13, J-08): Lücken der REST-API (Konfig-Gruppen/-Artikel, Variations-Aufpreise, Kategorie-Rabatte je Kundengruppe) über eigenen schreibgeschützten DB-Broker; SQL-Abfragen der Custom-API als Vorlage sichern (Admin-Zugang JTL-Server)
- [ ] **Stripe-Testmodus-Webhook `we_1SwguC…` → `test.meesenburg.neogris.com/api/payment/webhook/` (node2, Freelancer-Testinstanz, seit 22.10. von außen gesperrt) löschen** (geprüft 09.11.2026): bekommt auch die Ereignisse der POC-Tests (S100003–S100005, S100004/S100015 storniert am 05.11.) und schlägt fehl → Stripe-Warnmails. Ausführen nur mit OK.
- [ ] Stripe-Zugang (KW 42): Live- und beide Testschlüssel rollen, Webhook-Endpunkte bereinigen, neuen Testschlüssel in AppHost-User-Secrets `Parameters:stripe-*` eintragen (Bericht 12, 5.1)
- [ ] Sortimentsabgleich Stripe-Shop ↔ Shop-Kanal `2-2-5` (10.052 Artikel), Bericht 13 J-07
- [ ] Wer ist `217.74.152.246` (Norlys DK, wie die vitandi-IPs)? Root-Sitzungen auf node1 seit 07.10. 13:27 und auf **node3 seit 09.10. 06:53** (dort Projekt `configurators` neu gebaut und gestartet)
- [ ] Items-Mongo: „too many open connections“ – Verbindungsleck im Items-Dienst beheben bzw. Limit prüfen
- [x] `node2`/`node3` geprüft (09.10.2026) – Briefing 6.12
- [x] **node2 gesichert** (09.10.2026, `node2_20261009_0653`, 805 MB, schlank: Code ohne Bilder/DB-Rohdateien, Git + Diff, `.env`, DB-Dumps, 39 Auftrags-PDFs, `db_backups` 04–07/2026, Traefik, Logs, Journal ab Juni, `/etc`, `.ssh`; Prüfsummen OK)
- [x] **node3 gesichert** (09.10.2026, `node3_20261009_0639`, 1,7 GB – davon 1,6 GB System-Journal; `configurators`, Historien, `.ssh`, `/etc`, `/var/log`; Prüfsummen OK). `SN24`/`SN24static` (Jan. 2026) bewusst nicht gesichert
- [ ] `db_backups` auf node2 (20.04.–25.07.2026, auth/business/items/payment/strapi): prüfen, ob Produktionsstände (einzige historische DB-Stände!) – für Zahlungsabgleich (Bericht 11) nutzen; Kundendaten auf Testserver = DSGVO-Thema
- [ ] node2: öffentliche Ports 8025/1025 (Mailpit), 10002 (Frontend ohne Basic Auth) schließen; **Testinstanz spricht mit dem produktiven JTL** (gleiche Custom-API/REST wie live) – Testaufträge können im echten JTL landen
- [ ] Univelop (`app.univelop.de`) als Datenquelle der `configurators` auf node3: Konto/Inhaber und Inhalt klären (Konfigurator-Regeln?)
- [ ] Swarm (3 Manager, ungenutzt, Port 2377 öffentlich) und SSH-Passwortanmeldung (an, aber Passwörter gesperrt) bei der Härtung abschalten
- [x] (09.10.2026, siehe oben) **Vor jeder Änderung sichern:** Traefik-Access-Log (+ rotierte Dateien), Docker-Logs aller Dienste, Stack/Compose, produktive `.env`, Traefik-Konfiguration, `acme.json`, DB-Dumps, Uploads, Images (Checkliste Bericht 09, Abschnitt 3)
- [ ] Prüfsummen bilden, verschlüsselt ablegen (Access-Log enthält Session-Cookies)
- [ ] Loki/Promtail finden (Label `logging=promtail`) und Log-Historie exportieren
- [ ] Auswertung der kritischen Pfade (`single_order`, `order?user_id`, `mark_paid`, `maildata`, `pdf`) nach Bericht 09, Abschnitt 5
- [ ] Jeden `mark_paid`-Aufruf gegen Stripe abgleichen
- [ ] Rückfall-Mailadresse `c.seidel@neogris.com` (aktiver Freelancer): Mail-Logs prüfen, ob Rückfall-Mails verschickt wurden; läuft die Mail-Test-API (`mail_svc/src/mail/main.py`) produktiv?
- [ ] **AVV klären:** Gibt es einen Auftragsverarbeitungsvertrag mit den Freelancern? Vertragspartner (Person, neogris, Alt-Dienstleister?) und Umfang (Zugriff auf Kundendaten, Server)
- [ ] Rückfall-Empfänger entfernen (Fehler + Monitoring statt externes Postfach) – spätestens mit der neuen API
- [ ] Dokumentation nach Art. 33 Abs. 5 DSGVO + Bewertung durch DSB
- [ ] **Erst danach** die Lücken an die Freelancer weitergeben
- [ ] Systeme außerhalb von Meesenburg übernehmen (Liste Bericht 09, Abschnitt 7)

## Phase 0 – sofort (Bericht 00, Abschnitt 4)

- [ ] **JTL-Server:** `JTL-Worker` läuft nur in der Sitzung von `useradmin` → nicht abmelden, später als Dienst; Custom-API und JTL-REST laufen in Sitzung 0 – klären, ob sie nach einem Neustart automatisch starten; Admin-Zugang klären; Code der Custom-API sichern (Briefing 6.10)
- [ ] JTL-Server: Datenbank-Sicherung und Monitoring (Icinga) bei ecomdata erfragen (`SQLAgent$JTLWAWI` gestoppt); zweite SQL-Instanz `JTLWAWI2` und `C:\TMP\SMIG` klären
- [ ] JTL-Server: RDP für Mitarbeiter mittelfristig nur über VPN bzw. RD-Gateway mit MFA (IPBan schützt bereits gegen Rateangriffe)
- [ ] **ecomdata:** Vertragspartner bestätigen (NinjaOne-Mandant „Meesenburg GmbH“); wie starten Custom-API und JTL-REST nach einem Neustart (geplante Aufgaben?); Patch-Neustarts seit Juni; Backup-Konzept der VM; Admin-Zugang für Meesenburg (eigenes Konto); Pfad/Startbefehl der Python-Prozesse und **Kopie des Custom-API-Ordners** (liegt samt Python in einem gesperrten Profil, vermutlich `useradmin`); OpenSSH-Server (`C:\ProgramData\ssh`)?

- [ ] **Kommen echte Bestellungen in JTL an?** (Live-Test 08.10.: JTL-Übergabe scheitert still, Mail geht trotzdem raus – Briefing 6.8) Bestellnummern aus dem BCC-Postfach `info@sicherheitstechnik-nord.de` gegen JTL abgleichen; erfolgreiche Stripe-Zahlungen ohne JTL-Auftrag suchen
- [ ] In JTL nach doppelten Aufträgen und mehrfachen Stripe-Zahlungsbuchungen je Auftrag suchen (öffentliche `send/`- und `send_payed/`-Endpunkte)
- [ ] Testaufrufe vom 08.10.2026 an `send/` und `send_order_to_system/` dokumentieren (IDs, Uhrzeit); keine weiteren Aufrufe auf dem Live-System
- [ ] **Code aus den laufenden Containern sichern** – Produktionsstand weicht vom Repo ab (Antworten der Live-Endpunkte passen nicht zum Repo-Code)
- [ ] Ursache der gescheiterten JTL-Übergabe in den Logs des business-Containers klären (`send to erp` ohne `in erp: True`)
- [ ] Testbestellungen vom 08.10.2026 (Nummern notieren) nach der Sicherung bereinigen; nicht per `send_order_to_system` nachschieben

- [ ] (nach der Sicherung) Traefik-Allowlist; **unter `/api/order/` nur `POST /`, `GET /?user_id=`, `GET /single_order` (ohne `is_jtl_number`), `payment_methods`, `shipping_methods` zulassen – insbesondere `send/`, `send_payed/`, `send_order_to_system/`, `send_mail/` sperren** (Briefing 6.9); `mark_paid`, `/api/pdf`, `/api/maildata`, `single_order`, `order?user_id`, `cart/{id}`, Doku-Routen sperren
- [ ] **Mailpit 8025/1025 ist ohne Login aus dem Internet erreichbar (Portprüfung 07.10.2026):** Mailpit-Daten sichern, Inhalt durch befugte Person prüfen (liegen Produktivmails darin?), Ports schließen bzw. Mailpit aus Produktion entfernen
- [ ] Frontend-Direktport 10002 schließen; SSH (22) nur per Schlüssel + IP-Freigabe (RabbitMQ, Valkey, mongo-express, DBs waren von außen nicht erreichbar)
- [ ] Hetzner-Konto des Shop-Servers klären (136.243.107.86, RZ Falkenstein): Wer ist Kontoinhaber, Übernahme auf Meesenburg
- [ ] Secrets rotieren (JTL → Custom-API → SMTP → JWT → Strapi → DB/MQ → Stripe), danach Git-Historie bereinigen
- [ ] **Stripe-Abgleich seit Februar 2026** (Bericht 11): jede JTL-Zahlung „Stripe“ über die PaymentIntent-ID im Stripe-Dashboard prüfen (succeeded, Betrag, Doppelungen) und umgekehrt jede erfolgreiche Stripe-Zahlung einer JTL-Buchung zuordnen; Testmodus-Zahlungen in der Live-Wawi, Doppelzahlungen und nie gebuchte Zahlungen (Link, Amazon Pay, Revolut Pay) erkennen; zusätzlich Zeiträume mit Fehlberechnung aus Bericht 02 (17.08.–09.09., 08.–09.09., vor 23.09.2026)
- [ ] Datum der Umstellung Stripe Test → Live klären (Stripe-Dashboard)
- [x] **S8 Testauftrag** (12.10.2026, OK m.piper): S100005 → Wawi-Auftrag 91401, Kontrolle grün, storniert (J-26)
- [ ] Stornierten Testauftrag 91401 (S100005) in der Wawi von Hand löschen (REST 1.1 kann nicht löschen) – m.piper/Sachbearbeitung
- [ ] Doppelte Wawi-Kunden mit `m.piper@meesenburg.de` (1331962-07102026, 1331963-07102026, vom Stripe-Shop am 07.10.) bereinigen
- [x] S8 Automatik (12.10.2026): Outbox im Worker mit Wiederholung, Sperre, Klärungsfällen und Admin-Ansicht – **ausgeschaltet**
- [ ] **S8 für den Betrieb einschalten (nur nach OK):** AppHost-Secrets `SiseShop:Jtl:AllowWrites=true` und `SiseShop:OrderTransfer:Auto=true` (Betrieb: Key Vault/Umgebung); vorher Fachfragen F-28 bis F-32 klären
- [ ] S8: Benachrichtigung bei „Klärung nötig“/„fehlgeschlagen“ (Mail an Innendienst, mit S9)
- [ ] Test-E-Mail `test@example.com` ist in der Wawi ein echter Kunde (1318971-06082020) – nicht mehr für Tests verwenden
- [ ] **S7 Webhook live testen:** Stripe-CLI installieren (`brew install stripe/stripe-cli/stripe`, `stripe login`) und `stripe listen --forward-to https://localhost:7074/api/v1/payments/stripe/webhook --skip-verify`; klären, woher das Secret `whsec_…` in den User-Secrets stammt (CLI oder Dashboard-Endpunkt)
- [ ] Unbezahlte Online-Bestellungen nach Frist stornieren und PaymentIntent abbrechen (Worker); Frist mit Fachbereich (F-21)
- [ ] Zahlarten im Stripe-Dashboard auf die Antwort zu F-20 einstellen (Testkonto zeigt PayPal, Karte, SEPA, Klarna …); SEPA = „in Bearbeitung“ über Tage → im Betrieb nur mit Webhook
- [ ] Datenschutzvorfall bewerten (DSB, Art. 33 DSGVO)
- [ ] ecomdata: RDP (3389) nur per VPN, TLS bzw. VPN für JTL-REST und Custom-API, IP-Freigabe Entwickler + AKS-Egress
- [x] `svc_helper` gesichert (07.10.2026): Klon unter `referenz/svc_helper` (öffentliches Repo `gitlab.com/vitandi1/svc_helper`, Commit `5e65dee9`)
- [ ] **Nutzungsrechte an `svc_helper` schriftlich klären** (Inhaber der GitLab-Gruppe `vitandi1` bzw. Insolvenzverwalter); LICENSE ist MIT, aber mit kopiertem Fremd-Copyright
- [ ] Kopie von `svc_helper` in ein eigenes Azure-DevOps-Repo übernehmen (für Hotfixes an auth/mail/pdf)
- [ ] Prüfen, ob `gitlab.com/vitandi1/ansible_roles` die Servereinrichtung enthält; ggf. ebenfalls sichern
- [ ] Custom-API-Quellcode einfordern; Let's-Encrypt-Account, Strapi-Instanz, Nextcloud-Inhalte übernehmen

## Phase 1 – Entscheidungen bis Tag 0 (Bericht 08, Abschnitt 9)

- [ ] E1 Betriebsort Phase 1 (AKS oder Docker-Host)
- [ ] E2 Auth vorerst über auth_svc + Hotfix Passwort-Reset
- [ ] E3 Wer macht die Frontend-Anpassungen (AP11)
- [ ] E4 Lieferländer in Phase 1 (nur DE?)
- [ ] E5 Rolle der Freelancer (nur PR mit internem Review)
- [ ] E6 Freien Listenpreis-Code in esERP für den Shop-VK festlegen
- [ ] Neues Repo `Meesenburg.SiseShop` in Azure DevOps + Branch-Policy
- [ ] Ansprechpartner Fachbereich für Soll-Preise benennen

## Parallel

- [ ] Fachworkshop Konfigurationsregeln + Soll-Preise (Agenda: Bericht 08, Abschnitt 8)
- [ ] SAP-Fibu: Stripe-Verrechnungskonto, Ausgleichslogik, **Stripe-Referenzen durchgängig zuordenbar** (Shop-Bestellnummer in Stripe, JTL/esERP, SAP)

## esERP (Phase 3, Klärung jetzt)

- [ ] com4: Doku und Freigabe der Auftrags-API (Prüfliste Bericht 07, Abschnitt 5), Bestandsabruf je Artikel/Lager, Dokument-Upload ins DMS
- [ ] com4: Wirkung von Materialkatalog-Änderungen auf bestehende Artikel
- [ ] es2000: Neuerungen 3.4, Konfigurations- bzw. Variantenartikel (vorhanden oder geplant?)
- [ ] Zahlungsbedingung bzw. -art „Stripe – bezahlt“ in esERP und Übergabe im Fibu-Export


- [ ] Wartungsmodus im Frontend implementieren

## Testbetrieb AKS und Admin im SiSe-Portal (Bericht 22, 17.11.2026)
- [x] Entscheidungen E-T1 bis E-T4 (17.11.): nur Entra-Anmeldung vor dem Shop (kein eigener Ingress), Paket solange kompatibel (evtl. ins bestehende Paket, Festlegung bei P-3), Portal-Rollen, Sicherung macht M. Piper
- [ ] M. Piper: App-Registrierung „SiSe-Shop (Test)“ nach `deploy/aks-test/APP_REGISTRIERUNG.md`, danach Mandanten- und Client-ID an mich
- [ ] M. Piper: ES-Sicherungsstand (Commit + Tag `vor-siseshop`) vor P-5
- [ ] IT: V-1 Knotenpool, V-4 Gruppe `grp_mee_sise_shop_test` (V-3 entfällt), V-5 eigene KV-Identität, V-6 DNS/TLS
- [ ] IT + ecomdata: V-2 Ausgangs-IP des AKS für REST 50443 und SQL 50000 freischalten
- [x] T-1 Testbetrieb-Schalter `SiseShop:TestGate` (17.11., lokal getestet; echter Entra-Durchlauf offen bis App-Registrierung)
- [x] T-4 Cookie-Schlüssel und Login-Tickets in Mongo (17.11., Neustart-Test ok)
- [x] P-1 MudBlazor 9.12, Identity.Web 4.17, OpenIdConnect 10.0.14 (17.11., 324 Tests grün)
- [x] App-Registrierung „SiSe-Shop (Test)“ angelegt, Zustimmung erteilt; echter Entra-Durchlauf lokal ok (17.11.)
- [x] T-2/T-3 vorbereitet (17.11.): `deploy/aks-test/` (Manifest, secrets.sh, seed.sh, release-Ziel für PPR.DevTools), `appsettings.Staging.json`, `.dockerignore` als Freigabeliste. Daten im **Mongo-Cluster** `Dev-Sise-Shop` über KV-Secret `MongoDBConnectionString` wie ES (Helfer `AddKeyVaultSecrets` nach ES-Muster in ServiceDefaults), Knotenpool `agentpool` (M. Piper). Nichts angewendet
- [ ] OK + Ausführung: Images im ACR bauen → apply → secrets.sh (kopiert `es-keyvault-app`) → seed.sh (README)
- [ ] Aufräumen: alter Weg `AddSiseShopSecretsAsync` (JSON-Secret `SiseShop-Dev`, E-P) entfällt, wenn der KV-Weg läuft
- [ ] GitHub-Organisation: Namen festlegen, Code-Repo von Doku/Forensik trennen, dann Umzug (git → T-3 „nur aus Tag“)
- [ ] Worker im Test (Abgleich aus, Mailversand an) · Repo ist kein Git → T-3 „nur aus Tag“ erst mit Git-Repo
- [ ] T-5 Erstbefüllung + Rundgang SN24
- [x] P-2 Admin bereinigen (19.11.2026, Bericht 22 §6) – Weg B statt P-3 bis P-6: Shop-Admin wird Modul im eigenen SiSe-Portal (Analyse Neuorganisation, Schritte 3–5)
- [x] ~~P-3 RCL `Meesenburg.SiseShop.Admin` · P-4 Shop-Rechte ins Authorization-Paket · P-5 ES-Patch + Rauchtest · P-6 Paket veröffentlichen~~ entfällt (Weg B)
- [x] Hinweis an ES: Wildcard-Zertifikat `api.meesenburg.io` (19.11.) – laut M. Piper erneuert (17.11.)

## Testbetrieb AKS (18.11.2026)
- [x] ~~Admin-Begrüßung: „Hallo Piper,“ → „Hallo Michael“~~ erledigt 19.11.2026 (Vorname aus given_name bzw. nach dem Komma) (Anzeigename „Piper, Michael (HBG)“ wird am Komma getrennt; Vorname aus `given_name` bzw. Teil nach dem Komma nehmen). Spätestens mit dem Umzug des Admins ins SiSe-Portal (dort „Hallo, Michael“).
- [x] Zugang zum Testshop: nicht über `SiseShop:TestGate:AllowedGroups`, sondern über die Zuweisung von Benutzern in der App-Registrierung „SiSe-Shop (Test)“ (Entscheidung M. Piper, 18.11.2026).
- [x] ACR aufgeräumt: falsch abgelegte Repos `siseshop-web/-api/-worker` (ohne `meesenburg/`) gelöscht (18.11.2026).
- [x] Wawi-Zugang aus dem AKS (18.11.2026, per SSH über node1): Windows-Firewall auf c10330 ergänzt – Regel „SQL“ (50000) und „JTL REST API 50443“ zusätzlich für 20.31.106.131, bestehende Einträge unverändert; Sicherung `C:\Users\mpiper\firewall-sicherung\firewall-20261118-122847.wfw`. Test aus dem AKS: 50443 und 50000 offen, REST ohne Token → 401. 58080 (alte Custom-API) und SSH bewusst nicht freigegeben.
- [ ] **Wawi-REST spricht unverschlüsselt (http) über das Internet** – das API-Token ginge vom AKS im Klartext zu c10330 (vorher über den SSH-Tunnel verschlüsselt). Vor dem Einschalten im Worker: HTTPS auf 50443 aktivieren (JTL-REST-Server mit Zertifikat) oder Verbindung über VPN/Tunnel. SQL (50000) nur mit `Encrypt=True` verbinden.
- [ ] Danach Worker im Test mit `Erp__Jtl__*` (Basis-URL, Token aus dem Key Vault) konfigurieren, Abgleich prüfen.

## Prüfliste HTML-Auslieferung (Bericht 25, 18.11.2026)
- [x] Prüfliste von C. Feller gegen den .NET-Shop geprüft und abgehakt (22 erfüllt, 14 teilweise, 3 offen)
- [x] B-1 bis B-8 umgesetzt (19.11.2026, Bericht 25 „Umsetzung“): Weiterleitungen in einem Sprung bzw. 410, Nachfolger für entfernte Artikel, Canonical ?page=n + noindex für Filter, BreadcrumbList/Organization, Technische Daten, robots.txt, 301 auf kanonische Schreibweise, og:image Kategorien
- [x] Release 0.1.3 im Test (19.11.2026, 06:01): Pods laufen, Logs sauber; im Test geprüft: Canonical ?page=2, noindex bei Sortierung, 301 Schreibweise/entfernter Artikel, 410, Technische Daten, Feedback-Dialog mit Screenshot (nicht abgesendet)
- [ ] `System.Security.Cryptography.Pkcs` 10.0.0 (NU1903, GHSA-846f-f764-76mx) in Core/Api ausdrücklich anheben → 0.1.4
- [ ] SN24-Team: Inhalte mit href="#" und Links auf sicherheitstechnik-nord.de (B-9)
- [ ] Klären: GA4/Search Console, Stripe-Shop-Adressen 15.06.–14.10., Zugang für den Kontroll-Crawl (B-10)

## Zugang ecomdata-Server (18.11.2026)
- [ ] **SSH auf c22910 einrichten** (Entscheidung M. Piper 18.11.2026, statt „kein SSH auf dem Testserver“): nur Admin `mpiper`, nur Schlüssel (`~/.ssh/jtl_admin`), Port 50022, Windows-Firewall nur node1 (136.243.107.86), keine Weiterleitungen. Skript `~/repos/tools/jtl-tools/siseshop/jtl-ssh/02_c22910_admin_ssh.ps1` (per RDP als Admin, sichert vorher die Firewall); im ecomdata-Portal eingehend TCP 50022 nur von node1 freigeben. Mac: `ssh jtl-test`. Danach Hostschlüssel prüfen und hier eintragen. 19.11.: erster Lauf brach ab – OpenSSH ist auf c22910 **nicht** installiert (das bestehende OpenSSH ist auf vmmgmt), Windows-Feature scheitert am WSUS → Skript installiert jetzt das MSI von GitHub (Win32-OpenSSH) aus dem Download-Ordner. 19.11. 04:18: OpenSSH 10.4 per MSI installiert, sshd lauscht auf 50022, Windows-Regel nur node1, Port-22-Regel des MSI abgeschaltet, Hostschlüssel `SHA256:QelrfU4hqSgWBCbI0/s/0C96ko9Ppk9qKpNx3n45tWs`. **Offen:** Portal-Regel bei ecomdata (50022 von node1) – von node1 noch zu (3389 offen), M. Piper kam ins Portal gerade nicht hinein.
- [ ] Danach auf c22910 (per SSH): Regel für REST 50443 zusätzlich für die AKS-Ausgangs-IP 20.31.106.131 (wie c10330); SQL 50000 ist aus dem AKS schon offen (18.11. geprüft) → Regel prüfen, vermutlich „Any“, auf bekannte IPs einschränken (siehe „c22910 härten“ (4)).
- [ ] **Zugang zu den ecomdata-Servern ohne node1** (strategisch, M. Piper 18.11.2026): node1–3 (Hetzner) werden gekündigt – Kosten sparen, IT-Infrastruktur vereinheitlichen, keine Schatten-IT außerhalb von Azure/Rechenzentrum. Heute hängt an node1:
  - SSH-Sprung zu c10330 und c22910 (Port 50022, Windows- und Portal-Firewall nur node1), SQL-Tunnel `jtl-sql` (JtlSqlRead, lokale Entwicklung)
  - Windows-Firewall c10330: SQL 50000, REST 50443, Custom-API 58080 für node1 freigegeben
  - Stripe-Shop (offline seit 19.10.), Sicherungen/`db_backups`, Prüfungen von außen
  Optionen (Entscheidung offen):
  1. **Site-to-Site-VPN ecomdata ↔ Meesenburg** (E-O, bevorzugt): SSH/SQL/REST nur aus dem Meesenburg-Netz; AKS erreicht die Wawi über das bestehende VPN statt über das Internet → löst auch „Wawi-REST unverschlüsselt“. Anfrage an ecomdata (Kosten, Gerät, Netze).
  2. **Azure als Sprungpunkt:** SSH nur von der festen AKS-Ausgangs-IP 20.31.106.131 bzw. einer kleinen Azure-VM/Azure Bastion im bestehenden Abo; Zugang per Entra. Keine neue Firma, aber Internetweg bleibt.
  3. **Feste Meesenburg-Ausgangs-IP** (Firewall Rechenzentrum) in die Freigaben statt node1 – einfachste Lösung, wenn es eine feste IP gibt; Zugang dann nur aus dem Büro/VPN.
  Vor der Kündigung: alle node1-Freigaben (Portal- und Windows-Firewall c10330/c22910, ssh-config `jtl`, `jtl-sql`, `jtl-test`, JtlSqlRead) auf den neuen Weg umstellen und testen; Daten von node1–3 sichern bzw. löschen (DB-Dumps, `db_backups`, siehe oben).

## Strapi-Inhalte (Bericht 26, 19.11.2026)
- [ ] Entscheidung SN24-Team: JTL-Seiten behalten, Strapi-Seiten übernehmen oder neu anfangen (je Thema eine Quelle)
- [ ] Danach: Skript `strapi_inhalte.py` (19 Ratgeberseiten + FAQ als Entwürfe, Slugs wie im Stripe-Shop), ca. 1 PT

## Feedback-Knopf und Board (19.11.2026)
- [x] Feedback-Knopf in der Testversion gebaut (Screenshot mit html2canvas 1.4.1, SHA-512 gegen cdnjs geprüft; rot markieren, Art, Anmerkung) → Mongo `feedback` → GitHub-Issue. Lokal im Chrome getestet; GitHub-Teil ungetestet (kein Token)
- [ ] M. Piper: privates Repository `Meesenburg-GmbH/sise-shop-feedback` (README genügt, Branch `main`) anlegen
- [ ] M. Piper: Token anlegen – am besten GitHub-App der Organisation, sonst fein granularer PAT: Issues: Read and write auf `sise-shop`, Contents: Read and write auf `sise-shop-feedback` → Key Vault `SiseShop-GitHub-Feedback-Token`
- [ ] Danach Test: eine Rückmeldung aus shop-test, prüfen, ob das Bild im Issue sichtbar ist (privates Repository, nur für Mitglieder)
- [ ] Teams: GitHub-App für Teams (Benachrichtigungen `@GitHub subscribe Meesenburg-GmbH/sise-shop issues`) – muss im Teams Admin Center erlaubt sein. Ein GitHub-Projects-Board lässt sich nicht als Teams-Tab einbetten (GitHub erlaubt keine Einbettung in fremde Seiten) → Tab als Link

## Shop-Admin nach P-2 (19.11.2026)
- [x] **M. Piper:** In der App-Registrierung „SiSe-Shop (Test)“ die Umleitungs-URI `https://localhost:7240/signin-oidc` (Web) ergänzen (erledigt 19.11.2026, lokal getestet) – sonst lokal keine Admin-Anmeldung mehr (AADSTS50011), seit die Test-Personen entfernt sind. Danach einmal `~/repos/tools/ops-tools/siseshop/lokal-admin-recht.sh` (schon ausgeführt für M.Piper@meesenburg.de)
- [x] Release **v0.1.4** im Test (19.11.2026 07:34, Commit 480ca23, gebaut über DevTools; Admin im Testshop geprüft: Entra-Login, Kundenkonten, Inhalte, Prüfungen, CSV-Export, System). Versionen ab jetzt mit „v“ wie ES (release.sh angepasst). Hinweis: API loggt bei jedem Start `SecretNotFound` für das optionale `SiseShop-GitHub-Feedback-Token` als Fehler (Feedback-Arbeit) **0.1.3 nicht einspielen:** am 19.11. 05:52 während laufender P-2-Änderungen aus dem Arbeitsordner gebaut (Mischstand). Zeigt das Risiko „Release aus der Arbeitskopie“ → Build aus Tag/Commit (GitHub Actions)
- [ ] Timeout `ShopApi:TimeoutSeconds` (300 s) gilt auch für den öffentlichen Shop – kürzer für Shop-Seiten, lang nur für Admin-Exporte (eigener HttpClient im Modul)
- [ ] Collection `compare_runs` (Stripe-Vergleich) in `siseshop`/`Dev-Sise-Shop` löschen, wenn niemand die alten Läufe braucht

## Feedback → Liste „Problemverfolgung“ im Teams-Kanal (19.11.2026)
- [x] App „SiSe-Shop (Test)“: Sites.Selected (Anwendung) erteilt; Website des privaten Kanals `eCommersSiSe-Shopentwicklung` mit „write“ freigegeben (Graph Explorer, piper.admin als Websiteadmin); Geheimnis im Key Vault `SiseShop-Feedback-Graph-Secret` (läuft bis 18.11.2028)
- [x] Gegenprobe mit App-Token: Liste `Problemverfolgung` (f35598fb-8c5f-4339-8dee-212b144e140a) lesbar, Team-Hauptseite `eCommersSiSe` 403 (nur die Kanal-Website ist frei)
- [ ] M. Piper: `piper.admin` als Websiteadministrator von `eCommersSiSe-Shopentwicklung` wieder entfernen; az-Profil `~/.azure-piper-admin` abmelden und löschen
- [x] Spalten per App nicht anlegbar (write reicht nicht, Link-Spalten über Graph gar nicht) → keine Pflicht: Details und Bild stehen in der Problembeschreibung (Rich-Text), Probeeintrag mit Bild angelegt, gelesen, gelöscht (19.11.)
- [ ] Optional manuell: Spalten Art (Auswahl), Nummer, Melder → interne Namen in `Feedback:Lists:Columns`
- [x] Feedback-Knopf auf die Liste umgestellt (GraphListBroker, Screenshot in `Feedback-Screenshots/<Monat>` der Kanal-Website), 328 Tests grün; lokaler Durchlauf 19.11. ok (Eintrag mit Bild in der Liste, im Browser geprüft, danach gelöscht). Lokal aktiv über AppHost-User-Secret `Parameters:feedback-graph-secret`
- [ ] Benachrichtigung im Kanal: Power-Automate-Flow „Wenn ein Element erstellt wird“ (Liste) → Nachricht im Kanal (kein Webhook nötig)
- [ ] Ablauf des Geheimnisses (18.11.2028) überwachen
