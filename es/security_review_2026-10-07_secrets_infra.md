# Sicherheitsprüfung Meesenburg.ES – Secrets, Konfiguration, Container/Deployment, Abhängigkeiten, Git-Historie

- **Datum:** 2026-10-07
- **Art:** statische, rein lesende Prüfung im Auftrag des Eigentümers (keine Änderungen am Code, keine Tests gegen Systeme)
- **Umfang:** Arbeitsverzeichnis `/Users/piper/repos/Meesenburg.ES` (inkl. nicht committeter und nicht versionierter Dateien), Git-Index (gestagte Dateien) und die komplette Git-Historie (`git log --all`, 16 Commits, 2025-01-13 bis 2025-04-14)
- **Nicht im Umfang:** Anwendungslogik (AuthZ, Injection, DSGVO). Das prüft ein anderer Review.
- **Maskierung:** Secrets stehen in diesem Bericht nie im Klartext. Lange Secrets (ab 20 Zeichen) werden mit den ersten 4 Zeichen plus „…“ angegeben. Kurze Secrets (SAP-Passwort, Lizenzschlüssel, Domänenpasswort) nur mit den ersten 2 Zeichen, weil 4 Zeichen hier schon fast die Hälfte des Werts verraten würden. Dahinter steht jeweils die Länge.

---

## 1. Zusammenfassung

Die Aussage „ein paar Überbleibsel von Secrets“ trifft es nicht ganz. Im Repository liegen **sieben verschiedene, wahrscheinlich gültige Produktiv-Credentials**. Mindestens vier davon stehen auch im Git-Verlauf, der vollständig zu **Azure DevOps** (`Meesenburg-Group/Meesenburg/_git/Meesenburg.ES`) gepusht ist (`master` = `origin/master`). Alle, die in der Azure-DevOps-Organisation Lesezugriff auf dieses Repo haben, können sie also lesen. Dass das Repo veraltet ist, hilft nicht: Die Secrets darin sind dieselben, die heute noch in den Dockerfiles und appsettings verwendet werden.

Die drei schwerwiegendsten Punkte:

1. **Entra-Client-Secret der App `b4961a2a-…` („Key-Vault-App“) in 15 Fundstellen.** Es steht in allen `Dockerfile-Prod` als `ENV`, außerdem in `aspire-manifest.json`, `infra/docker-compose.yaml` und `AppHost/appsettings.json`. Seit März 2025 steht es auch im Git-Verlauf (dort zeitweise hart codiert in `AppHost/Program.cs`). Weil es per `ENV` gesetzt wird, steckt es **in jedem Layer jedes gebauten Images** in der Container-Registry. Über `DefaultAzureCredential` greift es auf den Key Vault `kv-meesenburg-prod` zu und damit auf **alle dort abgelegten Secrets** (c4v-API, Mule, IMAP, SAP-SOAP, Verschlüsselungsschlüssel für die Personalnummern …). Außerdem hat es Vorrang vor Managed bzw. Workload Identity (siehe ES-I-09).
2. **SAP-Passwort des Dienstbenutzers `srvusr01` (Mandant 100) und zwei ERPConnect-Lizenzschlüssel.** Sie stehen seit dem **allerersten Commit** (2025-01-13) in allen 16 Commits und heute noch in `SAPBroker.cs` und `appsettings.json`.
3. **Neue, noch nicht committete, aber teilweise schon gestagte Secrets:** das Graph-App-Secret (`8a27b169-…`, Mail- und Postfachzugriff), das außerdem **als `UserSecretsId` missbraucht** wird und dadurch in die kompilierten DLLs gelangt. Dazu das Web-OIDC-Secret (`fab99478-…`) und ein **Domänenpasswort im Klartext** (`meesenburg\es2kerp` für die Dateifreigabe `clfs002`). Mit dem nächsten `git commit && git push` landen sie ebenfalls dauerhaft in Azure DevOps.

Dazu kommen Schwächen im Container- und Deployment-Setup. Die Images enthalten die appsettings mit Secrets. Der Build-Kontext ist der komplette Repo-Ordner (`COPY . .`), und der DevOps-PAT wird als `ENV` gesetzt. Redis läuft in der Compose-Datei ohne Passwort, alle Fach-APIs sind als „external“ veröffentlicht, Tags sind `:latest`. Die Abhängigkeiten sind weitgehend aktuell. Kritisch sind vor allem die ins Repo eingecheckten Fremd-Binaries: ICU 50 von 2012, das SAP NW RFC SDK und ERPConnect.

**Gesamteinschätzung: hohes Risiko (kritisch).** Ursache ist nicht ein einzelner Fehler, sondern die Breite und Dauer der Offenlegung: produktive Credentials mit Zugriff auf den Key Vault, auf SAP und auf Postfächer, verteilt über Git-Remote, Images und Build-Artefakte. **Rotation hat Vorrang vor jeder Bereinigung.** Ein Umschreiben der Historie allein reicht nicht.

---

## 2. Secrets-Inventar (maskiert)

Legende „im Git“: **H** = in mindestens einem Commit (und damit auf dem Remote), **I** = nur im Index (gestagt, noch nicht committet), **W** = nur im Arbeitsverzeichnis (unversioniert oder nur lokal geändert).

