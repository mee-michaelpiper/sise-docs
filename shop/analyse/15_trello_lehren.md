# 15 – Lehren aus dem Trello-Board „ICS-Meesenburg“

> **Stand:** 10.10.2026 · **Quelle:** Trello-Board `7iXabNjr` (gelesen über das Konto von M. Piper, nur lesend): 94 Karten (75 abgeschlossen, 8 in Bearbeitung, 11 offen), 32 Kommentare, Zeitraum 21.01.–08.10.2026. Keine archivierten Karten.
> **Zweck:** Wiederkehrende Fehler des Stripe-Shops erkennen und sicherstellen, dass das neue Backend sie nicht wiederholt. Jede Lehre ist einem Schritt aus Bericht 14 bzw. einem Test zugeordnet.

## 1. Überblick

| Label | Karten | | Label | Karten |
|---|---|---|---|---|
| CMS/Strapi | 54 | | Hoch | 28 |
| Artikel-Daten | 26 | | Mittel | 27 |
| WaWi | 17 | | Niedrig | 29 |
| Konfigurator | 16 | | Kritisch | 6 |
| Checkout | 10 | | | |

**Prozess-Befund:** 13 Karten wurden vom Fachbereich **erneut eingestellt** („Zur Vollständigkeit füge ich noch einmal folgende Aufgabe hinzu“, „nach wie vor“, „erneut“): #17, #31, #33, #34, #35, #36, #38, #42, #44, #49, #83, #87, #90. Aufgaben galten als erledigt und waren es nicht – ohne Tests und ohne Abnahmekriterien nicht erkennbar. Mehrere Karten liegen seit Januar/Februar offen (#10, #15, #28, #31, #35).

## 2. Wiederkehrende Fehlermuster und was das neue Backend daraus macht

### 2.1 Preise an mehreren Stellen berechnet → widersprüchliche Preise

| Karte | Problem im Stripe-Shop |
|---|---|
| #53 (kritisch) | Gleich konfigurierte Zylinder/Schlüssel zeigen unterschiedliche Preise (alter/neuer Konfigurator) |
| #10 (seit 22.01. offen) | Aktionspreis in der Liste sichtbar, auf der Produktseite nicht |
| #91 | Menge des Gratisartikels frei änderbar |
| #67 | Gutscheine nur über Stripe statt im Checkout |

