# 27 Prüfliste „SEO-Anforderungen an das Shop-Backend“ gegen den .NET-Shop

Stand 23.11.2026. Grundlage: Prüfliste von C. Feller (20.11.2026, für sicherheit.meesenburg.com), Original unter
`~/Documents/Meesenburg/SiSe-Shop/SEO/Prüfliste-SEO-Anforderungen-Shop-Backend.pdf`. Gegenstück zur Frontend-Liste in Bericht 25.

## Wie geprüft
Gegen den Code in `Meesenburg.SiseShop.POC/src`, nur gelesen. Die wichtigsten Aussagen habe ich per Suche im Code gegengeprüft (`ShopHead.razor`, `RedirectsPage.razor`, `Web/Program.cs` robots.txt, keine Treffer für `google_product_category`, `hreflang`, GTM-Container, `google-site-verification`). Nicht im laufenden Shop geklickt.

## Ergebnis
| Status IT | Muss | Soll |
|---|---|---|
| vorhanden | 12 | 1 |
| per Konfiguration | 1 | – |
| Entwicklung nötig (teilweise da) | 15 | 8 |
| Entwicklung nötig (fehlt) | 4 | 8 |
| entfällt (einsprachig) | – | 1 |
| **Summe (50 Zeilen)** | **32** | **18** |

**Kern:** Die *Ausgabe* ist weitgehend da und ordentlich (Bericht 25: SSR, Canonical, noindex für Filter/Suche, JSON-LD, Sitemap, 301/410). Was fehlt, ist die **Steuerbarkeit ohne Entwickler**. Artikel-, Kategorie- und Herstellerseiten bekommen Titel, Beschreibung und Adresse aus der Wawi bzw. fest im Code (`Web/Services/ShopSeo.cs`). Im Admin pflegbar sind heute nur Inhaltsseiten (Titel, Adresse, Meta-Titel/-Beschreibung, Vorschau, Versionen), Weiterleitungen (301/302, 404-Liste) und GA4/Google Ads.

**Grundsatzfrage zuerst:** Wo pflegt das SN24-Team SEO für Artikel und Kategorien – in der Wawi (wie bisher im JTL-Shop) oder im Shop-Admin? Vorschlag: Wawi bleibt Quelle, der Admin kann je Seite **überschreiben**, sonst greift eine Vorlage. Daran hängen fast alle Punkte aus 1, 3 und 6.

## Status je Zeile (für die Spalte „Status IT“)

### 1. Meta-Angaben
| Anforderung | Prio | Status IT | Beleg / Kommentar |
|---|---|---|---|
| Title je Seite editierbar | Muss | Entwicklung nötig (teilweise) | Im Admin nur Inhaltsseiten (`ContentEditor.razor:77`). Artikel: SEO-Titel aus der Wawi. Startseite: appsettings. Kategorie = Name |
| Meta Description je Seite | Muss | Entwicklung nötig (teilweise) | Inhaltsseiten im Admin; Artikel aus der Wawi mit Präfix; Kategorie automatisch aus Artikelnamen (`ShopSeo.cs:108`) |
| H1 getrennt vom Title | Muss | Entwicklung nötig (teilweise) | Inhaltsseiten getrennt (Titel vs. Meta-Titel). Artikel nur, wenn in der Wawi ein SEO-Titel steht; Kategorie H1 = Name |
| Vorlagen mit Platzhaltern, manuell überschreibt | Muss | Entwicklung nötig | Nur feste Regeln im Code (`ShopSeo.cs:102-109`) |
| Vorlagen je Seitentyp | Soll | Entwicklung nötig | nur Präfix `Shop:Seo:DescriptionPrefix` |
| Zeichenzähler / Snippet-Vorschau | Soll | Entwicklung nötig | Textfelder ohne Zähler; gekürzt wird erst bei der Ausgabe (160 Zeichen) |
| Open Graph je Seite mit Fallback | Soll | Entwicklung nötig (teilweise) | og:title/description = Title/Description, og:image Artikel/Kategorie automatisch; Inhaltsseiten ohne Bild, kein eigenes Feld |

