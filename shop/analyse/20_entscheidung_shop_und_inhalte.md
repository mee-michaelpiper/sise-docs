# 20 Entscheidungsgrundlage: Shop-Oberfläche und Inhaltspflege

Stand: 04.11.2026. Grundlage sind lesende Abfragen gegen die JTL-Shop-Datenbank (nur Zählungen, keine Kundendaten), die Analyse des Stripe-Shops (Bericht 05), der Stand des .NET-Shops (Bericht 14, 18, 19) und ein lesender Blick in das produktive AKS (`aks-meesenburg-prod`). Aufwände sind Schätzungen in Personentagen (PT) für einen Entwickler mit KI-Unterstützung wie im POC, Genauigkeit etwa ± 30 %.

## 1. Kurzfassung

- **Ja, das geht im .NET-Shop**, und die Technik ist nicht der Engpass. Das Team nutzt aus dem großen Funktionsumfang des JTL-Shops nur einen kleinen, klar abgrenzbaren Teil: 8 Bausteintypen im OnPage Composer decken 98 % aller Inhalte ab. So etwas lässt sich in .NET sauber nachbauen.
- **Empfehlung: Option B.** Der .NET-Shop wird der ganze Shop: Oberfläche, Backend und ein eigener Bereich „Inhalte“ im Admin, mit Bausteinen, Vorschau, Zeitsteuerung und Weiterleitungen. Der ursprüngliche Plan (Svelte/Strapi anpassen) wird aufgegeben.
- **Gründe:** ein Technik-Stack (.NET wie ES), kein weiteres System im ohnehin vollen AKS, Inhalte hängen direkt an Kategorien und Artikeln wie im JTL-Shop, und die vorhandenen JTL-Inhalte lassen sich weitgehend automatisch übernehmen.
- **Aufwand bis zum Livegang:** etwa 70–110 PT. Davon entfallen rund 44–70 PT auf Punkte, die für jede Variante nötig sind (SEO, Rechtstexte, Zahlarten, Betrieb), und 26–39 PT auf das Inhaltsmodul.
- **Kein Zeitdruck:** Der JTL-Shop läuft und rechnet korrekt. Er bleibt die Brücke, bis der .NET-Shop fertig ist. Spätestens der Wechsel der Wawi auf esERP beendet den JTL-Shop, denn er funktioniert nur mit der JTL-Wawi.
- **Infrastruktur:**
  - Das AKS ist beim Arbeitsspeicher schon heute zu 76–99 % belegt. Der Shop braucht einen zusätzlichen Knoten, am besten einen eigenen Knotenpool.
  - Die Hetzner-Server des Stripe-Shops (node1–3) lassen sich nach dem Livegang kündigen. Vorher braucht es Ersatz für den SSH-Weg zur Wawi.
  - Der Timme-Server (JTL-Shop) läuft bis nach der Umschaltung weiter.
- **Nächster Schritt vor der Entscheidung:** ein kleiner Prototyp „Inhalte“ (2–3 PT) mit drei Bausteinen und Vorschau. Das SN24-Team probiert ihn aus und vergleicht mit dem JTL-Shop. Das ist günstiger als jede Diskussion auf Papier.

## 2. Ausgangslage