| # | Typ | Fundstelle(n) Datei:Zeile | Maskiert | im Git | vermutlich produktiv? | Empfehlung |
|---|-----|---------------------------|----------|--------|-----------------------|------------|
| S1 | **Entra Client Secret** – App `b4961a2a-0bb7-4f0a-a701-33f75612650d` (Tenant `a7473855-…`), verwendet als `AZURE_CLIENT_SECRET` für `DefaultAzureCredential` → Key Vault `kv-meesenburg-prod` | `Meesenburg.ES.Web/Dockerfile-Prod:10`; `Meesenburg.ES.ApiService/Dockerfile-Prod:8`; `ESDataWorker/Dockerfile-Prod:6`; `Meesenburg.de.ES.API.ESData/Dockerfile-Prod:8`; `Meesenburg.ES.API.SAPData/Dockerfile-Prod:8`; `Meesenburg.ES.API.Mule/Dockerfile-Prod:8`; `Meesenburg.ES.API.Graph/Dockerfile-Prod:8`; `Meesenburg.ES.API.Graph/Dockerfile:8`; `Meesenburg.ES.AppHost/appsettings.json:18`; `Meesenburg.ES.AppHost/aspire-manifest.json:72,101,133`; `Meesenburg.ES.AppHost/infra/docker-compose.yaml:50,70,109` | `uoN8…` (40, Entra-Format `…Q~…`) | **H** (Commits `9ca460a`…`eed1920`: früher `AppHost/Program.cs` Z. 26/38, `Graph/Web/ESData Dockerfile-Prod` Z. 8) + I + W | **ja** (Prod-Key-Vault, alle Prod-Dockerfiles) | **Sofort rotieren.** Danach kein Secret mehr: AKS Workload Identity / Managed Identity mit RBAC „Key Vault Secrets User“. ACR-Images mit altem Secret löschen. |
| S2 | **Entra Client Secret** – App `8a27b169-806f-4baf-a8a2-ab8415b611d0` (Microsoft Graph, Mail-Versand, IMAP/Postfach `invoices@…`) | `Meesenburg.ES.API.Graph/appsettings.json:11,15`; **als `<UserSecretsId>`** in `Meesenburg.ES.API.Graph/Meesenburg.ES.API.Graph.csproj:7` und `Meesenburg.ES.API.Mule/Meesenburg.ES.API.Mule.csproj:7` → dadurch auch in `obj/**/AssemblyInfo.cs` (8 Kopien) und im `UserSecretsIdAttribute` der kompilierten DLLs | `Ps28…` (40, Entra-Format) | **I** (gestagt) + W | **ja** | **Rotieren.** `UserSecretsId` durch eine neue GUID ersetzen, Secret in den Key Vault oder besser Managed Identity bzw. Zertifikat. Images neu bauen. |
| S3 | **Entra Client Secret** – App `fab99478-1842-41da-b4a2-8e4cfeab2c1c` (Blazor-Portal, OIDC + Token-Abruf für Downstream-APIs) | `Meesenburg.ES.Web/appsettings.json:14` (außerdem in `Meesenburg.ES.Web/bin/**/appsettings.json`, auch `publish/`) | `hZE8…` (40, Entra-Format) | W (nur lokal geändert, nicht in HEAD/Index) | **ja** | **Rotieren** (es lag in gebauten Images, siehe ES-I-08). In den Key Vault (`EntraId:ClientSecret`) oder Zertifikat / Federated Credential. |
| S4 | **SAP-Passwort** – Dienstbenutzer `srvusr01`, Mandant `100` (HCM/MEP, Hosts `172.20.22.41/.52`) | `Meesenburg.ES.API.SAPData/Brokers/ERP/SAPBroker.cs:43` (hart codiert); `Meesenburg.ES.API.SAPData/appsettings.json:25` (+ 8 Kopien in `bin/`) | `sr…` (9) | **H** (alle 16 Commits seit `70febb6`, 2025-01-13) + I + W | **ja** | **Sofort in SAP ändern** (SU01). Danach nur noch über den Key Vault (`Sap:Credentials:Password`), Default in `SAPBroker.cs` entfernen. Benutzer auf minimale Berechtigungen prüfen. |
| S5 | **Lizenzschlüssel ERPConnect** (aktiv) | `Meesenburg.ES.API.SAPData/Brokers/ERP/SAPBroker.cs:51`; `Meesenburg.ES.API.SAPData/appsettings.json:9` (+ `bin/`) | `F2…` (10) | **H** (alle 16 Commits) + I + W | ja | Beim Hersteller (Theobald Software) klären, ob ein Austausch nötig ist. Im Key Vault ablegen, aus Code und appsettings entfernen. |
| S6 | **Lizenzschlüssel ERPConnect** (alt, auskommentiert) | `Meesenburg.ES.API.SAPData/Brokers/ERP/SAPBroker.cs:50` | `PW…` (10) | **H** (alle 16 Commits) + I + W | vermutlich nicht mehr (auskommentiert) | Zeile entfernen. Prüfen, ob die Lizenz noch gültig oder zugeordnet ist. |
| S7 | **AD-Domänenpasswort** – `meesenburg\es2kerp`, SMB-Zugriff auf `\\clfs002.meesenburg.de\Drives\Daten\Programme\SAP\MEP\ES_ERP\Rechnungen` | `ESDataWorker/Services/AccountingService.cs:28` (Benutzer/Domäne Z. 26–27, Host Z. 19) | `tD…` (14) | **I** (gestagt, Datei neu) + W | **ja** | **Passwort im AD ändern.** Ins Key Vault verschieben, gMSA/Dienstkonto mit minimalen Freigaberechten nutzen. Prüfen, ob ein Konto mit Klartext-Passwort in einem Container nötig ist. |
| S8 | Redis-Passwort (lokale Aspire-Instanz, automatisch erzeugt) | `.idea/.idea.Meesenburg.ES/.idea/dataSources.xml:15,22` (JDBC-URL `redis://<pw>@localhost:…`) | `b1Mg…` (22) | **H** (in HEAD) | nein (lokal, flüchtig) | Gering. `.idea/` aus Git entfernen. |
| S9 | Platzhalter (kein Secret) | `Meesenburg.ES.API.Mule/appsettings.json:18` (`AzureAd:ClientSecret` = Template-Platzhalter aus dem MS-Identity-Template); `nuget.config:12` (`%AZURE_DEVOPS_PAT%` = Umgebungsvariable); `infra/.env:11,20` (leer) | – | I/W | nein | Platzhalter entfernen (verwirrend). Die `nuget.config` ist in Ordnung, siehe aber ES-I-07. |

**Nicht gefunden** (aktueller Stand, Index und gesamte Historie, ohne `bin/obj` und `wwwroot/lib`): Mongo-Connection-Strings mit Passwort, Storage-AccountKeys, Service-Bus-SAS-Keys (Service Bus nutzt `FullyQualifiedNamespace` + Entra), private Schlüssel (`-----BEGIN`), JWTs, Azure-DevOps-PATs im Klartext, AWS-Keys. `.http`-Dateien enthalten nur Platzhalter-Tokens. `Meesenburg.ES.API.SAPData/Http/customer-create.http` enthält nur Testdaten.

**Ohne Secrets, aber sensibel:**
- `Meesenburg.ES.API.SAPData/dev_rfc.log` (492 Zeilen, durch `*.log` ignoriert): enthält interne SAP-Host-IP `172.20.22.52:3300`, Hostnamen und Heimnetz-IPs des Entwickler-Rechners und den OS-Benutzer. Keine Passwörter.
- `docs/TODO.md:6–9` beschreibt die Secrets S1 und S2 samt Fundstellen. Das Dokument ist unversioniert, als Wegweiser aber heikel, falls es committet wird.

