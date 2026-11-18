# TODO Doro

Themen, die fachlich mit Doro abgestimmt werden müssen, bevor wir sie umsetzen.

## Artikel fürs esERP freigeben (PRAT8)
- [x] **Umgesetzt 14.10.2026** (freigegeben vom Fachbereich): Knopf „Für das ES freigeben“ in *SAP ↔ esERP vergleichen*, Berechtigung `eserp.article.release`. Setzt PRAT8 in **VKORG 298 und 100**, Vertriebsweg **00**, nur bei vorhandenem MVKE-Satz; Protokoll im Portal (Änderungsprotokoll), danach sofort Übertragung ins esERP.
  - **Stücklisten (MEQ-Test mit 22680 LUMF und 426263 ZLUM):** SAP setzt PRAT8 nur am angegebenen Material, die Komponenten bleiben unverändert. Die Komponenten brauchen PRAT8 aber selbst, sonst fehlen sie im esERP und die Stückliste (Kaskade) ist unvollständig → der Knopf gibt die Komponenten mit frei (bei 22680 fehlte es bei 17929). Geändert wird nur `MVKE-PRAT8` (Änderungsbeleg geprüft).
  - **Noch offen:** Wer bekommt `eserp.article.release`? PRAT8 wieder entfernen erlauben?
- [x] **Artikel im Portal fürs esERP kennzeichnen** – mit eigener Berechtigung (z. B. `eserp.article.release`), z. B. direkt auf der Seite *SAP ↔ esERP vergleichen*, wenn dort „nicht fürs ES freigegeben“ steht. – **erledigt** (`eserp.article.release`, Freigabe-Knopf im Vergleich, Web 1.5.0); offen nur, wer die Berechtigung bekommt.
  - **Technisch möglich (geprüft 13.10.2026, nur lesend):** `BAPI_MATERIAL_SAVEDATA` ist per RFC aufrufbar; PRAT8 (MVKE) heißt dort `SALESDATA-PROD_ATT_8` (+ `SALESDATAX-PROD_ATT_8`), danach `BAPI_TRANSACTION_COMMIT`. Die Änderung schreibt einen Änderungsbeleg (MATERIAL) – der Delta-Job „nur SAP-Änderungen“ überträgt den Artikel danach innerhalb von ~10 Minuten.
  - **Voraussetzung:** Das Material muss für Verkaufsorganisation 298 (+ Vertriebsweg) bereits angelegt sein (MVKE-Satz). Neu anlegen von Vertriebsdaten wäre deutlich mehr (Pflichtfelder, Steuer, …).
  - **Zu klären mit Doro:**
    - Wer darf freigeben (Rolle/Personenkreis)? Gibt es fachliche Prüfungen vorher (Preis YNEK vorhanden, Warengruppe, Status …)?
    - Auch wieder **entfernen** (PRAT8 löschen) erlauben? Was passiert dann mit dem Artikel im esERP?
    - Welcher Vertriebsweg (VTWEG) bei 298?
    - Protokoll: Wer hat wann freigegeben (im Portal speichern, SAP-Änderungsbeleg zeigt nur den technischen Benutzer `srvusr01`)?
    - **SAP-Materialkatalog im esERP** (kommt, Lieferant 2, alle ~225.000 aktiven Artikel der VKORG 298): Wird ein Artikel im esERP aus dem Katalog in den Artikelstamm übernommen, soll das Portal dann automatisch PRAT8 in SAP setzen? Sonst überträgt der Abgleich den Artikel nicht und er bleibt nur im esERP angelegt (Preise/Änderungen aus SAP kämen nicht nach).
  - Schreibender SAP-Zugriff in MEP → vor Umsetzung Freigabe durch Michael; erst in MEQ testen.

## Buchhaltungsübergabe
Seite *Buchhaltungsübergabe* (`/eserp/buchhaltung`) ist gebaut, aber noch nicht deployt. Technische Punkte stehen in `docs/TODO.md`, Abschnitt Buchhaltungsübergabe.
- [ ] **Wer bekommt welche Berechtigung?**
  - `accounting.transfer.read`: Dateien und Fehler sehen
  - `accounting.transfer.edit`: CSV korrigieren, „Neu aus esERP abfragen“, verwerfen
  - `accounting.transfer.run`: korrigierte Datei sofort in SAP buchen (sonst um 19:30 mit dem normalen Lauf)
