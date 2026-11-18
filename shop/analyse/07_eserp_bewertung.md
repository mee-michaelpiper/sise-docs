# 07 – esERP als führendes ERP für den Webshop (statt JTL-Wawi)

> **Stand:** 07.10.2026 · **Grundlage:**
> - Analyse des ES-Projekts (`/Users/piper/repos/Meesenburg.ES`)
> - Swagger der esapi (`https://esapi.meesenburg.de/swagger/v1/swagger.json`, Kopie unter `../referenz/esapi-swagger-2026-10-07.json`)
> - öffentliches Handbuch esERP 3.x von es2000
> - Klarstellungen aus dem Gespräch vom 07.10.2026
>
> **Frage:** Kann esERP die JTL-Wawi als führendes ERP des Shops ersetzen, insbesondere bei Konfigurationsartikeln (Schließanlagen)?

## 1. Ergebnis

**Ja.** esERP kann JTL als führendes ERP ersetzen: für Artikel, Preise, Bestand, Kunden, Aufträge, Dokumente und – über den bestehenden Export – die Buchhaltung in SAP. Das gilt unter der Voraussetzung, dass die (vorhandene, noch nicht freigegebene) Auftrags-API die Anforderungen in Abschnitt 5 erfüllt.

**Konfigurationsartikel wie in JTL gibt es in esERP nach öffentlicher Doku nicht.** Die Konfigurationsregeln gehören deshalb in die Shop-API. esERP liefert die Bausteine: konfigurationsbedingte Artikel als 500er mit Preisen und Bestand.

Das ist kein Nachteil. Auch heute liefert JTL nur die **Daten** für Konfig-Gruppen und -Artikel, und zwar über die Custom-API per SQL. Die eigentliche **Logik** steckt verteilt im Frontend und ist die Hauptquelle der Preisfehler (siehe Bericht 02).

## 2. Rahmenbedingungen (Klarstellungen 07.10.2026)

| Thema | Festlegung |
|---|---|
| SAP | bleibt für den Shop **außen vor**. Shop-Artikel werden direkt in esERP angelegt. |
| Artikel | konfigurationsbedingte Artikel werden in esERP als **500er** erfasst (esERP-eigen, nicht in SAP) |
| SAP-Abgleich | lässt 500er/800er bewusst aus (`ESDataWorker/Handler/ArticleJobHandler.cs:117`, `EsArticleNumbers.IsExcludedFromSync`): kein Überschreiben, keine Sync-Meldungen |
| Bestand | wird **von esERP selbst geführt**. Die Mule-Abfrage des SAP-Bestands dient nur zur Info für Mitarbeiter (Werke/große Läger vs. Direktbestellung beim Lieferanten) und spielt nichts ein. |
| Dokumente | PDFs (Schließplan, Auftragsbestätigung) können im **internen DMS von esERP** am Auftrag hängen |
| Auftrags-API | existiert, ist bei Meesenburg **noch nicht freigegeben** |
| esapi | bildet nur den aktuell benötigten Ausschnitt ab und ist erweiterbar (com4) |
| Buchhaltung | Erlöse und Warenbewegungen gehen aus esERP nach **SAP** (CSV-Export im ES-Projekt). Rechnungen müssen dort als bezahlt gelten, sonst wird gemahnt. |

## 3. Befunde