| Thema | Stand |
|---|---|
| Ursprünglicher Plan (09.10.2026) | .NET-Backend ersetzt das Python-Backend; das Svelte-Frontend und Strapi bleiben und werden auf die neue API umgestellt |
| Heute | Der .NET-Shop ist ein vollständiger Shop: Katalog mit allen JTL-Preisregeln (auf den Cent geprüft), Warenkorb, Kasse, Vorkasse und Stripe, Wawi-Übergabe, beide Konfiguratoren, Mails, Widerruf, Kundenkonten mit Übernahme der JTL-Passwörter, Admin. 233 Tests. Es fehlt die Inhaltspflege, außerdem sind einige Punkte nicht livegangreif (Abschnitt 6) |
| Svelte-Frontend | Reine Single-Page-App (`adapter-static`, nur `/410` vorgerendert) – schlecht für SEO. Eigene Preis- und Konfiguratorlogik im Browser (Ursache vieler Fehler, Bericht 02/05). Entwickelt von Freelancern, die nicht mehr verfügbar sind |
| Strapi | 41 Single-Types: **jede neue Seite ist ein eigener Inhaltstyp und braucht eine Schemaänderung und ein Deployment** (Bericht 05, Abschnitt 6). Redakteure können also keine neuen Seiten selbst anlegen. Dazu Sicherheitsbefunde (Secrets im Repo, Admin-JWT) |
| JTL-Shop | Live seit 19.10.2026, rechnet korrekt, das Team war damit zufrieden. Endet mit dem Wechsel auf esERP (dbeS gibt es nur mit der JTL-Wawi) |

## 3. Was das Team tatsächlich pflegt

### 3.1 JTL-Shop (Abfrage 04.11.2026)

| Funktion | Nutzung |
|---|---|
| **OnPage Composer (OPC)** | 204 gestaltete Seiten: **113 Artikelseiten, 68 Kategorieseiten**, 22 Inhaltsseiten, 1 Suchseite; 164 veröffentlicht. 56 gespeicherte Vorlagen |
| OPC-Bausteine (Anzahl / auf Seiten) | **Text 750 / 138, Bild 300 / 48, Zeile (Spalten) 223 / 38, Trenner 129 / 23, Container 91 / 26, Button 72 / 20, Überschrift 54 / 32, Video 29 / 17**. Selten: Banner 12 / 1, Bild-Slider 4, Produktliste 4, Akkordeon 3, Filter 7, FAQ 1, Countdown 1 |
| Pflegeintensität | Meist 1–3 Änderungen im Monat; **März 2026: 72, April: 16** (Vorbereitung Stripe-Shop?), seit der Rückkehr am 19.10.: 18 |
| Eigene Seiten | 70 Inhaltsseiten (54 aktiv), 19 externe Links, 23 Spezialseiten (Konto, Warenkorb …) |
| Seitenleiste | 39 eigene Boxen (mit/ohne Rahmen), 6 Linkgruppen-Boxen |
| Slider / Bildbanner | 8 Slider (2 aktiv, 42 Bilder), Bildbanner mit 119 klickbaren Flächen (meist alt, bis 2020) |
| News | 29 Beiträge (21 aktiv), zuletzt 11/2025 – kaum genutzt |
| **Weiterleitungen** | **9.046**, davon 6.414 genutzt, **726.000 Aufrufe** – vor allem aus der Umstellung 2025. Ohne sie verliert der Shop Suchmaschinen-Ranking |
| SEO-URLs | 8.488 |
| Mailvorlagen | 47 (41 aktiv) |
| Auswahlassistent | 5 Gruppen, 2 aktiv |
| Plugins mit Inhaltsbezug | FAQ, Trusted Shops, Rechtstexte vom Händlerbund, EU-Cookie, „Angebot anfragen“, Brutto-/Netto-Umschalter, Google-Codes, Upload- und Widerrufsformular, DHL-Wunschzustellung, PayPal Checkout, Google-Shopping-Export |
| Redaktion | 13 aktive Admin-Konten, **in den letzten 90 Tagen nur 2 angemeldet** |

**Bedarf daraus:** Bausteine (Text, Bild, Spalten, Trenner, Button, Überschrift, Video, dazu Banner/Slider, Produktliste, Akkordeon/FAQ), Inhalte auf Kategorie- und Artikelseiten, freie Inhaltsseiten, Seitenleiste/Navigation/Fuß, Banner mit Zeitraum, Weiterleitungen, SEO-Felder, Bilder, Vorlagen und Vorschau. Ein frei gestaltbarer „Pixel-Editor“ ist nicht nötig, denn das Team arbeitet heute schon mit Zeilen und Bausteinen.

### 3.2 Stripe-Shop (Strapi)

