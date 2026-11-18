# Konzept: Änderungsprotokoll und Materialkataloge

Stand 13.10.2026. Entscheidungen: SAP-Katalog = VKORG 298 aktiv (~225.000 Artikel), EK im SAP-Katalog vorerst YNEK, Herstellerkataloge als XLSX/CSV (BMEcat/DATANORM später, falls nötig), Protokoll 10 Jahre aufbewahren.

## 1. Änderungsprotokoll (Auditing)

### Ist-Stand
| Bereich | Heute | Lücke |
|---|---|---|
| Berechtigungen | eigenes Protokoll `PermissionAudit` (Portal-DB), Freitext vorher/nachher | Actor nur Name, kein Index, kaskadierte Löschungen nicht einzeln |
| Neukunde anlegen | nur Log (90 Tage) | kein dauerhafter Eintrag (wer, was, Kundennummer, MEP/MEQ); SAP-API kennt den Benutzer nicht |
| Buchhaltungsübergabe | Log-Zeilen; Benutzer kommt als Freitext im Body | keine Zellhistorie (alt/neu), „Verwerfen“ ohne Benutzer, Identität nicht abgesichert |
| Zeitpläne (Jobverwaltung) | Log ohne Benutzer | niemand weiß, wer einen Zeitplan geändert/gelöscht hat |
| Jobs, „Jetzt übertragen“, Neu abfragen | `EsJob.RequestedBy` | ok |
| Ideen, Fehlermeldungen, Sync-Probleme, Einstellungen | nur letzter Stand (`…By`/`…At`) | keine Historie, Löschen ohne Spur |
| Wartungsmodus | Konfiguration (ConfigMap) | außerhalb der App – bleibt so |

Das Mongo-Log ist heute das faktische Protokoll – aber nur 90 Tage, ohne automatischen Benutzer und mit Verwerfen bei voller Warteschlange.

### Ziel
Ein gemeinsames Änderungsprotokoll für alle fachlichen Schreibaktionen:
- **Collection `AuditLog`** in der Portal-DB (`Prod-ES-Portal`), TTL-Index 10 Jahre, Indizes auf `ObjectType + ObjectId` und `AtUtc`.
- **Eintrag:** `AtUtc`, `Actor` (ObjectId, Name, E-Mail), `Source` (Portal, ApiService, Worker, SAP-API), `Action` (z. B. `customer.created`, `accounting.cell.changed`, `schedule.deleted`), `ObjectType` + `ObjectId` (z. B. `Customer/10100208`, `AccountingFile/…csv`), `System` (MEP/MEQ/esERP), `Changes` (Feld, alt, neu), `Reason` (optional), `Result` (ok/Fehler + Meldung), `CorrelationId` (Job-Id, Request-Id).
- **Schreiben nie verlieren:** direkt in Mongo (nicht über den gepufferten Logger); scheitert das Protokoll, scheitert die Aktion nicht, aber es gibt eine Fehlermeldung im Log.
- **Ablage im Paket `Meesenburg.ES.Authorization`** (hat schon die Portal-DB) als `IAuditLog`, damit Web, ApiService und Worker dasselbe nutzen. `PermissionAudit` wird darauf umgestellt (alte Einträge migrieren).
- **Identität sicher übergeben:** Web → SAP-API bleibt App-Token; der Benutzer kommt als signierter Header (`X-Mee-Actor`, vom Web gesetzt, nur aus App-Token des Portals akzeptiert) statt als Body-Feld. SAP-API protokolliert selbst.
- **Oberfläche:** Seite *Verwaltung → Änderungsprotokoll* (Filter Bereich, Objekt, Person, Zeitraum) und je Objekt ein Reiter „Verlauf“ (z. B. Kunde im Vergleich, Datei in der Buchhaltungsübergabe, Katalog).
- **Neue Berechtigung** `portal.audit.read`.

