# 12 – Zugangsdaten tauschen und Entwickler aussperren

> **Stand:** 09.10.2026 · **vertraulich** · gilt für Hetzner (node1–3), JTL/ecomdata, Azure DevOps/Entra, Drittdienste
> Voraussetzung: Sicherungen vorhanden (Bericht 10, 6.11–6.12), GF-Freigabe, DSB informiert.
> Alle Werte in Befehlen sind **Platzhalter** (`<…>`). Neue Secrets nur im Key Vault ablegen, nie im Repo, Chat oder Ticket.

## 0. Grundsätze und Reihenfolge

**Ziel:** In **einem abgestimmten Zeitfenster** (≈ 3–4 h, außerhalb der Hauptbestellzeit) verlieren alle bisherigen Entwickler gleichzeitig jeden Zugang. Alle bekannten Secrets werden neu gesetzt. Gestaffelte Maßnahmen würden die Gegenseite vorwarnen.

| Schritt | Was | Warum zuerst / zuletzt |
|---|---|---|
| T-1 Tag | Eigene Zugänge testen (`mpiper` auf node1–3, Hetzner Robot mit 2FA, Azure DevOps-Admin, ecomdata-Kundenportal, Stripe-Owner, Mail-Hosting, DNS) | Nicht sich selbst aussperren |
| T-1 Tag | Neue Secrets erzeugen und im Key Vault ablegen (Abschnitt 6), noch nicht aktivieren | Zeitfenster kurz halten |
| T-1 Tag | ecomdata beauftragen: Termin und Ansprechpartner für Abschnitt 3 | ecomdata muss Admin-Aufgaben auf dem JTL-Server erledigen |
| T-1 Tag | Wartungsseite bzw. Hinweis vorbereiten; Kundensitzungen werden durch den JWT-Tausch ungültig | Alle Kunden müssen sich neu anmelden |
| **T0** | 1. Azure DevOps/Entra sperren (Abschnitt 1) | Kein Code-Zugriff mehr, keine Pushes |
| **T0** | 2. Hetzner: SSH-Schlüssel, Sitzungen, Swarm (Abschnitt 2) | Server-Zugriff weg |
| **T0** | 3. JTL/ecomdata: Firewall-IPs, Konten, Tokens (Abschnitt 3) | ERP-Zugriff weg |
| **T0** | 4. Secrets im Shop tauschen + Neustart der Container (Abschnitt 4) | Gestohlene Werte wertlos |
| **T0** | 5. Drittdienste (Abschnitt 5): Stripe, Mail, Strapi, Domains, netcup/Alloy | |
| T0+1 h | Prüfen (Abschnitt 7): Testbestellung, Logins, keine Fremdsitzungen | |
| danach | Freelancer **schriftlich** informieren (Herausgabe von Daten/Code, Löschbestätigung, AVV), Insolvenzverwalter | Nach Abstimmung mit GF/Recht |

**Hinweis:** Der Log-Export vom netcup-Server (Bericht 10, 6.11/6.13) sollte **vor T0** angefordert sein, falls er für die Forensik gebraucht wird. Nach der Aussperrung ist die Kooperation unsicherer.

---

## 1. Azure DevOps und Entra ID

**Bestand (abgefragt 09.10.2026):**

| Konto | Typ | Gruppe im Projekt | Zugriffsstufe | Zuletzt aktiv |
|---|---|---|---|---|
| Christopher Seidel (Extern), `c.seidel@neogris.com` | Entra-Gast | „Sustainable Solutions“ (→ Contributors) | Basic | 05.10.2026 |
| Dominic Bräuer (Extern), `dominicbraeuer@gmail.com` | Entra-Gast | „Sustainable Solutions“ (→ Contributors) | Basic | **08.10.2026 19:57** |

Keine Azure-RBAC-Rollen, keine Service-Connections, keine Pipelines. Die Gastkonten sind nur in „Alle Benutzer“.

```bash
ORG=https://dev.azure.com/Meesenburg-Group
P="Sicherheit und Service"

# 1a) Gruppe "Sustainable Solutions": Mitglieder ansehen
GRP=$(az devops security group list --org $ORG --project "$P" --query "graphGroups[?displayName=='Sustainable Solutions'].descriptor" -o tsv)
az devops security group membership list --org $ORG --id $GRP -o table

# 1b) Externe aus der Organisation entfernen (beendet alle Zugriffe, PATs und SSH-Keys in DevOps werden damit unbrauchbar)
az devops user remove --org $ORG --user c.seidel@neogris.com --yes
az devops user remove --org $ORG --user dominicbraeuer@gmail.com --yes

# 1c) Entra-Gastkonten sperren und Sitzungen widerrufen (statt löschen – bleibt für Audit nachvollziehbar)
for id in f3cc7340-bb86-46ea-a212-0d4628d3f3f3 99ffdf91-1091-481a-9a19-0d16c2539ebc; do
  az rest --method patch --url "https://graph.microsoft.com/v1.0/users/$id" --body '{"accountEnabled":false}'
  az rest --method post  --url "https://graph.microsoft.com/v1.0/users/$id/revokeSignInSessions"
done

# 1d) Prüfen
az devops user list --org $ORG --top 500 --query "items[].[user.displayName,user.mailAddress,lastAccessedDate]" -o table
```

