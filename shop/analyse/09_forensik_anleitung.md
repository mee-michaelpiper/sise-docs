# 09 – Forensik: Sichern und Auswerten (Server, Logs, Fremdsysteme)

> **Stand:** 07.10.2026 · **vertraulich** · Grundlage: Berichte 04 (Sicherheit) und 06 (Infrastruktur)
> **Zweck:** Beweise und den Produktionsstand sichern, **bevor** die Lücken geschlossen oder an die Freelancer weitergegeben werden. Danach klären, ob Daten abgeflossen sind (Art. 33 DSGVO).

## 1. Reihenfolge

1. **Zugang** zum Server bzw. zu den Swarm-Nodes erhalten (liegt nicht im Meesenburg-Rechenzentrum).
2. **Sichern** (Abschnitt 3): Logs, Konfiguration, `.env`, Datenbanken, Uploads, Images – **vor jeder Änderung**.
3. **Sperren** (Phase 0, Bericht 00 Abschnitt 4): Traefik-Allowlist, Ports schließen.
4. **Erst dann** die Lücken an die Freelancer weitergeben. Grund: Sie haben vermutlich Serverzugang. Logs und Stände sollen vorher unverändert gesichert sein.
5. **Auswerten** (Abschnitt 5), Ergebnis mit dem Datenschutzbeauftragten bewerten, dokumentieren (Abschnitt 6).

> Faustregel: **Nichts neu starten, neu ausrollen oder umkonfigurieren, bevor die Sicherung fertig ist.** Ein `docker restart` ist unkritisch. `docker compose up` mit geänderter Konfiguration, `docker rm`, `docker service update` oder ein Stack-Deploy erzeugen Container neu. Was nur im Container lag, ist dann weg.

## 2. Wo liegen die Logs?

Annahme aus dem Repo: Der Produktions-Traefik ist ein „globaler“ Traefik, dessen Konfiguration nicht im Repo liegt. Pfade können deshalb abweichen, also immer per `docker inspect` prüfen.

| Quelle | Ort laut Repo | Inhalt | Bemerkung |
|---|---|---|---|
| **Traefik-Access-Log** | im Container `/etc/traefik/data/access.log`. Im Repo ist `/etc/traefik` aus dem Host eingebunden (`compose/services*/traefik.yml:15` → `../../traefik:/etc/traefik`), also auf dem Host `<stack-ordner>/traefik/data/access.log` | JSON, jede Anfrage mit Zeit, Client-IP, Pfad, Status (200–599), **alle Header außer Authorization**, also auch **Session-Cookies** | Keine Rotation konfiguriert (`maxSize` auskommentiert) → evtl. **eine Datei seit dem Start**; auch nach `access.log.*`/`.gz` suchen |
| Traefik-Log | `/etc/traefik/data/traefik.log` | Fehler und Zertifikate | |
| Docker-Logs der Dienste | `docker logs <container>` bzw. `docker service logs <service>` (Swarm); Dateien unter `/var/lib/docker/containers/<id>/<id>-json.log` | uvicorn-Zugriffe der Python-Dienste (Pfad, Status; IP meist die von Traefik) | Mindestens ein Dienst mit `max-size: 10m` → nur kurzer Zeitraum |
| **Loki/Promtail** | Container tragen das Label `logging=promtail` (`docker-stack.yml`) | zentrale Log-Sammlung, ggf. lange Aufbewahrung | Standort unbekannt; auf dem Server `docker ps | grep -i -E "loki|promtail|grafana"`, sonst beim Dienstleister erfragen |
| JTL-Wawi | Aufträge, Zahlungen (`tZahlung`) | Abgleich „bezahlt“ ↔ Stripe | bei ecomdata bzw. in JTL selbst |
| Stripe | Dashboard / Export | tatsächliche Zahlungen | Grundlage für den Abgleich `mark_paid` |

### Traefik-Container finden und Log-Ort bestimmen