- [ ] **Rechnungen ohne PDF** werden nicht mehr übergeben, sondern als Sync-Problem gemeldet. Ist das so gewollt? Wer kümmert sich darum, dass das PDF im esERP nachkommt?
- [ ] **Benachrichtigung:** SAP lehnt Dateien still ab (Job endet immer mit „beendet“). Soll die Buchhaltung eine Mail bekommen, wenn eine Datei in „fehlerhaft“ landet? An wen?
- [ ] **Geschlossene Perioden:** Belege mit Buchungsdatum in einer gesperrten Periode – Buchungsdatum im Portal auf den ersten Tag der offenen Periode setzen (Vorschlag der Seite) oder Periode öffnen?
- [ ] **Fehlende Kostenstellen / falsche Steuerkennzeichen** kommen aus dem esERP. Soll das Portal nur korrigieren, oder muss jemand die Ursache im esERP (Stammdaten) beheben?
- [ ] **Ausgangsrechnungen:** esERP liefert dort kein Buchungsdatum (es gilt das Rechnungsdatum). Reicht das, oder bei com4vision anfragen?

## Neukundenanlage: Fehler
- [ ] **13 Neukunden mit falschem Namen 2** („Meesenburg GmbH HH/GT“ vom Musterkunden, angelegt 05.–09.10.2026, z. B. 10100197, 10100198). Ursache ist behoben. Wer korrigiert die Kunden in SAP (oder sollen wir das per Portal/RFC machen)?
- [x] **Kreditlimit:** siehe eigener Abschnitt unten. – **Bleibt so (10.11.2026): FIS-Aufwand zu hoch; Kreditlimit fürs esERP uninteressant, TAX egal, Werbeverbot mit Workaround.**
- [x] **Zahlungsbedingungen:** Feld ist fertig, aber ausgeblendet. Welche Bedingungen dürfen im Formular wählbar sein? – **erledigt** (05.11.2026: freigegeben, Liste in den Einstellungen).
- [ ] **Bestätigungsmail an Neukunden:** gewünscht? Entwurf in `docs/entwurf-neukunden-bestaetigungsmail.md`.
- [ ] **Wer darf Neukunden anlegen?** Heute alle Angemeldeten; Vorschlag: nur Rolle Vertrieb.
- [ ] **Datenschutzerklärung** der Meesenburg GmbH für Kundendaten fehlt (Links im Formular).

## Kreditlimit bei Neukunden
- [x] **Heute bekommt jeder Neukunde das Kreditlimit des Musterkunden (500 €).** Das SAP-Programm der Kundenanlage (FIS) liest die Kreditdaten aus dem Formular nicht. Gerade im Projektgeschäft ist das zu wenig. – **Bleibt so (10.11.2026): FIS-Aufwand zu hoch; Kreditlimit fürs esERP uninteressant, TAX egal, Werbeverbot mit Workaround.**
  - **Wie es heute läuft (SAP gelesen am 13.10.2026):** Klassisches Kreditmanagement (KNKK, Kreditkontrollbereich 298), FSCM wird nicht genutzt. Limits setzt die Buchhaltung von Hand in **FD32** (z. B. 1.000 €, 3.000 €, 30.000 €). Das ist also schon heute der Freigabeschritt.
  - **Technisch möglich, drei Wege:**
    1. **FIS:** kleiner RFC-fähiger Z-Baustein um `CREDITLIMIT_CHANGE` (sauberste Lösung, der Standardbaustein selbst ist nicht per RFC aufrufbar).
    2. **Ohne FIS:** FD32 per Batch-Input über den RFC-Baustein `RFC_CALL_TRANSACTION_USING` bzw. `ABAP4_CALL_TRANSACTION` (beide vorhanden). Funktioniert, hängt aber an den FD32-Bildern – bei Änderungen am Bildaufbau bricht es.
    3. **FIS:** das Kundenanlage-ABAP liest das Limit aus dem Formular (`ItCredit`) – dann nur bei der Anlage, nicht für spätere Änderungen.
  - **Zu klären mit Doro:**
    - Darf der Vertrieb bei der Anlage ein höheres Limit beantragen? Bis zu welcher Höhe ohne Freigabe?
    - Muss ein höheres Limit von der **Buchhaltung freigegeben** werden? Wer genau (heute pflegen es z. B. LINNENSU und DEMIRKZE in FD32), und wie schnell muss das gehen?
    - Ablauf-Vorschlag: Formular (oder Vergleichsseite) fragt „gewünschtes Kreditlimit“ + Begründung (z. B. Projekt) ab → bis X € setzt das Portal es direkt, darüber Freigabe-Anfrage an die Buchhaltung im Portal (wie Zugriffsanfragen) → nach Freigabe setzt das Portal das Limit in SAP.
    - Gelten Unterschiede nach Kundenart (Firma/Privat) oder Verkaufsbüro?
    - Soll der Standard von 500 € (Musterkunde) bleiben oder angehoben werden?