Weitere Punkte:
- [ ] Gruppe „Sustainable Solutions“ umbenennen oder auflösen, interne Mitglieder (Röhl, Reincke, Schannak) in „Sicherheit und Service Team“ übernehmen.
- [ ] Branch-Policy auf `main`: mindestens 1 Reviewer, keine Selbstfreigabe, kein Force-Push (heute gibt es keine Freigaben, Bericht 10).
- [ ] Repo-Einstellungen: SSH-Schlüssel der Organisation prüfen (*Organization Settings → Policies*: „Allow public projects“ aus, „Third-party application access via OAuth“ aus, falls ungenutzt).
- [ ] **Audit-Log** der Organisation (*Organization Settings → Auditing*) für 01.08.–T0 exportieren: Clones, PAT-Erstellung, Berechtigungsänderungen.
- [ ] Organisationsweit weitere Externe prüfen (gesehen: turbinekreuzberg, diva-e, valantic, `kontakt_david-wischner.de`, Dienstkonto „Meesenburg-Group-WebShop-…“). Diese gehören nicht zu SN24, ein Review lohnt sich trotzdem.
- [ ] Nach der Rotation: **Historie bereinigen** (`git filter-repo --replace-text`), dann Force-Push nach Absprache; alle internen Clones neu ziehen. Kopien bei Gitea und den Entwicklern bleiben bestehen – deshalb ist die Rotation entscheidend, nicht das Löschen.

---

## 2. Hetzner (node1, node2, node3)

### 2.1 Hetzner-Konto (Robot)
- [ ] Inhaber und **Zahler** der drei Server bestätigen (Meesenburg-Konto mit 2FA?). Sind die Server in einem Konto des Alt-Dienstleisters, muss das vor allem anderen geklärt werden (Übertragung per „Server-Übergabe“ im Robot).
- [ ] Robot → *Einstellungen*: weitere Benutzer/Unterkonten, **Webservice-Benutzer** (API) und **hinterlegte SSH-Schlüssel** (werden beim Rescue-System verwendet) prüfen und fremde entfernen; Passwort und 2FA erneuern.
- [ ] Robot → *Firewall* je Server: Regeln setzen (siehe 2.4) – greift vor dem Betriebssystem.
- [ ] Rescue-System: Bei aktiviertem Rescue kann sich jeder mit hinterlegtem Schlüssel anmelden → nur eigene Schlüssel hinterlegt lassen.

### 2.2 SSH-Zugänge (je Server, als `mpiper`)

**Erledigt 12.10.2026** (node1–3): Schlüssel entfernt, private Entwickler-Schlüssel gelöscht, sshd gehärtet (`00-meesenburg.conf`, nur `mpiper`), offene Root-Sitzungen beendet; Test: Root/vitandi abgewiesen, mpiper+sudo OK. Sicherung vorher: Image `aussperrung_20261012/`.

Bestand: Nur ein Root-Schlüssel (Kommentar `vitandi@work`, `SHA256:wNsjgLdW…`) auf allen drei Servern, auf node1 zusätzlich `admin` mit Schlüssel „m.schulz“. Root- und admin-Passwort sind gesperrt, `PasswordAuthentication yes` (wirkungslos, aber abschalten).

```bash
for ip in 136.243.107.86 136.243.110.39 136.243.110.31; do
ssh -i ~/.ssh/hetzner_shop mpiper@$ip 'sudo bash -s' <<'EOF'
set -e
# Vorher sichern
cp -a /root/.ssh/authorized_keys /root/authorized_keys.bak_$(date +%F) 2>/dev/null || true
cp -a /home/admin/.ssh/authorized_keys /root/admin_authorized_keys.bak_$(date +%F) 2>/dev/null || true
# Alle Schlüssel für root und admin entfernen (Zugang nur noch über mpiper + sudo)
: > /root/.ssh/authorized_keys
rm -f /root/.ssh/authorized_keys2 /home/admin/.ssh/authorized_keys /home/admin/.ssh/authorized_keys2
# Private Schlüssel des Alt-Dienstleisters auf den Servern entfernen (sind gesichert)
rm -f /home/admin/.ssh/git_ssh /home/admin/.ssh/ssh_root_static_file /home/admin/.ssh/ssh_root_static_file.pub
# sshd härten
cat > /etc/ssh/sshd_config.d/00-meesenburg.conf <<'CFG'
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
AuthorizedKeysFile .ssh/authorized_keys
AllowUsers mpiper
CFG
sshd -t && systemctl reload ssh
EOF
done
```
**Vor dem Schließen der Sitzung** in einem zweiten Terminal testen: `ssh -i ~/.ssh/hetzner_shop mpiper@<ip> 'sudo -n true && echo ok'`.