---

## 3. Git-Historie und Remote

- **Remote:** `origin` → `https://dev.azure.com/Meesenburg-Group/Meesenburg/_git/Meesenburg.ES`. Die URL enthält **keine** Credentials. In `.git/config` sind ebenfalls keine Credentials hinterlegt.
- **Stand:** 16 Commits, nur der Branch `master`, letzter Commit `eed1920` vom 2025-04-14. `master` und `origin/master` sind identisch, **die gesamte Historie liegt also auf dem Server.** Keine Stashes.
- **Arbeitsverzeichnis:** 745 Änderungen, davon 57 neu gestagt (`A`), 322 gestagt und geändert (`AM`), 243 unversioniert. Der **Index enthält bereits S1 (Dockerfiles), S2, S4, S5, S6 und S7.** Ein unbedachtes `git commit -a && git push` würde das alles dauerhaft veröffentlichen.

Secrets in der Historie (alle Commits auf dem Remote):

| Secret | Erster Commit | Fundstellen in der Historie | Heute noch vorhanden? |
|--------|---------------|-----------------------------|-----------------------|
| S4 SAP-Passwort | `70febb6` (2025-01-13) | `SAPBroker.cs` (Z. 22–40, wechselnd) in allen 16 Commits | ja (`SAPBroker.cs:43`, `appsettings.json:25`) |
| S5/S6 ERPConnect-Lizenzen | `70febb6` | `SAPBroker.cs` (Z. 29–33) in allen 16 Commits | ja |
| S1 Key-Vault-App-Secret | `9ca460a` (2025-03-14) | `Meesenburg.ES.AppHost/Program.cs` Z. 26/38 (hart codiert; heute über Konfiguration gelöst, **in der Historie aber erhalten**), `Meesenburg.ES.API.Graph/Dockerfile-Prod:8`, `Meesenburg.ES.Web/Dockerfile-Prod:8`, `Meesenburg.de.ES.API.ESData/Dockerfile-Prod:8` – 9 Commits bis `eed1920` | ja (15 Fundstellen) |

**Gelöscht, aber noch in der Historie:** im eigentlichen Sinn nur die **hart codierte Variante von S1 in `AppHost/Program.cs`**. Alle anderen Secrets aus der Historie sind auch heute noch vorhanden. Ein früheres, inzwischen entferntes Secret, das nur noch in der Historie stünde, wurde nicht gefunden. Gesucht wurde nach Entra-Secret-Format, `Password/Pwd/Secret/Token/ApiKey/LicenseKey/ConnectionString`-Zuweisungen, `AccountKey=`, `SharedAccessKey=`, `mongodb://`, `-----BEGIN`, `SetLic(`, JWT und 52-stelligen PATs.

**Folgerung:** Die Historie umzuschreiben (`git filter-repo` bzw. BFG) ist sinnvoll, aber **zweitrangig**. Die Werte sind bereits verteilt: auf den Azure-DevOps-Server, in alle Klone, in ACR-Images und in lokale `bin/`-Ordner. Sie gelten als kompromittiert und müssen **rotiert** werden. Prüfen, ob Azure DevOps Advanced Security / Secret Scanning (Push Protection) für das Projekt aktiviert werden kann.

---

## 4. Container und Deployment

### 4.1 Dockerfiles

| Datei | Base-Image | User | Secrets | Weitere Auffälligkeiten |
|-------|------------|------|---------|-------------------------|
| `*/Dockerfile-Prod` (Web, ApiService, ESDataWorker, ESData, SAPData, Mule, Graph) | `mcr.microsoft.com/dotnet/aspnet:10.0` / `sdk:10.0` (Tag, kein Digest) | `USER $APP_UID` (non-root) ✔ | **`ENV AZURE_CLIENT_SECRET=…` (S1) in der *final*-Stage** → im Image-Config und in `docker inspect` sichtbar; `ARG`/`ENV AZURE_DEVOPS_PAT` in der Build-Stage | `COPY nuget.config .` + **`COPY . .` vor dem Restore** (gesamter Repo-Kontext inkl. appsettings mit Secrets, Libs, docs, Logs in der Build-Stage); Release-Build ✔ |
| `Meesenburg.ES.API.SAPData/Dockerfile-Prod` | wie oben | non-root ✔ | wie oben | `COPY Libs/Linux/ /usr/local/lib/` (Fremd-Binaries inkl. ICU 50, siehe ES-I-14); `RUN … ls -la /usr/local/lib` (Debug-Ausgabe im Build-Log) |
| `Meesenburg.ES.API.Graph/Dockerfile` (Nicht-Prod, **.NET 9**) | `aspnet:9.0` | non-root | **`ENV AZURE_CLIENT_SECRET` (S1)** | veraltetes Framework gegenüber `net10.0` im csproj |
| `Mule/Dockerfile`, `ESData/Dockerfile`, `ESData/Dockerfile-bak`, `SAPData/Dockerfile.BAKUBAK`, `ESDataWorker/Dockerfile-old` | teils **9.0** | non-root | keine | Altlasten. `Dockerfile-old` ist kaputt (leeres `ENV`). `DockerfileAcr` ist leer. |
| `ESDataWorker/Dockerfile`, `ExternalCall/Dockerfile`, `ApiService/Dockerfile`, `SAPData/Dockerfile` | 10.0 | non-root | keine | ok |

Weitere Punkte:
- **appsettings in den Images:** `dotnet publish` kopiert `appsettings.json` ins Image. Damit stecken **S2/S3/S4/S5** in den Web-, Graph- und SAPData-Images, unabhängig vom `ENV`. Der Graph-Secret-Wert steckt zusätzlich als `UserSecretsIdAttribute` in `Meesenburg.ES.API.Graph.dll` und `Meesenburg.ES.API.Mule.dll`.
- **`.dockerignore`:** schließt `bin/obj/.env/.git` aus, nimmt aber per `!.git/config`, `!.git/HEAD` und `!.git/refs/heads/**` Git-Metadaten wieder auf. Logs, `docs/`, `_archive/`, `.idea/`, `*.BACKUP`, `*.BAK*`, `*.user` und `.DS_Store` sind nicht ausgeschlossen.
- **Tags:** Die AppHost-Konfiguration veröffentlicht `latest-stage` in die Registry `my-registry`. In der Compose-Datei steht überall `:latest`. Es gibt keine unveränderlichen Versionen und keine Digests.

