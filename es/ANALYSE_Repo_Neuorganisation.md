# Neuorganisation: ES, SiSe-Portal und Pakete in GitHub (Analyse 17.11.2026)

Anlass: Der .NET-Shop zieht in eine neue GitHub-Organisation, ES soll mit. Frage: Portal als eigenes Repo? Wiederverwendbares in neutrale Pakete ohne ES-Bezug?

Grundlage: Solution `Meesenburg.ES.sln`, alle `.csproj`, Zeilenzählung (cs/razor ohne bin/obj), `nuget.config`, `PPR.DevTools/scripts/release.sh`, Shop-Repo `Meesenburg.SiseShop.POC`, die Repos unter `~/repos`. Nur gelesen.

## 1. Bestand

| Projekt | Zeilen | Art | Bezug |
|---|---|---|---|
| Meesenburg.ES.Web (SiSe-Portal) | 29.900 | Dienst | **gemischt**: ≈ 10.600 allgemeines Portal (Dashboard, Neuigkeiten, Ideen, Fehlermeldungen, Mitteilungen, Nutzung, Suche, Rechte, Protokoll, Einstellungen, Layout), ≈ 10.400 ES (esERP-Seiten, Jobs, Buchhaltungsübergabe, Neukunde, Bestand, Systemtest …), Rest Komponenten/Modelle |
| Meesenburg.ES.DataWorker | 18.400 | Dienst | ES |
| Meesenburg.ES.API.SAPData | 11.500 | Dienst | ES (SAP) |
| Meesenburg.ES.API.ESData | 7.500 | Dienst | ES (esERP) |
| Meesenburg.ES.API.Mule | 5.300 | Dienst | ES (Mule) |
| Meesenburg.ES.ServiceDefaults | 3.600 | Projekt-Referenz für alle Dienste | **gemischt** (siehe unten) |
| Meesenburg.ES.ApiService | 2.700 | Dienst | ES (Jobs, Requery) |
| Meesenburg.ES.API.Graph | 2.500 | Dienst | **neutral-ish** (Entra/Exchange) |
| Meesenburg.ES.Authorization | 1.100 | Paket 1.0.15 | **neutral** bis auf `Permissions.cs` (zentrale ES-Liste) |
| Meesenburg.SAP.Soap | 1.050 | Paket 1.0.4 | SAP, neutral |
| Meesenburg.ES.Jobs.Contracts/.Client/.Messaging | 800 | Pakete | Job-System, enthält ES-Jobtypen |
| Meesenburg.ES.Core | 300 | Paket 1.0.2 | Mongo-Holder, Key-Vault-Connection-String (neutral) + `Plant` (SAP-Werk) |
| Meesenburg.ES.Infrastructure.KeyVault | 230 | Paket 1.0.1 | neutral |
| Meesenburg.ES.ExternalCall | 110 | Konsole | Testaufruf der Bestandsabfrage → gehört nach sap-tools |

**ServiceDefaults im Detail:**
- neutral: `Extensions` (OpenTelemetry, Health, Resilience), `ExceptionHandlingExtensions`, `ProblemExceptionHandler`, `HttpBodyLogging`, `HttpClientExtensions`, `HttpResponseExceptionLogging` (404 → Debug), `MeeApiAuthentication`, `Health/DependencyHealth`, `Configuration/KeyVaultSecretsConfiguration`, `Logging/*` (Mongo-Logger, RepeatedFailureLog), `Monitoring/*` (LogStore, SyncIssueStore, ResourceFaults, ShareRetry), `Search/*` (Suchindex, FuzzySearch), `Settings/SharedSettingsStore`.
- ES: `Articles/EsArticleNumbers`, `Customers/EsCustomerRules`, `Customers/InvoiceMailRules`, `Settings/EmployeeBranch`, `Settings/EmployeeMailGroupMember`, `SettingsDocuments` (konkrete Einstellungen), die Liste `SyncIssueKinds`.