**Ursache:** Preislogik verteilt auf Frontend, Backend und JTL (Bericht 02: 7 Preisberechnungen). **Neu:** genau eine Preisberechnung (`CartPricing`, Katalog-API rechnet mit denselben Daten), Tests. Sonderpreise (#10) kommen mit E-J in dieselbe Stelle – dann zwangsläufig überall gleich. Gratisartikel und Gutscheine als serverseitige Regeln (S6, neu aufgenommen).

### 2.2 Versand: Daten aus der Wawi kommen nicht oder falsch an

| Karte | Problem im Stripe-Shop |
|---|---|
| #70 (kritisch) | **Versandart wird nicht an die Wawi übermittelt**, nur die Kosten → Auftrag „fehlerhaft“, nicht bearbeitbar |
| #63 | Versandklassen in der Wawi richtig, im Shop falsche Versandarten (Metabo-Säge → UPS; KFV-Mehrfachverriegelung nur Spedition) |
| #90 (erneut eingestellt) | EU (AT, NL) und CH ab 99 € versandkostenfrei, obwohl die Wawi es anders regelt |
| #92 | DHL-Preis in der Wawi auf 8,50 € erhöht – musste im Shop nachgezogen werden |
| #85 (offen) | Abholung vorausgewählt statt DHL |

**Erkenntnis für E-H:** Die Versandkosten (je Land, Freigrenze) **pflegt der Fachbereich in der JTL-Wawi**. Eine zweite Pflegestelle im Shop (heutiger Stand S5) wiederholt das Muster von #90/#92: Der Shop weicht von der Wawi ab. → **E-H neu bewerten** (Abschnitt 3).
**Bereits umgesetzt:** Versandart nur, wenn alle Artikel eine erlaubte Versandklasse haben (#63, Test `Mixed_shipping_classes_offer_only_common_methods`); nur Lieferland DE (#90, E-F); Abholung nie vorausgewählt (#85, Test `Pickup_is_offered_but_never_preselected`); Versandart-Id = JTL-Versandart, wird an den Auftrag übergeben (#70 → Pflichttest in S8).

### 2.3 Auftragsübergabe an die Wawi unvollständig oder falsch

| Karte | Problem im Stripe-Shop |
|---|---|
| #69 | **Gleiche Kundennummer für verschiedene Kunden** → Kollegen tragen Kundendaten von Hand nach |
| #30 | Auftragsnummer nicht fortlaufend – falsche Firma („Sicherheitstechnik Nord oHG“) vorausgewählt |
| #38 | Testzahlung per Karte in der Wawi als „Bar“ erfasst; Storno in der Wawi nicht im Kundenkonto sichtbar |
| #95 | Bestätigungsmail sagt immer „per Rechnung eingegangen“, egal welche Zahlart |
| #80 (kritisch) | Konfigurationsartikel im Auftrag falsch dargestellt |
| #70 | Versandart fehlt im Auftrag (siehe 2.2) |

**Neu (S6–S9, Abnahmekriterien):** Kunde wird über E-Mail **und** Adresse eindeutig zugeordnet oder neu angelegt, nie über eine fremde Kundennummer; Firma 6 und eigener Nummernkreis (E-C); Zahlart aus dem tatsächlichen Stripe-Ereignis (nicht aus der Auswahl im Formular), Stripe-Referenz am Auftrag; Mailtext aus der tatsächlichen Zahlart; Versandart-Id im Auftrag; Trockenlauf zeigt den vollständigen JTL-Auftrag vor dem ersten echten Senden. Statusabgleich JTL → Kundenkonto (Storno, Versand) als eigener Punkt in S11.

### 2.4 Sortiment und Artikeldaten

| Karte | Problem im Stripe-Shop | Neu |
|---|---|---|
| #77 (kritisch) | In der Wawi deaktivierter Artikel weiter im Shop | **Korrigiert 10.10.2026:** sichtbar und bestellbar nur mit `IsActive`; Delta-Abgleich jede Minute; Test |
| #20 | Alle Varianten einzeln in der Kategorie | Nur Vaterartikel in Listen, Varianten auf der Artikelseite ✓ |
| #23, #57, #75 | Kategorien fehlen im Shop/Header | Kategoriebaum direkt aus JTL (Kanal-Aktivierung); verwaiste Kategorien auf der Prüfseite ✓ |
| #55, #68 | Kategoriepfad fehlt bzw. URL statt Name | Brotkrumen aus dem Kategoriebaum ✓ |
| #52 | JTL liefert zufällig falsche Bild-Sortiernummern | Als Datenbefund aufnehmen (gleiche Sortiernummern), Fachbereich |
| #59 | Lieferzeit fehlt auf Produktseite und im Warenkorb | Offen → S6 (Daten: Beschaffungs-/Bearbeitungszeit, Bestand) |
| #46 | Grundpreis (z. B. je 1 l) fehlt | **Pflichtangabe (PAngV)** → offen, Daten prüfen (v1: kein Grundpreis-Feld; v2: `unitPricing`) |

### 2.5 Rechtliche Pflichten im Checkout

#93 (kritisch, offen) Widerrufsbutton; #39 Bestätigung AGB/Widerruf; #46 Grundpreis; #44 Tracking/Consent. **Neu:** als Abnahmekriterien in S6 (Checkout) und S10 (Frontend) aufgenommen.

### 2.6 Konfigurator (16 Karten, u. a. #3, #4, #25, #29, #37, #51, #54, #61, #62, #64, #78, #84, #87, #88, #89)

Falsche Vorauswahl, Reihenfolge, Farbkappen, fehlende Zylinder, Preislogik (#53, #87). → Phase 2 (Fachworkshop, Regeln serverseitig, Tests je Regel). Die Karten sind Testfälle für den Workshop.

### 2.7 Texte, Übersetzung, CMS (54 Karten)

Viele englische Reste („Login successful“, „Items“, „+7 more“, „Default“), Footer-/Strapi-Rechte (#34, #83 erneut eingeschränkt). → Teil der Frontend-Entscheidung E-M; bei Blazor durchgängig deutsch, Inhalte pflegbar (Strapi oder Admin-CMS).

## 3. Konsequenzen für den Plan (Bericht 14)

| # | Konsequenz |
|---|---|
| 1 | **E-H neu bewerten:** Die Wawi ist die Quelle der Versandkosten (#90, #92). REST liefert sie in keiner Version. Empfehlung jetzt: **Versandkosten lesend aus der Wawi-Datenbank** (eigene, dokumentierte SQL-Abfrage, nur nach Prüfung und OK) statt Pflege im Shop. Bis dahin bleiben die Shop-Regeln mit Startwerten und nur DE |
| 2 | S8-Abnahme ergänzt: Versandart im Auftrag (#70), Kundenzuordnung ohne fremde Nummer (#69), Firma/Nummernkreis (#30), Zahlart aus Stripe (#38) |
| 3 | S9: Mailtexte nach tatsächlicher Zahlart (#95) |
| 4 | S6: Lieferzeit (#59), Grundpreis (#46), Pflichtfelder Checkout (#39, #93), Gratisartikel-Regel (#91), Gutscheine (#67, Fachentscheidung) |
| 5 | **Jede Fachbereichs-Meldung wird ein Test**, erst dann gilt sie als erledigt – gegen das Muster „erneut eingestellt“ |
| 6 | #47 (Wawi-Update): Damals ohne klaren Nutzen verworfen. Heute belegt: Wawi ≥ 2.1 bringt Variationsaufpreise und Konfig-Gruppen per REST (E-I) |
