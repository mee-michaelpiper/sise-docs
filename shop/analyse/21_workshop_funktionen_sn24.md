# 21 Workshop mit dem SN24-Team: Welche Funktionen braucht der Shop?

> Stand 05.11.2026. Vorlage für einen Workshop (ca. 2–3 Stunden) mit dem SN24-Team, Innendienst und Marketing.
> Ziel: festlegen, was der .NET-Shop zum Livegang können muss und was danach kommt, damit das Team den Shop **weitgehend selbst verwaltet**, ohne Entwickler.
> Zum Ausfüllen: `analyse/workshop/Workshop_Funktionen_SN24.xlsx` (je Funktion eine Zeile mit Vorschlag und Spalte „Entscheidung“).

## 1. Worum es geht

Der .NET-Shop kann heute schon:
- Artikel aus der Wawi zeigen (Preise, Rabatte, Sonderpreise, Staffeln, Bestand, Gratisgeschenk).
- Warenkorb, Kasse, Vorkasse und Stripe.
- Aufträge in die Wawi schreiben.
- Kundenkonten, Widerrufsformular.
- Inhaltsseiten mit Bausteinen und Zeitsteuerung.
- SEO-Adressen wie im JTL-Shop.

Offen ist, **welche weiteren Funktionen des JTL-Shops wirklich gebraucht werden** und was das Team künftig selbst pflegen soll. Im JTL-Shop ist vieles eingeschaltet, aber kaum genutzt. Deshalb steht unten zu jeder Funktion, wie stark sie heute genutzt wird.

## 2. Was der JTL-Shop heute nutzt (Abfrage 05.11.2026, nur lesend)

**Zeitraum der Besucherzahlen:** 19.10.–05.11.2026, also seit der JTL-Shop wieder live ist. Davor lief von Juni bis Oktober der Stripe-Shop.

**Vorsicht bei den Zahlen:** 257.000 der 335.000 als „Mensch“ gezählten Einstiege sind Filterkombinationen wie `/Keso-8000-Doppelzylinder__KESO-Doppelzylinder__Sicherheitsstufe-2__…`. Das sind fast sicher Suchmaschinen- und KI-Crawler, die JTL nicht als Bot erkennt. Aussagekräftiger sind:
- die Einstiege **über Google** (3.427),
- die **Bestellungen** nach Einstiegsseite (Mai–Juni und ab 19.10.: 157).

| Einstiegsseite | über Google | Bestellungen seit Mai |
|---|---|---|
| Artikelseiten | 1.319 | 38 |
| Startseite | 750 | 70 |
| Kategorieseiten | 299 | 8 |
| Eigene Seiten (Infoseiten, Schließanlagen …) | 222 | 23 |
| **Merkmal-Seiten** (z. B. `/Keso`, `/10-mm`, `/2-x-Rundbolzen`) | **54** | **0** |
| **Filterkombinationen** (`…__…`) | 28 | 1 |
| **News** | **7** | **1** |
| Hersteller-Seiten | 6 | 0 |
| sonstige (alte Adressen, `/navi.php` …) | 742 | 16 |