### 4.2 Aspire AppHost / `infra/`

- `Meesenburg.ES.AppHost/Program.cs:35–46`: Ein gemeinsames Dictionary `azureEnv` mit `AZURE_CLIENT_ID/SECRET/TENANT_ID` geht an **alle** Dienste (ApiService, SAPData, ESData, Mule, Worker, Web). Ein einziges Secret mit Key-Vault-Vollzugriff steckt also in jedem Dienst, ohne Trennung nach Least Privilege.
- `Program.cs:67,85,116,128,164`: `WithExternalHttpEndpoints()` für **SAPData, ESData, Graph, Mule und Web**. Im Manifest sind die Bindings `"external": true`. Nur das Web-Portal braucht einen externen Endpunkt. Die Fach-APIs mit SAP-Schreibzugriff (Neukundenanlage) und Mailversand sollten nur clusterintern erreichbar sein.
- `Program.cs:87`: `WithEnvironment("WHAT", "???")` (Debug-Rest).
- `infra/docker-compose.yaml`:
  - `cache` (Redis): `--requirepass $REDIS_PASSWORD` mit `CACHE_PASSWORD=` **leer** in `infra/.env:11`. Redis läuft dann effektiv **ohne Passwort**. Der Worker-Connection-String (Z. 106) enthält dasselbe leere Passwort.
  - `env-dashboard`: `mcr.microsoft.com/dotnet/nightly/aspire-dashboard:latest` (**Nightly-Image**), ohne Authentifizierungs-Konfiguration. Ports sind nur per `expose` intern freigegeben, was gut ist. Wird der Port veröffentlicht, sind Telemetrie, Logs und Umgebungsvariablen ungeschützt sichtbar.
  - Fach-APIs: `ports: - "8080"` veröffentlicht auf zufälligen Host-Ports, nur HTTP.
- `infra/main.bicep`, `my-acr*.bicep`: Hier werden nur ein bestehendes ACR referenziert und eine **leere** Rollenzuweisung angelegt (`principalType: ''`, `my-acr-roles.bicep` enthält keine Role-Assignment-Ressource). **Key Vault, AKS, Storage, Netzwerk und Firewall sind nicht als IaC im Repo.** Deren Konfiguration ist daher hier nicht prüfbar: öffentlicher Netzwerkzugriff auf `kv-meesenburg-prod`, Access Policies vs. RBAC, Private Endpoints, AKS-Workload-Identity, NetworkPolicies, Ingress-TLS.
- `azure.yaml`: `host: containerapp` (azd-Template), passt nicht zum AKS-Betrieb. Wohl ein Rest.
- `.azure/` ist korrekt ignoriert (`Meesenburg.ES.AppHost/.gitignore`).

### 4.3 Laufzeit-Konfiguration (Prod-relevant)

- **`DefaultAzureCredential` und das Secret per `ENV`:** In `Meesenburg.ES.Infrastructure.KeyVault`, `ServiceDefaults/Logging/MonitoringCredential.cs`, `Authorization/PortalDatabase.cs`, `ApiService/Program.cs:29–34,115` und `Web/Program.cs:56` wird Managed bzw. Workload Identity in Prod zwar erlaubt. `EnvironmentCredential` steht in der Kette aber **vor** Workload und Managed Identity. Solange `AZURE_CLIENT_SECRET` im Image steht, wird **immer das Secret verwendet**, auch wenn im AKS Workload Identity konfiguriert wäre.
- **Health-Endpunkte:** `ServiceDefaults/Extensions.cs:109–119` mappt `/health` und `/alive` **auch in Produktion** (die Development-Prüfung ist auskommentiert), ohne Authentifizierung. Das ist gering, solange nur „Healthy/Unhealthy“ zurückkommt. Bei externen Endpunkten ist es aber ein Informations- und DoS-Vektor, besonders wenn `/health` Key Vault, Mongo und Redis prüft.
- **OpenAPI/Scalar:** in allen APIs nur bei `IsDevelopment()` ✔.
- **Logging:** `Meesenburg.ES.ApiService/appsettings.json:28–31` setzt `Microsoft.IdentityModel` auf `Debug` und eine Microsoft-Kategorie auf `Trace` – **in der Basis-Konfiguration, also auch in Prod**. Je nach Bibliothek landen so Token-Metadaten, Claims und Validierungsdetails im (Mongo-)Log.
- **DataProtection-Schlüssel:** `Web/Program.cs:87–99` speichert die Schlüssel in Redis oder Mongo, ohne `ProtectKeysWith…` (unverschlüsselt). Wer Zugriff auf Redis hat (in der Compose-Datei ohne Passwort), kann Auth-Cookies fälschen oder entschlüsseln.
- HSTS und HTTPS-Redirect im Web außerhalb von Development ✔. `ForwardedHeaders` ist ohne `KnownProxies/KnownNetworks`-Einschränkung konfiguriert (`Web/Program.cs:105–109`). Bitte prüfen, ob die Default-Einschränkung auf Loopback greift oder ob sie geleert wird.

---

## 5. Abhängigkeiten

**Frameworks/SDK:** Alle Projekte nutzen `net10.0`, Aspire `13.5.3`. Die `global.json` verlangt SDK 9.0.0 mit `rollForward: latestMajor`, das passt nicht zu net10 (harmlos, aber unpräzise). Lokal ist SDK 10.0.201 installiert. Tool `dotnet-svcutil 8.0.0`.

**PackageReferences (aktueller Stand):**

