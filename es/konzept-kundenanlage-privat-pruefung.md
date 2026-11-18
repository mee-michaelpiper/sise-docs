# Neukundenanlage: Firmen als Privatkunden verhindern (Entwurf 19.10.2026)

Zur Abstimmung mit Vertrieb und Buchhaltung. Noch nichts umgesetzt.

## Befund (Prod, Änderungsprotokoll 14.–19.10.2026)

- 20 Kunden über das Portal angelegt, davon **17 als Privatkunde**.
- Davon sind mindestens 5–7 erkennbar keine Verbraucher:
  - Rechtsform im Namen: „… GmbH“, „… gGmbH/mbH“
  - Schule, Wohnungseigentümergemeinschaften (WEG/GdWE „c/o … Hausverwaltung GmbH“)
  - Firmen-Mail-Domains statt gmail/gmx/web.de (z. B. eine Hausverwaltung, ein Seniorenpark, eine Gerichtsvollzieherin)
- **Grund für den Umweg:** Firmenkunden brauchen heute USt-IdNr. **oder** Steuernummer (Pflichtfeld). Hat der Kunde sie nicht zur Hand, wird „Privat“ gewählt.

## Lösungsbausteine

Sortiert nach Aufwand. Am meisten bringt eine Kombination aus 1–4.

### 1. Erkennung im Formular (Portal, ~1 Tag)
Beim Speichern als Privatkunde prüft das Portal auf Firmenmerkmale:

| Merkmal | Beispiele | Folge |
|---|---|---|
| Rechtsform im Namen | GmbH, mbH, AG, KG, OHG, GbR, UG, e.K., e.V., SE, Ltd, & Co | **sperren**: „Das ist eine Firma – bitte als Firmenkunde anlegen“ |
| Organisation statt Person | Schule, Stadt, Gemeinde, Amt, Kirche, Verein, Praxis, Kanzlei, Hausverwaltung, Immobilien, WEG/GdWE, „c/o“, Bau, Tischlerei, Objektbetreuung … | **Warnung + Begründung Pflicht** |
| Firmen-Mail-Domain | alles außer bekannten Freemailern (gmail, gmx, web.de, t-online, outlook, yahoo, arcor …) | Warnung + Begründung |
| Rechnung per ZUGFeRD/XRechnung oder Leitweg-ID | – | **sperren**: Das gibt es nur bei Firmen bzw. Behörden |

- Die Begründung landet im Änderungsprotokoll und in der Auswertung (Punkt 4).
- Die Listen pflegen wir in Mongo, damit die Buchhaltung sie ohne Deploy ergänzen kann.
- Grenzen: Einzelunternehmer, die nur ihren eigenen Namen angeben („Hans Müller“ statt „Tischlerei Müller“), erkennt so keine Prüfung.

### 2. Privatkunde = Person (Portal, ~½ Tag)
- Für Privatkunden die Felder **Vorname + Nachname** statt „Name“.
- In SAP kommen Anrede, Vorname und Nachname in die Personenfelder.
- Ein Firmenname passt dort schlecht hinein, und die Erkennung aus Punkt 1 wird genauer.
- Die zweite und dritte Namenszeile (heute für „c/o …“ genutzt) entfallen bei Privatkunden.

### 3. Abgleich mit bestehenden Kunden (sapdata + Portal, ~1–2 Tage)
- **Gleiche Anschrift**, gleiche Telefonnummer oder gleiche Mail-Domain wie ein bestehender **Firmen**kunde → Hinweis „Unter dieser Adresse gibt es schon Firma X (10012345) – gehört der Kunde dazu?“
- Das verhindert nebenbei Dubletten.
- Benötigt eine Suche nach Straße + PLZ in SAP (sapdata, neuer Endpunkt). Der Suchindex hat heute nur Name, PLZ und Ort.

### 4. Nachkontrolle durch die Buchhaltung (Portal, ~1 Tag)
- Dashboard-Kachel und Liste „Neue Privatkunden mit Firmenmerkmalen“.
  - Inhalt: wer angelegt hat, welche Merkmale, welche Begründung.
  - Mit „passt“ / „umstellen auf Firma“.
