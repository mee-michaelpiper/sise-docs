# Überarbeitung übernommener JTL-Inhalte für den .NET-Shop (Meesenburg Sicherheit & Service)

Du überarbeitest Inhalte (Kategorieseiten und eigene Seiten), die automatisch aus dem alten JTL-Shop übernommen wurden. Ziel: **modern, frisch, gut lesbar, gute UX** – im Design des .NET-Shops. Sprache: **Deutsch, Sie-Form**, sachlich-freundlich, keine Werbefloskeln-Ketten.

## Eingabe
`in/paketN.json`: Liste von Inhalten. Je Inhalt: `id`, `placement` (Category|Page), `position` (Top = über der Artikelliste der Kategorie, Bottom = darunter; Page = eigene Seite), `targetName` (Kategorie), `slug`, `status`, `title`, `metaTitle`, `metaDescription`, `blocks` (aktueller Stand) und `images` (jede verwendete Bild-URL mit `localFile` zum Ansehen oder `broken: true`).

**Sieh dir jedes Bild mit `localFile` an (Read-Werkzeug auf die Datei).** Rufe keine URLs und keine APIs auf, ändere keine anderen Dateien.

## Ausgabe
Je Inhalt eine Datei `out/<id>.json` (UTF-8, gültiges JSON):
```json
{ "id": "...", "title": "...", "metaTitle": "... oder null", "metaDescription": "... oder null",
  "blocks": [ ... ], "outdated": false, "notes": ["kurze deutsche Hinweise für die Fachabteilung"] }
```
`blocks` nach dem Schema unten. `notes`: was du geändert hast (1–3 Stichpunkte) und **was die Fachabteilung prüfen sollte** (z. B. „Preis im alten Banner (ab 43,90 €) entfernt – Preise kommen aus dem Katalog“, „Formular durch Link zum Konfigurator ersetzt“).

## Gestaltungsregeln
1. **Oben kompakt:** Kategorie-Inhalte mit `position: Top` stehen über den Artikeln → höchstens: kurze Einleitung (2–3 Sätze), ggf. Kennzahlen/Vorteile, ggf. ein Banner. Ausführliches gehört nach unten: Liegt ein `Bottom`-Inhalt derselben Kategorie im Paket, verschiebe die Details dorthin; sonst in ein **Akkordeon** („Mehr zum System“ o. Ä.).
2. **Lange Texte gliedern:** Einleitung sichtbar, Details in Akkordeon-Abschnitte mit klaren Titeln (Technik, Sicherheit, Patent, Einsatzbereiche, Pflege …). Wiederholungen streichen, Sätze straffen. **Keine Fakten weglassen, die für den Kauf wichtig sind.**
3. **Nichts erfinden:** keine neuen technischen Angaben, Zertifizierungen, Preise, Lieferzeiten oder Versprechen. Nur umformulieren, kürzen, gliedern, was da ist (Text und Bilder).
4. **Bilder mit eingebranntem Text** (Tabellen, Systemleisten, Sicherheitsstufen, Farbtabellen mit Codes, beschriftete Schnittbilder, Banner mit Text):
   - Tabellen/Übersichten → Baustein `table` (alle Zeilen und Spalten exakt abschreiben, Sterne als Zahl „4 von 5“ o. ä.).
   - Systemleiste (z. B. dormakaba „Stiftreihen | VdS | Bohrschutz | Schlüssel inklusive | Farben | Fertigung | Express“) → `keyfacts`.
   - Farbübersichten mit Codes → `table` (Code | Farbe) oder Text-Liste; das Bild darf als Illustration bleiben, wenn es hilft.
   - Beschriftete Schnittbilder → Bild behalten (ohne Text darin geht nichts verloren), Beschriftungen zusätzlich als `features` oder Liste.
   - Banner mit eingebranntem Text → `banner`-Baustein mit dem Text als echtem Text. Bild: nur ein Bild **ohne** Text aus derselben Seite, sonst Variante `promo` (ohne Bild) oder `split` mit einem Produktfoto.
   - Ausfüllbare Formulare/Schließplan-Vorlagen als Bildschirmfoto → kurzer Text + `button` „Schließanlage online planen“ → `/konfigurator`; in `notes` vermerken.
   - Reine Produktfotos ohne Text → `image` behalten, mit sinnvollem `alt`.