**Doppelt vorhanden:** Der Shop hat eine eigene Kopie von Authorization (`Meesenburg.Authorization`, gleiche Dateien ohne Audit) und eigene ServiceDefaults/Key-Vault-Konfiguration. In GitHub `Meesenburg-Group` liegen außerdem schon `core-library` (Core.*, z. B. Scheduling, Office, FileTransfer) und `sap-library` (Sap.*), in Azure DevOps `Meesenburg.Libraries.Core/SAP`. → Es gibt heute drei Generationen „Core“-Bibliotheken.

**Betrieb heute:**
- ES-Repo: nur `master`, ≈ 915 nicht committete Änderungen; `Meesenburg.ES.Authorization` und `Navigation/` nicht versioniert.
- Pakete im Azure-Artifacts-Feed `Meesenburg.ES` (PAT nötig).
- Deploy: `release.sh` baut per `az acr build` **aus der Arbeitskopie** und macht `rollout restart`.
- **Kubernetes-Manifeste gibt es in keinem Repo.** Der Stand lebt nur im Cluster (u. a. die Änderungen vom 17.11.: nodeSelector, Requests, Ingress-HA). Wird ein Deployment versehentlich gelöscht, ist es nur aus dem Cluster-Backup wiederherstellbar.

## 2. Bewertung der Frage „Portal als eigenes Repo?“

Fachlich ja: Das Portal ist schon heute zur Hälfte allgemein und wird mit dem Shop-Admin noch allgemeiner. Der Name „ES“ passt nicht mehr.

Der Haken: **Die meisten ES-Änderungen gehen quer durch Worker, APIs und Portal** (Beispiele 16./17.11.: CSV-Maskierung + Requery = Worker + ApiService + Web; Lieferanten-Mapping = Worker + ServiceDefaults + Web). Liegen Portal und ES in getrennten Repos und koppelt man sie über Pakete, wird jede solche Änderung zu: Contracts-Paket bauen → veröffentlichen → im Portal hochziehen → zwei Releases. Bei einem Ein-Personen-Team ist das spürbare Reibung (siehe heutige Erfahrungen mit Local Feed und unveröffentlichten Versionen).

Deshalb ist die Grenze entscheidend, nicht die Zahl der Repos:

| Grenze | Was ändert sich selten? | Wie koppeln |
|---|---|---|
| **Plattform** (Hosting, Logging/Monitoring, Rechte, Key Vault, Mongo, Suche, Einstellungen, Portal-Modulvertrag) | selten, stabil | **Pakete** mit Versionen |
| **Portal-Host + allgemeine Portal-Funktionen** | mittel | Repo `sise-portal` |
| **Fach-Module im Portal** (ES-Seiten, Shop-Admin) | oft, meist zusammen mit dem Backend | **im Portal-Repo als Projekte**, Backend nur über HTTP + kleine Contracts-Pakete |
| **Backends** (ES-Worker/APIs, Shop-API/Worker/Frontend) | oft | je ein Repo |

Warum die Fach-Module ins Portal-Repo und nicht als Paket aus dem Backend-Repo: Ausgeliefert wird Modul-Code im Portal-Image. Repo = Deploy-Einheit spart einen Paketschritt bei jeder UI-Änderung. Das Backend-Paket (Contracts) ändert sich nur, wenn sich die Schnittstelle ändert.

**Alternative Monorepo** (ein Repo mit `src/Platform`, `src/Portal`, `src/ES`, `src/Shop`, Projekt- statt Paket-Referenzen, CI baut nur Geändertes): am wenigsten Reibung für ein kleines Team und atomare Änderungen über alle Teile. Dagegen: der Shop soll ohnehin eigenständig in der neuen Organisation liegen, Rechte lassen sich nur je Repo vergeben, und das Portal wird „globaler“. → Empfehlung bleibt die Aufteilung oben; das Monorepo ist die Rückfallebene, falls die Paket-Reibung zu groß wird.

## 3. Vorschlag Zielbild

### Repos (neue GitHub-Organisation)