- [ ] Weitere Meesenburg-Admins als eigene Benutzer anlegen (nicht `mpiper` teilen), Schlüssel im Key Vault/Passwortmanager dokumentieren.
- [ ] Laufende Fremdsitzungen beenden (z. B. `217.74.152.246` auf node1/node3):
```bash
who -u                                  # Sitzung + PID ermitteln
sudo pkill -KILL -t pts/<n>             # Terminal der fremden Sitzung beenden
sudo loginctl list-sessions; sudo loginctl terminate-session <id>
```

### 2.3 Persistenz prüfen (je Server)
```bash
sudo bash -c '
awk -F: "\$3>=1000 || \$3==0 {print \$1,\$3,\$7}" /etc/passwd; getent group sudo
ls -la /etc/sudoers.d; grep -rv "^#" /etc/sudoers.d
for u in root admin; do crontab -l -u $u 2>/dev/null | sed "s/^/$u: /"; done; ls /etc/cron.*
systemctl list-timers --all --no-pager
find /etc/systemd/system /lib/systemd/system -newermt "2026-06-01" -name "*.service" -o -newermt "2026-06-01" -name "*.timer"
grep -rn "curl\|wget\|nc \|bash -i" /root/.bashrc /home/*/.bashrc /etc/profile.d 2>/dev/null
docker ps --format "{{.Names}} {{.Image}}"; docker ps -q | xargs docker inspect -f "{{.Name}} {{range .Mounts}}{{.Source}} {{end}}" | grep -E " /( |etc|root)"
ss -tlnp
'
```
Auffälligkeiten dokumentieren, **nichts löschen**, was forensisch relevant sein könnte, ohne vorher zu sichern.

### 2.4 Netz und Dienste
- [ ] **Swarm auflösen** (ungenutzt, Port 2377 öffentlich, die Entwickler kennen die Join-Tokens):
  ```bash
  # node2 und node3:  sudo docker swarm leave --force
  # node1 zuletzt:    sudo docker swarm leave --force
  # Overlay-Netz web_cluster: hängt nur Traefik auf node1 → vorher Traefik-Netz prüfen, sonst bricht das Routing!
  ```
  Alternativ zunächst nur die Tokens rotieren: `sudo docker swarm join-token --rotate manager` und `... worker`.
- [ ] Firewall (Hetzner Robot oder `ufw`/nftables): eingehend nur 80/443 öffentlich; 22 nur von Meesenburg-IPs; 2377/7946/4789, 1025, 8025, 10002 zu.
- [x] node1: Ports 1025/8025/10002 per Firewall gesperrt (09.10.2026, `mee-ports-sperre.service`). Dauerhaft bei nächster Wartung aus `docker-compose.yml` entfernen und Firewall-Dienst danach abschalten.
- [x] node1: Traefik-Sperre für `/api/pdf`, `/api/maildata`, `/api/order/send*`, `mark_paid` (09.10.2026 11:49 UTC, `dynamic/zz-meesenburg-sperre-intern.yml`).
- [x] node1: **Alloy gestoppt** (09.10.2026 10:20 UTC, mit cAdvisor, Neustart-Regel `no`). Später entfernen bzw. auf eigenes Monitoring umstellen. Log-Export per Mail angefordert (Entwurf in `korrespondenz/`).
- [ ] node1: Traefik-Routen und Zertifikate für `mb-sn24.sustainable-solutions.it` entfernen (Fremd-Domain zeigt auf unseren Server).
- [ ] node2 (Testinstanz `sn24_test`, verbunden mit **Produktiv-JTL**): stoppen (`cd /home/admin/SN24_test && sudo docker compose stop`), DNS `test.meesenburg.neogris.com` liegt bei den Freelancern. Kundendaten in `db_backups` nach DSB-Entscheidung löschen.
- [ ] node3 (`configurators`): stoppen (`sudo docker compose -f /home/admin/configurators/compose.yml stop`); Univelop-API-Key steht in `proxy_api/config.yml` → bei Univelop sperren.
- [ ] Mittelfristig: Server ins eigene Konto/AKS umziehen, **root-Passwort** auf node1–3 setzen und in den Passwortmanager (Notfallzugang über die Hetzner-KVM-Konsole).

