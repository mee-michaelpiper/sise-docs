# Nachrichtenflussregel: Kopie der Rechnungsmails an eserp_mail@

Stand 14.10.2026. Ziel: Jede Mail, die das esERP als `eserp_mail@meesenburg.de` verschickt, landet zusätzlich als unsichtbare Kopie (Bcc) im Posteingang von eserp_mail@. Die Portal-Seite *Rechnungsversand* liest diese Kopien über Graph (Posteingang, Absender = eserp_mail@) und erkennt daran, welche Rechnung per Mail raus ist.

**Warum:** Das esERP verschickt über EWS ohne Kopie in „Gesendete Elemente“. `MessageCopyForSentAsEnabled` hilft nur beim Senden aus Outlook/OWA mit Send-As, nicht bei EWS. Die Bcc-Option im esERP würde an den Sachbearbeiter gehen.

**Voraussetzung im Portal:** es-api-graph muss deployt sein. Die Version mit dem Lesen des Posteingangs ist seit 13.10.2026 fertig, aber noch nicht deployt.

> **Stand 15.10.2026 – geändert:** Das esERP sendet per EWS (App „Meesenburg.EsErp.Mail“, `full_access_as_app`) **im Namen des Sachbearbeiters**, nicht als eserp_mail@, und ohne Kopie in dessen „Gesendete Elemente“ (Beispiel RG 900010316 von I.Grabs@). Die Regel erkennt esERP-Mails daher am Betreff (`Meesenburg GmbH <Belegart> <9-stellige Nummer>`), nicht am Absender. Sie gehört nach **Exchange Online** (nicht in den lokalen Exchange). Das Portal zählt jede Bcc-Kopie im Posteingang von eserp_mail@ (Postfach nicht in An/Cc) und übernimmt den Absender als Sachbearbeiter.
>
> ```powershell
> Set-TransportRule "Rechnungsversand: Kopie an eserp_mail" -From $null -FromScope InOrganization `
>   -SubjectMatchesPatterns '^Meesenburg GmbH \w\w* 9\d\d\d\d\d\d\d\d' `
>   -BlindCopyTo "eserp_mail@meesenburg.de" -ExceptIfSentTo "eserp_mail@meesenburg.de"
> ```
> Zweite Regel als Absicherung, falls der Betreff geändert wird (Anhang heißt `Rechnung 900010316.pdf`):
> ```powershell
> New-TransportRule -Name "Rechnungsversand: Kopie an eserp_mail (Anhang)" -FromScope InOrganization `
>   -AttachmentNameMatchesPatterns '^Rechnung 9\d\d\d\d\d\d\d\d\.pdf' -ExceptIfSubjectMatchesPatterns '^(AW|WG|RE|FW|Fwd):' `
>   -ExceptIfSentTo "eserp_mail@meesenburg.de" -BlindCopyTo "eserp_mail@meesenburg.de" -Mode Enforce
> ```
> Exchange-Muster kennen kein `{n}` – Ziffern ausschreiben. Die folgenden Abschnitte beschreiben den ursprünglichen Ansatz (Absender = eserp_mail@) und gelten nur noch sinngemäß.

## Variante A: Exchange Admin Center

1. https://admin.exchange.microsoft.com → **Nachrichtenfluss** → **Regeln** → **+ Regel hinzufügen** → **Neue Regel erstellen**.
2. **Name:** `Rechnungsversand: Kopie an eserp_mail`
3. **Diese Regel anwenden, wenn:** *Der Absender* → *ist diese Person* → `eserp_mail@meesenburg.de`
4. **Folgendes tun:** *Empfänger hinzufügen* → *zum Bcc-Feld* → `eserp_mail@meesenburg.de`
5. **Ausnahme:** *Der Empfänger* → *ist diese Person* → `eserp_mail@meesenburg.de` (sonst doppelt, wenn jemand direkt an das Postfach schreibt)
6. Weiter: **Regelmodus „Erzwingen“**, Schweregrad *Niedrig*, *Verarbeitung weiterer Regeln beenden* **nicht** anhaken → Speichern.
7. **Wichtig:** Neue Regeln sind im Admin Center zunächst **deaktiviert**. In der Liste die Regel öffnen und **Aktivieren** einschalten.

## Variante B: Exchange Online PowerShell

```powershell
Connect-ExchangeOnline -UserPrincipalName m.piper@meesenburg.de

New-TransportRule -Name "Rechnungsversand: Kopie an eserp_mail" `
  -From "eserp_mail@meesenburg.de" `
  -BlindCopyTo "eserp_mail@meesenburg.de" `
  -ExceptIfSentTo "eserp_mail@meesenburg.de" `
  -Mode Enforce `
  -Comments "Kopie für die Portal-Seite Rechnungsversand (Graph liest den Posteingang). 14.10.2026"

Get-TransportRule "Rechnungsversand: Kopie an eserp_mail" | Format-List Name,State,Mode,From,BlindCopyTo,ExceptIfSentTo
```

`State` muss `Enabled` sein. Bis die Regel überall greift, kann es bis zu 30 Minuten dauern.

## Prüfen

1. Im esERP eine Rechnung per Mail versenden, z. B. an dich selbst.
2. Nachrichtenablaufverfolgung:
   ```powershell
   Get-MessageTraceV2 -SenderAddress eserp_mail@meesenburg.de -StartDate (Get-Date).AddHours(-2) -EndDate (Get-Date)
   ```
   Die Mail sollte zweimal erscheinen: an den Kunden und an eserp_mail@. In `Get-MessageTraceDetailV2 -MessageTraceId <Id> -RecipientAddress eserp_mail@meesenburg.de` steht beim Ereignis *Transportregel* der Regelname.
3. Im Posteingang von eserp_mail@ liegt die Kopie, Absender eserp_mail@.
4. Portal → *Rechnungsversand*: Nach dem nächsten Lauf (alle 5 Minuten) steht beim Kanal Mail „versendet“.

**Taucht die Mail in der Ablaufverfolgung von Exchange Online gar nicht auf**, verschickt das esERP über den lokalen Exchange (Hybrid). Dann dieselbe Regel in der Exchange Management Shell auf dem lokalen Server anlegen; `New-TransportRule` hat dort dieselben Parameter.

## Hinweise

- **Postfachgröße:** Jede Rechnungsmail liegt doppelt vor, mit etwa 0,7 MB PDF. Bei rund 100 Mails am Tag sind das etwa 25 GB im Jahr. Später eine Aufbewahrungsrichtlinie für den Posteingang von eserp_mail@ setzen, z. B. Löschen nach 180 Tagen. Das Portal liest den Verlauf ab 01.09.2026.
- **Keine Posteingangsregeln** in eserp_mail@, die die Kopien verschieben: Das Portal liest nur den Ordner *Posteingang*.
- **Zurücknehmen:** `Disable-TransportRule "Rechnungsversand: Kopie an eserp_mail"` bzw. `Remove-TransportRule …`.