| Funktion im JTL-Shop | Nutzung |
|---|---|
| News | 29 Beiträge, 21 aktiv. Seit 2022 nur 2 neue (zuletzt 21.11.2025). Seit 19.10. 187 Einstiege, davon 7 über Google |
| Merkmal-Seiten | 686 Adressen (79 Merkmale). Seit 19.10. 5.904 Einstiege, davon 54 über Google, keine Bestellung |
| Weiterleitungen | 9.046, zusammen **726.624 Aufrufe**. Die meistgenutzte hat 5.832 Aufrufe (alte `/onlineshop/…`-Adressen) |
| Kontaktformular | 1.275 Nachrichten insgesamt, die letzte heute |
| Frage zum Artikel | 39 Anfragen |
| Newsletter | 1.408 Empfänger, Anmeldungen bis heute. Verschickt wurden aber nur 2 Newsletter, zuletzt 2020 |
| Gutscheine | 16 Kupons, 11 aktiv, der meistgenutzte 210-mal |
| Wunschzettel | 506 |
| Vergleichsliste | 2.857 (vermutlich viele von Crawlern) |
| Bewertungen | 39, die letzte vom 29.07.2020 |
| Verfügbarkeits-Benachrichtigung | 1 |
| Aktive Plugins | Trusted Shops, Rechtstexte Händlerbund, PayPal Checkout, Angebot anfragen, Brutto/Netto-Umschalter, Google Codes, Google Shopping, Upload-Formular, Widerrufsformular, Hinweisfenster, EU-Cookie, DHL-Wunschzustellung, reCAPTCHA, FAQ, Schließanlagen-Konfigurator |
| Aktive Zahlarten | Viele, darunter Vorkasse, Nachnahme, Rechnung, Lastschrift, Amazon Pay, PayPal, Ratepay, easyCredit und die GiroCheckout-Familie. Entschieden ist nur: Vorkasse, Stripe, PayPal direkt (F-20) |

## 3. Zwei Antworten vorab

### 3.1 Merkmal-Seiten: nicht nachbauen, aber Filter anbieten

Eine Merkmal-Seite ist eine automatische Liste aller Artikel mit einem Merkmalwert, z. B. `/10-mm` oder `/Keso`. Sie wird nicht gepflegt.

- **Zahlen:** 54 Einstiege über Google in 17 Tagen (1,6 %) und **keine Bestellung seit Mai**. Die Kategorie- und Artikelseiten decken dieselben Suchbegriffe besser ab.
- **Vorschlag:**
  1. Keine eigenen Merkmal-Seiten bauen.
  2. Jede der 686 Adressen per 301 dorthin leiten, wo die Artikel mit diesem Merkmal überwiegend liegen. Beispiel: `/2-x-Rundbolzen` geht auf die passende Kategorie. Wo das nicht eindeutig ist, auf die Suche.
  3. Damit bleibt der Google-Wert erhalten, und es gibt keine 404.
- **Was die Kunden wirklich brauchen, sind Filter in der Kategorie**, z. B. Länge, Sicherheitsstufe, Schließung. Die Merkmale liegen schon im .NET-Shop. Filter gibt es heute nur nach Hersteller und Preis.
  - Merkmal-Filter für Kategorien bauen. Welches Merkmal wo filtert, stellt das Team im Admin ein.
  - Gefilterte Seiten bekommen `noindex`, damit Crawler nicht wieder Hunderttausende Kombinationen abgrasen.
- **Aufwand:** Weiterleitungen 1 PT, Filter 3–4 PT.
- Als Frage **F-47** in Bericht 16 aufgenommen.

### 3.2 News: weglassen, „Ratgeber“ nur mit Redaktionsplan

- **Heute:** Die News sind veraltet. Seit 2022 kamen 2 Beiträge, über Google 7 Einstiege in 17 Tagen. Die Seiten können weg, ihre Adressen leiten per 301 auf die Startseite bzw. die passende Kategorie (z. B. „Keso 8000 Omega² verfügbar“ → Keso-8000-Kategorie).
- **Lohnt ein News- oder Blog-Modul?** Technisch ist es klein, weil es auf dem vorhandenen Inhaltsmodul aufsetzt:
  - Seitentyp „Beitrag“ mit Datum, Titelbild, Anrisstext und Themen
  - Übersicht `/ratgeber` und Teaser-Baustein für die Startseite
  - Aufnahme in Sitemap, Google-Daten „Article“
  - Aufwand 3–5 PT