---

## 3. JTL-Server (ecomdata, `c10330`)

Die meisten Punkte brauchen **Admin-Rechte** → von ecomdata ausführen lassen oder ein eigenes Admin-Konto für Meesenburg anfordern.

### 3.1 Bei ecomdata beauftragen
- [ ] Vertragspartner und Ansprechpartner bestätigen (Mandant „Meesenburg GmbH“).
- [ ] **Eigenes Admin-Konto** für Meesenburg (nicht `useradmin` teilen); Passwort von `useradmin`, `Administrator`/`cpsadmin` neu setzen – **vorher klären**, wer `useradmin` heute nutzt (aktive Sitzung mit JTL-Worker, nicht einfach abmelden).
- [ ] Kundenportal-/Firewall-Freigaben: alle **dänischen IPs** (Norlys/Telia, z. B. `217.74.152.246`) und sonstige Entwickler-IPs entfernen; nur Meesenburg-Standorte und Shop-Server node1 (`136.243.107.86`) freigeben.
- [ ] NinjaOne: Wer hat Fernzugriff auf die VM? Nur ecomdata-Personal.
- [ ] RDP nur noch über VPN/Gateway mit MFA; Custom-API und JTL-REST nur über HTTPS.
- [ ] Backup-Konzept der VM und der SQL-Datenbank (SQL Agent ist gestoppt).

### 3.2 Windows (als Admin, PowerShell)
```powershell
# Lokale Konten und letzte Anmeldung
Get-LocalUser | Sort-Object LastLogon -Descending | Format-Table Name,Enabled,LastLogon,PasswordLastSet
Get-LocalGroupMember Administrators; Get-LocalGroupMember "Remote Desktop Users"

# Fremde Konten deaktivieren, Passwörter neu setzen
Disable-LocalUser -Name "<konto>"
Set-LocalUser -Name "useradmin" -Password (Read-Host -AsSecureString "Neues Passwort")

# Firewall: Regeln mit IP-Filtern für 3389/50443/58080 anzeigen
Get-NetFirewallRule -Enabled True -Direction Inbound | ForEach-Object {
  $r = $PSItem
  $port = $r | Get-NetFirewallPortFilter
  $addr = $r | Get-NetFirewallAddressFilter
  [pscustomobject]@{ Name=$r.DisplayName; Port=$port.LocalPort; Remote=($addr.RemoteAddress -join ',') }
} | Where-Object { $PSItem.Port -match '3389|50443|58080' } | Format-Table -AutoSize

# Adressliste einer Regel ersetzen (nur Meesenburg + Shop-Server)
Get-NetFirewallRule -DisplayName "<Regelname>" | Set-NetFirewallAddressFilter -RemoteAddress "<Meesenburg-IP>","136.243.107.86"

# Geplante Aufgaben (Start der Custom-API/JTL-REST, Fremdes)
Get-ScheduledTask | Where-Object State -ne Disabled | Select-Object TaskPath,TaskName,@{n='Run';e={$PSItem.Actions.Execute + ' ' + $PSItem.Actions.Arguments}},@{n='User';e={$PSItem.Principal.UserId}} | Format-Table -AutoSize

# Dienste/Autostart von Fernwartungs-Tools (AnyDesk, TeamViewer, RustDesk, ngrok, OpenSSH)
Get-Service | Where-Object { $PSItem.Name -match 'anydesk|teamviewer|rust|ngrok|sshd|vnc' }
Get-CimInstance Win32_StartupCommand | Format-Table Name,Command,User
# OpenSSH-Server: authorized_keys prüfen
Get-ChildItem C:\ProgramData\ssh -Force; Get-Content C:\ProgramData\ssh\administrators_authorized_keys -ErrorAction SilentlyContinue
```

### 3.1a Freigegebene IPs der Windows-Firewall (Stand Sicherung 08.10.2026)
| Regel | Port | Freigegebene Adressen |
|---|---|---|
| `{375C23DF-…}` | 50443 (JTL-REST) | 212.97.236.0/24, 62.198.143.0/24, 79.200.133.76, 217.74.153.210, **136.243.107.86 (node1)**, 136.243.110.39 (node2), 136.243.110.31 (node3), 152.53.32.159 (neogris-Server) |
| `{787DE77D-…}` | 58080 (Custom-API) | wie oben, zusätzlich 212.97.237.0/24 und **217.74.152.0/24** (Netz der Root-Sitzungen) |