| Paket | Version(en) | Projekte | Bewertung |
|-------|-------------|----------|-----------|
| Aspire.* (Hosting, Azure.KeyVault, Redis, MongoDB, Docker, ContainerRegistry, StackExchange.Redis.*) | 13.5.3 | AppHost, Web, APIs, Worker | aktuell |
| **Aspire.Hosting** | 13.5.3 | **ServiceDefaults** (Laufzeit-Bibliothek!) | Hosting-Paket gehört nur in den AppHost. Es vergrößert die Angriffsfläche und die Images. Prüfen, ob nötig. |
| Microsoft.AspNetCore.* / Microsoft.Extensions.* | 10.0.11 | diverse | aktuell |
| Microsoft.Extensions.Http.Resilience / ServiceDiscovery | 10.9.0 | ServiceDefaults | aktuell |
| Microsoft.Identity.Web (+ GraphServiceClient, MicrosoftGraph, DownstreamApi) | 4.14.2 | Web, APIs | aktuell |
| System.IdentityModel.Tokens.Jwt | 8.22.0 | ESData, Worker | aktuell |
| Azure.Identity | 1.21.0 | diverse | aktuell |
| **Azure.Security.KeyVault.Secrets** | **4.8.0** (SAPData) / 4.11.0 (sonst) | – | uneinheitlich, SAPData angleichen |
| Azure.Messaging.ServiceBus | 7.20.2 | ApiService, Worker, Jobs.Messaging | aktuell |
| MongoDB.Driver | 3.11.0 | diverse | aktuell |
| OpenTelemetry.* | 1.18.0 | ServiceDefaults | aktuell |
| MailKit | 4.18.1 | Graph | aktuell (MimeKit hatte frühere CVEs, die in dieser Linie behoben sind) |
| MudBlazor | 9.9.0 | Web | aktuell |
| Scalar.AspNetCore | 2.17.1 | APIs | nur Dev ✔ |
| Asp.Versioning.Http | 10.2.3 | APIs | aktuell |
| HtmlAgilityPack | 1.13.0 | Worker | aktuell |
| **SMBLibrary** | 1.5.7.1 | Worker | Community-Projekt (Ein-Mann-Projekt). Wird mit Klartext-Domänen-Credentials genutzt (S7). **zu prüfen** (SMB-Signing/Encryption, NTLM-Version) |
| **FPE.Net** | **1.0.0** | SAPData | Format-Preserving Encryption (Personalnummern). Sehr kleines, seit Jahren unverändertes Paket ohne Audit. **Zu prüfen**, ob kryptografisch belastbar (FF1/FF3-1, Tweak ist eine Konstante: `EncryptionService.cs` „esErp-PersonalId-Tweak“). |
| **System.ServiceModel.Duplex / .Security** | **6.0.0** | SAP.Soap | Alte bzw. abgekündigte Pakete. Mischung mit `System.ServiceModel.Http/NetTcp/Federation 10.0.652802`, Versionen angleichen. |
| Microsoft.VisualStudio.Azure.Containers.Tools.Targets | 1.23.0 | Worker, SAPData | nur Build-Tooling, unkritisch |

- **`dotnet list package --vulnerable --include-transitive`** wurde einmalig für `Meesenburg.ES.API.SAPData` gegen nuget.org ausgeführt: **keine bekannten verwundbaren Pakete** (direkt und transitiv). Der Befehl hat dabei implizit ein Restore in die (ignorierten) `obj/`-Ordner von SAPData und ServiceDefaults ausgeführt. Am Git-Status hat sich nichts geändert. Weitere Läufe habe ich deshalb gemäß Vorgabe unterlassen. Der private Feed konnte ohne PAT nicht abgefragt werden (NU1900). **Empfehlung:** In der Pipeline für alle Projekte `dotnet list package --vulnerable --include-transitive` bzw. `NuGetAudit` mit `NuGetAuditMode=all` als Build-Fehler aktivieren.
- **Prerelease-Pakete:** keine gefunden.
- **Private Pakete** (`Meesenburg.ES.Core 1.0.2`, `.Infrastructure.KeyVault 1.0.1`, `.Authorization 1.0.3`, `.Jobs.Client 1.0.3`, `.Jobs.Contracts 1.0.12`, `.Jobs.Messaging 1.0.0`, `Meesenburg.SAP.Soap 1.0.3`): Der Quellcode liegt im selben Repo. Veröffentlicht werden sie in den Azure-Artifacts-Feed `Meesenburg-Group/Meesenburg/_packaging/Meesenburg.ES`, Authentifizierung über `%AZURE_DEVOPS_PAT%` in der Root-`nuget.config` ✔ (kein Klartext).
- **Dependency Confusion:** Die Root-`nuget.config` enthält nuget.org **und** den privaten Feed, aber **kein `packageSourceMapping`**. Ein Angreifer könnte `Meesenburg.ES.*` oder `Meesenburg.SAP.Soap` mit höherer Version auf nuget.org veröffentlichen. Empfehlung: `packageSourceMapping` (`Meesenburg.*` → privater Feed, `*` → nuget.org) und das Präfix `Meesenburg` auf nuget.org reservieren. Der AppHost hat bereits ein Mapping, aber nur auf nuget.org.
- **Fremde Binaries im Repo** (`Meesenburg.ES.API.SAPData/Libs/`, rund 150 MB, in Git versioniert):
  - `ERPConnect/ERPConnect35.dll`, `ERPConnect/ERPConnectStandard20.dll`, `Libs/ERPConnect35.dll` (doppelt): kommerzielle Theobald-Bibliothek. Der Projektverweis zeigt auf `ERPConnectStandard20.dll` per `HintPath`, ohne Versionsangabe und ohne Signaturprüfung. Version und Herkunft sind nicht nachvollziehbar. **Zu prüfen:** Version, Updates, Lizenz-Weitergabe.
  - SAP NW RFC SDK 7.50 PL17 (`libsapnwrfc.so/.dylib`, `sapnwrfc.dll`, `libsapucum.*`): laut `dev_rfc.log` Kernel 753 PL1449 von 2025, also halbwegs aktuell. **Lizenz:** Das SAP-SDK darf in der Regel nicht in Repositories oder Images weiterverteilt werden. Mit der SAP-Lizenzverwaltung klären.
  - **ICU 50** (`libicudata.so.50`, `libicui18n.so.50`, `libicuuc.so.50`, `.50.dylib`) und **ICU 57** (Windows) gehören zum NW RFC SDK, sind aber Versionen von **2012 bzw. 2016** mit zahlreichen bekannten CVEs. Sie werden nach `/usr/local/lib` im Prod-Image kopiert und per `LD_LIBRARY_PATH` vorrangig geladen. **Zu prüfen**, ob die .NET-Runtime im Container dadurch ebenfalls das alte ICU lädt (dann Risiko), sonst nur das RFC-SDK. Empfehlung: das aktuellste NW RFC SDK verwenden und die ICU-Bibliotheken des SDK nicht global in `/usr/local/lib`, sondern in einem eigenen Pfad ablegen.

---

## 6. Repo-Hygiene