Startseite, Navigation, Fuß und Promo-Banner als Einstellungen; 38 Inhaltsseiten (Rechtstexte, Ratgeber, Systembeschreibungen Keso); News, FAQ, Hilfetexte des Konfigurators. Dynamic Zones (Bausteine) gibt es. Neue Seiten anlegen kann aber nur ein Entwickler (Abschnitt 2). Das erklärt die Unzufriedenheit des Teams.

## 4. Die Optionen

| | Option | Kurz |
|---|---|---|
| **A** | Ursprünglicher Plan | Svelte-Frontend + Strapi bleiben, werden auf die .NET-API umgestellt |
| **B** | .NET-Shop mit eigenem Inhaltsmodul | Blazor-Shop + Bereich „Inhalte“ im Admin, Daten in Mongo, Bilder im Blob-Speicher |
| **C** | .NET-Shop + Umbraco | Open-Source-CMS auf .NET (Umbraco 17 LTS auf .NET 10, Support bis 11/2027, Sicherheitsupdates bis 11/2028) als eigene App im AKS mit SQL-Datenbank |
| **D** | .NET-Shop + SaaS-CMS | z. B. Storyblok (visueller Editor), Inhalte per API |
| **E** | Nur JTL-Shop | Kein eigener Shop; endet mit esERP – keine Dauerlösung, aber die **Brücke** für alle Optionen |

### 4.1 Vor- und Nachteile

**A – Svelte + Strapi auf .NET-API**

| Pro | Contra |
|---|---|
| Oberfläche existiert, Team kennt Strapi | Zwei Technik-Stacks (Node/Svelte + .NET) dauerhaft, Svelte-Know-how fehlt im Haus |
| Kein neues Design nötig | SPA ohne Server-Rendering: für SEO müsste auf SvelteKit-SSR umgebaut werden |
| | Preis- und Konfiguratorlogik im Frontend muss entfernt und neu angebunden werden; die Konfiguratoren sind im .NET-Shop schon fertig und geprüft |
| | Strapi: neue Seiten nur mit Entwickler, bis das Modell umgebaut ist; Sicherheitsaufräumen nötig; das Team ist nicht zufrieden |
| | Drei Systeme im AKS (Frontend, Strapi + DB, .NET) |

**B – .NET-Shop mit eigenem Inhaltsmodul (Empfehlung)**

| Pro | Contra |
|---|---|
| Ein Stack, ein Admin, eine Anmeldung (Entra ID wie ES), ein Deployment | Redaktionsfunktionen selbst bauen und pflegen (Vorschau, Versionen, Zeitsteuerung) |
| Genau die Funktionen, die das Team nutzt, so benannt wie im JTL-Shop | Jede spätere Erweiterung ist Eigenentwicklung |
| Inhalte direkt an Kategorie/Artikel gekoppelt, mit Artikelauswahl aus dem Katalog | Kein Drag-&-Drop-Editor auf der Seite selbst wie der OPC (Bearbeiten im Formular mit Vorschau daneben) |
| JTL-Inhalte (OPC-JSON, Seiten, Boxen, Weiterleitungen) automatisch übernehmbar – die Bausteine sind bekannt | Abhängigkeit vom .NET-Team |
| Kein zusätzliches System, keine Lizenz, kaum mehr Speicher im AKS | |

**C – .NET-Shop + Umbraco**

| Pro | Contra |
|---|---|
| Ausgereiftes CMS: Versionen, Vorschau, Zeitsteuerung, Medien, Rollen, Mehrsprachigkeit fertig | Zweites System mit eigener Oberfläche, eigener Benutzerverwaltung und eigener SQL-Datenbank |
| Ebenfalls .NET, im Haus verständlich | Updates/Versionssprünge alle ~2 Jahre (LTS), Fremdcode im Cluster |
| Wächst mit, falls später mehrere Shops/Sprachen kommen | Inhalte an Artikeln/Kategorien nur über Verweise auf Ids – weniger direkt als im JTL-Shop |
| | Zusätzlich 0,5–1 GiB Speicher im AKS |

