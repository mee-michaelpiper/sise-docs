# Entwurf: Bestätigungsmail an Neukunden

> Status: **Entwurf zur Abstimmung** – noch nicht umgesetzt.
> Auslöser: erfolgreiche Neukundenanlage über das SiSe-Portal (`/eserp/customers/new`, VKO 298 · Meesenburg GmbH).

---

**Betreff:** Ihr Kundenkonto bei der Meesenburg GmbH – Kundennummer {Kundennummer}

Guten Tag {Name},

vielen Dank für Ihren Auftrag. Wir haben Sie als Kunden angelegt. Bitte geben Sie bei Bestellungen und Rückfragen Ihre Kundennummer an.

| | |
|---|---|
| Kundennummer | {Kundennummer} |
| Name | {Name} {Namenszusatz} |
| Anschrift | {Straße} {Nr.}, {PLZ} {Ort} |
| Rechnungsversand | {Post · E-Mail an {Rechnungs-E-Mail} · ZUGFeRD an {Rechnungs-E-Mail} · XRechnung, Leitweg-ID {Leitweg-ID}} |
| Ihr Ansprechpartner | Meesenburg {Standort des Verkaufsbüros}, {Telefon/E-Mail des Standorts} |

Bitte prüfen Sie die Angaben und geben Sie uns Bescheid, falls etwas nicht stimmt.

**Allgemeine Geschäftsbedingungen**
Für unsere Lieferungen und Leistungen gelten die Allgemeinen Verkaufs-, Liefer- und Zahlungsbedingungen der Meesenburg GmbH. Sie finden sie im Anhang und unter https://info.meesenburg.com/agb.

**Datenschutz**
Wir verarbeiten Ihre Daten, um Ihre Aufträge abzuwickeln (Art. 6 Abs. 1 lit. b DSGVO). Wie wir mit Ihren Daten umgehen und welche Rechte Sie haben, lesen Sie in unserer Datenschutzerklärung: https://info.meesenburg.com/datenschutz

{nur wenn „Werbung erlaubt“:}
Sie haben zugestimmt, dass wir Sie über Produkte und Angebote informieren. Diese Zustimmung können Sie jederzeit formlos widerrufen, z. B. per E-Mail an {Adresse}.

Mit freundlichen Grüßen
Ihr Team der Meesenburg GmbH

{Signatur/Impressum der Meesenburg GmbH: Anschrift, Registergericht, HRB, Geschäftsführung, USt-IdNr.}

**Anhang:** AGB_Meesenburg_GmbH_01_2025.pdf

---

## Vor der Umsetzung zu klären

1. **Ist die Mail gewünscht?** Wer muss zustimmen (Vertrieb, Datenschutz, Rechtsabteilung)?
2. **Verantwortlicher im Datenschutz:** In der Online-Datenschutzerklärung ist die **Meesenburg GmbH & Co. KG** als Verantwortlicher genannt. Die Kunden der VKO 298 gehören aber zur **Meesenburg GmbH**. Gilt die Erklärung auch für die GmbH, oder braucht sie eine eigene bzw. eine Ergänzung?
3. **Absender:** Welches Postfach (z. B. ein Shared Mailbox des Vertriebs)? Antworten sollten beim zuständigen Verkaufsbüro landen (Reply-To).
4. **Empfänger:** Allgemeine E-Mail oder Rechnungs-E-Mail? Kopie (BCC) an das Verkaufsbüro oder den anlegenden Mitarbeiter?
5. **AGB als Anhang oder nur Link?** Ein Anhang ist ein Nachweis zum Zeitpunkt der Anlage, muss aber bei jeder AGB-Änderung im System getauscht werden. Die Datenschutzerklärung nur verlinken, weil sie sich häufiger ändert.
6. **Ansprechpartner je Verkaufsbüro:** Telefon/E-Mail der Standorte. Diese Werte könnten in MongoDB bei `Customer-SalesOffices` liegen.
7. **Werbung:** Soll die Mail die Werbe-Einwilligung bestätigen (gut für den Nachweis), oder bleibt das ein reines Info-Schreiben?

## Technische Skizze (später)

- Versand über Microsoft Graph `sendMail` aus einem Shared Mailbox. Ein `GraphBroker` mit App-Berechtigung gibt es bereits im ESDataWorker.
- Auslöser: Die SAP-API meldet eine Kundennummer. Versand **asynchron** (z. B. als Job im Worker), damit ein Mailfehler die Anlage nicht blockiert. Fehlgeschlagene Mails werden wiederholt und sind in der Jobübersicht sichtbar.
- Text als Vorlage (HTML) mit Platzhaltern, die AGB-PDF als Datei bzw. Blob, Version und Stand im Dateinamen.
