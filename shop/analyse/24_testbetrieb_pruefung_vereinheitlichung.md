# 24 Testbetrieb im AKS: Prüfung und Vereinheitlichung mit ES

Stand 18.11.2026. Geprüft: `infra/apps/siseshop-test/` (Manifest, `secrets.sh`, `seed.sh`, README), Release-Ziel `PPR.DevTools/deploy/siseshop-test`, Shop-Repo (`src/*/Dockerfile`, `appsettings.Staging.json`, `Program.cs`). Cluster nur gelesen: Namespace `mee-siseshop-test` existiert noch nicht, es ist also noch nichts ausgerollt.

## 1. Befunde

| # | Schwere | Befund | Folge | Vorschlag |
|---|---|---|---|---|
| B-1 | **hoch** | Die Shop-Images haben das **Meesenburg-CA-Zertifikat nicht** (ES kopiert `src/certs/*.crt` und ruft `update-ca-certificates` auf). Der Mongo-Cluster im Rechenzentrum nutzt Zertifikate dieser CA | TLS zu Mongo schlägt im Pod fehl → API, Portal (Rechte) und Worker starten nicht | CA-Zertifikat ins Shop-Repo (`src/certs/`) und in alle Shop-Dockerfiles wie bei ES |
| B-2 | **hoch** | **Testkonten-Passwörter im Klartext** in `appsettings.Staging.json` und `appsettings.Development.json` der API – seit 18.11. auch im GitHub-Repo `Meesenburg-GmbH/sise-shop` | Jeder mit Repo-Zugriff kann sich im Testshop (hinter der Entra-Sperre) als Testkunde anmelden; widerspricht der Regel „keine Zugangsdaten im Repo“ | Passwörter in das K8s-Secret (Test) bzw. die User-Secrets (lokal); neue Passwörter vergeben, die alten gelten als bekannt |
| B-3 | **hoch** | Der Shop liest Mongo über `MongoDBConnectionString` – das ist der **Cluster-Admin** mit Zugriff auf alle Produktiv-Datenbanken (ES, Portal, Logs). Bericht 14, E-P hatte genau das ausgeschlossen | Ein Fehler oder eine Lücke im Shop gefährdet alle Datenbanken | Eigener Mongo-Benutzer nur mit `readWrite` auf die Shop-Datenbank, Secret `SiseShop-Test`/`SiseShop-Prod` im Key Vault (JSON, wie E-P) |
| B-4 | mittel | Der Shop nutzt die **App-Identität von ES** (`es-keyvault-app`, kopiert) – Lesezugriff auf alle Secrets im Key Vault | Least Privilege verletzt (V-5) | Eigene App/Identität `siseshop-keyvault-app` mit Zugriff nur auf `SiseShop-*` |
| B-5 | mittel | **Worker fehlt** im Manifest, steht aber im Release-Ziel (`projects.conf`) | Mails bleiben in der Warteschlange, Vorkasse-Fristen laufen nicht; ein Release würde ein Image ohne Deployment bauen | Worker mit abgeschaltetem Wawi-Abgleich aufnehmen (1 Pod) |
| B-6 | mittel | Images `:latest` mit `imagePullPolicy: Always` | Stand im Cluster nicht nachvollziehbar, Rollback nur durch Neubau | Versions-Tags (DevTools gibt sie ohnehin mit), `IfNotPresent` |
| B-7 | niedrig | Ein Manifest für alles, fest auf Test (Namespace, Hostname, `Staging`, Mailpit) | Prod müsste kopiert werden → zwei Stände, die auseinanderlaufen | Kustomize: `base` + `overlays/test` (1 Pod je Dienst, Mailpit, Testzugang) + `overlays/prod` (2 Pods, PDB, ohne Mailpit) |
| B-8 | niedrig | Namen weichen von ES ab (siehe Tabelle) | Uneinheitlich | Abschnitt 2 |
| B-9 | niedrig | Kein Speicher-Limit-Muster wie in ES besprochen (Requests passen, Limits 768 MiB) | – | Limits 1 GiB (GC hält ~75 % davon), Requests wie Bericht 23 |
| B-10 | Hinweis | `secrets.sh` liest Stripe-Schlüssel aus den User-Secrets des Macs | Funktioniert nur auf diesem Rechner | Später aus dem Key Vault (`SiseShop-Test`) |

