# 18 – Konfiguratoren: Spezifikation für den .NET-Shop

Stand: 27.10.2026 · Quellen: JTL-Shop 5.5.2 (Kerncode und Plugin `sd_schliessanlagen_konfigurator`, nur lesend), Wawi-Datenbank (nur lesend), Stripe-Shop-Quellcode (`webshop/`, Stand 07.10.2026), Trello-Board „ICS-Meesenburg“ (16 Konfigurator-Karten). Begriffe: **JTL-Shop** (Referenz), **Stripe-Shop** (Python/Svelte, offline), **.NET-Shop** (Migration).

Es gibt zwei Konfiguratoren:

| | Kleiner Konfigurator | Großer Konfigurator |
|---|---|---|
| Was | JTL-Standard „Konfigurationsgruppen“ am Artikel: Zylinderlänge, Schlüssel, Schließfunktion, Farbkappen … | Schließanlage: Türen × Schlüssel, Schließplan (Matrix), Anlagentyp B–F |
| JTL-Shop | Kerncode `includes/src/Extensions/Config/*` | Plugin `sd_schliessanlagen_konfigurator` 1.1.0 (Star Designz, 2017, AngularJS) |
| Umfang | 464 Gruppen im Shop, 1.965 gültige Items, ≈ 3.700 Artikel für Endkunden | 12 aktive Systeme (Kategorie 4 „Schließanlagen“), 1.525 Bestellungen seit 2017 |
| Plan | S9a ✅ gebaut 27.10.2026 | POC ✅ gebaut 27.10.2026 (Vorstellung 28.10.), danach Phase 2 |

---

## A. Kleiner Konfigurator

### A.1 Daten (Wawi, dbo)

| Tabelle | Inhalt |
|---|---|
| `tkonfiggruppe` | `kKonfiggruppe` (= Id im JTL-Shop), `nMin`/`nMax` Anzahl gewählter Items (0 = ohne Grenze), `nTyp` 0 Checkbox · 1 Radio · 2 Dropdown · 3 Mehrfach, `nSort`, `bild` |
| `tkonfiggruppesprache` | `cName`, `cBeschreibung` (kSprache 1) |
| `tkonfigshop` | Gruppe gilt nur mit Zeile `kShop = 5` (464 Gruppen) |
| `tartikelkonfiggruppe` | Hauptartikel → Gruppen, `nSort` = Reihenfolge am Artikel |
| `tkonfigitem` | `kArtikel`, `bSelektiert` (Vorauswahl), `bEmpfohlen`, `bName` (Name vom Artikel), `bPreis` (Artikelpreis übernehmen, heute alle 1), `bRabatt`/`bZuschlag` (nur Anzeige), `fMin`/`fMax`/`fInitial` (Stück je Hauptartikel), `bIgnoreMultiplier` (heute alle 0), `nSort` |
| `tkonfigitemsprache` | eigener Name (nur bei `bName = 0`) |
| `tkonfigitempreis` | je Kundengruppe und Shop (5 vor 0): `nTyp` 0 = Betrag netto, 1 = Prozent; `fPreis` |
| Auftrag | `Verkauf.tAuftragPosition.kKonfigVater`/`kKonfigItem` (Vater-Kind) |

Regeln: Item nur, wenn sein Artikel in Shop 5 aktiv ist; **Sichtbarkeit je Kundengruppe gilt für Items nicht** (1.510 Items haben ausgeblendete Artikel). Gruppe ohne gültiges Item wird nicht gezeigt. Abfragen (getestet, deckungsgleich mit dem JTL-Shop) siehe `JtlSqlQueries` (Konfigurator).

### A.2 Preisregel (JTL-Shop `Item.php:337-367`)

- Item mit `bPreis`: Nettopreis des Item-Artikels für die Kundengruppe **wie für den Artikel selbst** (Kundengruppenpreis, Sonderpreis, höchster Kategorie-Rabatt; **ohne Staffel**), dazu `fPreis` (nTyp 0: + Betrag, nTyp 1: × (100 + %)/100). Ohne `bPreis`: `fPreis` fest, ohne Zeile 0 €.
- Hauptartikel: normaler Preis mit Staffel nach Menge.
- **Anzeige auf der Artikelseite** (1 Stück): round2(brutto Haupt) + Σ round4(brutto Item) × Itemmenge.
- **Warenkorb/Bestellung (maßgeblich):** Positionen netto × Menge, Summe round2(Σ netto × Menge × 1,19). Kindmenge = Itemmenge × Hauptmenge (außer `bIgnoreMultiplier`).