**Erledigt 12.10.2026:** beide Regeln nur noch `136.243.107.86`; geprüft von node1 (erreichbar), node2, node3 und extern (gesperrt). Ziel: nur noch node1. Skript `tools/restrict_jtl_firewall.ps1` (Probelauf, `-Apply` mit vorherigem `netsh`-Export). RDP (3389) hat keine IP-Regel in der Windows-Firewall → wird im **ecomdata-Kundenportal** gesteuert; dort alle Fremd-IPs entfernen, die eigenen Meesenburg-IPs behalten.

### 3.2a Was an den Windows-Konten hängt (Stand Sicherung 08.10.2026)
| Konto | Abhängigkeit | Folge eines Passwortwechsels | Umgang |
|---|---|---|---|
| `useradmin` | **Custom-API** = geplante Aufgabe `jtl_db_reader` (Autor/Benutzer `useradmin`, `LogonType Password` = gespeichertes Passwort, Starttrigger **beim Systemstart** +30 s, `uv run manage_apis.py` in `C:\Users\useradmin\Documents\jtl_db_reader`) | Läuft weiter, aber **startet nach dem nächsten Neustart nicht mehr** (z. B. Patch-Neustart durch ecomdata/NinjaOne) → Shop ohne Custom-API | Mit `-UpdateDependencies` rollieren (setzt das neue Passwort in der Aufgabe) |
| `useradmin` | **JTL-Worker** läuft in der interaktiven Sitzung | Bleibt aktiv; nach Abmeldung/Neustart nur mit neuem Passwort (bzw. Autologon) | Sitzung nicht abmelden; Autologon prüfen (Skript) |
| `useradmin` | Port-Reservierung JTL-REST `https://212.60.27.117:50443/` | Keine (Reservierung hängt am Konto, nicht am Passwort) | – |
| `useradmin` | JTL-REST (`JTL.Wawi.Rest`) in Sitzung 0, Startweg unbekannt | Falls Aufgabe/Dienst mit gespeichertem Passwort → wie Custom-API | Probelauf des Skripts zeigt es |
| `cpsadmin` / `Administrator` | Konto des Hosters ecomdata | ecomdata ausgesperrt | Vorher mit ecomdata abstimmen oder ausnehmen |
| `IcingaUser` | Monitoring ecomdata | Monitoring kann ausfallen | Mit ecomdata abstimmen oder ausnehmen |
| `nord1` … `nord5` | Mitarbeiter, die per RDP in der Wawi arbeiten | Müssen neue Passwörter bekommen | Verteilung vorbereiten; Wawi-Benutzer (eigene Anmeldung) sind nicht betroffen |

Skript: `tools/rotate_local_passwords.ps1` – **fest geschützt (Vorgabe 12.10.2026): `mpiper`, `cpsadmin`, `Administrator`, `IcingaUser`** (Probelauf standardmäßig, `-Apply`, `-UpdateDependencies`, Ausgabe-CSV nur für den eigenen Benutzer lesbar). Bestehende RDP-Sitzungen bleiben nach dem Wechsel angemeldet → danach `tools/disconnect_rdp_sessions.ps1 -Apply`: trennt alle Sitzungen außer der eigenen (Programme laufen weiter, JTL-Worker bleibt aktiv, beim Wiederverbinden wird das neue Passwort abgefragt); `-Logoff` meldet ab, `useradmin` wird nie abgemeldet.

### 3.3 SQL Server (`JTLWAWI`, SSMS als sysadmin)
```sql
-- Logins und letzte Änderung
SELECT name, type_desc, is_disabled, create_date, modify_date FROM sys.server_principals
WHERE type IN ('S','U','G') ORDER BY modify_date DESC;
SELECT r.name AS rolle, m.name AS mitglied FROM sys.server_role_members rm
JOIN sys.server_principals r ON r.principal_id = rm.role_principal_id
JOIN sys.server_principals m ON m.principal_id = rm.member_principal_id;

-- Login der Custom-API (jtl_db_reader) und 'sa' neu setzen
ALTER LOGIN [<custom_api_login>] WITH PASSWORD = '<neu>';
ALTER LOGIN [sa] WITH PASSWORD = '<neu>';   -- nur wenn SQL-Auth genutzt; vorher JTL-Wawi-Clients prüfen!
-- Fremde Logins sperren
ALTER LOGIN [<login>] DISABLE;
```
**Achtung:** Die JTL-Wawi-Clients der Mitarbeiter nutzen ein eigenes SQL-Login. Ein Passwortwechsel muss in allen Wawi-Profilen nachgezogen werden → mit ecomdata/JTL-Partner abstimmen.