- Optional als Mail an die Buchhaltung.
- Wirkt auch als Abschreckung: Jede Anlage ist namentlich protokolliert, das ist heute schon so.

### 5. Freigabe statt Sofortanlage bei Verdacht (Portal + SAP, ~2–3 Tage)
Variante A
- Bei Merkmalen aus Punkt 1 wird der Kunde **mit Auftragssperre** angelegt (AUFSD in KNVV).
- Die Buchhaltung gibt ihn frei.
- Der Vertrieb kann sofort weiterarbeiten (Angebot), aber nicht ausliefern bzw. fakturieren.

Variante B
- Der Antrag geht in eine Freigabeliste, angelegt wird erst nach Freigabe.
- Variante B ist strenger, bremst aber den Vertrieb.

### 6. Ursache entschärfen: Firma ohne Steuernummer zulassen (Abstimmung Buchhaltung)
- Für **inländische** Firmenkunden ist die USt-IdNr. für die Rechnung nicht zwingend. Sie ist nur für innergemeinschaftliche Lieferungen nötig.
- Vorschlag:
  - Firmenkunde ohne USt-IdNr./Steuernummer erlauben, aber mit Kennzeichen **„Steuerdaten fehlen“**.
  - Die Buchhaltung fragt nach, Liste wie in Punkt 4.
  - Optional zusätzlich die Auftragssperre aus Punkt 5.
- So muss niemand mehr den Umweg über „Privat“ nehmen. Das ist vermutlich der wirksamste Hebel.
- **Frage an die Buchhaltung:** Warum ist die Steuer-ID heute Pflicht, und welche Probleme entstehen konkret mit falschen Privatkunden (Steuerschlüssel, Mahnwesen, Reverse Charge, Bauleistungen §13b)?

### 7. Externe Prüfung (optional)
- **USt-IdNr. prüfen:** Das Bundeszentralamt für Steuern (BZSt) bietet eine kostenlose Schnittstelle zur Bestätigung (eVatR). Damit erkennen wir erfundene oder falsche IDs bei Firmenkunden. Ob Name und Ort passen, liefert nur die qualifizierte Anfrage.
- **Handelsregister** (northdata, OpenCorporates; beide kostenpflichtig):
  - Findet eingetragene Firmen am Ort.
  - Bringt gegenüber Punkt 1 wenig, weil eingetragene Firmen meist schon an der Rechtsform erkennbar sind.
  - Einzelunternehmer stehen dort nicht.

## Weiter gedacht: Was passiert, wenn die Regeln bekannt sind?

Wortlisten allein lernen die Leute schnell zu umgehen. Wahrscheinliche nächste Schritte:

- „GmbH“ weglassen oder verschreiben (G.m.b.H., Gmbh, GmbH in Name 2/3 oder in der Straße).
- Nur den Namen des Inhabers oder Geschäftsführers eintragen, mit dessen privater Mail.
- Firma als „c/o“ in die Adresse schreiben.
- Einen bestehenden Privatkunden weiterverwenden statt einen neuen anzulegen.
- In SAP direkt anlegen oder später umstellen, am Portal vorbei.

Deshalb vier Ebenen statt einer Prüfung.

### A. Anreiz wegnehmen (wirkt unabhängig von jeder Erkennung)
Wer den Umweg nimmt, hat einen Vorteil davon. Fällt der Vorteil weg, hört der Umweg auf.
- **Firma ohne Steuer-ID zulassen** (Punkt 6). Die Buchhaltung fasst nach, statt dass der Vertrieb blockiert ist.
- **Privatkunden unattraktiv machen, wo es fachlich ohnehin richtig ist:**
  - nur bestimmte Zahlungsbedingungen (z. B. Vorkasse oder kurze Frist)
  - Verbraucherpreise bzw. keine Firmenrabatte
  - keine ZUGFeRD/XRechnung
  - Wer als Firma bessere Konditionen bekommt, legt freiwillig als Firma an.
- **Klare Frage statt Auswahl:** Statt „Privat/Firma“ fragt das Formular: „Kauft der Kunde für sich privat – oder für eine Firma, einen Selbstständigen, einen Verein, eine Behörde, eine Hausverwaltung oder eine WEG?“ Grenzfälle sind dann eindeutig, und „ich wusste es nicht“ zählt nicht mehr.