### Was protokolliert wird
- Neukunde angelegt (Formulardaten ohne Datenschutz-Häkchen, Kundennummer, System, SAP-Meldungen) – auch Ablehnungen.
- Kunde in SAP geändert (künftig), Kreditlimit gesetzt (alt/neu, Freigabe).
- Buchhaltung: jede Zellkorrektur (Datei, Beleg, Zeile, Spalte, alt, neu), Speichern, Einreichen, Verwerfen, Jetzt buchen, Neu abfragen. Originaldatei bleibt unverändert in `fehlerhaft\ersetzt` (GoBD).
- Zeitpläne: anlegen, ändern (Diff), aktivieren/deaktivieren, löschen, sofort starten. `JobSchedule` bekommt `CreatedBy`/`UpdatedBy`.
- Materialkataloge: anlegen, Import (Datei, Zuordnung, Zeilen ok/fehlerhaft), manuelle Korrekturen, Löschen.
- Berechtigungen (wie heute, strukturiert), Ideen/Fehlermeldungen löschen, Sync-Problem ignorieren/erledigen.

## 2. Materialkataloge

### esERP-Schnittstelle (generisch)
`/Materialkatalog` (Kataloge: `katalogName`, `lieferantNr`), `/Materialkatalog/{katalogName}/Artikel` (Batch-Upsert, Felder wie `MaterialkatalogArtikelRequestDto`: artikelNr, lieferantNr, matchcode, eaNr, artikelBez1/2, langText, meEinh, warengruppe, brPreis, nePreis, ekPreis, vkPreis1/2, prEinheit, rabattGr, liefRabatt, packmenge, bestellNr, herstArtNr, produktBild, status, uebernahmeKz …), dazu Rohstoffzuschläge. **`/Materialkatalog_SAP` wird nicht genutzt** (Überbleibsel).

Die esdata-API reicht diese Endpunkte heute nicht durch → neu: `MaterialCatalogApi` in `Meesenburg.de.ES.API.ESData`.

### SAP-Katalog
- Neuer Jobtyp `MaterialCatalog` im Worker; Katalog „SAP“ unter **Lieferant 2 (Meesenburg KG)** – der SAP-Spiegel, bei 2 wird alles aus SAP bestellt (entschieden 13.10.2026).
- Quelle: SAP-API, alle Artikel VKORG 298 ohne Löschvormerkung und Vertriebssperre (~225.000), schlanke Abfrage direkt auf MARA/MAKT/MVKE/MARM/A662 (nicht die langsame Artikel-View), seitenweise.
- Felder: Artikelnummer, Bezeichnung 1/2, EAN, Mengeneinheit, Warengruppe, Herstellerartikelnr./Lieferant (EINA), EK = YNEK, Listenpreis = YNEK × 2, Preiseinheit, Status (freigegeben PRAT8 ja/nein).
- **Voll-Lauf** auf Knopfdruck im Portal und z. B. wöchentlich; **Delta** über die SAP-Änderungsbelege (wie beim Artikelabgleich).
- Portal: Seite *Materialkataloge* mit „SAP-Katalog neu aufbauen / aktualisieren“, Stand, Anzahl, letzter Lauf.

### Herstellerkataloge (Fachbereiche pflegen selbst)
1. **Hochladen** (XLSX, CSV; Zeichensatz und Trennzeichen automatisch).
2. **Analyse:** Spalten automatisch zuordnen – über Überschriften (Synonyme: „Art.-Nr.“, „Artikelnummer“, „EAN/GTIN“, „Bezeichnung“, „Listenpreis/Bruttopreis“, „Rabattgruppe“, „VPE“, „ME“ …) und Inhalte (13-stellige EAN mit Prüfziffer, Preise, Mengeneinheiten). Zuordnung je Hersteller als Vorlage speichern – beim nächsten Import automatisch.
3. **Zuordnung prüfen:** Spalte → Katalogfeld per Auswahl ändern, feste Werte setzen (z. B. Lieferant, Mengeneinheit), Preisregeln (Netto = Brutto − Rabatt, Rabatt je Rabattgruppe).
4. **Prüfen:** Pflichtfelder, Formate, Dubletten, unbekannte Mengeneinheiten/Warengruppen; Abgleich mit SAP (Artikel schon vorhanden? Herstellerartikelnr. in EINA → SAP-Artikelnummer zuordnen).
5. **Korrigieren:** Tabelle im Portal, fehlerhafte Zeilen oben, Zellen direkt bearbeiten, Zeilen ausschließen, Werte für viele Zeilen auf einmal setzen.
6. **Übernehmen:** als Job (Batch-Upsert), Ergebnis je Zeile; Import mit Datei und Zuordnung im Protokoll.
- **Später:** BMEcat (XML) und DATANORM als feste Formate ohne Zuordnung, sobald ein Hersteller sie liefert.
- Berechtigungen: `catalogs.read`, `catalogs.edit` (Herstellerkataloge pflegen), `catalogs.sap` (SAP-Katalog aufbauen).