## Kunden im Portal korrigieren
- [ ] **Fehler aus der Neuanlage schnell im Portal korrigieren** (Technik: `docs/TODO.md`, „Kunden in SAP ändern“). Zu klären mit Doro:
  - Wer darf ändern – alle, die anlegen dürfen, oder nur bestimmte Personen? Nur kürzlich angelegte Kunden (z. B. erste 14 Tage) oder alle?
  - Welche Felder sind tabu (z. B. Name nach erster Rechnung, Zahlungsbedingung, Bankdaten nur Buchhaltung)?
  - Soll die Buchhaltung über Änderungen informiert werden?

## Werbeverbot
- [x] **Werbeverbot wird bei Neukunden nie gesetzt.** Das SAP-Programm der Kundenanlage (FIS) liest die falsche Merkmalklasse (`ZCRM_DEB_POTENTIAL` statt `DEBITOR`). Folge: Das Werbeverbot aus dem Formular kommt nicht in SAP an, und jeder Neukunde bekommt eine leere Klasse `ZCRM_DEB_POTENTIAL`. – **Bleibt so (10.11.2026): FIS-Aufwand zu hoch; Kreditlimit fürs esERP uninteressant, TAX egal, Werbeverbot mit Workaround.**
  - **Entscheiden:** FIS repariert das ABAP (sauberste Lösung) **oder** wir setzen das Werbeverbot nach der Anlage selbst per `BAPI_OBJCL_CREATE`.
  - **Bestand:** Alle über das Portal angelegten Kunden haben kein Werbeverbot-Merkmal. Nachpflegen? Wer (Vertrieb, per Liste aus dem Portal, oder per RFC von uns)?
  - Ist die leere Zuordnung `ZCRM_DEB_POTENTIAL` ein Problem (CRM-Auswertungen)?

## Steuerfelder (TAX) bei Neukunden
- [x] **Nur die USt-IdNr. kommt an.** Das SAP-Programm der Kundenanlage (FIS) übernimmt die übrigen TAX-Items nicht (`MAP_CENTRAL_DATA`), z. B. die Steuernummer. Die USt-IdNr. kommt an (Michael, 13.10.2026) – für unseren Zweck vermutlich ausreichend. – **Bleibt so (10.11.2026): FIS-Aufwand zu hoch; Kreditlimit fürs esERP uninteressant, TAX egal, Werbeverbot mit Workaround.**
  - Kurz bestätigen: Brauchen wir die Steuernummer (Einzelunternehmer ohne USt-IdNr.) in SAP, z. B. für Auswertungen oder Rechnungen? Wenn nein: Thema schließen, FIS nicht beauftragen.

## Austritte von Mitarbeitern
Technik steht in `docs/TODO.md` (Datenabgleich → „Austritte von Mitarbeitern ins esERP“).
- [ ] **Ausgetretene Mitarbeiter werden heute nicht automatisch im esERP beendet.** Austrittsdaten pflegt jemand von Hand im esERP. In SAP (298) waren am 09.10.2026 146 Mitarbeiter aktiv und 142 ausgetreten; 27 Ausgetretene haben noch eine gültige E-Mail und gehen weiter als aktiv ins esERP.
  - **Zu klären mit Doro:**
    - Soll das Austrittsdatum (`persEndDat`) künftig automatisch aus SAP kommen? Ist SAP dafür verlässlich genug – die Personalabteilung prüft gerade die Liste `Mitarbeiterabgleich_298_2026-10-09.xlsx`.
    - Reicht das Austrittsdatum, oder muss im esERP zusätzlich der Status wechseln (Anmeldung sperren, aus Auswahllisten wie Bearbeiter/Vertreter nehmen)?
    - Was passiert mit offenen Vorgängen (Angebote, Aufträge, Wiedervorlagen) ausgetretener Mitarbeiter – wer übernimmt?
    - Wer pflegt bis dahin die 27 „aktiven“ Ausgetretenen nach?
  - Eintrittsdaten (`persStartDat`) übernehmen wir bewusst nicht aus SAP (durch die Systemumstellung teils falsch).