### B. Bei der Anlage: Bewertung statt Einzelregeln
- Viele schwache Signale ergeben **eine Punktzahl**, ab einer Schwelle folgen Hinweis, Begründung oder Sperre. Ein einzelnes umgangenes Merkmal reicht dann nicht.
- Die Signale:
  - **Alle Felder** prüfen (Name 1–3, Straße, Mail, Kontakt), normalisiert (Punkte, Groß/klein, Leerzeichen).
  - Mail-Postfach wie info@, buero@, rechnung@, verwaltung@, kontakt@.
  - Firmen-Mail-Domain.
  - Adresse in einem Gewerbegebiet bzw. mit Zusatz wie „Halle“ oder „Werk“.
  - Festnetz-Zentrale (Endung -0).
  - Gleiche Adresse, Telefon oder Domain wie ein bestehender Firmenkunde.
  - Dublette eines bestehenden Kunden.
- **KI-Einschätzung als zusätzliches Signal:**
  - Ein Sprachmodell bewertet Name, Adresse und Mail: Person oder Organisation, mit Begründung.
  - Das erkennt auch Muster, die in keiner Liste stehen, etwa „Objektbetreuung Bruhn“ oder „Seniorenpark“.
  - Vorher Datenschutz klären (Auftragsverarbeitung, EU-Region). Nur als Signal, nie als alleinige Sperre.
- Listen und Gewichte liegen in Mongo und sind ohne Deploy anpassbar.

### C. Nach der Anlage: Verhalten prüfen (lässt sich bei der Eingabe nicht vortäuschen)
- Ein Job prüft regelmäßig **alle** Privatkunden, auch alte und die in SAP angelegten oder umgestellten, und meldet Auffälligkeiten als Sync-Problem bzw. auf der Dashboard-Kachel der Buchhaltung:
  - **Bestellverhalten aus esERP/SAP:** viele Aufträge, hohe Umsätze, typische Profi-Mengen (z. B. 20 Türschließer), Lieferung an wechselnde Baustellen.
  - **Zahlungseingang:** Zahlt eine Firma (Kontoinhaber „… GmbH“) für einen Privatkunden, ist das ein starkes Signal. Diese Prüfung braucht die Buchhaltung bzw. SAP-FI.
  - **Neue Regeln rückwirkend:** Jede neue Regel läuft auch über den Bestand.

### D. Lernen und Verantwortung
- **Rückkopplung:** Stellt die Buchhaltung einen Kunden auf „Firma“ um, werden dessen Merkmale (Domain, Wörter, Adresse) automatisch in die Listen aus B aufgenommen.
- **Auswertung je Person bzw. Verkaufsbüro:**
  - Privatkunden-Quote und Anteil der Umstellungen.
  - Ausreißer gehen an die Teamleitung, nicht als Pranger.
  - Das ist mit Betriebsrat und Datenschutz abzustimmen, wie bei der Portal-Nutzung.
- **Alle Kundenanlagen im Portal:** Wird in SAP direkt angelegt, umgeht das alle Prüfungen. Die Anlage in SAP auf Buchhaltung und Stammdaten beschränken; Job C fängt den Rest.

## Aufwand und erwarteter Erfolg (Schätzung)

- Aufwand in Entwicklertagen (PT) inkl. Test.
- „Erfolg“ = geschätzter Anteil der Fälle, die verhindert oder gefunden werden:
  - **offensichtlich** = Fälle wie die heutigen (Firmenname, Schule, WEG)
  - **bewusst** = jemand umgeht die Regeln gezielt