### 2. URLs und Weiterleitungen
| Anforderung | Prio | Status IT | Beleg / Kommentar |
|---|---|---|---|
| Slug je Produkt/Kategorie/CMS editierbar | Muss | Entwicklung nötig (teilweise) | Nur Inhaltsseiten. Katalog-Adressen aus JTL (tseo) bzw. aus dem Namen (`CatalogSyncService.cs:615-637`) |
| Slug-Änderung → automatisch 301 | Muss | Entwicklung nötig (teilweise) | Katalog: alter JTL-Pfad leitet automatisch um (`SeoService.cs:122-128`). Inhaltsseiten: nein |
| Weiterleitungsmanager (anlegen/ändern/löschen, 301/302/410) | Muss | Entwicklung nötig (teilweise) | Admin „Weiterleitungen“ mit 301/302 und 404-Liste vorhanden; **410 nicht manuell wählbar** (nur automatisch bei totem Ziel) |
| CSV-Import/-Export | Muss | Entwicklung nötig | nur JTL-Import per API |
| Warnung bei Ketten/Schleifen | Soll | Entwicklung nötig (teilweise) | Ziel = Quelle wird abgelehnt, Ketten zur Laufzeit in einem Sprung aufgelöst; keine sichtbare Warnung |
| Einheitliche URL-Regeln | Muss | Entwicklung nötig (teilweise) | Inhaltsseiten klein + ae/oe/ue/ss. Katalog behält JTL-Schreibweise (`/Kaba-penta-cross-Doppelzylinder`), abweichende Schreibweise → 301. **Achtung:** Kleinschreibung erzwingen hieße 6.804 JTL-Adressen ändern → Abstimmung mit C. Feller |
| Keine IDs/Session-IDs/Parameter | Muss | Entwicklung nötig (teilweise) | Bei Namensgleichheit wird `-{id}` angehängt (`CatalogSyncService.cs:629-631`); `?page/?q/?sort` sind noindex |
| Produkt-URL unabhängig von Kategorie | Soll | vorhanden | alle Adressen flach unter `/` |

### 3. Indexierungssteuerung
| Anforderung | Prio | Status IT | Beleg / Kommentar |
|---|---|---|---|
| Robots je Seite wählbar | Muss | Entwicklung nötig | nur fest `noindex, follow` für Listen (`ShopHead.razor:21`) |
| Canonical überschreibbar, Standard selbst | Muss | Entwicklung nötig (teilweise) | Self-Canonical überall; Überschreiben nicht möglich |
| Je Filterattribut indexierbar | Muss | Entwicklung nötig | alle Filter pauschal noindex. Hängt an der offenen Entscheidung zu den 686 JTL-Merkmal-Filterseiten (TODO) |
| Sortierung/Anzahl/Ansicht nicht indexierbar | Muss | vorhanden | `ShopProductList.razor:143` |
| Interne Suche noindex | Muss | vorhanden | dto. |
| Paginierung eigene URL, Self-Canonical | Muss | vorhanden | `?page=n` (Bericht 25, B-3) |
| robots.txt im Backend editierbar | Soll | Entwicklung nötig | fest in `Web/Program.cs:109` |
| hreflang | Soll | entfällt | Shop einsprachig, keine Länderversion geplant |

### 4. Inhalte und Medien
| Anforderung | Prio | Status IT | Beleg / Kommentar |
|---|---|---|---|
| Kategorietexte mit H2/H3, Listen, Tabellen, Links, oben/unten | Muss | vorhanden | Inhalte mit Stelle „Kategorie“, Position oben/unten |
| Editor erzeugt sauberes HTML | Muss | Entwicklung nötig (teilweise) | Sanitizer entfernt nur `font-family`/`font-size` (`HtmlSanitizer.cs`); leere Tags und andere Inline-Styles bleiben |
| Produktbeschreibung mit Formatierung/Tabellen | Muss | vorhanden (Wawi) | HTML aus der Wawi, bereinigt; Zusatzinhalte je Artikel im Admin |
| Technische Merkmale strukturiert, als Tabelle | Muss | vorhanden (Wawi) | Wawi-Merkmale → „Technische Daten“ (B-5) |
| Felder Marke, MPN, GTIN | Muss | vorhanden (Wawi) | in der Wawi pflegbar |
| Alt-Text je Bild, Fallback Produktname | Muss | Entwicklung nötig (teilweise) | Artikelbilder: nur Fallback Name, Zusatzbilder `alt=""`; Inhalts-Bildbaustein hat Alt-Feld |
| Sprechende Bilddateinamen | Soll | Entwicklung nötig | Bilder unter `/img/{id}` bzw. `/medien/{id}` |
| Downloads je Produkt | Soll | Entwicklung nötig | kein Modell; Quelle klären (Wawi-Dateien?) |
| FAQ-Blöcke je Produkt/Kategorie | Soll | Entwicklung nötig (teilweise) | Baustein „Akkordeon“ an Artikel/Kategorie möglich; kein FAQPage-JSON-LD |

