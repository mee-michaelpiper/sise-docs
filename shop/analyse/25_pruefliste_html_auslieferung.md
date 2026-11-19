# 25 Prüfliste „Serverseitige HTML-Auslieferung“ gegen den .NET-Shop

Stand 18.11.2026. Grundlage: Prüfliste von C. Feller (17.11.2026, für sicherheit.meesenburg.com). Abgehakte Fassung mit Kommentar je Punkt:
`anhang/Prüfliste HTML-Auslieferung – geprüft .NET-Shop 2026-11-18.pdf`.

## Wie geprüft
- **Was:** der lokale .NET-Shop, gleicher Code wie die Testversion. Abgerufen ohne JavaScript, mit dem User-Agent des GPTBot. Werkzeug: `~/repos/tools/ops-tools/siseshop/seo_html_check.py`.
- **Seiten:**
  - Startseite, Kategorie und Unterkategorie;
  - Produkte mit und ohne Variante, ein entfernter Artikel;
  - 404-Seite, Suche, `?page=2`;
  - je 300 zufällige Adressen aus der Sitemap und aus den alten JTL-Adressen.

## Ergebnis
**22 erfüllt, 14 teilweise, 3 offen, 2 Livegang-Planung.** Das Grundprinzip ist erfüllt: Alle öffentlichen Seiten werden auf dem Server gerendert (statisches SSR). Inhalte, Links, Metadaten und JSON-LD stehen schon im ersten HTML, und Bots sehen dasselbe wie Browser. Die offenen Punkte sind Einzelfehler, kein Architekturproblem.

**Gut:**
- **Sitemap:** 4.733 URLs, in der Stichprobe 0 % fehlerhaft. Die Liste bemängelt beim JTL-Shop über 12 %.
- **Statuscodes:** 404 für unbekannte Adressen, 503 mit `Retry-After` im Wartungsmodus.
- **Produkt-JSON-LD** mit Offer, GTIN und MPN.
- **Bilder** mit `alt`, dazu Lazy Loading.
- **Testversion** hinter einer Entra-Anmeldung, nicht nur hinter noindex.

## Befunde, nach Gewicht

| # | Befund | Beleg | Vorschlag |
|---|---|---|---|
| B-1 | **Weiterleitungen ins Leere:** 75 % der 8.916 aus JTL übernommenen Weiterleitungen zeigen auf Ziele, die es nicht mehr gibt. Stichprobe: 194 von 300 enden auf 404, 6 brauchen 2 Sprünge | `seo_redirects` gegen den aktiven Katalog; Stichprobe mit Statusabruf | Beim Import und täglich prüfen: Ziele ohne Treffer auf die Kategorie des alten Artikels umleiten (wenn bekannt), sonst 410. Ketten auf ein Ziel verkürzen. Liste im Admin „Weiterleitungen“ mit Spalte „Ziel existiert“ |
| B-2 | **Soft-404 bei entfernten Artikeln:** Status 200, Titel „Artikel“, keine H1, kein Canonical | `/gu-mehrfachverriegelungen-tuerverschluss-secury-mr-r-12374` | Entfernte Artikel: 301 auf einen Nachfolger, falls gepflegt, sonst auf die Hauptkategorie; ohne Kategorie 410 |
| B-3 | **Canonical bei der Paginierung:** `?page=2` zeigt auf Seite 1 | `/schliesszylinder-systeme?page=2` | Canonical mit `?page=n` |
| B-4 | **BreadcrumbList fehlt auf Kategorie- und Inhaltsseiten** (Muss). Organization auf der Startseite fehlt (Soll) | JSON-LD leer | Ergänzen in `ShopSeo`, die Werte gibt es schon |
| B-5 | **Technische Daten:** Die Merkmale aus der Wawi (3.353 Artikel) werden nicht angezeigt | Produktseite ohne Tabelle | Merkmal-Tabelle auf der Produktseite |
| B-6 | **robots.txt sperrt die Suche, obwohl sie noindex trägt** (`Disallow: /shop?q=`, `/*?*q=`) | robots.txt | Die beiden Disallow-Zeilen streichen, noindex bleibt |
| B-7 | **Andere Schreibweisen der Adresse:** Kleinschreibung oder ein Schrägstrich am Ende liefern 200 mit Canonical statt 301 | `/einsteck-zimmertuerschloss-bad-abgerundet`, `…/` | 301 auf die kanonische Schreibweise |
| B-8 | **Kleinigkeiten:** og:image fehlt auf Start- und Kategorieseiten. Auf der Startseite steht eine H2 vor der H1 | Abruf | og:image (Logo bzw. Kategoriebild), Reihenfolge der Inhaltsblöcke |
| B-9 | **Inhalte:** Banner mit `href="#"`, Links auf die alte Domain `sicherheitstechnik-nord.de` | KESO-2500FP-Seite, Kategorie | Im Admin „Seiten & Inhalte“ bereinigen (SN24-Team) |
| B-10 | **Offen für den Livegang:** GA4 und Search-Console-Verifizierung fehlen. Weiterleitungen der Stripe-Shop-Adressen (15.06.–14.10.) sind nicht abgeglichen. Für den Kontroll-Crawl von C. Feller braucht es einen Zugang zur Testversion | | Abklären: GA4-Konto, Verifizierung per Meta-Tag, Adressliste des Stripe-Shops. Für den Crawl ein Entra-Gastkonto oder eine Ausnahme im Testzugang per IP |

