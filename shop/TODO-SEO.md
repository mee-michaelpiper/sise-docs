# TODO SEO – Shop (Frontend + Backend)

Angelegt 23.11.2026. Gemeinsame Liste für beide Prüflisten von C. Feller:
- **Frontend / HTML-Auslieferung** (17.11.): Bericht `analyse/25_pruefliste_html_auslieferung.md` – B-1 bis B-8 am 19.11. erledigt.
- **Backend / Steuerbarkeit im Admin** (20.11.): Bericht `analyse/27_pruefliste_seo_backend.md` – 32 Muss, 18 Soll; 13 vorhanden, 1 per Konfiguration, 35 Entwicklung, 1 entfällt.

Umsetzung erst nach Freigabe. Schätzungen grob (PT = Personentage).

## 0. Vorher klären (blockiert P1, P2, P5)
- [x] Wo pflegt das SN24-Team Artikel-/Kategorie-SEO? **Entschieden 23.11.2026 (M. Piper): Die Wawi zählt. Der Admin liest aus der Wawi und schreibt Änderungen per REST zurück** – jeder ändert dort, wo er gerade ist. Nur Felder, die die Wawi nicht kennt (H1, robots, Canonical, Alt-Text, Sitemap-Ausschluss), liegen im Shop (Mongo)
- [ ] **24.11.2026: App-Registrierung mit SN24** (Wawi-Part macht jemand von SN24: Admin → App-Registrierung starten, Anfrage bestätigen; Skript `~/repos/tools/jtl-tools/siseshop/jtl_app_register.py` schickt die Anfrage und speichert den Schlüssel). Danach Testversion im AKS umstellen, dann alte App `icsWebshop/v1` löschen
- [ ] **App-Registrierung neu mit Schreibrechten** (Schreibtest 23.11.: HTTP 401, J-43): Scopes `item.updateitemdescription`, `category.updatecategorydescription`, `property.updatepropertyvaluedescription`, ggf. `item.updateitemimage` – zusammen mit dem Token-Tausch (Bericht 12, 3.4); danach Schreibtest wiederholen (`jtl_rest_seo_writetest.py`)
- [ ] Kleinschreibung aller Adressen gewünscht? (6.804 JTL-Adressen gemischt geschrieben, alle bräuchten 301) – **zurückgestellt 23.11., M. Piper lässt prüfen**
- [x] Filterseiten: **ja, sollen von Google aufgenommen werden** (23.11., M. Piper) – generisch, siehe P5a
- [x] **GA4 direkt, kein GTM** (23.11.; Agentur schickt Codes, bekommt keinen Admin-Zugang)
- [ ] Search Console: auf `meesenburg.com` steht schon ein TXT `google-site-verification=…` → es gibt vermutlich bereits eine Domain-Property, die `sicherheit.` mit abdeckt. Klären, wem sie gehört; Meesenburg als Inhaber, Agentur als Nutzer
- [x] Downloads je Artikel: **aus der Wawi, sonst eigene** (23.11.). Wawi hat 6.146 PDFs an Artikeln (`tMedienDatei`, J-42) → P6
- [ ] Wo liegen die Mediendateien physisch (JTL-Shop `mediafiles/`?) – vor P6 prüfen
- [ ] Frontend B-9: Banner mit `href="#"`, Links auf `sicherheitstechnik-nord.de` (SN24-Team, Admin „Seiten & Inhalte“)
- [ ] Frontend B-10: GA4/Search Console, Stripe-Shop-Adressen 15.06.–14.10., Zugang für den Kontroll-Crawl