### A.3 Prüfungen (strenger als der JTL-Shop)

Item gehört zu einer Gruppe des Hauptartikels (JTL-Shop prüft das **nicht**); Anzahl je Gruppe in [nMin, nMax], Radio/Dropdown höchstens 1; Menge in [fMin, fMax]; Summe ≥ 0; Bestand je Item-Artikel; kein Umgehungsschalter (`konfig_ignore_limits`). Pflichtgruppe mit zu wenigen wählbaren Items sperrt den Hauptartikel.

### A.4 Warenkorb, Bestellung, Wawi

Vater + Kindpositionen mit gemeinsamer Konfigurations-Id, nie zusammengelegt, Kinder nicht einzeln löschbar, Menge nur über „Konfiguration bearbeiten“ (Trello #12). REST v1.1 kennt **keinen Vaterbezug** → Hauptartikel, direkt danach die Kinder als eigene Positionen (auch 0-€-Items), `Notice` „Konfiguration K1 zu {ArtNr}: {Gruppe}“, Bezug zusätzlich in der Auftragsnotiz (Trello #80: Reihenfolge je Zylinder). Ab Wawi 2.1: `parentSalesOrderLineItemId`, `configurationItemType`.

### A.5 Testfälle (Endkunde, gegen den JTL-Shop geprüft)

| Artikel | Auswahl | Soll |
|---|---|---|
| 30558 Keso 8000 Ω² DZ 81.A15 | A40, I35, nickelmatt, ohne Prio, Standardnocke, Einzelschließend, 3 × Langschlüssel, ohne | Anzeige 100,33 €; Warenkorb Menge 1 = 100,33 €, Menge 2 = 200,66 €, Menge 10 = 1.003,32 € |
| 30558, Menge 3 | A80, I45, Prio, golddecor ×2, Biffar, Pflegespray, Gleichschließung, 3987 × 2, 4000 × 1 | Anzeige 233,52 €; Warenkorb 700,56 € |
| 71904 (Gruppe 1299 Farbkappen) | Menge 2, Kappen 2836 + 2847 | Anzeige 59,18 €; Warenkorb 118,35 € |
| Prozentpreis | Item 700, A55 26,2773 × 0,65 | 17,080245 netto |

---

## B. Großer Konfigurator (Schließanlagen)

### B.1 Ablauf im JTL-Shop-Plugin

1. System wählen (Unterkategorie von Kategorie 4).
2. Zylinder: je Zeile **eine Tür**: Zylinderartikel, Türbezeichnung (Pflicht), Außen-/Innenlänge, Optionen.
3. Schlüssel: Artikel, Farbe, Bezeichnung, **Menge**.
4. Matrix Tür × Schlüssel; der Server bestimmt den Anlagentyp und den Preis.
5. Angebot als PDF (Pflicht vor dem Warenkorb), Teilen-Link, in den Warenkorb.

### B.2 Daten: nur JTL-Standard

- System = Kategorie unter „Schließanlagen“ (4) mit Unterkategorien „Zylinder“ und „Schlüssel“. Aktiv: Keso 8000 Ω², Keso 6000FP, Kaba penta, Kaba quattro, dormakaba pExtra Q, BKS helius, BKS janus, Abus Bravus 2000/3000/3500/4000, Bravus Kombi.
- Längen, Optionen, Anlagen-Code, Farbe sind **normale Variationen** (Namen per Plugin-Einstellung): `Außenlänge`, `Innenlänge`, `Varianten` (Werte `code_d`/`code_e`/`code_f` = Anlagenpreis, ein **Abschlag** gegenüber dem Serienpreis), Optionen `Prioritätsfunktion`, `Aufbohrschutz`, `Opt1…Opt6`, Freifelder `input` (Tür/Schlüssel) und `Matrix`, `Schlüsselfarbe`, `General key` (GHS/HS/Z).

### B.3 Preis

Das Plugin rechnet mit dem normalen JTL-Warenkorb: je Tür Grundpreis + Außen- + Innenlänge + Optionen + Code-Wert (nur bei D/E/F); Schlüssel Grundpreis × Menge + Farbe. Kein Zuschlag für den Schließplan.

Beispiel Abus Bravus 2000 (Typ E): Haustür 40/40, Wohnung 1 und 2 je 30/35; Verwalter 1× (alle), W1-Schlüssel 2× (Haustür + W1), W2-Schlüssel 2× (Haustür + W2) → **288,72 €**. Gleichschließung (B) mit 2 Doppelzylindern 30/30 und 3 Schlüsseln → **210,06 €**.

### B.4 Anlagentyp – JTL-Plugin und Stripe-Shop im Vergleich

| Fall | JTL-Plugin (`Calculator.php`) | Stripe-Shop (`determineLockPlanType.ts`) |
|---|---|---|
| Jeder Schlüssel schließt jede Tür | B | B |
| Jede Tür genau ein Schlüssel, kein Zentralschloss | C (auch wenn es mehr Schlüssel als Türen gibt) | C nur bei 1:1 (gleiche Anzahl) |
| Haustür für alle + je Wohnung eigener Schlüssel + Verwalter | E | E |
| **Chefschlüssel für alle Türen, je Mitarbeiter eine Tür, keine Zentraltür** | **F** (Code-Kommentar „TODO: What does happen if central lock not available?“) | **E** – fachlich plausibler |
| Gleiche Schlüsselspalten | zählen einmal | einzeln |

Der .NET-Shop rechnet nach dem JTL-Plugin (der Preis hängt am Typ: Code D/E/F) und zeigt die Stripe-Abweichung an → **Fachworkshop entscheidet**.

### B.5 Fehler, die wir nicht übernehmen

**JTL-Plugin:** Matrix-Datei abgeschnitten bei 10/20 Schlüsseln (`count(keys) % 10`); Dateiname = Unix-Zeit → zwei Kunden in derselben Sekunde überschreiben sich, URLs erratbar, mit Tür- und Schlüsselnamen (DSGVO); Teilen-Link ebenso erratbar; Typ F als Auffangklasse.
**Stripe-Shop (Bericht 02):** Preis im Browser, gleiche Zylinder zusammengelegt (Optionen nur einmal berechnet), Matrix erreicht die Bestellung nicht, Code per Regex auf den Namen, Mock-Daten, Konfigurator-Endpunkte ohne Anmeldung.

### B.6 Trello (Abnahmekriterien)

| Karte | Anforderung | .NET-Shop |
|---|---|---|
| #84, #61, #62 | Reihenfolge der Zylindertypen wie im JTL-Shop (Doppel → Halb → Knauf → …) | Sortierung nach Wawi-Sortiernummer |
| #54 | Keine Farbkappe vorauswählen | keine Vorauswahl bei mehreren Farben |
| #64 | nur eine Ausführung wählbar | Ausführung = Anlagentyp vom Server, nicht wählbar |
| #56 | Hinweis „Je Reihe ist eine Berechtigung erforderlich!“ | an der Matrix |
| #53, #87 | gleiche Preise wie JTL-Shop | Preis nur serverseitig mit der Shop-Preislogik, Golden-Tests |
| #80 | Auftrag: Längen je Zylinder zusammen | je Tür eine Position mit allen Angaben |
| #89 | gespeicherte Konfigurationen per Link öffnen | später: nicht erratbarer Link |
| #12 | Konfiguration aus dem Warenkorb bearbeiten | später |
| #3, #4, #37, #57, #78 | Marken/Systeme vollständig und richtig gefiltert | Systeme automatisch aus dem Kategoriebaum |

### B.6a Keso 8000 Omega³ (27.10.2026)

- Im Stripe-Shop kamen die Systeme aus dem Ast „Schließanlagen › mechanisch/mechatronisch/elektrisch › System“ (Regel `evaluated_data.in_configurator`), dort gab es „Keso 8000 O³“. **Dieser Ast ist heute leer** (0 Artikel) – der Stripe-Konfigurator zeigt deshalb nichts mehr.
- Im JTL-Shop ist Omega³ **kein Schließanlagen-System** (nicht unter Kategorie 4, kein Plugin-Eintrag, kein Anlagenpreis code_d/e/f). Die 50 Omega³-Artikel liegen unter „Schließzylinder Systeme › KESO 8000 Omega³“ (Zylinder) und „Schlüssel Keso 8000 O³“ und werden über den **kleinen Konfigurator** eingestellt (Länge außen/innen, Färbung, Nocke, Schlüssel, Zubehör).
- Für den großen Konfigurator bräuchte Omega³ Türen mit Konfigurationsgruppen statt Variationen – technisch machbar (Tür = konfigurierter Zylinder), fachlich offen: Wie wird eine Omega³-Schließanlage bepreist und bestellt? → Fachfrage F-35.
- **Nachtrag (27.10.2026, abends): Es gibt eigene Anlagen-Artikel.** 30 Artikel `…_Anl_O³` (z. B. 72644 „81.A15_Anl_O³“, angelegt/geändert 05.–08.10.2026) mit eigenen Konfigurationsgruppen: Länge außen/innen (1542/1543), Färbung, Prioritätsfunktion, Nocken, **Schlüssel (1539)**, Zubehör, **„Zylinder Schließfunktion“ (1320)**. Sie sind in der Wawi aktiv, aber **keinem Shop zugeordnet** (`tArtikelShop` leer). Der Stripe-Konfigurator nahm genau diese Artikel (Ast „Schließanlagen › mechanisch“, 22 Systeme, 179 Artikel). Alle 179 sind seit dem letzten Vollabgleich aus dem Shop-Kanal 2-2-5 verschwunden (im .NET-Lesemodell am 27.10. als entfernt markiert) – vermutlich bewusst, damit der JTL-Shop sie nicht als normale Artikel zeigt. Lösungsvorschlag: eigenes **Konfigurator-Sortiment** im .NET-Shop, unabhängig vom Shop-Kanal (Artikel des Asts bzw. `_Anl_`, aktiv in der Wawi), nie in Liste/Suche, nur im großen Konfigurator; Tür = Anlagen-Artikel mit seinen Konfigurationsgruppen (kleiner Konfigurator).

### B.7 POC im .NET-Shop (28.10.2026)

Alle 12 Systeme aus dem Kategoriebaum, Zylinder/Schlüssel mit echten Preisen und Aufpreisen aus dem Lesemodell; Türen, Schlüssel, Matrix; Typ und Preis vom Server bei jeder Änderung; Abweichung Stripe-Regel sichtbar; in den Warenkorb als Plan (Positionen je Tür und Schlüssel, Menge fest, nur gemeinsam entfernbar); Schließplan an Bestellung, Mail und Wawi-Notiz.

---

## C. Großer Konfigurator nach der Stripe-Shop-Systematik (Ziel, Entscheidung m.piper 27.10.2026)

Ziel ist der Konfigurator mit den **22 Systemen des Stripe-Shops**. Die JTL-Plugin-Variante (Teil B) bleibt zum Vergleich der Logiken bestehen.

### C.1 Sortiment

- Artikel unter „Schließanlagen“ (4) › mechanisch (2446) / mechatronisch (2447) / elektronisch (2448) › System; in der Wawi aktiv, **unabhängig vom Shop-Kanal** (heute keinem Shop zugeordnet). Im .NET-Shop als „nur Konfigurator“ eingelesen (Abgleich, `ConfiguratorRootCategoryIds`), nie in Liste/Suche.
- **197 Artikel**, alle Zylinder **mit Konfigurationsgruppen**; keine eigenen Schlüsselartikel. Stripe-Fehler: prüft „elektrisch“ statt „elektronisch“ und verlangt einen aktiven Verkaufskanal.

| System (kKat) | Art. | Längen | Schlüssel | Code-Gruppe (D / E / F netto) |
|---|---|---|---|---|
| Keso 8000 O² (2449) | 35 | 14/13, HZ 38, asym 165 | 1267 | 1320: K8Z 9,64 / K8E 16,29 / K8F 22,93 (Sonderpreis 16,29) |
| Keso 8000 O³ (2531) | 30 | 1542/1543, HZ 1541, asym 1540 | 1539 (6,53–9,24 €) | 1320 |
| Keso 6000FP2 (2451) | 14 | 14/13, 38, 165 | 1419 Satz „3 Stück Standard“ + 1420 Mehrschlüssel + 1299 Farbkappen | 1320 |
| dormakaba gemini pluS, expert pluS, expert cross, quattro, penta, penta cross (2452–2457) | 9–15 | 197/208, HZ 199, kurz 1215 | 1269 SK-3 „3 inkl.“ + 1271/1315 Mehrschlüssel + Farben 1497/1270/1316 | 1504: 8,40 / 8,40 / 16,81 |
| dormakaba pextra Q (2458) | 5 | 1303/1304, HZ 1309 | 1308 + 1310 | 1506: 1,68 / 1,68 / 4,20 |
| BKS Janus, Nuvius, Helius, detec3000 (2460–2462, 2467) | 5–9 | 1426/1427, 1432 | 1372 bzw. 1508–1510 „3 × Standard inkl.“ / Mehrschlüssel | 1505: 2,94 / 2,94 / 16,39 |
| Abus Bravus 2000/3000/3500/4000 (2463–2466) | 4–5 | 1422/1423 | 1416 „gratis max. 3 je Zylinder“ + 1415 Mehrschlüssel 11,47 | 1507: 33,61 je Code |
| Wilka Carat S4 (2534) | 3 | 1548/1549 + Variation | 1550 | – (Artikel ohne Preis → ausblenden) |
| Winkhaus OVSX, Wilka S5, Simons & Voss | 0 | – | – | – |

### C.2 Regeln für den .NET-Shop

1. Zylindertyp aus dem Merkmal „Zylindertyp“, Reihenfolge nach Wawi-Sortierung (#84).
2. Optionen = Konfigurationsgruppen des Zylinders (Regeln des kleinen Konfigurators). **Rollen je Gruppen-Id in einer Tabelle** (Länge außen/innen, Code, Schlüsselwahl, Mehrschlüssel, Farbe, Schlüsselcode, Schließfunktion, Option) – nie per Name/Regex.
3. Grundpreis = Kundengruppenpreis mit Sonderpreis und höchstem Kategorie-Rabatt (**35 % auf Keso O², O³, 6000FP2**); Positionen zum Artikelpreis + `tkonfigitempreis`.
4. Anlagentyp nur auf dem Server; **Code-Position automatisch**: B → Gleichschließend, C → Einzelschließend, D/E/F → Code-Position; Menge = Zylindermenge. Fehlt die Position → Anlage ablehnen.
5. Variation „Varianten“ code d/e/f (Altdaten auf 153 Artikeln, andere Beträge) **nie zusätzlich** berechnen.
6. Schlüssel aus dem Schlüsselkatalog des Systems (Positionen der Schlüsselgruppen); Inklusiv-Satz/Mehrschlüssel per Datentabelle; Farbe ohne Vorauswahl (#54).
7. Jede Tür eigene Position mit ihren Konfig-Positionen dahinter; Schlüssel mit Anlagenbezug; Schließplan in Bestellung, Mail und Wawi-Notiz.

### C.3 Typregel – Stripe-Shop im Gegentest (fachlich laut Wawi-Codes D = Zentral, E = Haupt, F = General-Haupt)

| Schließplan | Stripe | fachlich |
|---|---|---|
| Je Wohnung eigener Schlüssel, alle schließen die Haustür | E | D |
| Hauptschlüssel für alle + 1 Einzelschlüssel | D | E |
| 2 Türen, 3 Schlüsseltypen, je Tür einer | F | C |
| Hauptschlüssel + 2 Einzelschlüssel | E | E |
| GHS mit 2 Gruppen | F | F |

### C.4 Golden-Tests (Endkunde, Wawi 27.10.2026; Summe round2(Σ netto × Menge × 1,19))

Plan: T1 Büro DZ (A40/I35), T2 Lager DZ 30/30, T3 Keller HZ 10/30; S1 Chef 2× (T1–T3), S2 Büro 3× (T1); Typ E.
- **Keso 8000 O³:** netto 246,068397 → **292,82 €** (mit 35 % Rabatt; ohne wären es netto 324,84).
- **dormakaba gemini pluS:** netto 217,452085 → **258,77 €** (Schließfunktion 203 weggelassen, SK-3 einmal als Satz).

### C.4a Umsetzung im .NET-Shop (27.10.2026)

- Rollen und Code-Artikel: `Seed/lockplan-anlagen.json` (gepflegt, Stand Wawi 27.10.2026). Neue Gruppen bitte dort eintragen.
- **Typregel für die Anlagen-Systeme = fachliche Regel** (`LockPlanRules.FachType`): B jeder Schlüssel jede Tür · C jeder Schlüssel genau eine Tür · Hauptschlüssel = schließt alle Türen · Zentraltür = von ≥ 2 Nicht-Hauptschlüsseln geschlossen · D ohne Hauptschlüssel, jeder übrige Schlüssel genau eine eigene Tür (+ Zentraltüren) · E wie D mit Hauptschlüssel · F sonst (Gruppenschlüssel). Erfüllt alle Zeilen aus C.3; JTL- und Stripe-Regel werden zum Vergleich angezeigt.
- Golden-Tests mit Echtdaten: O³ 292,81 € (Soll 292,82), gemini pluS 258,74 € (Soll 258,77) – Differenz = Rundung R-1.
- Schalter nach Entscheidung F-36 (Workshop 29.10.2026): `LockPlan:KeysInSet` (3), `LockPlan:KeyCodeSurcharge` (**an**: Schlüsselcode je Schlüssel bei D/E/F, sofern das System die Position in der Wawi hat – heute nur Keso); Schließfunktion bestimmt der Konfigurator, nicht der Kunde (Gruppe weggelassen); Code-Zuschlag je Zylinder × Menge; Typ nach fachlicher Regel, JTL-/Stripe-Typ nur intern.

### C.5 Fachfragen F-36 (entschieden 29.10.2026, siehe Bericht 16)

1. „3 Schlüssel inklusive“: je Zylinder oder je Schlüsseltyp? Mengenregel bei Schlüsseln für mehrere Türen?
2. Schlüssel-Code-Zuschlag (1554 Keso_Schl_d/e/f 1,93 €, 1321 S_Anl 1,93 €) je Schlüssel bei D/E/F berechnen?
3. Code-Zuschlag je Zylinder × Menge oder einmal?
4. Gruppe „Schließfunktion“ (Einzel-/Gleichsperrend) in der Anlage weglassen oder nach Typ setzen?
5. Typregel (C.3) festlegen.

Wawi-Datenfehler: 72049 und 72072 mit falscher Code-Gruppe 1504, 72706 ohne Zylindertyp, Wilka S4 Länge doppelt (Variation und Gruppe), O³ ohne Schlüsselcode-Gruppe.


### C.6 Schließplan speichern, Link und PDF (K-3, umgesetzt 30.10.2026)

- **Warum:** Der Stripe-Shop übertrug den Schließplan nirgendwohin; der Innendienst rief Kunden an und nahm den Plan erneut auf.
- **Speichern:** „Plan speichern und Link teilen“ im Konfigurator (auch unvollständig) → `/konfigurator/plan/{id}`; Id = 128 Bit Zufall (Trello #89, nicht erratbar). Gespeichert wird der Entwurf (Türen, Schlüssel, Berechtigungen) als JSON in `lock_plans`; Preis und Typ werden beim Öffnen neu gerechnet. Nicht bestellte Pläne verfallen nach 180 Tagen (`LockPlan:SavedPlanDays`), bestellte bleiben.
- **Warenkorb/Bestellung:** Jeder Plan im Warenkorb wird automatisch gespeichert (`ShareId` am Schnappschuss); mit der Bestellung wird er der Bestellnummer zugeordnet.
- **Mails:** Kundenmail mit Link unter der Matrix. Neue interne Mail „Schließanlage bestellt“ an `Mail:InternalRecipients` (Vorkasse sofort, Online-Zahlung nach Zahlungseingang): Kunde mit Telefon und Anmerkung, Matrix, Link, Positionen, **PDF im Anhang**.
- **PDF:** A4 quer mit Türen, Schlüsseln und Matrix (●), mehrseitig bei großen Anlagen (ab 26 Schlüsseln Spaltenblöcke); SkiaSharp, Schrift Roboto bzw. Systemschrift. Download unter `/konfigurator/plan/{id}/pdf`.