**D – SaaS-CMS (Storyblok)**

| Pro | Contra |
|---|---|
| Bester visueller Editor (Bearbeiten direkt in der Seitenvorschau), kein Betrieb | Laufende Kosten (Drittquellen: ab etwa 99 $/Monat; genaue Preise nur beim Anbieter) |
| Schnell integriert | Datenstandort nur in den Vertriebs-Tarifen vertraglich garantiert (DSGVO prüfen) |
| | Abhängigkeit von einem Anbieter, Preisänderungen (Kundenklagen 2026 über gekürzte Kontingente) |
| | Inhalte liegen außerhalb, Produktbezug über Ids |

### 4.2 Aufwand im Vergleich (PT)

| Block | A | B | C | D |
|---|---|---|---|---|
| Gemeinsame Livegang-Punkte (Abschnitt 6) | 44–70 | 44–70 | 44–70 | 44–70 |
| Frontend anpassen (A) bzw. Shop-Oberfläche fertigstellen (B–D, in Abschnitt 6 enthalten) | 15–25 | – | – | – |
| SvelteKit auf Server-Rendering umbauen (SEO) | 5–10 | – | – | – |
| Strapi umbauen (Seiten als Sammlung), absichern, im AKS betreiben | 8–13 | – | – | – |
| Inhaltsmodul bzw. CMS-Anbindung | – | 21–31 | 20–30 | 12–18 |
| Übernahme JTL-/Strapi-Inhalte | 3–5 | 5–8 | 5–10 | 5–10 |
| **Summe** | **75–123** | **70–109** | **69–110** | **61–98** |
| Laufend | 3 Systeme, 2 Stacks | 1 System | 2 Systeme + Updates | 1 System + Abo |

Die Summen liegen nah beieinander. Den Unterschied machen der Betrieb, die Zufriedenheit des Teams und die Frage, wie gut die Inhalte zu Artikeln und Kategorien passen. Bei A kommt hinzu, dass schon geprüfte Teile (Konfiguratoren, Preise) im Svelte-Frontend neu angebunden werden müssten.

### 4.3 Inhaltsmodul (Option B) im Detail

| Teil | PT |
|---|---|
| Seiten und Bausteine: Text (mit Formatierung), Überschrift, Bild, Spalten/Zeile, Container, Trenner, Button, Video, Banner/Slider, Produktliste (aus dem Katalog), Akkordeon/FAQ | 8–12 |
| Inhalte auf Kategorie- und Artikelseiten (oben/unten), Seitenleisten-Boxen | 3–4 |
| Navigation, Fuß, Topbar, Promo-Banner mit Zeitraum | 2–3 |
| Medienbibliothek (Azure Blob, Bildgrößen wie im Katalog) | 2–3 |
| Entwurf/Veröffentlichen, Vorschau, Zeitsteuerung, Versionen, Vorlagen | 4–6 |
| Weiterleitungen und SEO-Felder verwalten | 2–3 |
| **Summe** | **21–31** |
| Übernahme: OPC-Seiten → Bausteine, Inhaltsseiten, Boxen, 9.046 Weiterleitungen, Strapi-Seiten | 5–8 |

## 5. Ob und wann

- **Ob:** Ja. Blazor/ASP.NET Core kann Shop und Redaktion tragen, und Meesenburg betreibt diese Technik schon (ES). Kritisch ist nicht die Technik, sondern dass die Redaktion bekommt, was sie im JTL-Shop hatte. Deshalb der Prototyp als erster Schritt.
- **Wann:** Bei etwa 70–110 PT und einem Entwickler sind es je nach Verfügbarkeit 4–6 Monate Kalenderzeit, mit ganzer Kraft weniger. Vorschlag für die Reihenfolge:
  1. **Prototyp „Inhalte“** (2–3 PT) → SN24-Team testet → Entscheidung
  2. **Livegang-Reife** (Abschnitt 6) und Inhaltsmodul parallel
  3. **Probebetrieb** auf einer Test-Domain im AKS: Fachbereich bestellt, Redaktion pflegt echte Inhalte, Last- und Sicherheitstest
  4. **Umschaltung** per DNS; der JTL-Shop bleibt einige Wochen als Rückfall (nur lesend)
  5. **esERP** später über den ERP-Broker (Bericht 07) – der Shop bleibt dabei unverändert

