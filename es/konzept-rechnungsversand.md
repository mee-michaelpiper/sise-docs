# Konzept: Rechnungsversand (ZUGFeRD, XRechnung, Post/Skorzak, Mail) im SiSe-Portal

Stand 13.10.2026 – Analyse nur lesend (Freigabe `Drives` auf clfs002, Zugang `EsErpSmbCredentials`, Lese-Werkzeug `~/repos/sap-tools/SmbRead`).

## Ablauf heute
esERP erzeugt die Rechnung → com4vision prüft/korrigiert vorab → Datei landet im Hauptordner der **Datendrehscheibe der Meesenburg KG** → die Drehscheibe validiert (valitool), konvertiert und versendet → verschiebt in Unterordner je Ergebnis (Tagesordner `yyyyMMdd`). Fehler werden nur über die Ordner sichtbar.

**Wichtig: Nie etwas in einen Hauptordner verschieben oder kopieren – die Drehscheibe versendet es dann erneut.** Das Portal liest nur.

## Ordner und Dateien
| Kanal | Pfad (Share `Drives`) | Unterordner | Dateien |
|---|---|---|---|
| XRechnung | `Daten\Kunden\XRechnung_ES2000` | Backup, Bypass, Duplikate, Konvertierung_erfolgreich, **Konvertierung_fehlerhaft**, Versand_erfolgreich, **Versand_fehlerhaft** | `Rechnung900010205.xml`, `…xml.exit26.report.de.xml` (valitool), `…_error.txt` |
| ZUGFeRD | `Daten\Kunden\ZUGFeRD_ES2000` | Backup, Konvertierung_erfolgreich, **Konvertierung_fehlerhaft**, Versand_erfolgreich, **Versand_fehlerhaft** | `Rechnung 900010272.pdf`, `…pdf.exit20.report.de.xml`, `Rechnung 900010272.txt` (Mail: to, subject, receivername, invoicenumber) |
| Post (Skorzak) | `Daten\Dienstleister\Skorzak\Rechnungen_ES2000` | Backup, Konvertierung_erfolgreich, **Konvertierung_fehlerhaft**, Upload_erfolgreich, **Upload_fehlerhaft** | `DE_<PLZ>_298<Kundennr>_002_E000083.pdf`, täglich 06:00 `Meesenburg_Es2000_<Datum>.txt` (Liste der hochgeladenen PDFs) |

- Fehlerbericht valitool (`vr:validationReport`): `isValid`, `exitCode`, je Befund `errorCode` (z. B. BR-DE-06, BR-CO-25, PEPPOL-EN16931-R010), `errorDescription`, `errorType` (Fehler/Warnung), betroffene BT, XPath, Zeile.
- Gefundene Fehler (Beispiele): 900010205/900010026 XRechnung (BR-DE-06 Telefon Verkäufer-Kontakt fehlt – esERP setzt als Kontakt nur `automation-ol@meesenburg.de`; PEPPOL-R010 E-Mail des Käufers fehlt), 900010091 ZUGFeRD (BR-CO-25 Fälligkeit/Zahlungsbedingung fehlt), Skorzak Upload_fehlerhaft 06.10./14.09.
- Auffällig: XRechnung `Versand_erfolgreich` und `Backup` sind leer – wohin gehen erfolgreiche XRechnungen? (klären)
- Skorzak-Dateinamen enthalten Kundennummer, aber **keine Rechnungsnummer** → aus dem PDF-Text lesen oder über esERP (`versandlaufnr`) zuordnen.

## Zuordnung zum Sachbearbeiter
esERP `GET /debitorenrechnungen/{rechnungnr}`: `personalnr`, `persnrvk` (Verkäufer), `aenderpersnr`, `versandartcode`, `versendet`, `versandlaufnr`, `dokumentStatus`, `/erechnung/{nr}` (Leitweg-ID, E-Mail). Personalnummer → Mitarbeiter (Abgleich aus SAP, mit E-Mail) → angemeldete Person im Portal.

