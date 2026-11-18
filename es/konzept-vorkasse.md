# Vorkasse: Zahlungseingang erkennen, erinnern, ggf. anlegen (Entwurf 30.10.2026)

Zur Abstimmung mit SN24 (Karina Petersen) und Debitorenbuchhaltung (Tatjana Klink). Noch nichts umgesetzt.
Alle Befunde nur lesend erhoben: SAP MEP (`SapRead`), JTL-Wawi (`~/repos/tools/jtl-tools/JtlSqlRead`).

## Ziel

- Kollegen sehen ohne Suchen, dass eine Vorkasse-Zahlung angekommen ist.
- Nach x Tagen ohne Zahlung geht automatisch eine Erinnerung an den Kunden.
- Später ggf.: Vorkasse aus esERP heraus anlegen (Absprung ins Portal wie bei der Bestandsabfrage), Daten prüfen/ergänzen, Mail mit Proforma bzw. Zahlungsaufforderung an den Kunden.

## Befund SAP (Buchungskreis 298)

- **Elektronischer Kontoauszug läuft täglich** für HVB, ETR und NOSPA (`FEBKO`/`FEBEP`), der Verwendungszweck steht vollständig in `FEBRE`. Jeder Posten wird automatisch gebucht.
- **Anzahlungen im SAP (Sonderhauptbuch F/A, Belegart 96) sind nur der Berliner Barverkauf:** 2026 205 Belege, 204 mit Kopftext „K8900“ (VK-Büro 8900), gebucht von `WF-BATCH` aus SD-Aufträgen (Auftragsart ZC2) gegen „Kasse Berlin SHZ“ (112890/132890), meist auf die CPD-Konten C1000/C8900. Für esERP- oder Shop-Vorkasse nicht genutzt.
- Keine Zahlungsbedingung mit „Vorkasse“ im Text gefunden (die meisten Texte sind leer).
- Stripe zahlt gesammelt aus (Verwendungszweck nur „STRIPE“ bzw. Kennung, keine Bestellnummern) → Sammelkonto C0011 „Paypal – Schleswig“. Stripe-Shop hat keine Vorkasse (nur PayPal, Karte & Co.), daher nicht relevant.

## Befund JTL-Wawi (SN24-Shop)

Vorkasse = Zahlungsart 23. Aufträge 2026 nach Präfix der Auftragsnummer:

| Präfix | Herkunft | Anzahl | Stand |
|---|---|---|---|
| 26… | Shop-Bestellungen (JTL-Shop `kShop` 5: 81, REST/Stripe-Shop: 274) | 357 | 27 bezahlt, 330 storniert, 0 offen; bezahlt im Schnitt nach 3 Tagen, höchstens 21 |
| 10… | in der Wawi angelegt | 184 | fast alle mit Rechnung und Zahlung |
| 21… | in der Wawi angelegt | 679 | 673 „offen“ (≈ 269.000 €), aber alle ausgeliefert, ohne Rechnung in der Wawi – vermutlich keine echte Vorkasse |

- Die 270 stornierten REST-Aufträge: Der Stripe-Shop legt offenbar erst als „Vorkasse“ an und storniert, wenn nicht bezahlt wird (Vermutung).
- **Zahlung wird in der Wawi von Hand gebucht**, Zahlungsart „Nord Ostsee Sparkasse – ohne Abzug“: Karina Petersen 157 von 185 Zahlungen (Login `kp`, dazu 3 über den Sammel-Login `sn`, info@sn-24.de, Abteilung Verkauf), außerdem Dominik Gaschka 19, Günther Manfrass 3, Andre Röhl 3.
- Der Zahlungsabgleich der Wawi (`tZahlungsabgleichUmsatz`) wird 2026 nicht genutzt.
- Die Wawi-REST-API kann keine Zahlungen buchen (SiseShop.POC J-09). Direkt in `tZahlung` schreiben hat die alte Custom-API gemacht – sicherheitskritisch (J-18), nicht nachbauen.

## Befund: Weg einer Shop-Zahlung (Stichprobe 5 Bestellungen)