- **Der Nutzen hängt aber allein am Schreiben.** Für die Suche zählen Ratgeber-Texte wie „Welcher Zylinder für die Haustür?“, „Was bedeutet Sicherheitsstufe 5?“ oder „Schließanlage planen“. Kurznachrichten wie „neu verfügbar“ bringen wenig.
- **Vorschlag:**
  - Nur bauen, wenn das Team ca. 1 Beitrag im Monat zusagt. Dann als **„Ratgeber“**, nicht als „News“, und nach dem Livegang.
  - Ohne Zusage: weglassen. Kurze Hinweise wie „Betriebsferien“ oder „neue Preise ab …“ laufen über die Hinweisleiste (Abschnitt 4, A-5).
- Als Frage **F-48** in Bericht 16 aufgenommen.

## 4. Funktionen zum Durchsprechen

Legende Priorität (Vorschlag):
- **L** = zum Livegang nötig
- **N** = bald nach dem Livegang
- **S** = später oder bei Bedarf
- **W** = weglassen

PT = Personentage Entwicklung.

### A. Inhalte selbst pflegen

| Nr | Funktion | Heute im .NET-Shop | Vorschlag | Prio | PT |
|---|---|---|---|---|---|
| A-1 | Seiten und Bausteine (Text, Bild, Spalten, Banner, Video, Produktliste) | vorhanden | so lassen; Rückmeldung des Teams zum Editor einholen | – | – |
| A-2 | **Menü und Fußzeile selbst pflegen** (Einträge, Reihenfolge, Links) | **gebaut 05.11.** (Admin „Menüs & Hinweise“) | Pflege im Admin; sonst braucht jede neue Infoseite im Menü einen Entwickler | **L** | 2 |
| A-3 | Startseite: Banner/Slider, Aktionen mit Zeitraum | Bausteine mit Zeitraum vorhanden | Slider-Baustein ergänzen, falls gewünscht | N | 1 |
| A-4 | Inhalte oben auf Artikelseiten (vor der Kurzbeschreibung, wie im JTL-Shop) | nur unten | Stelle „oben“ ergänzen | L | 0,5 |
| A-5 | **Hinweisleiste** (z. B. „Betriebsferien 23.12.–02.01.“), mit Zeitraum | **gebaut 05.11.** | Leiste über dem Kopf, im Admin mit Zeitraum | **L** | 1 |
| A-6 | FAQ (Fragen und Antworten, aufklappbar) | Akkordeon-Baustein vorhanden | Baustein reicht; Google-Daten „FAQ“ ergänzen | N | 0,5 |
| A-7 | Freigabe (eine Person schreibt, eine andere veröffentlicht) | Rolle „Redaktion (Entwürfe)“ vorhanden | genügt das? | – | – |
| A-8 | Produktliste nach Kategorie, Merkmal oder „Angebote“ (automatisch statt Artikelnummern) | nur Artikelnummern | ergänzen | N | 1 |
| A-9 | Ratgeber/Blog (siehe 3.2) | fehlt | nur mit Redaktionsplan | S | 3–5 |
| A-10 | Bilder zentral (Bibliothek, Blob Storage) | Bibliothek vorhanden, Bilder z. T. noch beim JTL-Shop | Bilder übernehmen | **L** | 1 |

### B. Verkaufen und Marketing

