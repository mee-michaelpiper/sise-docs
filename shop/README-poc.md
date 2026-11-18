# Shop-POC: JTL-Daten selbst prüfen

.NET-10-POC für die Ablösung des FastAPI-Backends im Webshop Sicherheit und Service (SN24).
Hintergrund und Analyse: [`analyse/00_gesamtbericht.md`](analyse/00_gesamtbericht.md).

| Projekt | Zweck | Port (lokal) |
|---|---|---|
| `src/Meesenburg.Shop.JtlApi` | API: Artikel aus JTL-Wawi, serverseitige Bruttopreise, Schnittstellen-Prüfung, Custom-API (nur lesend) | http://localhost:5279 · Doku: `/scalar` |
| `src/Meesenburg.Shop.Portal` | Oberfläche im SiSe-Portal-Design (Blazor Server, MudBlazor 9.9) | http://localhost:5036 |
| `postman/` | Postman-Collection für die Freigabe-Tests (nur GET) | – |

## Ordner

| Ordner | Inhalt |
|---|---|
| `TODO.md` | offene Punkte (Phase 0, Entscheidungen, JTL-Prüfungen, esERP-Klärungen) |
| `analyse/` | Gesamtanalyse des Shops (00), Detailberichte 02–06, esERP-Bewertung (07); PDFs unter `analyse/pdf/` |
| `src/` | POC: JTL-API und Portal-Oberfläche |
| `postman/` | Test-Requests JTL-REST und Custom-API (nur lesend, Tokens als Variablen) |
| `referenz/` | Stand der esapi-Schnittstelle (Swagger, 07.10.2026); `svc_helper` (gesicherter Klon der Python-Kernbibliothek des Python-Backends, eigenes `.git` mit Historie) |
| `tools/` | `md2pdf.py`: erzeugt die PDFs aus den Markdown-Berichten neu |

**Vertraulich:** `analyse/04_sicherheit.md` beschreibt offene Lücken des Live-Shops. Nicht in Repos oder Ordner legen, auf die die Freelancer Zugriff haben.

PDFs neu erzeugen (Google Chrome erforderlich):

```bash
python3 -m venv .venv && .venv/bin/pip install -r tools/requirements.txt && .venv/bin/python tools/md2pdf.py analyse
```

## Starten (neues Backend, alles zusammen)

Voraussetzungen: .NET 10, Docker, Aspire-CLI 13.5; für echte JTL-Daten der SSH-Tunnel (`ZUSAMMENFASSUNG.md`).

```bash
dotnet user-secrets --project src/Meesenburg.SiseShop.AppHost set "Parameters:jtl-base-url" "http://localhost:50443"
```

```bash
dotnet user-secrets --project src/Meesenburg.SiseShop.AppHost set "Parameters:jtl-api-token" "Wawi <token>"
```

```bash
aspire run --project src/Meesenburg.SiseShop.AppHost
```

Dashboard: https://localhost:17217 (lokal ohne Anmeldung) · Shop-API: http://localhost:5072/scalar · .NET-Test-Shop: http://localhost:5036 · Admin: http://localhost:5036/admin (System, Abgleich, JTL-Diagnose).

Einzelne Dienste nach einer Codeänderung neu starten, ohne den laufenden Abgleich abzubrechen: `dotnet build Meesenburg.SiseShop.slnx`, danach `aspire resource shopapi restart` (bzw. `portal`, `worker`).
Schreibzugriffe auf JTL sind gesperrt (`Parameters:jtl-allow-writes` = `false`). Freigabe nur nach ausdrücklichem OK.

Container: `aspire publish --project src/Meesenburg.SiseShop.AppHost --output-path deploy/compose` erzeugt `docker-compose.yaml` + `.env`; einzelne Images mit den Dockerfiles in den Projektordnern (Build-Kontext = Repo-Stamm). Plan und Fortschritt: `analyse/14_plan_backend.md`.

## Starten (POC einzeln)

```bash
cd src/Meesenburg.Shop.JtlApi && dotnet run --launch-profile http
```

```bash
cd src/Meesenburg.Shop.Portal && dotnet run --launch-profile http
```

Im Development-Modus läuft die API mit **Beispieldaten** (`Jtl:UseSampleData=true`). Diese Artikel sind erfunden und enthalten absichtlich Fehlerfälle. Die Oberfläche kennzeichnet das.

## Gegen die echte JTL-Wawi

Voraussetzung: ecomdata hat die ausgehende IP für Port 50443 (REST) bzw. 58080 (Custom-API) freigegeben. Test mit der Postman-Collection.

