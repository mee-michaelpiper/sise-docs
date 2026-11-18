# Drehbuch: Vorstellung .NET-Shop (GF, SN24-Team, IT)

Stand 10.11.2026 · intern · Folien: Artifact „SN24 .NET-Shop – Vorstellung“ (12 Folien, Notizen je Folie)

**Ziel:** Freigabe von Option B (der .NET-Shop wird der neue Shop, der Stripe-Shop wird nicht repariert), Ansprechpartner in der IT, zwei Personen aus dem SN24-Team für den Probebetrieb.

**Ablauf (ca. 45 Minuten):**

| Zeit | Teil | Folien |
|---|---|---|
| 0–10 | Lage, Optionen, was der Shop kann | 1–8 |
| 10–30 | Live-Demo: Kunde (12 min), Team (8 min) | 9 |
| 30–40 | Was fehlt, was wir brauchen, Entscheidung | 10–12 |
| 40–45 | Fragen | – |

## 1. Vorbereitung (am Vortag und 30 Minuten vorher)

| # | Schritt | Prüfung |
|---|---|---|
| 1 | `podman machine start` (nach Neustart des Rechners) | `podman machine list` zeigt „Currently running“ |
| 2 | Tunnel zur Wawi (ZUSAMMENFASSUNG, Abschnitt „Zugang zu JTL“) | Systemstatus im Admin: Wawi erreichbar |
| 3 | `aspire run` im Repo, Dashboard öffnen | Portal http://localhost:5036 antwortet |
| 4 | Browser: Zoom 125 %, zwei Fenster (Shop als Gast, Admin in privatem Fenster) | – |
| 5 | Admin: als Test-Person **Anna Admin** anmelden (Entwicklung) | Startseite mit Kacheln |
| 6 | Admin → Gutscheine: einen Demo-Gutschein anlegen (Code selbst wählen, 10 %, 1 Nutzung je Kunde) | Gutschein aktiv |
| 7 | Mailpit aus dem Aspire-Dashboard öffnen (Link bei Ressource „mailpit“), alte Mails löschen | leer |
| 8 | Warenkorb leeren (Cookie `siseshop.warenkorb` löschen oder privates Fenster) | Zähler 0 |
| 9 | **Kein Stripe-Test** in der Demo, solange der fremde Test-Webhook (`test.meesenburg.neogris.com`) im Stripe-Testmodus steht: jede Testzahlung erzeugt sonst wieder Fehlermails. Demo mit **Vorkasse** | – |
| 10 | Einmal komplett durchspielen; die Bestellung danach im Admin stornieren | – |

Rückfall-Screenshots: `analyse/vorstellung/bilder/` (Start, Kategorie, Artikel, Konfigurator, Systeme, Admin-Inhalte). Wenn live etwas hängt: auf die Folie bzw. den Screenshot wechseln und weitererzählen.

## 2. Demo Kunde (ca. 12 Minuten)

1. **Startseite** http://localhost:5036
   - Zeigen: Aufbau wie sicherheit.meesenburg.com, Aktionsbanner (kommt aus dem Admin), Menü mit Kategorien aus der Wawi.
   - Satz: „Alle Adressen sind wie im JTL-Shop, Google findet die Seiten weiter.“
2. **Suche** „Keso 8000“ → Ergebnisliste, Filter Hersteller/Preis.
3. **Artikel** z. B. „Keso 8000 Omega³ Doppelzylinder 81.B15 Basic plus mit Ziehschutz“
   - Zeigen: Preis mit Streichpreis, Lieferzeit-Ampel, Konfiguration (Längen, Färbung), Preis ändert sich mit der Auswahl.
   - Satz: „Der Preis wird auf dem Server gerechnet, genau wie im JTL-Shop. Im Stripe-Shop waren über 200 Artikel falsch.“
   - „In den Warenkorb“ → Hinweis oben, Zähler im Kopf.