| Nr | Funktion | Heute im .NET-Shop | Vorschlag | Prio | PT |
|---|---|---|---|---|---|
| B-1 | Preise, Sonderpreise, Rabatte, Gratisgeschenke | aus der Wawi | bleibt in der Wawi (eine Pflegestelle) | – | – |
| B-2 | **Gutscheine** (16 Kupons, einer 210-mal genutzt) | **gebaut 05.11.**, 8 aktive übernommen | Gutscheine im Admin: Code, Wert oder Prozent, Mindestwert, Zeitraum, Anzahl, Kundengruppe; Übernahme der 11 aktiven | **L** | 3 |
| B-3 | Google Shopping (Produktfeed) | **gebaut 05.11.** (`/google-shopping.xml`) | Feed automatisch aus dem Katalog | **L** | 2 |
| B-4 | Google-Codes (Analytics, Ads) mit Einwilligung | **gebaut 05.11.**, Ids fehlen noch | mit Cookie-Einwilligung (C-3); welche Dienste? | L | 2 |
| B-5 | Trusted Shops (Siegel, Bewertungen) | fehlt | einbinden, falls der Vertrag weiterläuft | L | 1–2 |
| B-6 | Newsletter (1.408 Empfänger, seit 2020 nichts verschickt) | fehlt | **Entscheidung:** einstellen (Empfänger nicht übernehmen) oder an ein Newsletter-Werkzeug übergeben (z. B. CleverReach, Brevo) mit Anmeldung im Shop | S/W | 2 |
| B-7 | „Angebot anfragen“ (Artikel ohne Preis, große Mengen) | fehlt (P9) | Anfrage-Formular an den Innendienst | N | 1–2 |
| B-8 | Brutto/Netto-Umschalter für Geschäftskunden | Händler sehen ihre Preise nach Anmeldung | Umschalter nur, wenn gewünscht | S | 1 |
| B-9 | Produktbewertungen (39, letzte 2020) | fehlt | **W** – Trusted Shops übernimmt das | W | – |
| B-10 | Wunschzettel / Vergleichsliste | fehlt | **W** zum Livegang; Wunschzettel ggf. später im Kundenkonto | S | 2 |
| B-11 | Verfügbarkeits-Benachrichtigung (1 Nutzung) | fehlt | **W** | W | – |

### C. SEO und Recht selbst im Griff

| Nr | Funktion | Heute im .NET-Shop | Vorschlag | Prio | PT |
|---|---|---|---|---|---|
| C-1 | **Weiterleitungen im Admin** + Liste der 404-Aufrufe mit „Weiterleitung anlegen“ | **gebaut 05.11.** | Admin-Seite; das Team sieht kaputte Links selbst und behebt sie | **L** | 2 |
| C-2 | Titel, Beschreibung, Adresse je Inhaltsseite | Adresse ja, Titel/Beschreibung prüfen | Felder ergänzen; Artikel und Kategorien kommen aus der Wawi | L | 0,5 |
| C-3 | **Cookie-Einwilligung** | **gebaut 05.11.** | nötig, sobald Google-Codes oder Trusted Shops laufen | **L** | 1–2 |
| C-4 | **Rechtstexte** (AGB, Widerruf, Datenschutz, Impressum) automatisch vom Händlerbund | **übernommen 05.11. aus Strapi** (aktuellste Fassung); automatischer Abruf offen | Abruf über die Händlerbund-Schnittstelle, täglich; PDF für die Mail (F-33) | **L** | 2 |
| C-5 | Sitemap, robots.txt, Google-Daten (Produkt, Brotkrumen) | fehlt | automatisch | **L** | 1–2 |
| C-6 | Merkmal-Seiten (3.1) | 404 | Weiterleitung + Filter | L/N | 1 + 3–4 |

### D. Kundenservice

| Nr | Funktion | Heute im .NET-Shop | Vorschlag | Prio | PT |
|---|---|---|---|---|---|
| D-1 | **Kontaktformular** (1.275 Nachrichten) | **gebaut 05.11.** (`/kontakt`) | Formular → Mail an sicherheit@, Antwort an den Kunden; Spamschutz wie beim Widerruf | **L** | 1 |
| D-2 | Frage zum Artikel (39) | **gebaut 05.11.** (Link auf der Artikelseite) | Knopf auf der Artikelseite, gleiches Formular mit Artikel | N | 0,5 |
| D-3 | Upload-Formular (Plugin, z. B. für Schließpläne, Fotos) | fehlt | **Frage:** wofür genutzt? Sonst W | ? | 1–2 |
| D-4 | DHL-Wunschzustellung (Wunschort/-nachbar) | fehlt | **Frage:** genutzt? | S | 2 |
| D-5 | Widerrufsformular | vorhanden | – | – | – |

