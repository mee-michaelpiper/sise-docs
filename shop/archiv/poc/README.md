# Archiv: erster POC (07.–10.10.2026)

Abgelöst durch das neue Backend (`src/Meesenburg.SiseShop.*`, Bericht 14). Nicht mehr in Solution und AppHost.

| Inhalt | Zweck damals |
|---|---|
| `Meesenburg.Shop.JtlApi/` | JTL-REST und Custom-API direkt lesen, Schnittstellen-Prüfung gegen die Pydantic-Erwartungen des Alt-Backends, Snapshots |
| `portal/Pages-Jtl/`, `portal/JtlApiBroker.cs`, `portal/SampleDataHint.razor` | Portal-Seiten „Artikel“, „Schnittstellen-Prüfung“, „Konfigurator-Daten“, „Verbindung“ |

Die Erkenntnisse stehen in `analyse/13_jtl_schnittstelle_referenz.md` (J-01 ff.). Die Custom-API wird vom neuen Backend nicht genutzt.