| Repo | Inhalt |
|---|---|
| `platform-dotnet` | neutrale Pakete (Tabelle unten), eigene Versionen, CHANGELOG |
| `sise-portal` | Portal-Host, allgemeine Funktionen, Module `Portal.Modules.EsErp`, `Portal.Modules.Shop` (Shop-Admin), DESIGN_GUIDE, ReleaseNotes |
| `es-integration` | Worker, ApiService, SAPData, ESData, Mule, Graph, AppHost (Dev), Paket `Meesenburg.ES.Contracts` (DTOs, Jobtypen, SyncIssue-Arten, Einstellungsdokumente) |
| `sise-shop` | Shop-Frontend, Shop-API, Worker, Paket `Meesenburg.SiseShop.Contracts` |
| `infra` | K8s-Manifeste (Kustomize/Helm) je Namespace, Helm-Values (ingress-nginx), Release-Workflows |
| `sap-tools` | bleibt privat (Analysen, Werkzeuge) |
| `sap-library` / `core-library` | prüfen und zusammenführen: `Meesenburg.SAP.Soap` gehört zu sap-library; aus core-library nur übernehmen, was genutzt wird |

### Pakete (neutral, ohne „ES“)

| Neues Paket | Aus | Hinweis |
|---|---|---|
| `Meesenburg.Hosting` | ServiceDefaults (neutraler Teil), `MeeApiAuthentication`, `DependencyHealth` | Aspire-Service-Defaults für alle Dienste (ES, Shop, künftige) |
| `Meesenburg.Azure.KeyVault` | ES.Infrastructure.KeyVault + `KeyVaultSecretsConfiguration` + Key-Vault-Teil aus ES.Core | |
| `Meesenburg.Mongo` | ES.Core/Mongo | |
| `Meesenburg.Monitoring` | ServiceDefaults/Logging + Monitoring | SyncIssue-Arten registriert jedes Produkt selbst (statt fester Liste) |
| `Meesenburg.Authorization` | ES.Authorization + Shop-Kopie zusammenführen | **Rechte-Katalog erweiterbar**: jedes Modul meldet seine Rechte an → keine Paketversion mehr pro neuer Berechtigung |
| `Meesenburg.Settings` | `SharedSettingsStore` | konkrete Einstellungen bleiben im Produkt |
| `Meesenburg.Portal.Search` | ServiceDefaults/Search | Schema des Suchindex; Anbieter liefern Produkte |
| `Meesenburg.Portal.Abstractions` | neu | Modulvertrag: Menüeinträge (`PortalTool`, Bereiche), Rechte, Suchbereiche, Dashboard-Kacheln, Mitteilungen. Ersetzt das feste `enum PortalArea` |
| `Meesenburg.ES.Jobs.*` | bleibt vorerst ES | erst verallgemeinern, wenn ein zweites Produkt Jobs braucht (der Shop hat eine eigene Outbox) |

Namensräume ändern sich mit (`Meesenburg.ES.ServiceDefaults.Monitoring` → `Meesenburg.Monitoring`). Am besten einmalig beim Umzug, mit Find/Replace und Build.

### Paket-Feed und Deploy
- **Feed:** GitHub Packages (NuGet) der Organisation. Geht in Actions ohne PAT (`GITHUB_TOKEN`); lokal braucht es ein PAT mit `read:packages`. Azure Artifacts kann bleiben, ist dann aber ein zweiter Ort.
- **CI:** Build + Tests auf jedem PR; Pakete beim Tag `platform/vX.Y.Z`; Images beim Tag je Dienst (z. B. `es-dataworker/v1.8.0`), Anmeldung an Azure per OIDC (Federated Credential, kein Secret im Repo).
- **Deploy nur aus Tags**, Manifeste aus `infra` (`kubectl apply -k` bzw. Helm), Image-Tags mit Version statt `latest`. Löst das Risiko „Deploy aus der Arbeitskopie“ (Bericht 22/23 im Shop-Repo, R-4).

## 4. Reihenfolge