### 3.4 JTL-Wawi
- [ ] *Admin → Benutzerverwaltung*: Wawi-Benutzer der Entwickler (und alte Dienstleister-Benutzer) deaktivieren; Passwörter aller Admin-Benutzer neu setzen.
- [ ] *Admin → JTL-Wawi API / App-Registrierungen*: App `icsWebshop/v1` **löschen** und neu registrieren → neues API-Token (`Wawi <…>`). Damit ist das Token aus Readme und `.env` ungültig. Neues Token in Key Vault `Jtl--ApiToken` und Shop-`.env` (`JTL__API_TOKEN`).
- [ ] **Custom-API-Token** (`CUSTOM_API__API_TOKEN`): Steht in der Konfiguration von `jtl_db_reader` im gesperrten Profil → mit Admin-Rechten neu setzen, Dienst neu starten; neuer Wert in Key Vault `CustomApi--ApiToken` und Shop-`.env`.
- [ ] JTL-Kundenkonto (jtl-software.de) und ggf. JTL-Partnerzugang: Wer hat Zugang? Passwort und 2FA.
- [ ] Ordner `C:\TMP\SMIG` und zweite SQL-Instanz `JTLWAWI2` (Altdaten 04/2025): Zweck klären, ggf. sichern und löschen.

---

## 4. Shop-Secrets tauschen (node1)

**Ist-Zustand live (09.10.2026, aus `docker inspect`, maskiert):** Alle Werte außer JTL, Custom-API, Stripe und SMTP sind **Standardwerte aus dem Repo**.

| Secret | Live-Wert | Risiko | Neu setzen über |
|---|---|---|---|
| `SECURITY__KEY` (JWT-Signatur Login, auth + business) | `foob…` (**6 Zeichen, steht im Repo**) | **Kritisch:** Jeder mit Repo-Kenntnis kann Login-Tokens für beliebige Kunden fälschen | `.env` + Compose-Variable |
| Mongo-Root-Passwörter (4×) | `pw12…` (5 Zeichen, Repo) | Mittel (Ports nicht öffentlich) | `changeUserPassword` + `.env` |
| RabbitMQ `RABBITMQ_DEFAULT_PASS` / `MESSAGE_QUEUE__PASSWORD` | `pw12…` | Mittel | `rabbitmqctl change_password` + `.env` |
| Strapi `APP_KEYS`, `API_TOKEN_SALT`, `ADMIN_JWT_SECRET`, `TRANSFER_TOKEN_SALT`, `JWT_SECRET` | Repo-Werte | **Hoch:** Admin-Sitzungen fälschbar | `.env` |
| Strapi-DB (`DATABASE_PASSWORD`, `MYSQL_*`) | `pw12…` | Mittel | `ALTER USER` + `.env` |
| Strapi-Admins, API-Tokens | aus Dev-Dump? | Hoch | Strapi-Admin-Oberfläche |
| `JTL__API_TOKEN`, `CUSTOM_API__API_TOKEN` | live, im Repo | Kritisch | Abschnitt 3.4 |
| `STRIPE_SECRET_KEY` (**`sk_live_`**), `STRIPE_SIGNING_SECRET` | live | Kritisch | Abschnitt 5.1 |
| `MAIL_SMTP_PASSWORD` (`k69g81.meinserver.io`, Postfach `@sicherheitstechnik-nord.de`) | live, im Repo | Hoch (Phishing im Shop-Namen) | Abschnitt 5.2 |
| Blowfish-Schlüssel Alt-Shop (`migrate_jtl_data.py`) | im Repo | Mittel (Alt-Kundendaten) | Alt-Dumps löschen, Schlüssel aus Repo |

### 4.1 Neue Werte erzeugen und ablegen
```bash
KV=<key-vault-name>
gen(){ openssl rand -base64 48 | tr -d '/+=\n' | cut -c1-48; }
for s in Shop--SecurityKey Shop--MongoAuth Shop--MongoBusiness Shop--MongoItems Shop--MongoPayment Shop--RabbitMq \
         Shop--StrapiDbPassword Shop--StrapiAdminJwtSecret Shop--StrapiJwtSecret Shop--StrapiApiTokenSalt Shop--StrapiTransferTokenSalt; do
  az keyvault secret set --vault-name $KV --name $s --value "$(gen)" --output none
done
# Strapi APP_KEYS = 4 Schlüssel, kommagetrennt
az keyvault secret set --vault-name $KV --name Shop--StrapiAppKeys --value "$(gen),$(gen),$(gen),$(gen)" --output none
```