## P1 SEO-Grundlage – zuerst (≈ 3–4 PT) – **umgesetzt 23.11.2026 (lokal getestet, nicht deployt)**
Stand: `SeoComposer` (Regeln, 14 Tests), `SeoMetaService` (Mongo `seo_settings`, `seo_pages`, 30 s Cache), `SeoAdminService`, API `/api/v1/admin/seo/settings|pages`, Kopf über `SeoMetaDto` in Artikel-, Kategorie-, Hersteller-, Inhalts- und Startseite; Admin-Seite **„SEO“** (`/shop-admin/seo`, Recht `shop.seo.manage`) mit Google-Vorschau, Zeichenzähler, Vorlagen mit Live-Beispiel. Wawi-SEO jetzt auch für **Kategorien (109) und Hersteller (1)** – vorher Titel = Name. Pakete Contracts 1.1.0, Admin 0.2.0.
Eine Stelle, aus der Kopf, Sitemap und Admin lesen. Alles Weitere baut darauf auf.
- [x] Mongo `seo_pages` (statt `SeoOverride`) je Seite (Artikel, Kategorie, Hersteller, Inhaltsseite, Startseite): Title, Description, H1, robots (index/noindex, follow/nofollow), Canonical, og:Bild, „nicht in Sitemap“
- [x] Vorlagen je Seitentyp mit Platzhaltern (`{Produktname} – {Marke} | Meesenburg`); Reihenfolge: Admin → Wawi → Vorlage
- [ ] Felder, die die Wawi kennt (Titel, Beschreibung, Schlüsselwörter, Adresse – Artikel und Kategorien), **zurück in die Wawi schreiben** (`PATCH …/descriptions`, J-41) statt in Mongo zu überschreiben: eine Quelle, Rückfall-JTL-Shop zeigt dasselbe. Vorher Schreibtest an einem Artikel (nur nach OK, Schreibsperre)
- [x] `ShopSeo`/`ShopHead` lesen daraus; Sitemap lässt noindex, fremd-kanonische und ausgeschlossene Seiten weg
- [x] Tests für die Auflösung (reine Logik): `SeoComposerTests`
Deckt: 1.1–1.5, 1.7, 3.1, 3.2, 6.2, 6.5

## P2 SEO-Bearbeitung im Admin (≈ 2–3 PT, nach P1)
- [x] Admin „SEO“ für Artikel, Kategorie, Hersteller, Inhaltsseiten, Startseite (Suche nach Seite, dann Formular) – 23.11.
- [ ] Titel/Beschreibung/Schlüsselwörter im Admin bearbeiten → per REST in die Wawi (nach App-Registrierung, J-43); heute nur Anzeige mit Hinweis „in der Wawi ändern“
- [x] Zeichenzähler + Snippet-Vorschau (Google-Ansicht) auf der Seite „SEO“ (23.11.) – im Inhalte-Editor noch offen
- [ ] Adresse (Slug) überschreibbar; Änderung legt automatisch 301 an – auch bei Inhaltsseiten
- [ ] Vorschau vor dem Speichern (wie bei Inhalten)
- [ ] Portal-Suche/Neuigkeiten mitdenken
Deckt: 1.6, 2.1, 2.2, 8.1

## P3 Weiterleitungen ausbauen (≈ 2 PT, unabhängig) – **umgesetzt 23.11.2026 (lokal getestet, nicht deployt)**
Befund beim Test: **≈ 180 der 8.916 aus JTL übernommenen Weiterleitungen hängen in Schleifen** (z. B. `/kaba-expert-plus-doppelzylinder-kurz_1` ↔ `_2`, `/bks-detect3` ↔ `/BKS-detect3000`) – Besucher bekommen dort 410. In der Liste jetzt rot „Schleife“; bereinigen per CSV (Export → Ziel korrigieren → Import).
- [x] Status 410 manuell wählbar (Auswahl 301/302/410 statt Checkbox)
- [x] CSV-Import/-Export (mit Probelauf: neu/geändert/unverändert/Problem je Zeile; Export Semikolon, UTF-8 mit BOM)
- [x] Spalte „Ziel geprüft“ (ok, Kette, Schleife, Ziel fehlt, extern); Schleifen werden beim Speichern und Import abgelehnt
- [x] Verlauf inkl. Löschen und Import (Mongo `seo_redirect_history`, Reiter „Verlauf“) – ab 23.11.2026, ältere Änderungen nicht nachträglich
- [x] Eigene Berechtigung `shop.seo.manage` „SEO pflegen“ (23.11., Rolle „Redaktion“) – Weiterleitungen nutzen noch `Content.Publish`
- [x] Weiterleitungen bearbeiten: `shop.content.publish` **oder** `shop.seo.manage` (23.11.)
- [ ] Die ≈ 180 Schleifen bereinigen (SN24-Team bzw. C. Feller: richtiges Ziel je Adresse) (Seiten per Berechtigung schaltbar)
Deckt: 2.3, 2.4, 2.5, 8.2, 8.3