Positiv: NetworkPolicies (API und Mailpit nur im Namespace, Shop nur vom Ingress), Testzugang vor dem ganzen Shop, Schreibsperre zur Wawi, Mails nur an meesenburg.de, Sticky Sessions für Blazor, Probes `/health` und `/alive`, `runAsNonRoot`.

## 2. Vereinheitlichung mit ES

| | ES | Shop heute | Vorschlag |
|---|---|---|---|
| Namespace | `mee-eserp` | `mee-siseshop-test` | `mee-siseshop` (Prod), `mee-siseshop-test` (Test) |
| Oberfläche | Projekt `Meesenburg.ES.Web`, Deployment `es-web` | `Meesenburg.Shop.Portal`, `siseshop-portal` | **`Meesenburg.SiseShop.Web`**, **`siseshop-web`** |
| API | `Meesenburg.ES.ApiService`, `es-apiservice` | `Meesenburg.SiseShop.Api`, `siseshop-api` | bleibt |
| Worker | `Meesenburg.ES.DataWorker`, `es-dataworker` | `Meesenburg.SiseShop.Worker`, `siseshop-worker` | bleibt |
| Dockerfile | `src/<Projekt>/Dockerfile-Prod` | `src/<Projekt>/Dockerfile` | `Dockerfile-Prod` |
| Image | `crmeesenburg.azurecr.io/meesenburg/es-web` | `…/meesenburg/siseshop-portal` | `…/meesenburg/siseshop-web` |
| Key-Vault-Zugang | Secret `es-keyvault-app` | Kopie `es-keyvault-app` | `siseshop-keyvault-app` (eigene App) |
| App-Secrets | – | `siseshop-test` | `siseshop-secrets` (gleicher Name in Test und Prod, Namespace trennt) |
| Gemeinsame Konfiguration | ConfigMap `shared-config` | im Manifest je Dienst | ConfigMap `shared-config` |
| Labels | `app: es-web` | `app: siseshop-api` | `app: <deployment>` + `app.kubernetes.io/part-of: siseshop` |
| Ablage | `infra/clusters/aks-meesenburg-prod/mee-eserp/` | `infra/apps/siseshop-test/` | `infra/apps/siseshop/{base,overlays/test,overlays/prod}` |
| Replicas | 2 | 1 | Test 1, Prod 2 (+ PDB) |
| Rechte-Paket | `Meesenburg.ES.Authorization` | `Meesenburg.Authorization` (Kopie) | später gemeinsam `Meesenburg.SiSe.Authorization` (Analyse Neuorganisation) |

Umbenennung `Meesenburg.Shop.Portal` → `Meesenburg.SiseShop.Web`: 28 Dateien (Namensräume, Projekt, Solution, AppHost, Tests), mit Build und Tests prüfbar.

## 3. Entscheidungen (18.11.2026, M. Piper)

| # | Thema | Entscheidung |
|---|---|---|
| E-24-1 | Mongo- und Key-Vault-Zugang (B-3, B-4) | **Vorerst wie ES** (`MongoDBConnectionString`, `es-keyvault-app`), um Pflegeaufwand zu sparen. Risiko bekannt: Cluster-Admin-Rechte im Shop |
| E-24-2 | Testkonten-Passwörter (B-2) | **Bleiben** in `appsettings.Staging.json`/`.Development.json` – Testshop ist per Entra gesperrt, Konten dienen nur dem einfachen Testen |
| E-24-3 | Umbenennung und Dockerfiles | Umgesetzt 18.11.: `Meesenburg.Shop.Portal` → `Meesenburg.SiseShop.Web` (Image/Deployment `siseshop-web`), alle Dienste mit `Dockerfile-Prod` nach ES-Muster (CA-Zertifikat, Feed-Zugang per `AZURE_DEVOPS_PAT`). Build 0 Fehler, 324/324 Tests grün, Web-Image lokal gebaut (CA vorhanden, läuft als UID 1654) |
| E-24-4 | DevTools-Ziel `siseshop-test` | Umgestellt auf `Dockerfile-Prod` und `siseshop-web` |