Zugangsdaten **nie** in `appsettings*.json`, sondern als User-Secrets setzen (Werte aktuell in `Readme.md` des Shop-Repos; nach der Rotation aus dem Key Vault):

```bash
cd src/Meesenburg.Shop.JtlApi
dotnet user-secrets set "Jtl:UseSampleData" "false"
dotnet user-secrets set "Jtl:BaseUrl" "http://c10330.servers.ecomdata.de:50443"
dotnet user-secrets set "Jtl:ApiToken" "Wawi <token>"
dotnet user-secrets set "CustomApi:BaseUrl" "http://c10330.servers.ecomdata.de:58080"
dotnet user-secrets set "CustomApi:ApiToken" "<token>"
```

Alternativ aus dem Key Vault: Setze `KeyVault:Uri` (z. B. `https://kv-meesenburg-prod.vault.azure.net/`) und lege die Secrets `Jtl--ApiToken`, `Jtl--BaseUrl`, `CustomApi--ApiToken`, `CustomApi--BaseUrl` an. Lokal genügt `az login`, im AKS eine Workload Identity.

**Erster Schritt mit echten Daten:** Öffne *Schnittstellen-Prüfung*, trage 5–10 typische Artikel-Ids ein (Konfigurator-Grundartikel, Zylinder, Schlüssel) und wähle **„Stand speichern“**. Jede spätere Prüfung zeigt dann jede Strukturänderung der JTL-Antworten mit Datum. Damit lässt sich die Behauptung „undokumentierte Änderungen“ belegen oder widerlegen.

## Was die Prüfung zeigt

- **Bricht Python-Backend:** Die Antwort verletzt die Pydantic-Modelle des FastAPI-Backends (`backend/items_svc/src/jtl/schemas/item_raw_schemas.py`), zum Beispiel durch `null`, ein fehlendes Feld oder `7.5` in einem Ganzzahlfeld. Das Python-Backend verwirft dann still Daten. Welche das sind, steht im Tooltip bzw. in der Detailansicht.
- **Geändert:** Die Struktur weicht vom gespeicherten Stand ab oder unterscheidet sich zwischen `/v1/…` und dem unversionierten Pfad, den das Python-Backend für Detaildaten nutzt.
- **Steuersatz:** Ist er nicht ermittelbar, zeigt der POC **keinen** Bruttopreis. Das Python-Backend hätte still 1 % angesetzt.

## API-Endpunkte

| Methode | Pfad | Zweck |
|---|---|---|
| GET | `/jtl/articles?search=&page=&pageSize=` | Artikelliste (Text oder Id), je Artikel Prüfergebnis |
| GET | `/jtl/articles/{id}` | Artikel mit Preisen (netto/brutto), Kanal- und Sonderpreisen, Variationen, Bildern, Prüfbefunden, Abrufprotokoll |
| GET | `/jtl/articles/{id}/raw?part=` | unveränderte JTL-Antwort |
| GET | `/jtl/contract/check?itemIds=&snapshot=` | Schnittstellen-Prüfung |
| GET/POST | `/jtl/contract/snapshots?itemIds=` | gespeicherte Stände / neuen Stand speichern |
| GET | `/custom/{key}` | Custom-API lesend: `config_groups`, `config_items`, `property_values`, `categories`, `shipping_methods`, `child_item_mappings`, `item_units` |
| GET | `/jtl/status` | Erreichbarkeit |
| GET | `/health` | Healthcheck (Kubernetes) |

## Container / AKS

```bash
docker build -f src/Meesenburg.Shop.JtlApi/Dockerfile -t meesenburg-shop-jtlapi:poc .
```

```bash
docker build -f src/Meesenburg.Shop.Portal/Dockerfile -t meesenburg-shop-portal:poc .
```

Beide Images laufen als Nicht-root-Benutzer auf Port 8080. Für den Betrieb im AKS:
- Konfiguration über Umgebungsvariablen (`Jtl__BaseUrl` …) bzw. Key Vault
- `/app/data` als Volume für die Snapshots
- Egress-IP des Clusters bei ecomdata freigeben lassen

## Bewusst nicht im POC

Es gibt keine Anmeldung (die Oberfläche ist nur lokal gedacht). Später kommt Entra ID wie im SiSe Portal dazu. Außerdem fehlen schreibende Zugriffe, Preis-Engine und Bestellung (siehe Plan im Gesamtbericht, Woche 2–3). Das ERP wird im POC direkt angesprochen. In der echten API läuft das über `IErp…Broker`-Schnittstellen, damit später der Wechsel auf esERP möglich ist.