### 4.2 Datenbank- und Broker-Passwörter ändern (im laufenden Container, dann `.env`)
```bash
cd /home/admin/SN24
# MongoDB (je Instanz; Root-Benutzername aus der Container-Umgebung)
for c in auth business items payment; do
  NEW='<neu-aus-keyvault>'
  sudo docker exec -e NEW="$NEW" sn24-${c}_mongo-1 sh -c \
    'mongosh --quiet -u "$MONGO_INITDB_ROOT_USERNAME" -p "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin \
     --eval "db.getSiblingDB(\"admin\").changeUserPassword(\"$MONGO_INITDB_ROOT_USERNAME\", process.env.NEW)"'
done
# RabbitMQ
sudo docker exec sn24-rabbitmq-1 rabbitmqctl change_password <user> '<neu>'
# MariaDB (Strapi)
sudo docker exec -it sn24-strapi_db-1 mariadb -uroot -p -e "ALTER USER 'root'@'%' IDENTIFIED BY '<neu>'; ALTER USER '<strapi_user>'@'%' IDENTIFIED BY '<neu>'; FLUSH PRIVILEGES;"
```
**Achtung, Fallstrick:** `MONGO_INITDB_*` wirkt nur beim ersten Start einer leeren Datenbank. Nach `changeUserPassword` müssen **alle** Dienste den neuen Wert über `.env` bekommen, sonst schlägt die Verbindung fehl. Bekannter Bug: `payment_svc` setzt `MONGO_PASSWORD` auf den Benutzernamen (`compose/*/payment_svc.yml`, Bericht 04 #19) → Variable korrigieren.

### 4.3 `.env` und Compose umstellen, neu starten
- [ ] Alle Werte aus 4.1/4.2 sowie JTL, Custom-API, Stripe und SMTP in `/home/admin/SN24/.env` eintragen (Datei `chmod 600`, Eigentümer root).
- [ ] In den Compose-Dateien **Standardwerte entfernen** (`${VAR:-pw12…}` → `${VAR:?fehlt}`), damit ein fehlender Wert den Start verhindert statt still den Repo-Wert zu nehmen.
- [ ] Neu starten (Wartungsfenster): `cd /home/admin/SN24 && sudo docker compose up -d` → danach Abschnitt 7.
- [ ] Folge: Alle Kunden-Logins werden ungültig (neuer `SECURITY__KEY`), Strapi-Admins müssen sich neu anmelden.

---

## 5. Drittdienste

### 5.1 Stripe (live)
- [ ] Dashboard → *Einstellungen → Team*: Wer hat Zugang? Entwickler und Alt-Dienstleister entfernen; Owner = Meesenburg; 2FA erzwingen.
- [ ] *Entwickler → API-Schlüssel*: **Secret Key rollen** (sofort ablaufen lassen), Restricted Keys prüfen und löschen.
- [ ] *Webhooks*: Endpunkte prüfen (nur `sicherheit.meesenburg.com`), **Signing Secret rollen**; fremde Endpunkte (z. B. Test-/neogris-Domains) löschen.
- [ ] *Sicherheitsverlauf/Logs*: API-Zugriffe von fremden IPs seit Juni prüfen.
- [ ] Neuer Key → Key Vault `Shop--StripeSecretKey`, `Shop--StripeSigningSecret` → `.env`.
- [ ] **Testschlüssel mitrollen** (geprüft 10.10.2026 per `GET /v1/account`, beide Meesenburg):
  - **A** = Testmodus des Live-Kontos `acct_1SKDgIP8r25vnpQU` („Meesenburg GmbH“, gleiche Konto-ID wie der Live-Schlüssel): in Testinstanz node2 (`SN24_test/.env`), alten node1-`.env`-Ständen und **übergangsweise in den POC-User-Secrets** (`Parameters:stripe-*` im AppHost). Nach dem Rollen neuen Testschlüssel dort eintragen.
  - **B** = eigene Sandbox `acct_1SKDgW0YgQUlpmuU` („Meesenburg GmbH Sandbox“, Kontakt `i.…@meesenburg.de`): steht im Repo-Readme und in `.env_test`. Rollen oder Sandbox löschen, falls ungenutzt.
  - Webhook-Secret von node2 gehört zum Test-Endpunkt der Freelancer (neogris-Testinstanz) → Endpunkt im Testmodus löschen; lokal für den POC `stripe listen` mit eigenem Secret nutzen.

### 5.2 Mail (`k69g81.meinserver.io`, Postfach `…@sicherheitstechnik-nord.de`)
- [ ] Wer verwaltet das Hosting (Kundenkonto)? Passwort des Shop-Postfachs ändern, Weiterleitungen/Regeln im Postfach prüfen (Abfluss!).
- [ ] Rückfall-Empfänger `c.seidel@neogris.com` im Code/`.env` entfernen.
- [ ] Domain `sicherheitstechnik-nord.de`: DNS-/Registrar-Zugang bei Meesenburg? SPF/DKIM prüfen.

### 5.3 Strapi (CMS)
- [ ] *Settings → Administration Panel → Users*: alle Entwickler-Admins löschen, eigenes Admin-Konto, starke Passwörter.
- [ ] *Settings → API Tokens / Transfer Tokens*: alle löschen und nur benötigte neu erzeugen (neues Token ins Frontend/Backend).
- [ ] Strapi „Global“-Instanz `cms.sn24.sustainable-solutions.it` (netcup, Freelancer): Inhalte exportieren, Abhängigkeit beenden.

### 5.4 Domains, Zertifikate, Fremdsysteme
- [ ] DNS `sicherheit.meesenburg.com` / `cms.sicherheit.meesenburg.com`: Zugang nur Meesenburg-IT.
- [ ] Let's-Encrypt-Account in Traefik (`acme.json`) läuft auf eine Mail des Alt-Dienstleisters → neue Account-Mail, Zertifikate neu ausstellen lassen.
- [ ] **netcup `85.235.65.67`** (Loki/Prometheus/Grafana, Seidel/neogris): Log-Export + Löschbestätigung anfordern; danach Alloy-Ziel entfernen (2.4).
- [ ] Gitea `gitea.sustainable-solutions.it`, Nextcloud (`138.201.221.151`), Registry, `svc_helper` (GitLab `vitandi1`): Herausgabe über den Insolvenzverwalter; `svc_helper` als Kopie im eigenen Repo vendoren.
- [ ] Univelop (`app.univelop.de`, Datenquelle der `configurators`): Inhaber des Kontos, API-Key sperren, Daten exportieren.
- [ ] `erp-demo.meesenburg.neogris.com`: Was läuft dort mit Meesenburg-Bezug? Herausgabe/Löschung verlangen.

---

## 6. Key Vault als einzige Quelle

| Secret-Name | Ziel |
|---|---|
| `Jtl--ApiToken`, `Jtl--BaseUrl`, `CustomApi--ApiToken`, `CustomApi--BaseUrl` | POC/neue API (bereits so vorgesehen) |
| `Shop--SecurityKey`, `Shop--Mongo*`, `Shop--RabbitMq`, `Shop--Strapi*` | Alt-Shop `.env` bis zur Ablösung |
| `Shop--StripeSecretKey`, `Shop--StripeSigningSecret`, `Shop--SmtpPassword` | Alt-Shop und neue API |

- [ ] Zugriff auf den Key Vault nur für benannte Meesenburg-Konten (RBAC „Key Vault Secrets Officer/User“), Diagnoseprotokolle an.
- [ ] Übergangsweise `.env` aus dem Key Vault erzeugen (Skript), nicht von Hand pflegen.

---

## 7. Prüfung nach T0
- [ ] `ssh root@<ip>` mit dem vitandi-Schlüssel → **muss scheitern** (Test durch Meesenburg mit gesichertem Schlüssel `~/.ssh/sn24_hetzner_root`); danach diesen Schlüssel lokal löschen.
- [ ] `who`/`last` auf node1–3: nur `mpiper`.
- [ ] Shop: Startseite, Login (neu anmelden), Testbestellung mit Stripe-Testkarte **im Testmodus** bzw. echte Kleinstbestellung → Auftrag in JTL, Mail, PDF.
- [ ] JTL: Altes API-Token liefert 401; neues Token funktioniert (`GET /api/eazybusiness/v1/items?pageSize=1`).
- [ ] Stripe: Webhook-Zustellung grün.
- [ ] Alloy: keine Verbindungen mehr zu `85.235.65.67` (`sudo ss -tnp | grep 85.235.65.67`).
- [ ] Azure DevOps: Externe nicht mehr in der Benutzerliste; Audit-Log exportiert.
- [ ] Alles im Protokoll (`_PROTOKOLL.md` im Sicherungs-Image) mit Uhrzeit dokumentieren (DSGVO-Nachweis).

## 8. Danach
- Freelancer schriftlich: Ende der Zugänge, Herausgabe (Code-Stände, Custom-API-Quellcode, Strapi-Global, Logs), **Löschbestätigung** aller Kunden- und Zugangsdaten (Testinstanz-DBs, `db_backups`, Loki), Abrechnung.
- Insolvenzverwalter: Rechte am Code, Gitea/Nextcloud/Registry, Domain `sustainable-solutions.it`.
- DSB: Dokumentation Art. 33 DSGVO (Fristen ab Kenntnis 07./09.10.2026).