## 6. Gemeinsame Livegang-Punkte (für jede Option)

| Punkt | PT |
|---|---|
| Öffentliche Seiten als statisches Server-Rendering, interaktiv nur Warenkorb, Konfigurator, Kasse. Heute ist der ganze Shop eine dauerhafte Blazor-Verbindung je Besucher: schlechter für SEO, Speicher und Mobilgeräte | 8–12 |
| SEO: URLs wie im JTL-Shop (8.488), Weiterleitungen, Sitemap, Canonical, strukturierte Daten, 404/410 | 5–8 |
| Rechtstexte (Händlerbund-Schnittstelle), Cookie-Einwilligung, Trusted Shops, Google-Codes | 4–6 |
| Zahlung: PayPal direkt (F-20), Stripe live | 4–6 |
| Kundenkonten: Übernahme (K2), Preise je Kundengruppe (K4), Stripe-Shop-Konten (K5) | 6–10 |
| Admin-Anmeldung mit Entra ID und Rollen (Redaktion, Innendienst, Admin) | 2–3 |
| Produktfeed Google Shopping | 2–3 |
| Betrieb: AKS-Deployment, CI/CD, Monitoring, Mongo-Sicherung, Lasttest, externer Sicherheitstest | 8–12 |
| Restpunkte Workshop (F-15 weltweit, F-35, F-36.1, Angebot anfragen …) | 5–10 |
| **Summe** | **44–70** |

## 7. Hosting und Kosten

**AKS heute** (lesend geprüft 04.11.2026): 3 Knoten `Standard_D2ds_v5` (2 vCPU, 8 GB, nutzbar je ~5,6 GiB).

| Knoten | CPU genutzt | Speicher genutzt | Speicher reserviert (Requests) | Speicher-Limits |
|---|---|---|---|---|
| 1 | 11 % | 76 % | 43 % | 477 % |
| 2 | 10 % | **99 %** | 39 % | 269 % |
| 3 | 9 % | 82 % | 30 % | 697 % |

- Die CPU ist fast frei, der **Speicher ist voll**. ES belegt 8,2 GiB, das System 2 GiB.
- Die Reservierungen sind zu niedrig angesetzt, die Limits weit überbucht. Der Scheduler packt die Knoten deshalb voll, und unter Last drohen Abstürze wegen Speichermangel (OOM).
- **Sofort-Hinweis für ES:** Das Aspire-Dashboard läuft produktiv und belegt allein 1,8 GiB. `es-api-sapdata` belegt bis 1,5 GiB.

**Bedarf des Shops** (Schätzung): API 2×, Portal 2× und Worker 1×, je 300–600 MiB, zusammen etwa 2–3 GiB. Mit Umbraco (Option C) kämen 0,5–1 GiB dazu. Empfehlung:
- einen **eigenen Knotenpool** für den Shop anlegen, z. B. 2 × D2ds_v5 oder 1 × D4ds_v5. Das trennt den Shop von ES und erlaubt feste Sitzungen für Blazor.
- die Reservierungen in ES realistisch setzen.
- Die Kosten liegen je D2ds_v5 grob um 100 € im Monat (ohne Rabatte). Bitte mit dem Azure-Preisrechner bzw. dem eigenen Vertrag prüfen.

**Server:**