| Baustein | Aufwand | offensichtlich | bewusst | Bemerkung |
|---|---|---|---|---|
| A1 Firma ohne Steuer-ID + Nachfassliste | 1–1,5 PT | – | **hoch** (Motiv entfällt) | wirkt nur, wenn die fehlende Steuer-ID wirklich der Grund ist – morgen beim Vertrieb erfragen |
| A2 Klare Kundenfrage + Vor-/Nachname | 0,5–1 PT | mittel (Grenzfälle WEG/Schule/Verein) | gering | billig, sofort |
| A3 Konditionen je Kundenart | 0,5 PT Portal | – | hoch, falls es Unterschiede gibt | vor allem eine fachliche Entscheidung |
| B1 Punktzahl-Regeln über alle Felder, Begründung, Listen in Mongo + Pflegeseite | 2–3 PT | **80–90 %** | 20–30 % | die ersten Umgehungen kommen schnell |
| B2 Abgleich Adresse/Telefon/Domain mit Firmenkunden | 2 PT (neuer sapdata-Endpunkt) | mittel | 30–40 % | findet nebenbei Dubletten |
| B3 KI-Einschätzung | 1–1,5 PT + Datenschutz | hoch | 20–30 % | erkennt keine reinen Personennamen |
| C1 Job Bestellverhalten (Aufträge, Umsatz, Mengen) | 3–5 PT | hoch | **60–80 %** der relevanten Fälle | nicht zu umgehen, greift aber erst nach den ersten Aufträgen |
| C2 Zahlungseingang („Firma zahlt für Privatkunde“) | 2–3 PT + Klärung SAP-FI-Daten | – | hoch | hängt davon ab, ob wir an die Zahlerdaten kommen |
| D1 Rückkopplung + Auswertung je Person/Büro | 1,5–2 PT | – | mittel (Abschreckung) | Betriebsrat/Datenschutz |
| Auftragssperre bei Verdacht (5A) | 1–2 PT | – | – | verhindert Schaden statt nur zu finden |
| Liste der bisherigen Fälle (einmalig) | 0,5 PT | – | – | sofort Nutzen für die Buchhaltung |

**Pakete**
- **Schnell (≈ 5–6 PT):** A1, A2, B1, Liste der bisherigen Fälle, Dashboard-Kachel.
  - Stoppt die offensichtlichen Fälle fast vollständig.
  - Ist die Steuer-ID das Motiv, sinkt der Missbrauch deutlich.
- **Vollständig (≈ 15–20 PT):** zusätzlich B2, B3, C1, C2, D1 und die Auftragssperre.
  - Fängt auch die meisten bewussten Umgehungen ab, die für die Buchhaltung relevant sind. Wer mehr als ab und zu bestellt, fällt über C auf.
- **Ehrliche Grenze:** Ein Einzelunternehmer mit eigenem Namen, privater Mail und Privatadresse ist bei der Anlage von einem Verbraucher **nicht** zu unterscheiden. Auffallen kann er erst über sein Verhalten (C) oder über die Konditionen (A3).

## Empfehlung
1. **Anreiz wegnehmen (A):**
   - Firma ohne Steuer-ID mit Nachfassen.
   - Klare Kundenfrage.
   - Konditionen für Privatkunden prüfen.
   - Das ist der dauerhafte Hebel.
2. **Bei der Anlage (B):**
   - Punktzahl über alle Felder: Rechtsform sperren, sonst Begründung.
   - Vor- und Nachname.
   - ZUGFeRD, XRechnung und Leitweg-ID nur für Firmen.
   - KI-Signal nach Datenschutzfreigabe.
3. **Laufend (C + D):**
   - Job über alle Privatkunden, mit Bestell- und Zahlungsverhalten.
   - Kachel und Liste für die Buchhaltung.
   - Umstellungen fließen in die Regeln zurück.
4. **Bei anhaltendem Missbrauch:** Auftragssperre bei Verdacht (Punkt 5, Variante A).
5. **Bestehende Fälle:**
   - Einmalige Liste aller Privatkunden mit Merkmalen, damit die Buchhaltung sie umstellt.
   - Quellen: das Änderungsprotokoll, für ältere Fälle SAP (Branche B38).

## Fragen für morgen
- Was genau geht in der Buchhaltung schief? Davon hängt ab, ob „sperren“ oder „Hinweis“ reicht.
- Wie gehen wir mit WEG/GdWE, Erbengemeinschaften, Schulen und Vereinen um? Firma, Privat oder eigene Kundengruppe?
- Darf der Vertrieb Firmen ohne Steuer-ID anlegen, wenn die Buchhaltung nachfasst?
- Wer prüft verdächtige Anlagen (Buchhaltung, Teamleitung) und wie schnell?
- Sperren, Begründung oder nur Protokoll: Wie streng soll es sein?