```bash
docker ps --format '{{.ID}}  {{.Names}}  {{.Image}}' | grep -i traefik
```

```bash
docker inspect -f '{{range .Mounts}}{{.Source}} -> {{.Destination}}{{println}}{{end}}' <container-id>
```

- **Mount für `/etc/traefik` vorhanden:** Das Log liegt auf dem Host unter `<Source>/data/access.log`.
- **Kein Mount:** Das Log liegt **nur im Container**. Sofort herauskopieren:

```bash
docker cp <container-id>:/etc/traefik/data/access.log ./access_$(date +%F).log
```

## 3. Sicherungs-Checkliste (Server)

Alles in einen Ordner `sicherung_<datum>/`, danach **Prüfsummen** bilden und **verschlüsselt** ablegen (nicht per Mail, nicht in Teams-Chats).

| # | Was | Wie / Wo |
|---|---|---|
| 1 | Traefik-Access-Log + rotierte Dateien, Traefik-Log | Abschnitt 2 |
| 2 | Docker-Logs aller Dienste | `docker logs <c> > <name>.log 2>&1` je Container (bzw. `docker service logs`) |
| 3 | Stack- und Compose-Dateien, **produktive `.env`**, Traefik-Konfiguration (statisch + dynamisch), `acme.json` | Stack-Ordner (laut Repo `/srv/docker-stack`), Traefik-Ordner |
| 4 | Laufende Konfiguration | `docker ps -a`, `docker inspect` aller Container, `docker service ls`/`docker stack ps` (Swarm), `docker image ls --digests` |
| 5 | Datenbanken | Dumps aller MongoDB-Instanzen (`mongodump --archive --gzip`), MariaDB (Strapi, `mariadb-dump`) |
| 6 | Uploads und Dateien | **Bestellanhänge/Sicherungskarten aus dem business-Container** (`resources/order_attachments`, liegt **ohne Volume** im Container → `docker cp <business-container>:/app/src/business_api/resources/order_attachments ./order_attachments`; Pfad per `docker exec … ls` prüfen; Container **vorher nicht neu erzeugen**), **Mailpit-Daten** (Volume bzw. `docker cp`), Bestellanhänge, Security-Card-Uploads, Strapi-Uploads, Produktbilder (`/assets/img/shop_items`) |
| 6a | **Code aus den laufenden Containern** (der Produktionsstand weicht nachweislich vom Repo ab, Briefing 6.9) | je Dienst `docker cp <container>:/app ./code_<dienst>`; Image-Digests mit `docker inspect --format '{{.Image}}' <container>` |
| 7 | Images | aus `zot.local:5000` bzw. lokal: `docker save` oder Kopie in die Azure Container Registry |
| 8 | Crontabs, Logrotate, Firewall-Regeln | `crontab -l`, `/etc/logrotate.d/`, `iptables-save` / `ufw status` |
| 9 | Benutzer und Schlüssel mit Serverzugang | `/etc/passwd`, `~/.ssh/authorized_keys` aller Benutzer → später bereinigen |

Prüfsummen:

```bash
find sicherung_$(date +%F) -type f -exec shasum -a 256 {} \; > sicherung_$(date +%F).sha256
```

## 4. Schutz der gesicherten Daten

- Das Access-Log enthält **Session-Cookies** (`access_token`, `refresh_token`) und personenbezogene Daten. Es ist selbst schützenswert: Ablage verschlüsselt, Zugriff nur für die Personen, die auswerten.
- Für die Auswertung (auch für Claude) eine **bereinigte Kopie ohne Cookies und Tokens** erzeugen:

```bash
jq -c 'del(.["request_Cookie"], .["request_Authorization"], .["downstream_Set-Cookie"])' access.log > access_ohne_cookies.log
```

- Nach der Sicherung die **Secrets rotieren** (TODO Phase 0). Der neue JWT-Schlüssel macht die geloggten Session-Cookies wertlos.

## 5. Auswertung

### 5.1 Kritische Pfade herausfiltern