### 5. Strukturierte Daten und Produktfeed
| Anforderung | Prio | Status IT | Beleg / Kommentar |
|---|---|---|---|
| Product/Offer JSON-LD automatisch | Muss | vorhanden | `ShopSeo.cs:30-67`. Lücke: bei „ab“-Preisen kein Offer, GTIN nur 13-stellig |
| BreadcrumbList automatisch | Muss | vorhanden | B-4 |
| Organization global konfigurierbar | Soll | Entwicklung nötig (teilweise) | fest im Code (Name, Logo, sameAs), ohne Kontakt, nicht pflegbar |
| Google-Merchant-Feed | Soll | Entwicklung nötig (teilweise) | `/google-shopping.xml` vorhanden; `google_product_category` fehlt |

### 6. XML-Sitemap
| Anforderung | Prio | Status IT | Beleg / Kommentar |
|---|---|---|---|
| Automatisch, bei Änderung aktuell | Muss | vorhanden | dynamisch, 1 h Zwischenspeicher |
| Nur indexierbare 200er mit Self-Canonical | Muss | Entwicklung nötig (teilweise) | heute erfüllt (Stichprobe 0 % Fehler, B-Bericht 25); sobald robots/Canonical je Seite pflegbar sind, muss die Sitemap das beachten |
| Aufteilung nach Typ mit Index | Soll | Entwicklung nötig | eine Datei |
| lastmod nur bei echter Änderung | Soll | Entwicklung nötig (teilweise) | Fallback auf Abgleichzeit; Kategorien ohne lastmod |
| Einzelne Seiten ausschließbar | Soll | Entwicklung nötig | kein Feld |

### 7. Produktlebenszyklus
| Anforderung | Prio | Status IT | Beleg / Kommentar |
|---|---|---|---|
| Nicht lieferbar bleibt online (200) | Muss | vorhanden | Bestand spielt für die Sichtbarkeit keine Rolle; JSON-LD OutOfStock/BackOrder |
| Verhalten bei Deaktivieren/Löschen wählbar | Muss | Entwicklung nötig (teilweise) | automatisch: Vaterartikel → Kategorie → 410 (B-2); nicht wählbar |
| Feld für Nachfolgeprodukt | Soll | Entwicklung nötig | Ersatz heute: Weiterleitung von Hand |
| Kategorie löschen/verschieben → Weiterleitung | Muss | Entwicklung nötig (teilweise) | umbenannter JTL-Pfad leitet um; gelöschte Kategorie → 404 und 404-Liste |

### 8. Qualitätssicherung und Betrieb
| Anforderung | Prio | Status IT | Beleg / Kommentar |
|---|---|---|---|
| Vorschau vor Veröffentlichung | Muss | vorhanden (Inhalte) | Entwurf, Vorschau, Versionen. SEO-Felder für Katalogseiten gibt es noch nicht |
| Änderungsprotokoll SEO/URLs/Weiterleitungen | Soll | Entwicklung nötig (teilweise) | Inhalte mit Versionen; Weiterleitungen nur letzter Bearbeiter, Löschen ohne Protokoll |
| Rollen/Rechte für URL/Weiterleitung/Index | Soll | Entwicklung nötig (teilweise) | Weiterleitungen = Recht „Inhalte veröffentlichen“; keine eigene SEO-Berechtigung |
| Tracking (GTM) und Search Console ohne Code | Muss | Entwicklung nötig (teilweise) | GA4- und Ads-Id im Admin (mit Cookie-Einwilligung); kein GTM-Container, kein Verifizierungs-Meta-Tag (Alternative: DNS) |
| Globaler Schalter Test/Staging | Muss | per Konfiguration | `SiseShop:TestGate:Enabled` (Entra-Login + `X-Robots-Tag: noindex` + robots `Disallow: /`); Nachweis beim Livegang fehlt → Anzeige im Admin + Prüfschritt |

## Vorschlag Umsetzung
Pakete und Reihenfolge in `docs/TODO-SEO.md`. Grobe Schätzung gesamt **etwa 14–18 PT** (meine Schätzung, nicht belastbar).

## Fragen an C. Feller / SN24-Team
1. SEO-Pflege für Artikel/Kategorien: Wawi oder Shop-Admin (Vorschlag: Wawi + Überschreiben im Admin)?
2. Kleinschreibung für alle Adressen gewünscht, obwohl die JTL-Adressen gemischt geschrieben sind (6.804 Adressen, alle bräuchten 301)?
3. Welche Filter sollen überhaupt indexierbare Seiten bekommen (JTL hatte 686 Merkmal-Filterseiten)?
4. GTM-Container statt GA4 direkt? Search Console per Meta-Tag oder DNS?
5. Downloads je Artikel: Woher kommen die Dateien (Wawi, Sharepoint, Upload im Admin)?