5. **Kaputte Bilder** (`broken: true`) weglassen.
6. **Veraltetes** (Weihnachtsgrüße, abgelaufene Aktionen/Gutscheincodes, Jahreszahlen in der Vergangenheit, „Black Friday“ etc.): `"outdated": true`, Blöcke trotzdem sauber liefern (oder leer), in `notes` begründen. Gutscheincodes nie übernehmen.
7. **Links** unverändert übernehmen (auch absolute Links auf sicherheit.meesenburg.com). Button-Texte kurz und aktiv („Zum Konfigurator“, „Alle Keso-Zylinder ansehen“).
8. **Überschriften:** Seitentitel kommt vom Shop – im Inhalt mit `heading` level 2 für Abschnitte, level 3 für Unterabschnitte. Keine Überschrift „- Perfektion und Flexibilität“ mit Strich am Anfang; Großschreibung normal (keine VERSALIEN-Sätze).
9. **Meta:** `metaDescription` (max. 155 Zeichen) für eigene Seiten und Kategorien sinnvoll füllen, wenn leer.
10. `title` = interner Titel (eigene Seite: sichtbarer Seitentitel) – bei Seiten klar und kurz.

## Baustein-Schema
Jeder Block: `{"id": "<kurz, eindeutig, z. B. b1>", "type": "...", "props": {...}, "columns": [ {"blocks": [...]} ]}` – `columns` nur bei `columns` und `accordion`. Alle Werte in `props` sind **Strings**.

| type | props | Hinweise |
|---|---|---|
| heading | text, level ("2"/"3") | |
| text | html | nur `<p> <h3> <ul> <ol> <li> <strong> <em> <a href> <br>` – keine style/class, keine Farben |
| image | src, alt, href (optional) | src = Original-URL aus `images[].url` |
| button | label, href, style ("primary"/"outline") | |
| divider | – | sparsam |
| video | url | YouTube/Vimeo-URL |
| table | title (opt.), rows, note (opt.) | rows: Zeilen mit `\n`, Spalten mit ` \| `; erste Zeile = Kopf; max. 8 Spalten |
| keyfacts | items | je Zeile „Wert \| Bezeichnung“, 3–8 Kacheln, z. B. „5+A / 22 \| Stiftreihen / Stiftpaare“ |
| features | title (opt.), items | je Zeile „Titel \| Text (1–2 Sätze)“, 3–6 Stück |
| banner | variant ("hero"/"split"/"promo"), eyebrow (opt., z. B. „Neu“), title, text, label, href, src | Bild ohne Text; promo ohne Bild |
| columns | – | 2–4 Spalten; darin nur: heading, text, image, button, divider, video, table, keyfacts, features |
| accordion | titles (je Zeile ein Abschnittstitel), open ("first" optional) | `columns` = je Abschnitt ein Eintrag `{"blocks":[...]}` in der Reihenfolge der Titel; darin dieselben einfachen Bausteine |
| products | items (Artikelnummern kommagetrennt), title | nur übernehmen, wenn schon vorhanden |

## Qualität
- Gültiges JSON, jede Datei für jeden Inhalt des Pakets (auch wenn kaum Änderung nötig ist).
- Lieber weniger, gute Bausteine als viele. Typische gute Seite: Einleitung (text) → keyfacts oder features → Bild/Spalten → table → accordion mit Details → button.
- Am Ende kurze Zusammenfassung zurückgeben: Anzahl Inhalte, Anzahl umgewandelter Text-Bilder, veraltete Inhalte, offene Punkte.
