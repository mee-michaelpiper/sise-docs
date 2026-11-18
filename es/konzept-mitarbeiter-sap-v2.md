# Mitarbeiter aus SAP HCM – Abfrage V2 (umgesetzt 22.10.2026)

Die bisherige Abfrage (`BAPI_EMPLOYEE_GETDATA` mit `DATE` = heute) bleibt **unverändert** im Broker bestehen.
V2 ist – wie bei Artikeln (`SelectAllArticlesV2Async`) – nur eine neue Broker-Funktion `SelectAllEmployeesV2Async`; der Endpunkt
`GET /sap/employees` bleibt, der Service wählt V1/V2 per Schalter und liefert die zusätzlichen Felder optional mit (V1-Aufrufer merken nichts).
Entscheidungen 22.10.2026: Auszubildende kommen ins esERP; alles andere (Geringfügige, Praktikanten, Fahrer …) nur auf Anforderung über Ausnahmen.

## Was V1 heute falsch oder gar nicht liefert

Grundlage: Abgleich HCM ↔ esERP vom 22.10.2026 (`~/repos/sap-tools/tools/hr-abgleich/abgleich.py`).

| Problem | Beispiel | Folge im esERP |
|---|---|---|
| Kein Austrittsdatum, auch kein geplantes | Austritt zum 31.10.2026 in IT0000 schon erfasst | Person bleibt bis zum Austritt planbar, danach verschwindet sie nur, weil sie nicht mehr geliefert wird; `persEndDat` wird nie gesetzt |
| Langzeit-Abwesende gelten als aktiv | Elternzeit (IT2001 Art 0601) bei Lindner (290) bis 08/2028, Herold (Techniker) bis 03/2028 | in der Verplanung (PEP) sichtbar, belegen ggf. Lizenzen |
| Haupt- und Nebenbeschäftigung = zwei Mitarbeiter | Philipp: 298/790 Kundenmanagerin + 290/291 geringfügig; Lindner: 290 in Elternzeit + 298 geringfügig | doppelte Einträge (zwei Personalnummern, gleiche E-Mail) |
| Keine Merkmale für die Einordnung | Mitarbeitergruppe (geringfügig), Mitarbeiterkreis (Azubi), Planstelle, Org.-Einheit fehlen bzw. nur als Text | Ausschlüsse nur über Kostenstelle (790/791) und von Hand (fiktive Austrittsdaten) |
| Wechsel in andere Buchungskreise unklar | Ekilmis seit 06/2022 in BK 100 | – (V1 filtert richtig, aber nicht nachvollziehbar) |

## V2: Broker-Funktion

`SAPBroker.SelectAllEmployeesV2Async(DateOnly date)`; `EmployeeService` nutzt sie bei `Employees:SapQueryVersion = 2` (Standard 1).
Endpunkt, Absicherung und **Verschlüsselung der Personalnummer** unverändert.

Quelle: HCM-Tabellen direkt per RFC am Stichtag (statt BAPI), je Personalnummer:

| Feld | Quelle | Zweck |
|---|---|---|
| `employeeId` (verschlüsselt), Name, E-Mail, Benutzer | IT0001/0002/0105 | wie V1 |
| `companyCode`, `costCenter` | IT0001 | wie V1 |
| `site` (Personalteilbereich-Text), `orgUnit`, `orgPath`, `job` (Stelle), `position` (Planstelle) | IT0001, T001P, HRP1000/1001 | Einordnung Projekt/Fachmarkt, Techniker/Innendienst |
| `employeeGroup` (1 Arbeitnehmer, G geringfügig, K kurzfristig, 9 extern), `employeeSubgroup` (01 GF … 40/42 Azubi, 45/46 Praktikant) | IT0001 PERSG/PERSK | Ausschlüsse ohne fiktive Daten |
| `entryDate`, `exitDate` (tatsächlich oder geplant = Tag vor der Austrittsmaßnahme) | IT0000 | `persStartDat`/`persEndDat` |
| `absence` {Art, Text, von, bis} für Langzeit-Abwesenheiten | IT2001 Arten 0500 Mutterschutz, 0601–0603 Elternzeit, 0610/0612 Krankengeld/EU-Rente, 0620 unbezahlter Urlaub, 9300 Freistellung (Liste konfigurierbar) | ruhendes Arbeitsverhältnis erkennen |
| `personKey` + `isPrimary` | Name + Geburtsdatum (nur intern, wird nicht ausgegeben – nur der Schlüssel als Hash) | Haupt-/Nebenbeschäftigung |

**Hauptbeschäftigung** je Person (`isPrimary`): unter den am Stichtag aktiven Personalnummern derselben Person
1. ohne Langzeit-Abwesenheit vor mit,
2. Mitarbeitergruppe 1 (Arbeitnehmer) vor G/K (geringfügig/kurzfristig),
3. bei Gleichstand der neuere Eintritt.