```bash
jq -r 'select(.RequestPath|test("/api/(order/single_order|order/\\?user_id|order/[0-9]+/mark_paid|order/send|maildata|pdf|user/\\?identifier|cart/)")) | [.time, .ClientHost, .RequestMethod, .RequestPath, .DownstreamStatus, .["request_User-Agent"]] | @tsv' access_ohne_cookies.log > kritische_zugriffe.tsv
```

### 5.2 Zugriffe je IP

```bash
cut -f2 kritische_zugriffe.tsv | sort | uniq -c | sort -rn | head -30
```

### 5.3 Worauf achten

| Muster | Bedeutung | Lücke |
|---|---|---|
| Eine IP ruft `single_order?number=` mit **vielen verschiedenen** Nummern ab, besonders fortlaufend | Abgreifen von Bestellungen | S-01 |
| `order/?user_id=` mit wechselnden IDs von einer IP | Abgreifen von Kundendaten | S-01 |
| **Jeder** Aufruf von `PATCH …/mark_paid` | Das Frontend nutzt diesen Endpunkt nicht → jeder Treffer ist verdächtig. **Gegen Stripe abgleichen** | S-02 |
| `/api/pdf/…/template_string`, `/html`, `/markdown` | Versuch der Codeausführung bzw. SSRF | S-03 |
| `/api/maildata/*` von außen | Abruf von Mail-Inhalten inkl. Auftrags-PDF (base64 = nur kodiert, nicht verschlüsselt) | S-01 |
| `GET /api/order/send/{id}`, `/send_payed/{id}`, `/send_order_to_system/{id}`, `/send_mail/{id}` | Auftrag bzw. Zahlung in JTL ohne Prüfung erzeugt → in JTL nach Doppel-Aufträgen und Mehrfach-Zahlungsbuchungen suchen | S-02 (Nachtrag 08.10.) |
| Zugriffe auf `/api/*/docs`, `openapi.json` | Erkundung der API | S-15 |
| User-Agents wie `python-requests`, `curl`, `sqlmap`, `nuclei`, leere User-Agents | automatisierte Abfragen | – |

**Eigene IPs herausrechnen:** Shop-Server, Freelancer, Monitoring, Meesenburg-Netze. Wer welche IP hat, im Zweifel bei den Freelancern erfragen, **nachdem** die Sicherung steht.

### 5.4 Abgleich `mark_paid` ↔ Stripe

1. Alle `mark_paid`-Aufrufe aus dem Log (Bestell-ID, Zeit, IP).
2. Je Bestellung: Gibt es eine erfolgreiche Stripe-Zahlung in passender Höhe (PaymentIntent bzw. Charge, Betrag, Zeit)?
3. Zusätzlich in JTL alle Aufträge mit Zahlung „Stripe“ gegen den Stripe-Export abgleichen, besonders in den Zeiträumen mit Fehlberechnung (17.08.–09.09., 08.–09.09., vor 23.09.2026; Bericht 02).

## 6. Dokumentation (Art. 33 Abs. 5 DSGVO)

Unabhängig vom Ergebnis festhalten:

| Punkt | Inhalt |
|---|---|
| Lücke | betroffene Endpunkte, seit wann offen (Go-Live Juni 2026), welche Daten (Name, Anschrift, E-Mail, Telefon, Positionen, Auftrags-PDF) |
| Kenntnis | Datum, an dem die Lücke bekannt wurde (07.10.2026, Code-Analyse) |
| Prüfung | welche Logs für welchen Zeitraum vorlagen bzw. fehlten |
| Ergebnis | Zugriffe durch Unbefugte: ja / nein / nicht feststellbar; Umfang |
| Maßnahmen | Sperrung (Datum/Uhrzeit), Rotation der Secrets, Fix |
| Bewertung | durch den DSB: Meldung an die Aufsichtsbehörde ja/nein (72-Stunden-Frist ab Kenntnis eines Vorfalls), Benachrichtigung Betroffener ja/nein |