| # | Schritt | Wer | Aufwand (grob) |
|---|---|---|---|
| 0 | Sicherungsstand: alles committen (inkl. Authorization, Navigation), Tag `vor-github` in Azure DevOps | M. Piper | – |
| 1 | Manifeste aus dem Cluster exportieren und bereinigen → Repo `infra` (heute nirgends gesichert) | ich | 1–1,5 PT |
| 2 | ES 1:1 nach GitHub spiegeln (`git push --mirror`, Historie bleibt), CI baut wie bisher, Deploy aus Tag statt Arbeitskopie | M. Piper (Push) / ich (Workflows) | 2–3 PT |
| 3 | `platform-dotnet` anlegen, Pakete einzeln herauslösen (Reihenfolge: KeyVault, Mongo, Hosting, Monitoring, Authorization, Settings, Search), ES stellt Stück für Stück um | ich | 4–6 PT |
| 4 | Modulvertrag (`Portal.Abstractions`), Portal in `sise-portal` herauslösen, ES-Seiten als Modul | ich | 3–5 PT |
| 5 | Shop-Admin als zweites Modul (ersetzt den ES-Patch aus Bericht 22, P-3/P-5) | ich | 2–3 PT |
| 6 | core-/sap-library sichten, SAP.Soap umziehen, Doppeltes löschen | ich | 1 PT |
| | **Summe** | | **≈ 13–20 PT** |

Schritte 1 und 2 bringen sofort Sicherheit und sind unabhängig vom Rest. 3–5 können schrittweise neben dem Tagesgeschäft laufen; ES bleibt dabei jederzeit lauffähig.

## 5. Risiken

- **Großer Umbau ohne Tests:** Es gibt kein Testprojekt (TODO „Tests“). Vor Schritt 3 mindestens Rauchtests je Dienst und die Mapper-Tests anlegen, sonst fallen Namensraum-Fehler erst in Prod auf.
- **Doppelte Wahrheit während der Übergangszeit:** Azure DevOps und GitHub parallel → Azure-Repo nach dem Spiegeln schreibschützen.
- **Paket-Reibung:** Mit erweiterbarem Rechte-Katalog und Modulen im Portal-Repo bleibt sie klein. Wird sie trotzdem zu groß → Monorepo (Abschnitt 2).
- **Secrets:** bleiben im Key Vault. In GitHub nur die OIDC-Anbindung, keine Secrets.

## 6. Offene Fragen an M. Piper
1. Name der neuen Organisation, und ist sie getrennt von `Meesenburg-Group` (dort liegen core-library/sap-library)?
2. Feed: GitHub Packages oder Azure Artifacts behalten?
3. Repo-Namen (Vorschlag oben) und ob `es-integration` so heißen soll.
4. Bleibt PPR.DevTools (Mac-App) das Deploy-Werkzeug, oder übernehmen GitHub Actions?

## 7. Entscheidungen und Konzept Pipeline (17.11.2026, M. Piper)

- **Organisation:** `meesenburg-gmbh` (GitHub). `Meesenburg-Group` und deren Libraries (auf den dortigen DataHub zugeschnitten) werden ignoriert; ggf. später Teil einer Gruppen-Organisation. Abschnitt 6, Frage 1 und Abschnitt 4, Schritt 6 entfallen damit.
- **Repo-Namen** wie in Abschnitt 3.
- **Paketnamen `Meesenburg.SiSe.*`** (statt neutral `Meesenburg.*`):