## Vorschlag
1. **Worker-Job „Rechnungsversand“** (alle 10 Min., nur lesend): Ordner scannen, Berichte auswerten, je Rechnung Status in Mongo (`InvoiceDispatch`: Nummer, Kanal, Status eingegangen/fehlerhaft/versendet/hochgeladen, Zeitpunkte, Empfänger, Befunde, Dateipfade), mit esERP-Daten (Sachbearbeiter, Kunde, Versandart) anreichern. Hängend = im Hauptordner älter als X Min. oder in *_fehlerhaft.
2. **Mail-Kanal** (`eserp_mail@meesenburg.de`, Send-As): Gesendete Elemente per Graph lesen (Postfach online? App-Berechtigung nur für dieses Postfach per Application Access Policy) oder Exchange-Nachrichtenablaufverfolgung.
3. **Portal-Seite „Rechnungsversand“**: „Meine Rechnungen“ (eigene, über Personalnummer) mit Status je Kanal und Zeitstempel; mit Berechtigung alle; Filter hängend/fehlerhaft. Detail mit Befunden in Klartext und **Vermutung zur Ursache** (Regelcode → typische Ursache, z. B. BR-CO-25 → Zahlungsbedingung fehlt am Kunden; R010 → E-Mail/Leitweg-ID am Kunden fehlt; BR-DE-06 → Exportfehler com4vision).
4. **„An com4vision melden“**: Mail mit Rechnungsnummer, Kanal, Datei, Befunden, Vermutung, Bericht im Anhang; im Änderungsprotokoll vermerkt.
5. **Benachrichtigung** des Sachbearbeiters bei Fehler (Portal-Hinweis, optional Mail) und „Versendet“-Rückmeldung.

## Entscheidungen (13.10.2026)
- Bisher gab es noch keine erfolgreiche XRechnung – „versendet“ für XRechnung ergänzen, sobald die erste durch ist.
- Skorzak: Rechnungsnummer aus dem PDF lesen (Dateiname muss für Skorzak so bleiben).
- Mail: Graph-App `Meesenburg.API.Graph` (8a27b169-806f-4baf-a8a2-ab8415b611d0) hat bereits Mail.ReadWrite; ob eine Postfach-Beschränkung greift, zeigt `GET /graph/mail/dispatch/check`.
- Erst nur die Portal-Seite; Meldung an com4vision per vorbereiteter Mail.

## Abgleich fehlerhaft ↔ erfolgreich (13.10.2026)
| Rechnung | Kanal | Fehler | Später erfolgreich? |
|---|---|---|---|
| 900010026 | XRechnung | Konvertierung 18.09. (Exit 26) | nein |
| 900010156 | XRechnung | Konvertierung 01.10. (Exit 26) | nein |
| 900010205 | XRechnung | Konvertierung 06.10. (BR-DE-06, PEPPOL-R010) | nein |
| 900010091 | ZUGFeRD | Konvertierung 24.09. (BR-CO-25) | nein |
| 900010041, 043, 044 | Post | Konvertierung 19.09. | ja, Upload 23.09. |
| 900010012 | Post | Upload 18.09. | per ZUGFeRD schon am 25.08. versendet |
| 900010191 | Post | Upload 07.10. | nein |

## Umsetzung
- Worker-Job `InvoiceDispatch` (Contracts 1.0.15): `InvoiceShareReader` (nur lesend, keine Schreibmethode), `InvoiceReportParser` (valitool, Vermutungen je Regelcode), `InvoicePdfReader` (PdfPig: Belegart+Nummer vor „Seite“, Sachbearbeiter-Mail, Kunden-Nr.), Mongo `InvoiceDispatch` + Zwischenspeicher `InvoiceDispatch-Files`; Mails über Graph `/graph/mail/dispatch/sent` (erster Lauf ab Livegang 01.09.2026, danach ab letztem Versand − 1 Tag); Sachbearbeiter ohne PDF über esdata `/es/accounting/outgoing/debitor/{nr}` → Personalnummer → SAP-Mitarbeiter.
- ApiService `/apisvc/invoice-dispatch` (eigene per E-Mail, alle mit `invoices.dispatch.all`), `POST /{id}/reported` (+ Änderungsprotokoll).
- Portal `/eserp/rechnungsversand`.
- Offen: Zeitplan alle 10 Min. anlegen; Prüfbericht als Datei an com4vision (heute Pfad + Befunde im Mailtext); Benachrichtigung per Mail (später).