Ergebnis heute: Philipp → 298/790 (Haupt), 290/291 Neben; Lindner → 298 (geringfügig) aktiv, 290 ruhend (Elternzeit).

Optional zusätzlich: ausgetretene/gewechselte Personalnummern der letzten 90 Tage mit `exitDate` liefern, damit der Worker `persEndDat` setzen kann (V1 liefert Ausgetretene gar nicht mehr).

## Regeln und Ausnahmen in Mongo, gepflegt im Portal

Statt appsettings: Collection `Employee-SyncRules` in der Worker-DB (wie `Customer-SalesOffices`), gepflegt auf einer **Einstellungsseite für Admins** im Portal.
Der Worker liest die Regeln bei jedem Lauf (kein Deploy für Änderungen).

- **Regeln:** ausgeschlossene Kostenstellen (790, 791), ausgeschlossene Mitarbeitergruppen (G, K, 9) und -kreise (45/46 Praktikanten), ausgeschlossene Stellen (Fahrer), Langzeit-Abwesenheitsarten (0500, 0601–0603, 0610, 0612, 0620, 9300).
- **Ausnahmen je Person** (HCM-Nr. bzw. esERP-Nr. + Name zur Anzeige, Grund, wer/wann): „immer übertragen“ (z. B. Geringfügige mit esERP-Bedarf), „nie übertragen“, optional feste Lizenz/Rolle. Ausnahme schlägt Regel.
- Vorschau auf der Seite: „Mit diesen Regeln würden übertragen: …, ausgeschlossen: … (Grund)“ – aus dem letzten Lauf, ohne SAP-Aufruf.

## Worker mit V2

Startwerte der Regeln:

1. **Nicht ins esERP:** Kostenstellen 790, 791 (wie heute); Mitarbeitergruppe G/K/9; Praktikanten; Stelle „Fahrer“; `isPrimary = false` – außer per Ausnahme „immer übertragen“. **Auszubildende werden übertragen.**
2. **Ruhend (Langzeit-Abwesenheit):** übertragen, aber `persEndDat` = Tag vor Beginn der Abwesenheit → aus Suche und PEP raus, Daten bleiben; nach Rückkehr wieder leer.
3. **Austritt:** `persEndDat` = `exitDate` (auch geplant, dann ab dem Tag ausgeblendet).
4. **Vorschlag Lizenz/Rolle** (nur Bericht, nicht automatisch): Service-Techniker → Techniker, Kundenmanager FM/Fachmarkt/Onlinevertrieb → prüfen, übrige → Innendienst.
5. Bereits im esERP stehende Personen, die nach 1. nicht mehr übertragen werden: **einmalig** `persEndDat` setzen statt der heutigen fiktiven Daten (Liste vorher freigeben).

Abweichungen (mehrfach aktiv ohne eindeutige Hauptbeschäftigung, aktiv ohne E-Mail, E-Mail doppelt) als **Sync-Problem** je Person statt Log-Warnung.

## Offene Punkte vor der Umsetzung

- **`persEndDat` im esERP:** Laut API bei allen 144 leer, obwohl z. B. Sven Rabe (32740450) im esERP ein Austrittsdatum hat → klären, ob die API das Feld nicht liefert oder unser Abgleich es löscht (siehe `docs/TODO.md`). Erst danach Regel 2/3/5 aktivieren.
- Liste der Langzeit-Abwesenheitsarten mit der Personalabteilung abstimmen.
- Buchungskreis 290 wird zum 31.12.2026 aufgelöst → danach nur 298; Regeln für Doppelbeschäftigung bleiben trotzdem sinnvoll.

## Einstellungsseite für Admins (Portal)

`/portal/einstellungen`, Berechtigung `portal.settings.manage` (über Rolle, Grundzugang nein). Ein Bereich je Thema, jede Änderung im Änderungsprotokoll:

| Bereich | Inhalt | Ablage heute |
|---|---|---|
| Neukundenanlage | Firma/Behörde als Privatkunde: nur Warnung/gesperrt (heute Schalter auf der Erfassungsseite); Verkaufsbüro → Musterkunde (MEP/MEQ) | Portal-DB `CustomerSettings`; Worker-DB `Customer-SalesOffices` (nur per Mongo pflegbar) |
| Mitarbeiter-Sync | Regeln und Ausnahmen (s. o.), Vorschau | neu: Worker-DB `Employee-SyncRules` |
| später | weitere Zuordnungen (z. B. Zahlungsbedingungen-Positivliste, Kostenstellen-Mapping Gsnr/Kostenstelle aus `Employee.cs`) | appsettings / Code |