**Hinweis:** Eine offene Lücke allein ist noch keine meldepflichtige Verletzung. Entscheidend ist, ob ein unbefugter Zugriff stattgefunden hat oder nicht ausgeschlossen werden kann. Die Bewertung trifft der Datenschutzbeauftragte. Dieses Dokument ist keine Rechtsberatung.

## 7. Systeme außerhalb von Meesenburg (sichern bzw. übernehmen)

Laut Repo liegen mehrere Systeme beim insolventen Dienstleister oder bei Dritten (Details Bericht 06, Abschnitt 6):

| System | Ort | Risiko | Maßnahme |
|---|---|---|---|
| **Shop-Server** (Swarm `node1..3`, `/srv/docker-stack`) | **Hetzner Online, RZ Falkenstein** (IP 136.243.107.86 laut DNS/RIPE, 07.10.2026); Kontoinhaber unbekannt; nicht im Meesenburg-RZ | Produktionsstand nur dort | Zugang, Sicherung (Abschnitt 3), Betreiber und Vertrag klären |
| **Loki/Promtail** | unbekannt | Log-Historie evtl. nur dort | Standort erfragen, exportieren |
| **Docker-Registry** `zot.local:5000` | unbekannt | keine Rollback-Images | Images sichern |
| **Strapi „Global“** `cms.sn24.sustainable-solutions.it` | Dienstleister | Inhalte/Bilder evtl. nur dort; Transfer-Token im Repo | `strapi export`, Uploads sichern, Token widerrufen |
| **Nextcloud** `cloud.sustainable-solutions.it` (`SN24/repo/static/`, `SN24/repo/dbs/`) | Dienstleister | Assets, DB-Dumps | herunterladen |
| **Gitea** `gitea.sustainable-solutions.it:2221` | Dienstleister | Issues, PR-Diskussionen, Wiki, CI-Historie | exportieren |
| **Let's-Encrypt-Account** `c.seidel@sustainable-solutions.it` | Dienstleister | Zertifikatswarnungen gehen ins Leere | auf Meesenburg-Adresse umstellen |
| **Rückfall-Mailempfänger** `c.seidel@neogris.com` (`backend/mail_svc/src/mail/utils/mail_templates.py:185, 203, 224, 253`) | extern (Postfach eines aktiven Freelancers) | Auftragsbestätigung inkl. PDF bzw. Versand-/Storno-/Bezahlt-Mail geht dorthin, wenn **keine Kunden-E-Mail bzw. kein Empfänger** vorliegt; dazu die Test-API `mail_svc/src/mail/main.py`, die laut Bericht 03 Produktionsdaten an feste Adressen schickt | **AVV prüfen** (Vertragspartner: Person, neogris oder Alt-Dienstleister? Umfang?); Mail-Logs auf Rückfall-Versand prüfen; läuft die Test-API produktiv?; Rückfall durch Fehler + Monitoring oder Meesenburg-Funktionsadresse ersetzen |
| **GitLab-Gruppe** `vitandi1` (`svc_helper`, `ansible_roles`, …) | gitlab.com | Code jederzeit entziehbar | `svc_helper` gesichert; `ansible_roles` prüfen |
| **JTL-Wawi + Custom-API** | ecomdata (`c10330.servers.ecomdata.de`) | RDP offen, HTTP ohne TLS, Custom-API-Code fehlt | Hoster ansprechen (TODO Phase 0) |
| **SMTP** `k69g81.meinserver.io` | Dritter | Shop-Postfach, Passwort im Repo | Passwort rotieren, Inhaber klären |
| **Domains / DNS** `sicherheit.meesenburg.com`, `cms.sicherheit.meesenburg.com`, `mb-sn24.meesenburg.de`, `mb-sn24.sustainable-solutions.it` | DNS-Verwaltung prüfen | Abhängigkeiten zur Dienstleister-Domain | DNS-Inhaber und Einträge prüfen |