- **`.gitignore`** (Visual-Studio-Standard) deckt `bin/obj`, `*.user`, `*.DotSettings.user`, `*.log`, `[Ll]ogs/` ab ✔. **Es fehlen:** `.DS_Store`, `.env` / `**/infra/.env`, `.idea/` (teilweise über `.idea/.gitignore`), `*.BAK*`, `*.BACKUP`, `*.bak`, `_archive/`, `*.zip`, `aspire-manifest.json`, `.claude/`, `.junie/`, `dev_rfc.log` (zwar über `*.log` abgedeckt, aber ein `sapnwrfc.ini`, das das SDK sucht, wäre es nicht).
- **Versioniert, sollte aber nicht im Repo sein:** 4× `.DS_Store` (`SAPData/`, `SAPData/Brokers/`, `SAPData/Libs/`, `ExternalCall/`); `.idea/.idea.Meesenburg.ES/.idea/dataSources.xml` (S8), `fr.socolin.hsf.user.xml`; Backup-Dateien `*.BAKK` (5× Worker), `MuleAuthHandler.cs.BACKUP`, `EncryptionService.cs.BACKUP`, `C4vAuthenticationHandler.cs.BACKUP`, `MongoInitializationHostedService.js.BACKUP`, `MainLayout.razor.BAK(.css)`, `meesenburg-*-base.css.BAK`, `Dockerfile.BAKUBAK`, `Dockerfile-bak`, `Dockerfile-old`. Die geprüften Backup-Dateien enthalten **keine** Secrets, sondern nur Key-Vault-Secret-Namen (`c4vApiCredentials(-prod)`, `muleClientCredentials`).
- **Unversioniert, aber sensibel:** `Meesenburg.ES.AppHost/infra/.env`, `infra/docker-compose.yaml` (S1, 3×), `aspire-manifest.json` (S1, 3×), `Meesenburg.ES.ApiService/Dockerfile-Prod` (S1). Diese Dateien werden durch kein Ignore-Muster geschützt, ein `git add .` würde sie mitnehmen.
- **Große Binärdateien:** `SAPData/Libs/**` mit 12 Dateien zwischen 1,5 und 25 MB. `.git` ist 57 MB groß (12,8 MB gepackt). Empfehlung: Git LFS oder besser ein Artefakt-Feed bzw. Base-Image.
- `_archive/Meesenburg.ES.Web/Components.zip` (53 KB, unversioniert): Inhalt nicht entpackt und nicht geprüft. Bei Bedarf prüfen oder löschen.
- `Meesenburg.ES.sln.DotSettings.user` (58 KB) ist ignoriert ✔.

---

## 7. Befunde