### 3.1 esERP (Hersteller es2000 Errichter Software, Osnabrück)
- Branchenlösung für **Errichter und Servicebetriebe** (Sicherheitstechnik, Brandschutz, Gebäudetechnik), Nachfolger von ESOFFICE. Unter den Referenzen sind keine Händler oder Webshops. [es2000.de](https://www.es2000.de), [Referenzen](https://www.es2000.de/referenzen/)
- Handbuch für „eserp 3.xx“ (Stand 31.07.2025). Release-Notes zu 3.4 sind **nicht öffentlich**, sie kommen nur über Support oder Newsletter. [Handbuch](https://manual.es2000.de/eserp/3/de-de/system/eserp_de-de/sys_eserp_de-de.htm)
- **Kaskade:** Vaterartikel mit Kinderartikeln in fester Menge. Die Kinder werden automatisch mitgeladen, die Preise kumuliert. Auswahlgruppen, optionale Positionen und Min/Max sind **nicht beschrieben**. → Für **starre Sets** geeignet, für einen Konfigurator nicht. [Kaskade](https://manual.es2000.de/eserp/3/de-de/eserp/artikel/art_leist_kaskade.htm)
- **Artikelklassifizierung** mit bis zu 8 Stufen (Beispiel aus dem Handbuch: „Zylinder > Doppelzylinder > Eigenprofil“). Das ist eine Gruppierung, kein Variantenmodell. [Klassifizierung](https://manual.es2000.de/eserp/3/de-de/eserp/artikel/art_artikelklassifizierung.htm)
- **Preise:**
  - 4 Listenpreise je Artikel (VK + Aufschlag %)
  - Preisgruppen und Kunden-Rabattgruppen
  - Aufschlaggruppen
  - EK-basierte Kalkulation
  - **Staffelpreise nicht gefunden.** [Preise](https://manual.es2000.de/eserp/3/de-de/eserp/artikel/art_leist_preise.htm)
- **Schnittstellen des Herstellers:**
  - Dokumentiert ist nur die lesende xDokument-API.
  - eswawi ist ein Connector für die **Einkaufsseite** (Lieferanten und Distributoren).
  - Dazu kommen DATANORM, OpenTrans, GAEB und ITScope.
  - Eine Shop-Anbindung oder eine öffentliche Auftrags-API ist nicht dokumentiert. [SDK](https://manual.es2000.de/eserp/3/de-de/eserp/faq/software_development_kit.htm), [eswawi](https://manual.es2000.de/eserp/3/de-de/module/eswawi/eswawi.htm), [Zusatztools](https://www.es2000.de/loesungen/erweiterungen-zusatz-tools/)
- **Schließanlagen** (Schließplan, Herstellerschnittstellen DOM/EVVA/BKS …): **kein Modul gefunden.** [Module](https://manual.es2000.de/eserp/3/de-de/module/module.htm)

### 3.2 esapi (`com4EsErpApi`, Betreiber com4/„c4v“)
- OpenAPI 3.0.1, 115 Pfade, 164 Schemas, Anmeldung per `POST /login` → JWT (Bearer). Die Zugangsdaten liegen im Key Vault (`c4vApiCredentials-prod`, Test `c4vApiCredentials`).
- **Artikel** (`/artikel`, lesen + Upsert/Batch): 88 Felder, darunter
  - `artikelnr`, `matchcode`, `eanr`, `warengruppe`
  - `kdrabattgr` (Kundenrabattgruppe)
  - `vkpreiS1–4` und `listenpreise[]` (`listPrCode`, `aufschlag`, `vkPreis`)
  - `lieferanten[]` (EK)
  - `mwstcode`, `meeinh`, `gewicht`, Maße
  - `kaskade`, `produktbild` (nur Pfad)
- **Kaskaden** (`/artikel/{nr}/kaskaden`): `artkaskpos`, `artikelnrkas`, `anzahl`, also starre Sets.
- **Materialkatalog** (`/Materialkatalog/{katalog}/Artikel/{artikelNr}/{lieferantNr}`, CRUD + Upsert): Lieferantenkatalog mit EK/Brutto/Netto, `vkPreis1/2`, Rabattgruppe, Lieferzeit, Bestellnummer und Übernahme-/Verarbeitungskennzeichen.
- **Lager:** `/lager` (Lagerorte), `/lager/bewegungen` (Bewegungshistorie mit Restbestand). **Kein direkter Bestandsabruf je Artikel und Lager.**
- **Belege:** `GET /auftragpositionen/{nr}`, `GET /angebotpositionen/{nr}/{variante}`, ohne Preise und nur lesend. Die Auftragsanlage ist laut Meesenburg vorhanden, aber nicht freigegeben.
- **Kunden** `/kunden` (Upsert), Geschäftspartner, Kundenbanken, SEPA-Mandate, Dokumentenversand.
- **Fibu:** `/fibu/rechnungsausgang|rechnungseingang` + `infibugeb`, `/erechnung/{nr}`.
- Varianten, Merkmale, Staffelpreise, Bilder-Upload, Dokument-Upload, Zahlungen: **nicht vorhanden**.

### 3.3 ES-Projekt
- esERP ist das operative ERP der Sparte **SiSe** (Werke 1900 … 8900). Stammdaten SAP → esERP per Job (`ESDataWorker`), Rechnungen und Lagerbewegungen esERP → SAP als CSV (`ESDataWorker/Services/AccountingService.cs`, `SapCsvWriter.cs`) – **eigener Code, also anpassbar**.
- Für SAP-Artikel wird der VK heute als SAP-EK × 2 übertragen (Listenpreis-Code 1). Für 500er gilt das nicht.
- Upserts der esapi sind langsam (Timeouts erhöht, laut Code-Kommentar ca. 190 Abbrüche pro Tag). Für den Shop sind Antwortzeiten und Wiederholbarkeit zu prüfen.

## 4. Zielbild

| Was | Wo |
|---|---|
| Konfigurationsbedingte Artikel (Grundzylinder, Längen-Aufpreise, Schließcode-Zuschläge, Zusatzschlüssel, Farbkappen …) als 500er | **esERP**, Pflege über das SiSe Portal per esapi |
| VK je Artikel (eigener Listenpreis-Code „Shop“), EK über `lieferanten[]`, MwSt-Code, Kundenrabattgruppe für B2B | **esERP** |
| Feste Bündel | **esERP-Kaskade** |
| Bestand | **esERP** (neuer esapi-Endpunkt nötig) |
| Konfigurationsregeln: Optionsgruppen, Min/Max, erlaubte Längen je Zylindertyp, Schließcode-Logik, Zuordnung Option → 500er-Artikelnummer | **Shop-API** (Pflege per SiSe Portal) |
| Preisberechnung | **Shop-API**, eine Preis-Engine: Summe der esERP-Listenpreise der aufgelösten Artikel, MwSt, Versand, Rabatt |
| Auftrag | **esERP**: aufgelöste Positionen, Schließplan + Auftragsbestätigung als PDF im DMS |
| Buchhaltung | esERP → SAP (bestehender CSV-Export), erweitert um das Zahlungskennzeichen |

Die Shop-API spricht das ERP nur über Broker-Schnittstellen an (`IErpCatalogBroker`, `IErpOrderBroker`, `IErpCustomerBroker`, `IErpStockBroker`). Konfigurationsregeln und Preis-Engine verweisen nur auf **Artikelnummern**. Der Wechsel JTL → esERP tauscht damit nur den Broker aus.

### 4.1 Pflege-Ort der Shop-Artikel: Artikelstamm oder Materialkatalog?
- **Empfehlung: direkt im Artikelstamm.** Das SiSe Portal pflegt die 500er per `PUT /artikel/{nr}` bzw. `POST /artikel/upsert`, inklusive `listenpreise` und `lieferanten`. So gibt es eine einzige Quelle, und die esapi kann das bereits.
- **Materialkatalog** ist im esERP-Modell ein **Lieferantenkatalog**: Der Schlüssel ist Artikel + Lieferant, gedacht für DATANORM bzw. EK-Daten, aus dem Artikel in den Stamm übernommen werden. Als Pflege-Ort für den Shop entstünden zwei Stammdaten-Orte.
  - Sinnvoll ist er, wenn die Schließanlagen-Hersteller ihre EK-Preislisten liefern und der VK über Aufschläge daraus abgeleitet werden soll.
  - **Mit com4 klären:** Wie und wann wirken Katalogänderungen auf bestehende Artikel (automatisch oder manuell, welche Felder)?

### 4.2 Zahlung und Mahnwesen
Die Buchhaltung läuft in SAP. Bezahlte Shop-Rechnungen dürfen dort nicht offen stehen.
1. Shop-Aufträge bekommen in esERP eine eigene **Zahlungsbedingung bzw. Zahlungsart „Stripe – bezahlt“**. Falls es das nicht gibt, übergangsweise ein dokumentiertes Feld.
2. Der CSV-Export esERP → SAP (eigener Code) überträgt das Kennzeichen. SAP gleicht die Forderung gegen ein **Stripe-Verrechnungskonto** aus, sodass keine Mahnung entsteht.
3. Die Stripe-Auszahlungen auf das Bankkonto werden gegen das Verrechnungskonto gebucht. Der Abgleich läuft über die Stripe-Referenz (bzw. die Shop-Bestellnummer) und ist automatisierbar.

→ Abstimmung mit der SAP-Finanzbuchhaltung nötig (Konto, Belegart, Ausgleichslogik).

## 5. Anforderungen an die Auftrags-API (Prüfliste)

| # | Anforderung | Warum |
|---|---|---|
| 1 | Haupt- und Unterpositionen (Zylinder → Aufpreis-Artikel) | lesbare Rechnung, Bezug der Aufpreise zum Zylinder |
| 2 | Positionspreise vom Shop übernehmen **oder** esERP-Preisfindung, mit identischem Ergebnis | ein Betrag in Shop, Stripe, esERP und SAP |
| 3 | Text- bzw. Langtextpositionen | Schließplan-Kurzinfo, Hinweise |
| 4 | Dokument-Upload ins DMS am Auftrag | Schließplan und Auftragsbestätigung als PDF |
| 5 | Kunde: Anlage je Shopkunde (`POST /kunden` vorhanden) oder Sammelkunde + Adressen im Auftrag | B2C ohne Stammdatenflut |
| 6 | Zahlungsbedingung/-art bzw. Kennzeichen „bezahlt (Stripe)“ + Transaktions-ID | Mahnwesen in SAP (Abschnitt 4.2) |
| 7 | Versandkosten als Artikelposition | Erlöskonto, MwSt |
| 8 | Externe Referenz (Shop-Bestellnummer) und Schutz vor doppelter Anlage | sichere Wiederholung nach Timeout |
| 9 | Status zurücklesen (offen, in Arbeit, geliefert, Rechnung) + Sendungsnummer | „Meine Bestellungen“, Versandmail |
| 10 | Antwortzeit und Verfügbarkeit (vorhandene Upserts gelten als langsam) | Checkout darf nicht hängen; ggf. asynchron per Warteschlange |

**Zusätzlich nötige esapi-Endpunkte:** Bestand je Artikel und Lager, Dokument-Upload, ggf. Listenpreise je Code gezielt lesen.

## 6. Fragen an es2000 bzw. com4
1. Was ist neu in esERP 3.4? Gibt es Konfigurations- bzw. Variantenartikel, gegebenenfalls in Planung?
2. Doku der Auftrags-API (Prüfliste Abschnitt 5), Freigabe, Kosten und Zeitrahmen.
3. Bestandsabruf und Dokument-Upload über die esapi.
4. Materialkatalog: Übernahme- bzw. Aktualisierungslogik in den Artikelstamm.
5. Gibt es eine Zahlungsbedingung bzw. -art, die als „bereits bezahlt“ in den Fibu-Export geht?
6. Erfahrungen mit Webshops als Auftragsquelle, Last bzw. Antwortzeiten.

## 7. Einordnung in den Zeitplan
Siehe `00_gesamtbericht.md`, Abschnitt 5a.

- **Phase 1:** Die .NET-API wird gegen JTL gebaut, mit Broker-Schnittstellen und Preisautorität über die aufgelösten Artikel.
- **Phase 2:** Die Konfigurationsregeln wandern nach dem Fachworkshop in die Shop-API.
- **Phase 3:** Wechsel auf esERP.

Die 500er-Artikel können parallel in esERP angelegt werden, sobald das Schema der Optionsgruppen feststeht.