4. **Konfigurator** (Menü „Konfigurator“) → Keso 8000 O² → **Beispiel „Mehrfamilienhaus“**
   - Zeigen: Türen, Schlüssel, Schließplan-Matrix, rechts der Preis (412,20 €), „Plan speichern und Link teilen“, „Als PDF“.
   - „In den Warenkorb“.
5. **Warenkorb**: Schließanlage als Block, Konfiguration kompakt unter jedem Zylinder, Gratisgeschenk ab 200 €, Gutschein aus Schritt 1.6 eingeben.
6. **Kasse**: als Gast, Testadresse, **Vorkasse**, AGB/Widerruf bestätigen, „Zahlungspflichtig bestellen“.
7. **Bestätigungsseite** → **Mailpit**: Kundenmail mit **AGB_Meesenburg.pdf** und **Widerrufsbelehrung_Meesenburg.pdf**, interne Mail „Schließanlage bestellt“ mit Schließplan-PDF.
   - Satz: „Die Fassung der AGB steht im Verlauf der Bestellung, damit ist später nachweisbar, was der Kunde bekommen hat.“

## 3. Demo Team (ca. 8 Minuten)

1. **Admin → Seiten & Inhalte** → „Aktion Herbst: dormakaba penta cross“
   - Text im Banner ändern, rechts Live-Vorschau, „Entwurf speichern“, „Vorschau im Shop“, „Veröffentlichen“ → Startseite neu laden.
   - Zeigen: Zeitraum, Versionen. Satz: „Neue Seiten legt die Redaktion selbst an, im Stripe-Shop ging das nur mit einem Entwickler.“
   - Danach den alten Text wieder eintragen und veröffentlichen.
2. **Admin → Bestellungen** → die Demo-Bestellung: Positionen, Verlauf (Mails mit Anhang, Fassung der AGB), Status der Wawi-Übergabe (Schreibsperre: Trockenlauf).
3. **Admin → Weiterleitungen**: Liste „Ins Leere“ (404) und eine Weiterleitung anlegen, „Vorübergehend (302)“.
4. **Admin → Systemstatus**: Wawi erreichbar, Datenbank, Hintergrunddienste (Abgleich alle 15 Minuten, Storno unbezahlter Bestellungen).
5. Optional für IT: Aspire-Dashboard mit den Diensten (API, Worker, Portal, Mongo) – das ist auch der Aufbau im AKS.

## 4. Fragen, die kommen können

| Frage | Antwort |
|---|---|
| Was kostet der Betrieb? | Eigener Knotenpool im AKS, grob um 100 € im Monat je D2ds_v5-Knoten (Bericht 20, Abschnitt 7). Die Hetzner-Server des Stripe-Shops fallen weg |
| Was passiert bei esERP? | Der Shop bleibt, nur die Anbindung an die Wawi wird getauscht (Bericht 07) |
| Was, wenn der neue Shop ausfällt? | Der JTL-Shop bleibt einige Wochen als Rückfall, Umschaltung per DNS |
| Wann ist der Livegang? | Eigene Arbeit ca. 26–43 PT; der Termin hängt an den IT-Punkten (Folie 11) |
| Kann das Team den Shop vorher testen? | Ja, Probebetrieb auf einer Test-Domain im AKS (Folie 12, Schritt 3) |
| Warum nicht Umbraco oder ein SaaS-CMS? | Möglich (Bericht 20, Optionen C/D). Das Team nutzt nur wenige Bausteine; ein eigenes Modul hängt Inhalte direkt an Artikel und Kategorien |
| Wie sicher ist das? | Zahlbetrag vom Server, interne Funktionen nur mit Schlüssel, 303 Tests; vor dem Livegang ein externer Sicherheitstest |

## 5. Nach der Demo

- Demo-Bestellung im Admin stornieren, Demo-Gutschein deaktivieren.
- Entscheidung und Ansprechpartner in ZUSAMMENFASSUNG und TODO eintragen.