| Server | Heute | Empfehlung |
|---|---|---|
| node1 (Hetzner) | Stripe-Shop (offline, Container laufen), **unser SSH-Weg zur Wawi** (ecomdata gibt nur node1 frei) | Nach dem Livegang kündigen. Vorher die feste Ausgangs-IP des AKS (NAT-Gateway) bei ecomdata freischalten lassen. Sicherungen für die Forensik liegen vor (Image 09.10.2026) |
| node2 (Hetzner) | Testinstanz der Freelancer, **spricht mit dem Produktiv-JTL** | Sicherheitsrisiko – unabhängig von der Entscheidung abschalten |
| node3 (Hetzner) | Projekt „configurators“ (Univelop) | Klären, ob noch gebraucht; sonst kündigen |
| Timme (JTL-Shop, SMTP) | Live-Shop | Bis einige Wochen nach der Umschaltung behalten (Rückfall, Export der Bestellhistorie), dann kündigen. Mails laufen künftig über Microsoft Graph |

Den Stripe-Shop auf einen anderen Server (z. B. Timme) umzuziehen, lohnt nicht. Er wird ersetzt, und nur die Sicherungen bleiben.

## 8. Fragen an das SN24-Team (vor der Entscheidung)

1. Welche Inhalte legt ihr wirklich selbst an? Inhaltsseiten, Texte auf Kategorie-/Artikelseiten, Banner auf der Startseite, Aktionen mit Zeitraum, News?
2. Was war im JTL-Shop besonders gut? Das Bearbeiten direkt auf der Seite (OPC), die Vorlagen, die Vorschau?
3. Was stört an Strapi am meisten? Keine neuen Seiten, die Oberfläche, die Vorschau?
4. Braucht ihr eine Freigabe (einer schreibt, ein anderer veröffentlicht) oder Zeitsteuerung?
5. Wer kümmert sich um SEO, Weiterleitungen und Google Shopping?
6. Wie viele Personen pflegen, wie oft? (JTL: 2 aktive Konten in 90 Tagen)
7. Mehrsprachigkeit oder weitere Shops in Sicht? Falls ja, spräche das eher für Option C.

## 9. Entscheidungskriterien

- **B wählen**, wenn das Team mit Bausteinen, Formular und Vorschau gut arbeiten kann (Prototyp bestätigt) und keine großen neuen Redaktionswünsche kommen.
- **C wählen**, wenn Mehrsprachigkeit, mehrere Sites, Freigabe-Workflows oder viele Redakteure absehbar sind.
- **D wählen**, wenn das Bearbeiten direkt in der Seite zwingend ist und Abo-Kosten und Datenstandort akzeptabel sind.
- **A** nur, wenn das Svelte-Design unbedingt bleiben muss. Dann aber mit SSR-Umbau, Strapi-Umbau und dauerhaft zwei Stacks.

## 10. Prototyp „Inhalte“, Admin-Umbau und Berechtigungen (umgesetzt 04.11.2026)

**Inhalte (Option B als Prototyp):**

| Teil | Stand |
|---|---|
| Bausteine | Überschrift, Text (Formatierung, Listen, Links; HTML wird gesäubert), Bild (Bibliothek oder https), Spalten (2–4, darin Bausteine), Button, Trenner, Video (nur YouTube ohne Cookies und Vimeo), Produktliste (Artikelnummern, Suche im Katalog), Banner |
| Stellen | Eigene Seite `/seite/{adresse}`, Startseite oben/unten, Kategorieseite oben/unten, Artikelseite unten |
| Ablauf | Entwurf und veröffentlichter Stand getrennt; „Entwurf speichern“ ändert nichts im Shop; Veröffentlichen prüft Adresse und Ziel; Zeitraum (sichtbar ab/bis); Versionen (letzte 20, als Entwurf zurückholbar); Zurückziehen; Vorschau im Shop-Layout |
| Editor | Links Einstellungen und Bausteine (verschieben, kopieren, löschen), rechts Live-Vorschau mit denselben Komponenten wie im Shop |
| Bilder | Bibliothek mit Hochladen (bis 8 MB), Größen wie Artikelbilder; im Prototyp in Mongo, im Betrieb Azure Blob Storage |
| Übernahme JTL | Export `topcpage` + `tlink/tlinksprache` → 237 Inhalte (Kategorie- und Artikelseiten oben/unten, 70 eigene Seiten), 198 veröffentlicht wie im JTL-Shop; nicht übernommen nur 23 Bausteine (13 Popup-Plugin, 10 im JTL-Shop ausgeblendet). Wiederholbar. Bilder und Links zeigen vorerst auf den JTL-Shop |

