# Buchhaltungsübergabe 18.11.2026: Abendexport abgebrochen

Stand 19.11.2026, nur gelesen (SAP MEP per SapRead, Freigabe per SmbRead, Mongo `Prod-ES-Logs`/`Prod-ES-Worker`).

## Was passiert ist

| Zeit (MEZ) | Ereignis | Beleg |
|---|---|---|
| 18.11. 19:30:05 | Abendexport startet (Accounting-Job `46d4b712…`), 38 Materialbewegungen gelesen | Log `es-dataworker` |
| 19:30:06–19:30:21 | 81 Rechnungs-PDFs in den Eingang kopiert (46 ER, 35 AR + 1) | 81× „Rechnungskopie …“ im Log |
| 19:30:21 | **Abbruch: `System.Exception: Not enough credits`** (SMBLibrary `SMB2Client.TrySendCommand`) in `AccountingService.WriteFile` ← `CopyInvoiceDocumentsAsync` (Zeile 491) | Log, Stacktrace |
| – | Keine CSV geschrieben, kein Eintrag in `Accounting-Exports`, **kein Übergabe-Kennzeichen in esERP gesetzt** (passiert erst nach der CSV) | Code `ProcessInvoicesAsync`, Mongo |
| 20:00:52 | SAP-Job `YFI_ES_ERP_BUCHUNGEN` läuft leer (keine CSV im Eingang, kein Spool) | Jobprotokoll 20005001 |
| 20:10 | `YFI_ES_ERP_ARCHIVIERUNG` findet zu den 82 PDFs keine Buchung → alle nach `fehlerhaft` („Datei konnte nicht verarbeitet werden“) | Spool 20105000 (82 Zeilen) |

## Folgen

- **Nichts gebucht, nichts verloren:** Die Rechnungen sind in esERP weiter offen und kommen beim nächsten Abendexport wieder. Das Materialfenster schließt an den letzten Export (17.11. 19:30) an, keine Lücke.
- In `fehlerhaft` liegen 82 PDFs vom 18.11. – Doppel, der nächste Export kopiert sie neu in den Eingang. Später aufräumen.
- **Schon einmal am 08.11.** derselbe Abbruch („Not enough credits“); der 09.11. hat ihn mit 70 Rechnungen aufgeholt. Größter erfolgreicher Lauf: 16.11. mit 76 Rechnungen – an der Menge liegt es nicht (Ursache unten).
- **Heute Abend (19.11.) bricht es sicher wieder ab**, solange AR 900010890 (PDF > 1 MiB) dabei ist – jeden Abend, bis der Worker korrigiert ist.
- **ER 910010126** steht wieder offen in esERP, war aber schon in `SAP_Export_20261014_173012.csv` und ist archiviert (`verarbeitet/I_2026_0910010126.pdf`, 14.10.). Wird sie erneut übergeben, droht die Ablehnung der ganzen Datei (doppelte Referenz). Die anderen 81 waren noch in keiner Übergabedatei (Abgleich mit allen 54 Einträgen in `Accounting-Exports`).
- Niemand wurde benachrichtigt: Der Fehler steht nur im Log (TODO „Benachrichtigungen“).

## Ursache (geklärt 19.11.2026)

**Ein Fehler in unserem Worker, nicht in der Infrastruktur.** `ESDataWorker/Helpers/SmbFileStream.Write` schreibt die ganze Datei mit **einem** `WriteFile`-Aufruf. SMB2 rechnet je 64 KiB eine „Credit“; SMBLibrary hält 16 Credits vorrätig (= 1 MiB, zugleich die maximale Schreibgröße). Ein PDF über 1 MiB braucht mehr Credits auf einmal → `Not enough credits`, bevor überhaupt etwas gesendet wird. Windows-Clients (und unsere anderen Dienste) schreiben in Blöcken – `TransferShare` (sapdata) und `InvoiceResubmitter` (ApiService) mit 60 KB, nur der Worker nicht.

Belege:
- Die abgebrochene Datei `O_2026_0900010890.pdf` liegt mit **0 Byte** in `fehlerhaft` (angelegt, Schreiben gescheitert).
- Von 1.389 PDFs in `verarbeitet` ist **keines größer als 1 MiB** (größtes 929.847 Byte) – größere kamen nie durch.
- SMBLibrary-Quelle `SMB2Client.TrySendCommand`: wirft „Not enough credits“, wenn `CreditCharge` (= ⌈Länge/64 KiB⌉) die verfügbaren Credits übersteigt; `DesiredCredits = 16`.

Folge: **Deterministisch** – solange AR 900010890 offen ist, bricht jeder Abendexport an dieser Datei ab (heute Abend wieder). Ebenso am 08.11.

## Lösung

Gezielt statt Pakete: `SmbFileStream.Write` schreibt in 60-KB-Blöcken wie die anderen Dienste (je Block 1 Credit). Optional zusätzlich: ein SMB-Fehler bei einer einzelnen PDF stellt nur diese Rechnung zurück (wie „PDF nicht lesbar“) statt den ganzen Export abzubrechen. Ändert nicht, was gebucht wird. Test: Datei > 1 MiB in den MEQ-Testordner `SiSe-Test` schreiben, Größe prüfen, löschen. Deploy: nur `es-dataworker`, vor 19:30.

Zu ER 910010126: Buchhaltung/com4vision fragen, warum sie wieder offen ist (Korrektur? Storno?). Bis zur Antwort entweder aus dem Lauf nehmen (in esERP als übergeben markieren) oder bewusst laufen lassen und eine Ablehnung mit „Korrekte Belege jetzt buchen“ trennen.