| ID | Schweregrad | Fundstelle | Beschreibung | Auswirkung | Empfehlung |
|----|-------------|-----------|--------------|------------|------------|
| ES-I-01 | **Kritisch** | S1: 7× `*/Dockerfile-Prod`, `Graph/Dockerfile:8`, `AppHost/appsettings.json:18`, `aspire-manifest.json:72,101,133`, `infra/docker-compose.yaml:50,70,109`; Historie `AppHost/Program.cs` (9 Commits) | Entra-Client-Secret der Key-Vault-App im Klartext, als `ENV` in jedem Prod-Image, im Git-Remote | Wer ein Image oder einen Repo-Klon hat, kann sich als App anmelden und **alle Secrets aus `kv-meesenburg-prod`** lesen (c4v, Mule, IMAP, SAP-SOAP, FPE-Schlüssel), je nach RBAC auch schreiben | Secret **sofort rotieren** und das alte löschen. Anmelde-Logs der App (Entra Sign-in-Logs / Service Principal) und Key-Vault-Diagnose-Logs auf Fremdzugriffe prüfen. Auf AKS Workload Identity umstellen, `ENV`-Zeilen entfernen, ACR-Images mit dem alten Secret löschen. |
| ES-I-02 | **Kritisch** | S4: `SAPBroker.cs:43`, `SAPData/appsettings.json:25`; alle 16 Commits | SAP-Dienstbenutzer-Passwort im Code und in der Konfiguration, seit dem ersten Commit auf dem Remote | Direkter RFC-Zugriff auf SAP (Mandant 100, HCM/MEP) mit den Rechten von `srvusr01` (u. a. Kunden anlegen, Personaldaten lesen) | Passwort in SAP **ändern**. Nur noch über den Key Vault (`Sap--Credentials--Password`), Default aus `SAPBroker.cs` entfernen. Berechtigungen von `srvusr01` minimieren, Security Audit Log für den Benutzer prüfen. |
| ES-I-03 | **Hoch** | S7: `ESDataWorker/Services/AccountingService.cs:19–28` (gestagt) | Domänenkonto `meesenburg\es2kerp` mit Klartext-Passwort für die SMB-Freigabe `clfs002` | Zugriff auf Rechnungsablage und ggf. weitere Freigaben bzw. Anmeldung im AD. Mit dem nächsten Commit auch auf dem Remote. | Passwort im AD **ändern**. Credentials aus dem Key Vault laden, Konto auf die eine Freigabe beschränken (kein interaktives Login). Datei vor dem Commit bereinigen. |
| ES-I-04 | **Hoch** | S2: `Graph/appsettings.json:11,15`; `Graph.csproj:7`, `Mule.csproj:7` als `UserSecretsId` (gestagt) | Graph-App-Secret im Klartext und als UserSecretsId missbraucht → steht im `UserSecretsIdAttribute` der kompilierten DLLs | Mailversand als `sise-portal@…`, Zugriff auf das Postfach `invoices@…` und weitere je nach Graph-Berechtigung (Application Permissions, z. B. `Mail.Send`/`Mail.ReadWrite`). Das Secret steckt in jedem Build-Artefakt. | **Rotieren.** Neue GUID als `UserSecretsId`. Secret in den Key Vault oder Zertifikat/MI. Graph-Berechtigungen per Application Access Policy auf die beiden Postfächer beschränken. |
| ES-I-05 | **Hoch** | S3: `Web/appsettings.json:14`; `Web/bin/**/publish/appsettings.json` | OIDC-Client-Secret des Portals im Klartext. Gelangt per `dotnet publish` ins Image. | Token-Abruf im Namen der Portal-App, Zugriff auf Downstream-APIs (ApiService-Scope) | **Rotieren**, in den Key Vault verschieben oder Zertifikat/Federated Credential. |
| ES-I-06 | **Hoch** | `*/Dockerfile-Prod` (`COPY . .`, `ENV`), Publish-Ausgabe | Images enthalten Secrets in drei Formen: `ENV` (S1), `appsettings.json` (S2–S5) und DLL-Metadaten (S2). Der Build-Kontext ist das gesamte Repo inkl. Logs und Backups. | Wer Pull-Rechte auf ACR hat (inkl. Pipeline- und Cluster-Identitäten, ggf. Dritte), erhält alle Secrets | Secrets nur zur Laufzeit (Key-Vault-Provider/CSI). Gezielte `COPY`s, strengere `.dockerignore`. Image-Scan (Defender for Containers / Trivy mit Secret-Scanner) in der Pipeline. Alte Images löschen. |
| ES-I-07 | Mittel | `*/Dockerfile-Prod`: `ARG AZURE_DEVOPS_PAT` + `ENV AZURE_DEVOPS_PAT=$AZURE_DEVOPS_PAT` | PAT als Build-Arg/ENV in der Build-Stage | Steht in der Build-History bzw. im Cache und in `docker history` der Build-Stage. Wird die Stage gepusht oder gecacht (z. B. `--cache-to`), ist der PAT lesbar. | BuildKit-Secrets (`RUN --mount=type=secret,id=nuget …`) oder den Azure Artifacts Credential Provider mit `VSS_NUGET_EXTERNAL_FEED_ENDPOINTS` nur im `RUN`. PAT auf `Packaging (Read)` und kurze Laufzeit beschränken. |
| ES-I-08 | Mittel | S5/S6: `SAPBroker.cs:50–51`, `SAPData/appsettings.json:9`; alle 16 Commits | Zwei ERPConnect-Lizenzschlüssel im Code und in der Historie | Lizenzmissbrauch bzw. -verstoß, keine direkte technische Kompromittierung | Mit dem Hersteller klären, in den Key Vault verschieben, auskommentierten Altschlüssel entfernen |
| ES-I-09 | Mittel | `AppHost/Program.cs:35–46`; alle Dienste via `DefaultAzureCredential` | Ein einziges Secret mit Key-Vault-Zugriff für alle Dienste. `EnvironmentCredential` verdrängt Workload/Managed Identity. | Keine Trennung nach Least Privilege, ein Leck kompromittiert alles. Eine geplante MI-Umstellung wirkt nicht, solange `ENV` gesetzt ist. | Pro Dienst eine eigene Workload Identity mit minimalen Key-Vault-Rechten (RBAC, ggf. getrennte Vaults oder Secret-Scopes) |
| ES-I-10 | Mittel | `AppHost/Program.cs:67,85,116,128`; Manifest `"external": true` | SAPData-, ESData-, Graph- und Mule-API als externe Endpunkte veröffentlicht | Vergrößerte Angriffsfläche: SAP-Schreib-API und Mail-API aus dem Netz erreichbar (nur JWT als Schutz) | Nur `webfrontend` extern. APIs clusterintern (ClusterIP) plus Kubernetes NetworkPolicies. |
| ES-I-11 | Mittel | `infra/docker-compose.yaml:10–22,106`, `infra/.env:11` | Redis mit leerem `requirepass`. Aspire-Dashboard als Nightly-Image `:latest` ohne Authentifizierung. Alle Images `:latest`. | Redis (Sessions, Output-Cache, ggf. DataProtection-Keys) ohne Authentifizierung. Nicht reproduzierbare oder unsignierte Deployments. | Passwort erzwingen (Aspire-Parameter mit Wert), feste Versionstags oder Digests, Dashboard stabil und mit Auth (`DASHBOARD__FRONTEND__AUTHMODE`) |
| ES-I-12 | Mittel | `Web/Program.cs:87–99` | DataProtection-Keys unverschlüsselt in Redis oder Mongo | Mit Zugriff auf Redis/Mongo lassen sich Auth-Cookies fälschen | `ProtectKeysWithAzureKeyVault(...)` |
| ES-I-13 | Mittel | `nuget.config` (Root) | Kein `packageSourceMapping` bei gemischten Feeds | Dependency Confusion für `Meesenburg.*` | Source-Mapping einführen, Präfix auf nuget.org reservieren |
| ES-I-14 | Mittel | `SAPData/Libs/**`, `SAPData/Dockerfile-Prod:39–41` | Fremd-Binaries im Repo: ERPConnect (Herkunft/Version unklar), SAP NW RFC SDK (Lizenz), **ICU 50/57 (2012/2016)**, global in `/usr/local/lib` mit `LD_LIBRARY_PATH` | Bekannte ICU-Schwachstellen im Prod-Image, Lizenzverstoß, keine Integritätsprüfung | Aktuelles SDK aus SAP-Quelle, Ablage in privatem Artefakt-Feed oder Base-Image mit Prüfsumme, eigener Lib-Pfad |
| ES-I-15 | Niedrig | `ApiService/appsettings.json:28–31` | `Microsoft.IdentityModel` = Debug, eine Microsoft-Kategorie = Trace in der Basis-Konfiguration | Token- und Claim-Details im Prod-Log (Mongo) | Debug/Trace nur in `appsettings.Development.json` |
| ES-I-16 | Niedrig | `ServiceDefaults/Extensions.cs:109–119` | `/health` und `/alive` in Prod ohne Auth (Dev-Prüfung auskommentiert) | Informationsabfluss bzw. Last auf Abhängigkeiten bei externen Endpunkten | Nur intern erreichbar (Probe-Port) oder `RequireHost`/Auth. Bewusste Entscheidung dokumentieren. |
| ES-I-17 | Niedrig | Repo gesamt | 745 offene Änderungen, darunter Secrets im **Index**. `.gitignore` deckt `.env`, `.DS_Store`, `*.BAK*`, `_archive/`, `aspire-manifest.json` nicht ab. `.DS_Store`, `.idea/dataSources.xml` (S8) und Backup-Dateien sind versioniert. | Hohe Gefahr, die Secrets beim nächsten Commit erneut zu veröffentlichen | Vor dem nächsten Commit die Secrets aus den Dateien entfernen, `.gitignore` ergänzen, Backups und `.DS_Store` aus Git entfernen. Pre-commit-Hook mit gitleaks oder `detect-secrets`. |
| ES-I-18 | Niedrig | `SAPData/dev_rfc.log` (ignoriert); `docs/TODO.md:6–9` (unversioniert) | RFC-Log mit interner SAP-IP und Hostnamen. TODO mit Fundstellen-Beschreibung der Secrets. | Recon-Hilfe, falls die Dateien weitergegeben oder committet werden | `RFC_TRACE`-Pfad außerhalb des Repos, Log löschen. TODO nach der Rotation aktualisieren. |
| ES-I-19 | Niedrig | `SAP.Soap.csproj`, `SAPData.csproj`, `ServiceDefaults.csproj`, `SAPData/Services/EncryptionService.cs` | Uneinheitliche oder alte Pakete (`System.ServiceModel.Duplex/Security 6.0.0`, `KeyVault.Secrets 4.8.0`), `Aspire.Hosting` in Runtime-Lib, `FPE.Net 1.0.0` mit konstantem Tweak | Wartbarkeit, Kryptografie nicht belegt | Versionen angleichen, `FPE.Net` gegen eine geprüfte FF1-Implementierung abwägen, NuGetAudit im CI |
| ES-I-20 | Info | `infra/*.bicep`, `azure.yaml` | Key Vault, AKS, Storage und Netz sind nicht als IaC im Repo, die Bicep-Dateien sind nur Stubs (leere Rollenzuweisung) | Prüfung von Firewall, Public Access, KV-Zugriffsmodell und AKS-Settings hier nicht möglich | Separat prüfen: KV `publicNetworkAccess`, RBAC statt Access Policies, Private Endpoint, Soft-Delete/Purge-Protection. AKS: Workload Identity, NetworkPolicy, Azure Policy, Ingress-TLS. |