B-1 bis B-7 sind Codeänderungen im Shop (etwa 2–3 PT). Umsetzung erst nach Freigabe.

## Umsetzung (19.11.2026)

Erledigt im Code, lokal geprüft mit `seo_html_check.py`. 324 Tests grün; Stichprobe 600 Sitemap-URLs: 0 % fehlerhaft.

| # | Umsetzung | Ergebnis |
|---|---|---|
| B-1 | Weiterleitungen werden in **einem** Sprung aufgelöst. Ist das Ziel weg: der Nachfolger, wenn es einen gibt, sonst 410 statt 301 ins Leere (`SeoService.RedirectFromTableAsync`) | 300 alte Adressen: 58× 301 → 200 in einem Sprung, 242× 410, keine Ketten, kein 404 mehr. Stichprobe der 410-Fälle: Das Ziel gibt es nicht mehr (33/40) oder es ist ein reiner Konfigurator-Artikel ohne eigene Seite (7/40). Diese waren vorher Soft-404 |
| B-2 | Artikel ohne eigene Seite: 301 auf den Vaterartikel bzw. die erste noch aktive Kategorie, sonst 410. Eine gemeinsame Regel `CatalogVisibility.HasProductPage` für Artikelseite und Adressauflösung | `/gu-mehrfachverriegelungen-…-12374` → 301 `/Massivriegel` |
| B-3 | Canonical mit `?page=n`. Suche, Filter, Sortierung, Seitengröße: `noindex, follow` | `?page=2` kanonisch auf sich, `?sort=…` noindex |
| B-4 | BreadcrumbList auf Kategorie-, Hersteller- und Inhaltsseiten, Organization auf der Startseite (Logo, Wikipedia, Wikidata) | im JSON-LD |
| B-5 | Tabelle „Technische Daten“ aus den Wawi-Merkmalen auf der Produktseite | im ersten HTML; in Chrome geprüft |
| B-6 | `Disallow: /shop?q=` und `/*?*q=` aus robots.txt entfernt | – |
| B-7 | Abweichende Schreibweise/Schrägstrich → 301 auf die kanonische Adresse. Dabei gefunden und behoben: Artikel mit „/“ in der JTL-Adresse lieferten 404 | `/einsteck-…` → 301 `/Einsteck-…` |
| B-8 | og:image auf Kategorien (Bild des ersten Artikels). H2 vor H1 auf der Startseite liegt am Inhalt (Baustein über der Überschrift) → SN24-Team | teilweise |

Offen bleiben B-9 (Inhalte, SN24-Team) und B-10 (GA4/Search Console, Kontroll-Crawl, Adressen des Stripe-Shops, siehe Bericht 26).