Zusätzlicher Befund (B-11): Das alte Dockerfile der Oberfläche kopierte weder `nuget.config` noch das Token – der Restore von `Meesenburg.ES.Infrastructure.KeyVault` (über die ServiceDefaults) wäre im ACR-Build gescheitert. Mit E-24-3 behoben.
| E-24-5 | Kustomize (B-5, B-6, B-7, B-8) | Umgesetzt 18.11.: `infra/apps/siseshop/` mit `base` (siseshop-web/-api/-worker, Ingress, NetworkPolicies, ConfigMap `shared-config`) und `overlays/test` (mee-siseshop-test, je 1 Pod, Mailpit, Dev-Sise-Shop) bzw. `overlays/prod` (mee-siseshop, Web/API je 2 + PDB, Prod-Sise-Shop, Platzhalter-Domain, noch nicht einspielen). Feste Versionen statt `latest`; `release.sh <test|prod> <version> [--apply]` setzt die Version, zeigt den Unterschied und spielt erst mit `--apply` ein. Secret heißt jetzt `siseshop-secrets`. `apps/siseshop-test/` entfällt. Geprüft: beide Overlays rendern, `kubectl apply --dry-run=client` ok, Vorschau gegen den Cluster ok (nichts eingespielt) |

## 4. Erster Testbetrieb im AKS (18.11.2026)

- Images 0.1.0 gebaut (ACR), Namespace `mee-siseshop-test` angelegt, Secrets (`es-keyvault-app` Kopie, `siseshop-secrets`), Daten übernommen (Katalog, 244 Inhalte, ~3.000 Bilder, 8.916 Weiterleitungen, Gutscheine, Versand), Startrecht „Shop-Administration“ für M. Piper, 4 Testkonten über den Admin.
- Läuft unter https://shop-test.meesenburg-service.de, Entra-Anmeldung vor dem ganzen Shop (per SSO), Admin unter `/admin`. Geprüft im Chrome: Startseite, Bilder, Preise, Konfigurator, Admin (Startseite, Kundenkonten, Bestellungen, Seiten & Inhalte).
- Unterwegs behoben:
  - **Image-Pfad:** DevTools-`projects.conf` brauchte den vollen Namen `crmeesenburg.azurecr.io/meesenburg/…` (wie ES); die ersten Images lagen unter `siseshop-*` und wurden per `az acr import` kopiert (gleicher Digest). Die Repos `siseshop-web/-api/-worker` (ohne `meesenburg/`) in der ACR sind überflüssig.
  - **seed.sh:** CA-Zertifikat für den lokalen Mongo-Container, Abbruch bei Fehlern (`pipefail`), saubere IDs (`crypto.randomUUID`).
  - **0.1.1 – TestGate:** Rückwege aller Entra-Anmeldungen durchlassen (Admin-Login endete mit 401, weil Entra per seitenübergreifendem POST zurückkommt und das Lax-Cookie fehlt).
  - **0.1.2 – StaffAuth:** Entra-Identität als „Mitarbeiter“ kennzeichnen (`TokenValidationParameters.AuthenticationType`), sonst „Kein Zugriff“. Lokal unsichtbar, weil die Test-Anmeldung den Typ selbst setzt.
- Offen: Testgruppe für den Zugang (heute: jedes Meesenburg-Konto, `SiseShop:TestGate:AllowedGroups` leer); ecomdata-Freigabe der Ausgangs-IP 20.31.106.131 (V-2), danach Wawi-Zugang für den Worker; Begrüßung im Admin zeigt „Hallo Piper,“ (Anzeigename „Piper, Michael (HBG)“ wird am Komma getrennt).