1. Der Kunde überweist auf die **NOSPA**, die Bestellnummer steht im Verwendungszweck („Bestellung 264955“, „#264981“). Alle 5 Stichproben im SAP-Kontoauszug gefunden, Betrag und Datum passen zur Wawi (eine weicht ab, vermutlich Versand oder Sammelzahlung).
2. Die automatische Kontoauszugsbuchung legt sie auf **80060 „Ungeklärte Zahlungseingänge“** (Buchungstext: Zahler + Bestellnummer).
3. **Tatjana Klink** (Debitorenbuchhaltung, `KLINKTA`) gleicht per FB05 auf das Kundenkonto um. Die Kundenkonten (z. B. 10100288/91/93) wurden am 13.10. von `WF-BATCH` angelegt → die Shop-Kunden kommen offenbar nachträglich über esERP ins SAP; erst danach kann sie zuordnen (Umbuchung am 27.10. für Zahlungen vom 05./08.10.).
4. Parallel bucht Karina Petersen die Zahlung in der Wawi.

→ Dieselbe Zahlung wird zweimal von Hand bearbeitet, erkannt nur über die Bestellnummer. Stand 30.10.: 264981 (12.10.) und 264955 (28.10.) noch auf 80060.

## Vorschlag

**Variante B (empfohlen, ohne SAP-Änderung):**

1. **Vorgänge:** Offene Vorkasse-Nummern sammeln – 26er-Aufträge aus der Wawi (lesend, `Verkauf.tAuftrag` + `tAuftragEckdaten`) und eigene Vorgänge aus dem Portal (Referenz z. B. `VK-2026-0123`, später mit esERP-Absprung).
2. **Abgleich-Job (Worker, täglich nach Kontoauszugsimport):** neue NOSPA-Posten (`FEBEP`/`FEBRE`) lesen, Bestellnummer/Referenz im Verwendungszweck suchen, Betrag prüfen.
3. **Treffer:** Meldung an den zuständigen Kollegen (Mail bzw. Liste im Portal: „Zahlung zu 264955 über 234,05 € am 28.10. eingegangen“). Die Buchung in der Wawi macht weiter der Kollege.
4. **Für die Debitorenbuchhaltung:** Liste der Posten auf 80060 mit Vorschlag (Bestellnummer → Wawi-Auftrag → esERP-/SAP-Kunde, sobald angelegt). Buchen bleibt bei ihr.
5. **Erinnerung:** nach x Tagen ohne Zahlung Mail an den Kunden (Graph), nach y Tagen Meldung an den Kollegen bzw. Storno-Hinweis.

Grenze: Meldung erst nach dem Kontoauszugsimport, also frühestens am nächsten Werktag.

**Variante A (Anzahlungsanforderung im SAP):** nicht empfohlen. Der SAP-Weg hängt an SD-Aufträgen und der Kasse Berlin; für esERP-/Shop-Aufträge bräuchte es eigenes ABAP (Merkposten gehen nicht über `BAPI_ACC_DOCUMENT_POST`) und die Fibu müsste zusätzlich ausgleichen. Nur, wenn die Fibu die Vorgänge ausdrücklich im SAP sehen will.

Suche (Portal): Vorkasse-Vorgänge als eigener Suchtyp einplanen (Referenz, Bestellnummer, Kunde).

## Offene Fragen

**An Karina Petersen (SN24):**
1. Was sind die 673 offenen 21er-Aufträge mit Zahlungsart Vorkasse, und wo wird deren Zahlung geprüft?
2. Bleibt es bei Überweisung auf die NOSPA mit Bestellnummer im Verwendungszweck? Gibt es Vorkasse auch auf andere Konten?
3. Wie prüfst du heute, ob eine Zahlung da ist (Online-Banking?), und wie oft musst du nachhaken bzw. stornieren?
4. Nach wie vielen Tagen soll erinnert, nach wie vielen storniert werden? Wer bekommt die Meldungen?
5. Gehört die Pflege der Zahlungen in der Wawi zu SN24 oder zur Buchhaltung?

**An Tatjana Klink (Debitorenbuchhaltung):**
6. Wann und wie kommen die Shop-Kunden ins SAP (über esERP)? Wie lange liegen Shop-Zahlungen typischerweise auf 80060?
7. Würde dir eine Zuordnungsliste für 80060 (Zahlung → Bestellung → Kunde) helfen? Welche Angaben brauchst du?
8. Soll die Fibu Vorkasse-Vorgänge im SAP sehen (Variante A) oder reicht das Portal?

**Fachlich/esERP:**
9. Wie läuft Vorkasse heute bei esERP-Aufträgen (wer, welcher Beleg, Zahlungsbedingung)? Wie viele Fälle pro Monat?
10. Proforma-Rechnung aus dem Portal oder aus esERP drucken?
11. Welche Daten gibt ein esERP-Absprung ins Portal mit (Auftrag, Kunde, Betrag)? → com4vision
12. Bankverbindung und Texte der Vorkasse-Mails (deckt sich mit SiseShop.POC F-21/F-33 für den .NET-Shop – gemeinsamer Baustein).
