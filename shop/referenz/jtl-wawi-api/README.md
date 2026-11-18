# JTL-Wawi REST-API: offizielle Spezifikationen

> Abgerufen am 10.10.2026 von `https://developer.jtl-software.com/openapi/<version>.json` (2.2/2.3: 404). Unverändert gespeichert.
> **Unser Server: JTL-Wawi 1.11.9, nur API-Version 1 → maßgeblich ist Spezifikation 1.1.** Details und Live-Befunde: `analyse/13_jtl_schnittstelle_referenz.md`.

| Spez. | JTL-Wawi-Version | Pfade | Datei | SHA-256 |
|---|---|---|---|---|
| 1.0 | 0.0.0 | 312 | `jtl-wawi-api-1.0_wawi-0.0.0_openapi.json` | `8af913bc8e52b977…` |
| 1.1 | 1.11.0.0 | 157 | `jtl-wawi-api-1.1_wawi-1.11.0.0_openapi.json` | `416bcd3df01e08ed…` |
| 1.2 | 2.0.0+Sha.881251b | 340 | `jtl-wawi-api-1.2_wawi-2.0.0_openapi.json` | `ce9a6ed6bcca93f0…` |
| 1.3 | 2.1.0 | 312 | `jtl-wawi-api-1.3_wawi-2.1.0_openapi.json` | `c77e88cef4462987…` |
| 1.4 | 2.2.0-alpha.10857+Sha.e630d1a | 318 | `jtl-wawi-api-1.4_wawi-2.2.0_openapi.json` | `bc6008ad00306540…` |
| 2.0 | 2.1.0 | 236 | `jtl-wawi-api-2.0_wawi-2.1.0_openapi.json` | `378b1d1d5e67aa08…` |
| 2.1 | 2.2.0-alpha.10857+Sha.e630d1a | 295 | `jtl-wawi-api-2.1_wawi-2.2.0_openapi.json` | `2f85a4827ef4c95d…` |

## Was neuere Versionen bringen (Vergleich 10.10.2026)

| Thema | Spez. 1.1 (Wawi 1.11, **unser Stand**) | Spez. 1.2–1.4 (Wawi 2.0–2.2, Pfade `/v1`) | Spez. 2.0/2.1 (Wawi 2.1/2.2, Pfade `/v2`) |
|---|---|---|---|
| Variationswerte | nur `Id`, `Name`, `Translations` | unverändert | **`surcharges`** (Aufpreise), `weightOffset`, `itemNumber`, `gTIN`, Bild |
| Konfigurationsgruppen am Artikel | – | – | **`ConfigurationGroup`, `ItemConfigurationGroups`** |
| Artikel-Preise | `ItemPriceData` + je Artikel `/salesChannelPrices`, `/specialprices`, `/customerPrices` | unverändert | alles am Artikel (`prices.defaultPrices` je Kundengruppe, `salesChannelPrices`, `customerPrices`, `specialPrices`) |
| Artikelliste | Filter `searchKeyWord`, `categoryId`, `manufacturerId`, `parentItemId`, `changedSince`, `isActiveOnSalesChannelId` | unverändert | Suche (`searchTerm/Field/Operator`), `modifiedTimestampAfter`, Bestände (`stockAvailable` …), Brutto-VK, Herstellername; **kein Kanalfilter mehr** (`/v2/items/sales-channels/batch` prüfen) |
| Bestand | `/stocks`, `/stocks/changes` | + Reservierungen (`/delivery/*`, nur 1.2) | `/v2/stocks/items`, Reservierungen, Chargen/MHD |
| Versandarten | `Id`, `Name`, `Priority`, `GrossPrice` (bei uns immer 0), `ExtraWeight` | unverändert | nur `id`, `name`, `isActive` – **Versandkosten in keiner Version** |
| Preisberechnung | – | – | `GET /v2/sales-orders/item-price-details` (Standard-/letzter VK je Kunde, **nicht** die Shop-Preisfindung) |
| Kategorien | `/categories` (Seiten, Baum über `ParentCategoryId`) | unverändert | `/v2/categories`, `/v2/resources/categories` |

**Fazit:** Variationsaufpreise und Konfigurationsgruppen gibt es per REST erst ab **Wawi 2.1 (API v2)**. Versandkosten liefert keine Version, dafür braucht es Regeln im Shop-Backend.

Neue Version prüfen: Datei laden, hier eintragen, mit `/jtl/contract/check` (POC) bzw. den Broker-Tests vergleichen.