## Reihenfolge
1. **Änderungsprotokoll** (Grundlage, sofort für Neukunde, Buchhaltung, Zeitpläne).
2. **esdata-Durchreichung Materialkatalog** + **SAP-Katalog** (Job + Portal-Seite).
3. **Herstellerkatalog-Import** XLSX/CSV mit Zuordnung und Korrekturtabelle.
4. BMEcat/DATANORM nach Bedarf.

## Offene Fragen
- Wie übernimmt das esERP Katalogartikel in den Artikelstamm (`uebernahmeKz`, `status`)? → `docs/TODO-com4vision.md`. Ob die Übernahme eines SAP-Artikels auch PRAT8 in SAP setzen soll → `docs/TODO-Doro.md`.
- Originaldateien der Hersteller werden aufbewahrt (entschieden) – Ablage auf dem Fileserver clfs002: `Drives\Daten\Programme\ES2000\Data\Prod\Materialkataloge` (festgelegt von Michael, 13.10.2026; der SAP-Ordner bleibt SAP-Themen vorbehalten).

## Stand 13.10.2026 (abends)
- **Gebaut:** esdata-Durchreichung `/es/materialcatalogs` (Liste, Anlegen, Löschen, Artikel lesen, Batch-Upsert ≤ 1.000, Artikel löschen); SAP-API `/sap/catalog/article-ids` (225.273 Nummern in ~4 s, sortiert nach aufgefüllter MATNR) und `/sap/catalog/articles` (≤ 5.000; 1.000 Artikel in ~1,6 s über MATNR-Bereich); Jobtyp `MaterialCatalog` (Contracts 1.0.15) mit `MaterialCatalogJobHandler` (Katalog „SAP“ anlegen falls fehlt, Blöcke à 1.000, Wiederholung, Abbruch nach 5 Fehlblöcken); ApiService `POST /apisvc/jobs/material-catalog/sap` (`catalogs.sap`); Portal-Seite `/eserp/materialkataloge`; Berechtigungen `catalogs.read/edit/sap` (Authorization 1.0.6).
- **Mapping SAP → Katalog:** artikelNr = Material ohne Nullen, lieferantNr = 2, artikelBez1/bestellBez = MAKTX, matchcode = MAKTX groß, eaNr = EAN11, meEinh = MEINS, warengruppe = MATKL, ekPreis/nePreis = YNEK, brPreis/vkPreis1 = YNEK × 2, prEinheit = KPEIN, gueltigAbDat = DATAB, bestellNr = Artikelnummer (bestellt wird bei 2), herstArtNr = EINA-IDNLF bzw. MARA-MFRPN.
- **Blocker:** esERP liefert beim Auflisten der Katalogartikel 500 (→ `docs/TODO-com4vision.md`). Vor dem ersten Voll-Lauf einen kleinen Probelauf über die Jobverwaltung: Jobtyp Materialkatalog, Filter `{"Catalog":"SAP","ArticleIds":["9025264","9025878"]}`.
- **Offen:** Delta über SAP-Änderungsbelege (ohne PRAT8-Filter), Artikel entfernen, die in SAP gesperrt/gelöscht wurden, Herstellerimport (Schritt 3).