**Admin-Umbau:** Menü nach Bereichen (Verkauf, Inhalte, Katalog, System) und Berechtigungen, Startseite mit Werkzeug-Kacheln je Bereich, Profilmenü (Name, „Mein Zugriff“, Abmelden), Anmeldeseite, Seite „Kein Zugriff“ mit der fehlenden Berechtigung – Design weiter nach dem SiSe-Portal-Guide.

**Berechtigungen:** Neues Projekt `src/Meesenburg.Authorization` als Vorstufe eines gemeinsamen Pakets mit dem SiSe-Portal (README dort): gleiches Datenmodell, gleiche Collections, Claims und Policies wie `Meesenburg.ES.Authorization`, neu ein erweiterbarer Katalog, Startrollen von der Anwendung und Auflösung nur für Mitarbeiter (nicht für Kunden-Cookies). Startrollen: Redaktion (Entwürfe), Redaktion, Innendienst, Shop-Betrieb, Shop-Administration; Entra-Gruppen `grp_mee_sise_shop_content|sales|operations|admin`, Entwickler `grp_mee_sise_developer`. Anmeldung mit Entra ID, sobald es eine App-Registrierung gibt; lokal Test-Personen. Die Admin-API der Shop-API nimmt nur noch Anfragen mit internem Schlüssel an.

**Geprüft (lokal):**
- Redakteurin sieht nur „Inhalte“; Startseiten-Aktion angelegt, als Entwurf gespeichert (Shop unverändert), veröffentlicht (Shop zeigt sie).
- Innendienst bekommt bei „Berechtigungen“ den Hinweis „Kein Zugriff“ mit der fehlenden Berechtigung.
- Die Admin-Person weist eine Rolle persönlich zu. Die Person mit „Redaktion (Entwürfe)“ kann bearbeiten, aber nicht veröffentlichen.
- Übernommener OPC-Inhalt erscheint auf der Kategorieseite „DORMAKABA penta cross“.
- 249 Tests.

**ES-Portal als Admin des Shops?** Ja, aber nicht jetzt. Erst das gemeinsame Paket (eine Rechte-Datenbank, eine Rollenverwaltung), dann entscheiden, ob die Shop-Seiten ins ES-Portal wandern. Für den Livegang ist der eigene Shop-Admin einfacher: eigenes Deployment, Shop-Daten nahe der Shop-API, keine Kopplung an ES-Releases. Im ES-Portal reicht eine Kachel „Shop-Admin“.

**Offen:**
- Entra-App-Registrierung und die Gruppen `grp_mee_sise_shop_*`.
- Shop-Rechte im SiSe-Portal verwalten (gemeinsames Paket).
- Zugriffsanfragen wie im SiSe-Portal.
- Inhalte oben auf Artikelseiten: Der OPC nutzt meist den Bereich vor der Kurzbeschreibung, im Prototyp erscheinen sie unten.
- Produktlisten nach Kategorie.
- SEO-Adressen und Weiterleitungen (danach relative Links), Bilder in den Blob Storage übernehmen.
- Freigabe-Ablauf, falls gewünscht.

## 11. Gestaltung (04.11.2026)

Für den öffentlichen Shop gab es bisher keine eigene Richtlinie, nur „Farben wie das SiSe-Portal“. Dabei war für Knöpfe das Orange-Rot `#e24312` übrig geblieben, das im SiSe-Guide nur für Fehler gilt. Neu: `src/Meesenburg.Shop.Portal/DESIGN_SHOP.md` (Markenblau für Knöpfe und Banner, Gelb `#f8c301` nur als Hervorhebung wie im Stripe-Shop, Rot nur für Fehler).