## Lieferantenstatus
Technik steht in `docs/TODO.md` (Datenabgleich → „Lieferantenstatus in esERP definieren“).
- [ ] **In SAP gesperrte Lieferanten bleiben im esERP aktiv.** Der Abgleich überträgt gesperrte Lieferanten gar nicht mehr (statt sie zu sperren), und alle übertragenen haben im esERP fest den Status 1 (aktiv). Folge: Im esERP kann weiter bei Lieferanten bestellt werden, die in SAP gesperrt sind.
  - **Zu klären mit Doro:**
    - Welche Werte hat `liefstatus` im esERP, und was bewirken sie (Bestellung gesperrt, nur Warnung, ausgeblendet)? Ggf. bei com4vision erfragen.
    - Welche SAP-Sperre soll was bedeuten: zentrale Buchungssperre, Einkaufssperre, Sperre für den Buchungskreis (`Locked`, `BALocked`, `GenerelLocked`)?
    - Wie bei Kunden zusätzlich „GESPERRT“ im Matchcode anzeigen?
    - SAP ist führend – einverstanden, dass eine Sperre in SAP die Sperre im esERP immer überschreibt (auch wenn jemand im esERP entsperrt hat)?

## EK-Preise (Dorma, Geze, Türautomation/Türschließer)
Technik und Zahlen: `docs/TODO.md`, „EK-Preise im esERP zu hoch“.
- [ ] **Im esERP steht als EK der Einkaufspreis der KG (YNEK), nicht der Preis, den SiSe tatsächlich zahlt.** SiSe kauft mit eigenen Einkaufsorganisationen (S110–S160) und hat bei Dorma/Geze teils deutlich bessere Konditionen (Beispiel Geze 9066348: SiSe ~297 €, KG/YNEK 606 €). Seit im esERP echte Bestellungen geschrieben werden, gehen diese zu hohen Preise in Bestellungen und in die Kalkulation. Ein Drittel der 2026 bestellten Dorma/Geze-Artikel hat gar keinen YNEK (EK 0 im esERP).
  - **Zu klären mit Doro:**
    - Welcher EK ist „richtig“ für die Kalkulation: der Preis aus den SiSe-Konditionen (Vorschlag) oder der Verrechnungspreis je Werk? Der Verrechnungspreis ist in vielen Werken veraltet bzw. unbrauchbar.
    - Bei welchem Lieferanten wird im esERP bestellt – direkt beim Hersteller (Geze/Dorma) oder bei der Meesenburg KG? Für die KG gilt der Verrechnungspreis, für den Hersteller der SiSe-Einkaufspreis.
    - Welche SiSe-Einkaufsorganisation ist maßgeblich (S110 Flensburg, S140 Berlin …) – sind die Konditionen dort gleich?
    - Soll der Listenpreis (heute YNEK × 2) bleiben, oder sich am neuen EK orientieren?
    - FIS beauftragen, den SiSe-EK in SAP mitzurechnen (empfohlen), oder Übergangslösung „letzter SiSe-Bestellpreis“?

## Rechnungsversand per Mail (15.10.2026)
- [ ] **Ändern Sachbearbeiter beim Mailversand aus dem esERP den Betreff?** Das Portal erkennt versendete Rechnungsmails am Betreff `Meesenburg GmbH Rechnung <Nummer>` (Nachrichtenflussregel → Kopie an eserp_mail@). Wird der Betreff geändert, fehlt die Mail im *Rechnungsversand*. Wenn ja: Bitte nicht ändern, oder wir sichern zusätzlich über den Anhangnamen ab (Frage an com4vision).
- Hinweis: Das esERP verschickt im Namen des Sachbearbeiters, legt aber **keine Kopie in „Gesendete Elemente“** ab – deshalb finden die Kollegen ihre Mails nicht. Ist bei com4vision angefragt; bis dahin zeigt die Seite *Rechnungsversand*, ob und wann die Mail rausging.

