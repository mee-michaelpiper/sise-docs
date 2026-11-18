# Entwurf: E-Mail an den externen Datenschutzbeauftragten – PDF-/Auftragsdaten-Schnittstelle

- **Stand:** 09.10.2026, Entwurf, noch **nicht versendet**
- **An:** externer DSB (Adresse ergänzen)
- **Umfang:** nur die Schnittstelle `/api/maildata` (Auftrags-PDF und Auftragsdaten). Die übrigen Befunde meldet ein Kollege separat.
- **Grundlagen:** Bericht 10 (Nachträge vom 09.10.2026), Protokoll im Sicherungs-Image

---

**Betreff:** Webshop „Sicherheit und Service“ – möglicher Datenschutzvorfall (offene Auftrags-Schnittstelle), Bitte um Bewertung nach Art. 33 DSGVO

Sehr geehrte/r [Name],

wir möchten Sie über einen möglichen Datenschutzvorfall in unserem Webshop „Sicherheit und Service“ (sicherheit.meesenburg.com) informieren und bitten um Ihre Bewertung, insbesondere zur Meldepflicht nach Art. 33 DSGVO. Diese Mail betrifft ausschließlich die nachfolgend beschriebene Schnittstelle. Weitere Befunde aus der laufenden Prüfung des Shops meldet Ihnen ein Kollege gesondert.

**1. Sachverhalt**
Bei einer Code- und Sicherheitsprüfung des Shops haben wir am **07.10.2026** festgestellt, dass eine Schnittstelle des Shop-Backends ohne Anmeldung aus dem Internet erreichbar war:
- `https://sicherheit.meesenburg.com/api/maildata/order_pdf/{Auftragsnummer}`: Auftragsbestätigung als PDF
- `https://sicherheit.meesenburg.com/api/maildata/order/{Auftragsnummer}` sowie `…/shipping/`, `…/cancel/`, `…/payed/`: Auftragsdaten

Die Schnittstelle war für den internen Mailversand gedacht. Sie wurde von den externen Dienstleistern des Shops eingeführt und war nirgends verlinkt (weder im Shop noch in Mails oder in der Sitemap). Wir haben die Lücke am 07.10.2026 abends mit einzelnen eigenen Testaufrufen verifiziert.

**2. Zeitraum**
- **Potenziell offen ab 17.08.2026** (Einbindung der Schnittstelle laut Versionsverwaltung), **sicher ab 09.09.2026**
- **Geschlossen am 09.10.2026 um 13:49 Uhr.** Seitdem sind die Aufrufe von außen gesperrt (HTTP 403), intern bleiben sie für den Mailversand erreichbar.

**3. Betroffene Daten und Personen**
- **Datenarten:** Name, Firma, Rechnungs- und Lieferadresse, E-Mail-Adresse, Telefon-, Mobil- und Faxnummer, USt-IdNr., bestellte Artikel mit Preisen, Zahlart sowie die Auftragsbestätigung als PDF. Keine Bank- oder Kartendaten, keine Passwörter.
- **Besondere Sensibilität:** Wir vertreiben Sicherheits- und Schließtechnik. Aus den Daten geht hervor, wer welche Schließtechnik an welche Adresse bestellt hat.
- **Umfang:** Die Schnittstelle griff auf einen Spiegel aller JTL-Aufträge zu, nicht nur auf Shop-Bestellungen. Laut Datenbankstand vom 09.10.2026 waren potenziell **bis zu 43.787 Aufträge** (Auftragsdatum 2007 bis 2026, alle Verkaufskanäle einschließlich Altshop, Telefon/Wawi, Amazon und eBay) von **rund 29.000 verschiedenen Kunden** abrufbar. Die Auftragsnummern sind teilweise fortlaufend und damit durch einfaches Hochzählen erratbar.

**4. Prüfung auf tatsächliche Zugriffe**
- **Eigene Zugriffslogs des Servers (05.10.–09.10.2026, ca. 472.000 Anfragen):** Außer unseren eigenen Testaufrufen (07.10. und 08.10.) gibt es **keinen** Aufruf der Schnittstelle.
- **Zusätzliche Logs vom externen Monitoring-Server der Dienstleister (25.09.–09.10.2026, lückenlos):** Diese haben wir angefordert, gesichert und ausgewertet. Auch hier gibt es **keinen fremden Aufruf**, nur unsere eigenen Tests.
- **Suchmaschinen und Crawler:** In beiden Zeiträumen hat kein Suchmaschinen- oder anderer Bot die Schnittstelle aufgerufen, obwohl der Shop regelmäßig gecrawlt wird. Eine Websuche ergab keine indexierten Treffer.
- **Nicht prüfbar: 17.08.–24.09.2026.** Für diesen Zeitraum existieren keine Zugriffslogs mehr: Das Monitoring des Dienstleisters löschte Logs nach 14 Tagen, und der Server selbst bewahrt Zugriffslogs nur 5 Tage auf.

**5. Ergriffene Maßnahmen**
- Schnittstelle am 09.10.2026 von außen gesperrt, ebenso weitere interne Endpunkte (PDF-Dienst, Zahlungsmarkierung)
- Server, Datenbanken und Logs am 08./09.10.2026 vollständig gesichert (verschlüsselt, mit Prüfsummen und Protokoll)
- Logs vom Monitoring-Server des Dienstleisters herausgefordert und ausgewertet. Die Übermittlung dorthin wurde beendet, und der Dienstleister hat die Löschung der Daten am 09.10.2026 schriftlich bestätigt.
- Dauerhafte Behebung im Code im Rahmen der Ablösung des Shop-Backends

**6. Unsere Bitte**
Bitte bewerten Sie, ob eine Meldung an die Aufsichtsbehörde nach Art. 33 DSGVO und gegebenenfalls eine Benachrichtigung der Betroffenen nach Art. 34 DSGVO erforderlich ist. Wir haben am **07.10.2026** Kenntnis erlangt. Die 72-Stunden-Frist für eine etwaige Meldung liefe damit am **10.10.2026** ab. Wir wären daher für eine kurzfristige Rückmeldung dankbar.

Für Rückfragen stehe ich Ihnen gern zur Verfügung. Die Zeitleiste, die Auswertung der Logs und die Sicherungsprotokolle stelle ich Ihnen bei Bedarf zur Verfügung.

Mit freundlichen Grüßen
Michael Piper
Meesenburg GmbH
