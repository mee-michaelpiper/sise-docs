# Buchhaltungsübergabe 19.11.2026: „14 schon gebucht“ und fehlende Materialbewegungen

Stand 20.11.2026, nur gelesen (Freigabe per SmbRead/TransferFileInspect, SAP MEP per SapRead, Mongo, esERP-API `/lager/bewegungen` per ApiMovementProbe).

## 1. „14 schon gebucht“ in `SAP_Export_20261119_183015.csv`

- Datei vom Abendexport 19.11. (30 ER, 12 AR, 2 AR-Gutschriften „YY“ = 44 Belege), von SAP am 19.11. 20:00 abgelehnt (ER 910010753, Rundung 0,01 €).
- 20.11. 07:07 D. Hoburg-Osygus: „Neu aus esERP abgefragt“ und „Zur Buchung eingereicht“ → Job `YFI_ES_ERP_BUCHUNGEN_SISE` 07:07 → alle 44 gebucht (BKPF 20.11., JOBUSER). Fassungen: `fehlerhaft/ersetzt/…_20261120060742.csv` (Original) und `verarbeitet/…183015.csv` (gebucht) – gleiche 44 Belege, keiner entfernt, keine Überschneidung mit dem Morgenlauf 091541.
- Die Portal-Ansicht der **gebuchten** Datei zählt „schon gebucht“, indem sie je Beleg BKPF über **XBLNR = Referenz der Datei** sucht. Gefunden werden die 14 Ausgangsrechnungen – gebucht von **dieser** Datei heute 07:07, nicht vorher.
- Die 30 Eingangsrechnungen findet die Suche nicht: Der SAP-Report schreibt bei ER die **Rechnungsnummer des Lieferanten** (Spalte 9, z. B. RE26-124216, 3015730036) in XBLNR, nicht unsere laufende Nummer (910010764). Belegt: BKPF 17., 19. und 20.11., BLART 57.
- **Folge:** Die Erkennung „bereits gebucht“ (Prüfansicht, Einreichen) ist für Eingangsrechnungen blind – eine doppelt eingereichte ER würde nicht gewarnt, erst SAP lehnt ab (Doppelprüfung über Lieferant + Rechnungsnummer). Ausnahme: ER ohne Lieferanten-Nr. (z. B. 910010126, XBLNR = 910010126).

## 2. Materialbewegungen: 0 im Abendlauf 19.11.

Zeitfenster (Mongo `Accounting-Exports`, lückenlos aneinander):

| Datei | Fenster (UTC) | Bewegungen |
|---|---|---|
| SAP_Export_20261117_183015 | 16.11. 18:30 → 17.11. 18:30 | 89 |
| SAP_Export_20261119_091541 (Nachhol-Lauf von Hand) | 17.11. 18:30 → 19.11. 09:15 | 54 |
| SAP_Export_20261119_183015 | 19.11. 09:15 → 19.11. 18:30 | **0** |

**Ursache: Die esERP-API `/lager/bewegungen` filtert nur nach dem Datum** – ermittelt mit fünf Abfragen:

| datumVon → datumBis | Ergebnis |
|---|---|
| 18.11. 00:00 → 19.11. 00:00 | 18.11. **und** 19.11. vollständig (106) |
| 19.11. 00:00 → 19.11. 23:59 | 19.11. vollständig (68) |
| 19.11. 12:00 → 20.11. 12:00 | nur 20.11. (10) – nichts vom 19.11., obwohl dort Bewegungen bis 14:03 |
| 19.11. 09:15 → 19.11. 18:30 | 0 |

Regel: `Datum(bewegDatum) ≥ datumVon` (Tagesanfang 00:00 gegen die volle Uhrzeit verglichen) und `Datum(bewegDatum) ≤ Datum(datumBis)`. Ein Fenster, das nicht um 00:00 beginnt, verliert den ersten Tag; ein Fenster innerhalb eines Tages liefert nie etwas.

**Fehlend am 19.11.: 52 Bewegungen** (630016069 … , gebucht 11:01–14:03 Ortszeit) – nicht im Morgenlauf (der lief 10:15), nicht im Abendlauf (Fenster innerhalb des Tages), und der nächste Lauf beginnt beim 20.11. **Sie kommen nicht von selbst.** Vom 18.11. fehlen 2 (630015945, 630015946, 08:19) – vermutlich vom Mapper bewusst ausgelassen (prüfen).

**Im Normalbetrieb** (nur Abendläufe 19:30) fällt die Regel kaum auf: Fenster X 18:30 → X+1 18:30 liefert genau den Tag X+1 (Stand 19:30). Verloren gehen dabei Bewegungen, die **nach 19:30 noch mit dem Tagesdatum** gebucht werden. **Jeder Lauf außer der Reihe** (Nachhol-Lauf, Export von Hand) verliert den Rest seines Tages.

## 3. Optionen (Buchhaltungslogik – erst nach Freigabe umsetzen)

| # | Was | Folge |
|---|---|---|
| A (empfohlen) | Worker fragt **ganze Tage** ab (ab dem Datum des letzten Fensterendes 00:00 bis heute) und lässt Bewegungen weg, deren Nummer schon in einer Übergabedatei war (`Accounting-Exports.References`, „ES-<bewegNr>“). Doppelte Übergabe ausgeschlossen, Nachzügler nach 19:30 kommen am nächsten Abend | Robust gegen Läufe außer der Reihe; braucht die Referenzliste (vorhanden) |
| B | com4vision: `/lager/bewegungen` mit Uhrzeit filtern (TODO-com4vision) | Behebt die Ursache, Termin offen; bis dahin A |
| C | Nur Läufe außer der Reihe verbieten | Verliert weiter die Nachzügler nach 19:30 |

**Nachholen** der 52 Bewegungen vom 19.11. (und Prüfung der 2 vom 18.11.): einmaliger Export nur dieser Bewegungsnummern – nach Freigabe der Buchhaltung, SAP prüft „ES-<Nr>“ zusätzlich als Referenz.

**Prüfen, ob es früher schon passiert ist:** alle Läufe außer der Reihe seit Start (z. B. `SAP_Export_20261111_112819`, `…20261110_080130`, `…20261110_084155`) und je Tag „Bewegungen laut API“ gegen „in Übergabedateien“ abgleichen.