| Paket | Inhalt |
|---|---|
| `Meesenburg.SiSe.Hosting` | Service-Defaults (OpenTelemetry, Health, Resilience, ProblemDetails, HttpBodyLogging, 404→Debug, MeeApiAuthentication, DependencyHealth) |
| `Meesenburg.SiSe.KeyVault` | Secret-Provider, Key-Vault-Konfiguration |
| `Meesenburg.SiSe.Mongo` | Datenbank-Holder, Connection-String aus dem Key Vault |
| `Meesenburg.SiSe.Monitoring` | Mongo-Logger, LogStore, SyncIssueStore (Arten registriert das Produkt) |
| `Meesenburg.SiSe.Authorization` | Rollen, Rechte, Protokoll; erweiterbarer Rechte-Katalog; ersetzt `Meesenburg.ES.Authorization` und die Shop-Kopie |
| `Meesenburg.SiSe.Settings` | SharedSettingsStore |
| `Meesenburg.SiSe.Portal.Search` | Suchindex-Schema und Store |
| `Meesenburg.SiSe.Portal.Abstractions` | Modulvertrag des Portals |
| `Meesenburg.SiSe.SapSoap` | heute `Meesenburg.SAP.Soap` |
| `Meesenburg.SiSe.EsErp.Contracts` | DTOs, Jobtypen, Einstellungsdokumente von ES (inkl. heutiger `ES.Jobs.*`) |
| `Meesenburg.SiSe.Shop.Contracts` | heute `Meesenburg.SiseShop.Contracts` |

- **Feed:** GitHub Packages der Organisation.
- **Deploy:** PPR.DevTools bleibt die Oberfläche, GitHub Actions bauen.

### Pakete und Dockerfiles

Heute bekommt das Dockerfile das Azure-DevOps-PAT als `ARG` → `ENV` (Dockerfile-Prod, Zeile 19–20) und `release.sh` baut per `az acr build` aus der Arbeitskopie. Das Token steht damit in der Build-Stufe des Images.

Mit GitHub Actions:
- Der Workflow hat ein kurzlebiges `GITHUB_TOKEN` mit `packages: read` – **kein PAT, nichts zu verwalten**.
- Am einfachsten: `dotnet restore`/`publish` läuft im Runner, das Image baut die .NET-SDK-Container-Unterstützung (`dotnet publish -t:PublishContainer`) oder ein schlankes Dockerfile kopiert nur das fertige Publish-Ergebnis. **Das Dockerfile braucht dann gar keinen Feed-Zugang mehr.**
- Alternativ Dockerfile mit Restore wie heute, Token aber als BuildKit-Secret (`--secret id=nuget_token`), landet in keiner Image-Schicht.
- Anmeldung an ACR und AKS per OIDC (Federated Credential auf eine Entra-App bzw. Managed Identity) – keine Azure-Secrets in GitHub.
- Lokal (Mac) braucht es für den Restore ein GitHub-PAT mit `read:packages` in der Benutzer-`nuget.config` bzw. Keychain – einmalig.

### PPR.DevTools + GitHub Actions

| Aufgabe | Wer |
|---|---|
| Auswahl Dienste, nächste Version vorschlagen (aus dem letzten Tag, Patch/Minor/Major), Release-Notiz | DevTools |
| Prüfen: Arbeitskopie sauber, Stand gepusht, Tests grün (letzter CI-Lauf) | DevTools (über `git`/`gh`) |
| Tag setzen und pushen (`es-web/v1.6.8`) **oder** `workflow_dispatch` mit Eingaben (Dienst, Version, Umgebung) | DevTools (`gh workflow run` bzw. REST-API) |
| Build, Tests, Image mit Versions-Tag nach ACR, Pakete nach GitHub Packages | Actions |
| Deploy: Manifest aus `infra` mit der neuen Image-Version anwenden, `rollout status` | Actions (Umgebung „prod“ mit Freigabe-Regel optional) |
| Fortschritt, Log, Ergebnis anzeigen; Rollback = Workflow mit älterer Version | DevTools (`gh run watch`/REST) |

Damit bleibt die Bedienung wie heute (ein Klick in der App), aber gebaut wird immer ein bestimmter Commit mit Tag – nie mehr die Arbeitskopie. Paralleles Deployen mehrerer Dienste bleibt möglich (ein Workflow-Lauf je Dienst oder eine Matrix).

Aufwand (zusätzlich zu Abschnitt 4): Workflows je Repo 1,5–2 PT, OIDC/Federated Credentials mit der IT 0,5 PT, DevTools-Anbindung an `gh` (Release-Ansicht, Statusanzeige) 1,5–2,5 PT.