### E. Innendienst und Betrieb

| Nr | Funktion | Heute im .NET-Shop | Vorschlag | Prio | PT |
|---|---|---|---|---|---|
| E-1 | Bestellungen ansehen, „Zahlung eingegangen“, Widerrufe, Kundenkonten | vorhanden | – | – | – |
| E-2 | **Mailtexte selbst ändern** (Bestellbestätigung, Vorkasse, Erinnerung …) | im Code | Texte im Admin mit Platzhaltern und Vorschau | N | 2–3 |
| E-3 | Versandregeln und Länder | Versandkosten aus der Wawi, Admin-Seite vorhanden | Länder ausschließen (F-15) | L | 1–2 |
| E-4 | Zahlarten ein-/ausschalten | Konfiguration | Schalter im Admin | N | 1 |
| E-5 | Wartungsmodus (Shop zu, Admin offen) | **gebaut 05.11.** | Schalter im Admin mit Text | L | 0,5 |
| E-6 | Rechte: wer darf was (Redaktion, Innendienst, Betrieb, Admin) | vorhanden, Anmeldung mit Entra fehlt noch | Entra-App (IT) | **L** | – |

## 5. Vorschlag: Was zum Livegang, was danach

**Zum Livegang:**
- A-2, A-4, A-5, A-10
- B-2, B-3, B-4, B-5
- C-1 bis C-5 und die Weiterleitungen aus C-6
- D-1
- E-3, E-5, E-6

Das sind **ca. 25–30 PT** zusätzlich zu den Livegang-Punkten aus Bericht 20, Abschnitt 6 (Rendering, Zahlung, Betrieb). Teilweise überschneiden sie sich mit ihnen (SEO, Rechtstexte, Google Shopping).

**Bald danach:**
- A-3, A-6, A-8
- B-7
- C-6 (Filter)
- D-2
- E-2, E-4

**Weglassen:** News (Adressen umleiten), Bewertungen, Vergleichsliste, Verfügbarkeits-Benachrichtigung, Newsletter-Anmeldung (falls B-6 = einstellen).

## 6. Fragen an das Team im Workshop

1. Welche der Funktionen oben nutzt ihr heute wirklich, welche vermisst ihr im JTL-Shop?
2. Was soll **ohne Entwickler** gehen? Menü, Banner, Hinweise, Weiterleitungen, Gutscheine, Mailtexte, Zahlarten?
3. Ratgeber: Gibt es jemanden, der regelmäßig schreibt? Welche Themen fragen Kunden am häufigsten?
4. Newsletter: einstellen oder neu starten? Wer würde ihn schreiben?
5. Gutscheine: Welche Arten braucht ihr (Betrag, Prozent, versandkostenfrei, einmalig je Kunde, Neukunde)? Hinweis: Hersteller untersagen teils Werbung mit Rabatt-Prozenten (R-2).
6. Trusted Shops und Händlerbund: Laufen die Verträge weiter? Zugangsdaten für die Schnittstellen?
7. Google: Welche Dienste sind im Einsatz (Analytics, Ads, Merchant Center, Search Console)? Wer betreut sie?
8. Upload-Formular und DHL-Wunschzustellung: wofür, wie oft?
9. Brutto/Netto-Umschalter: Brauchen Geschäftskunden das ohne Anmeldung?
10. Merkmal-Filter: Nach welchen Eigenschaften suchen Kunden in Zylinder-, Beschlag- und Schlüsselkategorien?

## 7. Nach dem Workshop

Die Antworten fließen ein in:
- Bericht 16 (F-47, F-48)
- TODO
- den Livegang-Plan (Bericht 14, Abschnitt 5b)

Die Liste „Livegang“ wird dort mit Datum abgearbeitet.