## P4 Produktlebenszyklus (≈ 1,5 PT, nach P3) – **umgesetzt 23.11.2026 (lokal getestet, nicht deployt)**
Befund: lokal 142 Artikel in den letzten 90 Tagen aus dem Shop genommen, **102 Kategorien aus dem JTL-Shop gibt es nicht mehr** – die endeten bisher mit 404, jetzt 301 auf die nächste sichtbare Oberkategorie (z. B. `/Lupus-Electronics` → `/ueberwachungstechnik`).
- [x] Feld Nachfolgeartikel (Admin „SEO“ → Artikel), setzt die Weiterleitung automatisch; Nachfolger muss im Shop sichtbar sein
- [x] Verhalten bei entferntem Artikel wählbar: automatisch (wie bisher) / Nachfolger / Kategorie / 410 – vorher festlegbar und nachträglich
- [x] Entfernte/deaktivierte Kategorie: automatisch 301 auf die nächste sichtbare Oberkategorie (sonst 410); eigene Weiterleitung hat Vorrang. Liste „SEO → Aus dem Shop genommen“ mit dem jeweils geltenden Ziel (Verschieben ändert die Adresse nicht – flache Pfade)
Deckt: 7.2, 7.3, 7.4

## P5 Indexierung & Adressen (≈ 2 PT, nach P1 und den Klärungen) – **umgesetzt 23.11.2026 (lokal getestet, nicht deployt)**
- [x] robots.txt: feste Zeilen + Ergänzungen im Admin (SEO → Vorlagen), geprüft (nur User-agent/Disallow/Allow/Crawl-delay/#, kein „Disallow: /“); Testzugang ersetzt weiterhin alles
- [x] Filter je Merkmal indexierbar → P5a (Merkmal-Seiten je Merkmal an/aus)
- [x] Sitemap-Index `/sitemap.xml` → `/sitemap-{seiten|kategorien|hersteller|artikel|merkmale|groessen}.xml`; lastmod nur bei bekannter Wawi-Änderung (Artikel) bzw. Veröffentlichung (Inhalte), sonst keins
- [x] Neue Adressen bei Namensgleichheit „-2“, „-3“ statt `-{id}` (bestehende bleiben); Schrägstrich am Ende → 301 auf kanonisch (seit B-7)
- [ ] Kleinschreibung (zurückgestellt, siehe oben)
Deckt: 2.6, 2.7, 3.3, 3.7, 6.3, 6.4

## P5a Landeseiten für Merkmale und Größen – generisch (≈ 3–4 PT, nach P1) – **umgesetzt 23.11.2026 (lokal getestet, nicht deployt)**
Lokal: 713 Merkmalwerte (192 mit Artikeln, 130 in der Google-Suche), 288 Größen-Seiten (171 ab 3 Artikeln). Technische Daten auf der Artikelseite jetzt mit **allen** Wawi-Merkmalen (vorher 3) und Links auf die Merkmal-Seiten.
Keine Seite von Hand: Der Shop erzeugt sie aus den Wawi-Daten, Titel/Beschreibung/H1 über Vorlagen (P1), Text optional aus der Wawi (`cBeschreibung`/`cMetaTitle`/`cMetaDescription` am Merkmalwert, J-44).
- [x] **Merkmalwert-Seiten:** eine Adresse je Merkmalwert, Adresse wie JTL gebildet (cSeo aus der Wawi, sonst aus dem Wert, bei Belegung Merkmal davor)
- [x] **Exakte JTL-Adressen der Merkmalwerte** (686) übernommen – lokal 23.11.2026 (J-46). **Nach dem Deploy auf Test/Live wiederholen:** `SHOP_KEY=… SHOP_API=…/api/v1/admin/seo/jtl-import python3 ~/repos/tools/jtl-tools/siseshop/jtl-shop/merkmal_seo_uebernehmen.py`, dann Stammdaten-Abgleich
- [x] **Größen-Seiten aus den Variationen:** z. B. „Doppelzylinder 30/35 mm“ = Zylindertyp × Außen-/Innenlänge, listet alle Zylinder, die es in dieser Größe gibt, mit Preis für genau diese Größe; Link führt auf den Artikel mit vorgewählter Größe
- [x] Je Merkmal im Admin schaltbar (Standard aus: Sicherungskarte, Empfehlungsgrad, Sicherheitslevel); ab N Artikeln (Standard 3) indexierbar und in der Sitemap, sonst noindex
- [x] Interne Verlinkung: Technische Daten → Merkmal-Seite; Zylindertyp-Seite → alle Größen; Größen-Seite → benachbarte Größen
- [ ] Wawi-Pflege: Wert „Doppelzylinder“ hat cSeo/Titel „KESO Doppelzylinder“ → Adresse `/KESO-Doppelzylinder` listet auch Kaba-Zylinder (SN24-Team prüfen)
- [ ] Admin im Browser ansehen: „SEO → Vorlagen“ (Merkmal-/Größen-Seiten, robots.txt) und „SEO → Artikel“ (Bilder mit Alt-Text, Downloads an/aus) – beim Test war der Chrome-Tab im Hintergrund, nur per API geprüft
- [ ] Vorher mit C. Feller: welche Größen-Suchanfragen am häufigsten sind (Search Console) und welche Adressform

## P6a Bilder (≈ 2 PT, nach Entscheidung „nur Shop-Bilder“) – **umgesetzt 23.11.2026 (lokal getestet, nicht deployt)**
Heute: Bilder kommen aus der Wawi (REST `/items/{id}/images`), liegen nach dem Abgleich in Mongo (GridFS) und werden als `/img/{id}?w=…` (WebP) ausgeliefert. Gezeigt werden **alle** Bilder des Artikels, zuerst die dem Shop-Kanal zugeordneten, dann nach Sortiernummer (`CatalogService.cs:155`). Der JTL-Shop zeigt nur die Bilder des Shop-Kanals.
- [x] Nur Bilder des Shop-Kanals (wie JTL-Shop, Rückfall auf andere, wenn keine) – Steuerung bleibt in der Wawi (Kanal-Zuordnung + Reihenfolge)
- [x] Reihenfolge aus dem Shop-Kanal nehmen (greift je Artikel mit dem nächsten Artikel-Abgleich) (heute Sortiernummer des ersten Eintrags, egal welcher Kanal – vermutlich Abweichungen, prüfen)
- [x] Sprechende Adressen: `/img/{id}/{artikel-adresse}-{n}.webp` (Seite, Karten, og:image, JSON-LD und Google-Feed als .jpg) (Wawi-Bildnamen taugen nicht: „unbekannt“, „DZ“, J-42); alte Adressen bleiben gültig
- [x] Alt-Text: Artikelname, weitere „– Ansicht n“, im Admin je Bild überschreibbar (SEO → Artikel)
- [x] **Extras je Artikel im Admin an/aus** (Wunsch M. Piper 23.11.): Liste der Wawi-Bilder und -Mediendateien des Artikels mit Schalter „im Shop zeigen“; Standard = Wawi-Kanalzuordnung
- [x] Start: **PDF-Datenblätter und Maßskizzen als Downloads** („Extras“ am Artikel, mit Namen aus der Wawi); Dateien einmalig aus dem JTL-Shop `/mediafiles/` kopieren (J-45), danach bei Änderung nachziehen
- [x] Technische Daten möglichst vollständig (P5a: alle Merkmale) (Merkmale + Maße aus der Wawi) – je mehr, desto besser für SEO

## P6 Inhalte & Medien (≈ 3 PT, unabhängig) – **umgesetzt 23.11.2026 (lokal getestet, nicht deployt)**
- [x] Alt-Text je Artikelbild pflegbar (Fallback Artikelname, auch Zusatzbilder statt `alt=""`)
- [x] HTML-Bereinigung: keine Inline-Styles, keine leeren Elemente, H1 im Text → H2
- [x] FAQPage-JSON-LD aus dem Akkordeon-Baustein (überall, wo er steht)
- [x] Downloads je Artikel aus den Wawi-Mediendateien (279 Dateien hinter 23.987 Zuordnungen; je Stammdaten-Abgleich bis 100 kopiert, sonst beim ersten Abruf) – eigene Uploads im Admin: offen (PDF, Name aus `tMedienDateiSprache`, z. B. „Flyer“); eigene Uploads im Admin als Ergänzung
- [x] Flash-Videos (.flv) nicht übernehmen
- [x] AggregateOffer bei „ab“-Preisen, GTIN 8/12/13/14 (AggregateOffer lokal nicht live gesehen – kein Artikel mit ab-Preis in der Stichprobe)
Deckt: 4.2, 4.6–4.9, 5.1

## P7 Google & Betrieb (≈ 1,5–2 PT, unabhängig) – **umgesetzt 23.11.2026 (lokal getestet, nicht deployt)**
- [x] ~~GTM-Container-Id, Search-Console-Meta-Tag~~ entfällt: GA4 direkt, Search Console per DNS (23.11.)
- [ ] DNS zum Livegang (Zone bei der Telekom): `sicherheit.meesenburg.com` A heute `178.63.45.204` (TTL 3600) → Ingress-IP des AKS; TTL vorher auf 300 senken. Search Console: kein neuer Eintrag nötig, falls die bestehende Domain-Property passt
- [x] Organization pflegbar (SEO → Vorlagen → Firmenangaben: Name, Mutter, Adresse, E-Mail, Telefon, Logo, Profile)
- [x] `google_product_category` je Kategorie (SEO → Seiten → Kategorie) mit Vererbung auf Unterkategorien/Artikel, Standard in den Vorlagen
- [ ] Google-Kategorien zuordnen (SN24-Team / C. Feller, Liste: taxonomy-with-ids.de-DE.txt)
- [x] Reiter „SEO → Google-Prüfung“: ruft Mail:ShopBaseUrl von außen ab (Anmeldung/Testzugang, X-Robots-Tag, robots-Meta, robots.txt, Sitemap) – Prüfschritt im Livegang: alles grün
- [x] Dateien (Bilder, Downloads, Inhaltsbilder) aus Mongo/GridFS in Azure Blob (24.11.2026): Speicherkonto `meesenburg`, Container `sise-shop` (privat), Test unter `test/`; `Files:Provider` Mongo|Blob, Worker kopiert beim Start Fehlendes; erzeugte Größen der Inhaltsbilder werden jetzt auch abgelegt. Container `sise-shop` (umbenannt, damit keine Verwechslung mit dem Shop der Großhandels-KG) und Rollen angelegt 24.11.; offen: im Test prüfen
Deckt: 5.3, 5.4, 8.4, 8.5

## Test im Testbetrieb (23.11.2026, nach Deploy Shop 0.1.8/0.1.9)
- [x] Testzugang dicht: `/`, Sitemaps → Anmeldung, überall `X-Robots-Tag: noindex`, `robots.txt` = `Disallow: /`
- [x] Artikelseite: Titel/Beschreibung aus Vorlage, Canonical, 1× h1, keine `style`-Attribute, Product-JSON-LD mit gtin13, Marke, Offer, Bilder mit sprechenden `.jpg`-Adressen, Galerie-Alt-Texte „– Ansicht n“, Bilder `/img/{id}/{name}.webp` 200
- [x] Sitemap-Index mit 6 Teilen, Artikel-Teil 4.415 Adressen; 404 für Unbekanntes
- [x] `og:image` mit Namen und als JPEG 1200 px (`/img/{id}/{slug}.jpg?w=1200`) – lokal geprüft, kommt mit dem nächsten Shop-Deploy
- [x] Testbetrieb neu befüllt (seed.sh um 5 Collections erweitert, lokale Mongo jetzt mit TLS): Merkmal- und Größen-Seiten, `?auswahl=`-Vorauswahl, Downloads (PDF/Bilder 200) im Shop geprüft
- [x] Admin im Portal (es-web v0.6.85): alle vier SEO-Reiter, Artikel bearbeiten (Download aus-/einblenden wirkt sofort, „Auf Standard zurücksetzen“), Weiterleitungen (410 anlegen → Shop 410, löschen, Verlauf), Google-Prüfung „gesperrt“
- [x] Behoben (Admin 0.2.1): Link zur Google-Kategorieliste sah aus wie Text; robots.txt-Hinweis (Sitemap-Zeile kommt ans Ende); Google-Prüfung meldete „Startseite ohne noindex“ grün, obwohl die Startseite hinter dem Testzugang gar nicht lesbar war
- [x] Behoben: „Aus dem Shop genommen“ brauchte im Testbetrieb ~50 s (Einzelabfragen je Eintrag übers Netz) → Weiterleitungen einmal laden, Ziele 8-fach parallel; im Test danach 14,6 s (24.11.2026). Hauptursache: alle Kategorien (1 MB) je Eintrag neu geladen → 1 Minute im Speicher (lokal 0,87 → 0,2 s, Ergebnis identisch); Reiter wechselt sofort, alte Liste bleibt beim Neuladen stehen (Admin 0.2.2) – Zeit im Test nach Deploy prüfen
- [x] (24.11.2026, lokal geprüft: mit `?auswahl=` 105,04 €, ohne „ab 97,90 €“) UX: Größen-Seite nennt den Preis für genau diese Länge (z. B. 105,04 €), die Artikelseite zeigt trotz Vorauswahl „ab 97,90 €“ – Preis der gewählten Varianten im Preiskasten anzeigen (gilt für alle Variantenartikel, bestand schon vorher)
- [x] (24.11.2026: Merkmal entfällt, wenn der Wert damit beginnt – Titel „Sicherheitsstufe 1“) Vorschlag: Merkmal-Vorlage `{Name} – {Merkmal}` ergibt „Sicherheitsstufe 1 – Sicherheitsstufe“ – Merkmal weglassen, wenn der Name damit beginnt (oder Vorlage auf `{Name}` ändern)
- [x] (24.11.2026: 410-Seite „Diese Seite gibt es nicht mehr“, Lösch-Dialog ohne 410-Rat bei 410, Reiter ohne „(0)“ beim Laden – auch „Ins Leere“) Kleinigkeiten: 410-Seite zeigt „Seite nicht gefunden“ statt „gibt es nicht mehr“; Lösch-Dialog rät bei einer 410 zu „410 ist besser als Löschen“; Reiter „Weiterleitungen (0)“ bis zum ersten Öffnen
- [x] „Ins Leere“: `/mailto` 9 Aufrufe – kein solcher Link gefunden (Start, Kontakt, Impressum, Merkmal-/Artikelseite, alle Mongo-Inhalte am 24.11.2026 durchsucht); Verweis zeigt auf sich selbst → vermutlich Robot/Handaufruf, im Admin ignorieren
- [x] Im Test fehlten Inhaltsbilder (`/medien/…` ins Leere): seed.sh übertrug `content_media` nicht → ergänzt (24.11.2026); mit Blob übernimmt sie der Worker

## Entfällt
- hreflang (3.8): Shop einsprachig