---

## 8. Priorisierte Maßnahmen

**Phase 0 – sofort (heute/morgen), Rotation zuerst**
1. **S1 rotieren** (App `b4961a2a-…`): neues Secret erzeugen, im Cluster nur als Kubernetes-Secret bzw. über den CSI-Treiber einspielen, **altes Secret löschen**. Sign-in-Logs des Service Principals und Key-Vault-Audit-Logs (rückwirkend ab 2025-03-14) auf unbekannte IPs prüfen.
2. **Alle Secrets im Key Vault `kv-meesenburg-prod` als potenziell offengelegt behandeln** und nach Kritikalität rotieren (c4v-API-Credentials, Mule-Client-Credentials, IMAP-Credentials, SAP-SOAP-Credentials). Der FPE-Schlüssel ist ein Sonderfall: Eine Rotation ändert die verschlüsselten Personalnummern, das muss vorher geplant werden.
3. **SAP-Passwort `srvusr01` (S4) ändern** und das SAP Security Audit Log prüfen.
4. **Domänenpasswort `es2kerp` (S7) ändern.**
5. **Graph-Secret (S2) und Web-OIDC-Secret (S3) rotieren.** Neue `UserSecretsId`-GUIDs in Graph und Mule.
6. **Vor dem nächsten Commit:** Secrets aus allen Dateien im Index und Arbeitsverzeichnis entfernen (`SAPBroker.cs`, `AccountingService.cs`, appsettings, csproj, Dockerfiles). `git diff --cached` mit gitleaks prüfen.

**Phase 1 – kurzfristig (1–2 Wochen)**
7. `ENV AZURE_CLIENT_SECRET` und `ENV AZURE_DEVOPS_PAT` aus allen Dockerfiles entfernen. BuildKit-Secret für den NuGet-Restore, gezielte `COPY`s, `.dockerignore` ergänzen.
8. Auf **AKS Workload Identity** pro Dienst umstellen. `azureEnv` aus `AppHost/Program.cs` entfernen.
9. Alle bisherigen Images in ACR (`crmeesenburg`, Tags `latest`/`latest-stage`) löschen bzw. per Retention-Policy entfernen und neu bauen.
10. `WithExternalHttpEndpoints()` nur noch für `webfrontend`. Redis-Passwort setzen, DataProtection-Keys mit Key Vault schützen.
11. `.gitignore` ergänzen. `.DS_Store`, `.idea/`, Backup-Dateien und Dockerfile-Altlasten aus Git entfernen. Pre-commit-Hook (gitleaks) und Secret Scanning bzw. Push Protection in Azure DevOps aktivieren.

**Phase 2 – mittelfristig**
12. **Historie bereinigen** (nach der Rotation): `git filter-repo --replace-text` für S1, S4, S5, S6. Danach Force-Push, alle Klone neu ziehen lassen, Azure-DevOps-PR-Refs und Forks prüfen. Hinweis: Das ersetzt keine Rotation.
13. `packageSourceMapping` einführen, NuGetAudit (`all`) als Build-Fehler, Image-Scanning im CI.
14. SAP-Binaries (NW RFC SDK, ICU) aktualisieren und aus dem Repo in einen Artefakt-Feed oder ein Base-Image verschieben. ERPConnect-Version und Lizenzen klären.
15. Infrastruktur (Key Vault, AKS, Netz) als IaC versionieren und separat reviewen (ES-I-20).
16. Logging-Level in Prod reduzieren und Health-Endpunkte absichern (ES-I-15/16).

---

## Nachtrag (Gegenprüfung 07.10.2026)

- **Gestagt (Index) und damit im nächsten Commit:** Graph-App-Secret (`Ps28…`) in `Meesenburg.ES.API.Graph/appsettings.json` sowie als `UserSecretsId` in `Meesenburg.ES.API.Graph.csproj` und `Meesenburg.ES.API.Mule.csproj`. Diese Dateien sind neu (`A`) und noch in keinem Commit, also noch nicht in der Historie auf Azure DevOps. **Vor dem nächsten Commit bereinigen.**
- **Nur in der Arbeitskopie, nicht im Index:** Domänen-Kennwort `es2…`/`tDw…` in `ESDataWorker/Services/AccountingService.cs`. Der gestagte Stand dieser Datei enthält es nicht. Ein `git add -A`/`commit -a` würde es aber übernehmen.
- **Korrektur zu ES-I-01:** `AZURE_CLIENT_SECRET` (`uoN8…`) steht in 8 `Dockerfile*`-Dateien als `ENV`, diese sind aber **nur lokal** (nicht committet) – und damit in allen daraus gebauten Images. **In Git (Remote `dev.azure.com/Meesenburg-Group/Meesenburg/_git/Meesenburg.ES`, `origin/master`)** liegt der Wert nur in **einer** Datei: `Meesenburg.ES.AppHost/Program.cs`, seit Commit `ab4382a` (14.03.2025). Der Git-Stand ist laut Projektleitung veraltet (letzter Commit 14.04.2025) und nur einem kleinen internen Personenkreis zugänglich. Rotation bleibt wegen der Images nötig.