**Umgesetzt (Empfehlung B):**
- Knöpfe blau.
- Banner in drei Varianten (groß mit Bild, Bild und Text, Aktionsleiste), mit gelber Hervorhebung.
- Neue Typografie der Bausteine.
- Übernommene JTL-Inhalte bereinigt: ohne Fremdfarben, Schriftgrößen und Zentrierungen, harte Umbrüche entfernt.

**Für die Fachabteilung** (`analyse/workshop/design/`):
- `Vorschlag_1_Farben_Knoepfe.png`: Varianten A (heute), B (Empfehlung) und C (Gelb als Kaufknopf).
- `Vorschlag_2_Banner.png`
- `Vorschlag_3_JTL_Inhalte_vorher_nachher.png`
- `Vorschlag_4_Startseite_umgesetzt.png`

Die Umstellung auf C ist eine Zeile CSS.

### 11a. Übernommene Inhalte überarbeitet (04.11.2026)

Alle 237 aus dem JTL-Shop übernommenen Inhalte sind im .NET-Shop neu gegliedert (gleiche Fakten, keine Ergänzungen):

| | Anzahl |
|---|---|
| Kategorie-Inhalte und eigene Seiten | 140 (114 wieder veröffentlicht, 13 waren Entwurf) |
| Artikel-Inhalte | 97 (Sicherheitsstufe als Kachel neben dem Bild, 78 veröffentlicht) |
| Bilder mit eingebranntem Text ersetzt | ca. 110 (Sicherheitsstufen, Schlüsselformen, Farbcodes, Systemleisten, Schließmatrizen → Tabelle/Kennzahlen; Text-Banner → Banner-Baustein) |
| Veraltet, aus dem Shop genommen | 17 (Weihnachts- und Gutscheinaktionen 2018–2022, „patentiert bis 2023“, JTL-Demo-Inhalte) |

**Aufbau:**
- Kategorie oben: Einleitung und Kennzahlen, Details unten bzw. im Akkordeon. Beispiel: penta cross oben von 97 auf 6 Bausteine.
- FAQ: von 143 doppelten Fragen auf 112 in 6 Themen.
- Schließplan-Bildschirmfotos ersetzt durch „Schließanlage online planen“.

**Ablage:**
- Daten in `analyse/workshop/inhalte/daten/`.
- Einspielen mit `tools/inhalte_einspielen.py`. Die vorige Fassung bleibt als Version im Admin.

**Prüfung:** `analyse/workshop/Pruefliste_Inhalte.md` für die Fachabteilung. Wiederkehrend:
- alte Links auf sicherheitstechnik-nord.de
- widersprüchliche Lieferzeiten und Telefonnummern
- aus Bildern abgelesene Farbnamen
- fehlende Fußnoten
- neu gesetzte Knopf-Ziele

### 11b. SEO wie im JTL-Shop (05.11.2026)

**Titel und Meta-Angaben:**
- Seitentitel und Meta-Angaben stammen wie im JTL-Shop aus der Wawi.
- Der Titel ist der SEO-Titel oder der Artikelname, ohne den Zusatz des Shopnamens.
- Die Beschreibung beginnt mit dem Präfix „Meesenburg Sicherheit und Service Onlineshop“.

**Adressen:**
- Sie sind die JTL-Adressen, flach unter `/` und ohne `/shop/` und Zahl. Beides waren Überbleibsel aus dem Strapi-/Stripe-Shop.
- Übernommen: 6.804 Pfade und 8.916 Weiterleitungen.
- Alte Adressen leiten per 301 weiter.

**Was noch fehlt:**
- Merkmal-Filterseiten, Suchseiten, News und einige JTL-Sonderseiten gibt es noch nicht (siehe TODO).
- 5.346 alte JTL-Weiterleitungen zeigen auch im JTL-Shop ins Leere.

**Angebote:** Angebote sind rot, das hat das Marketing freigegeben.

