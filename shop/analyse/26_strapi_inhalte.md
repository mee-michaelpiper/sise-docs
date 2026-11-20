# 26 Inhalte aus dem Strapi des Stripe-Shops übernehmen?

Stand 19.11.2026. Frage von M. Piper: Können die Seiten aus dem Strapi-Shop (Stripe-Shop, live 15.06.–14.10.2026) in den .NET-Shop übernommen werden? Hintergrund: Das SN24-Team entscheidet gerade, ob die JTL-Seiten bleiben oder ob es neu anfängt.

## Ergebnis
**Ja, mit wenig Aufwand (etwa 1 PT).** Die Strapi-Seiten sind einfach aufgebaut, und ein Übernahmeweg existiert schon (Rechtstexte am 05.11. per `rechtstexte_strapi.py`, JTL-Seiten per `JtlContentImport`).

## Was in Strapi steht
Gelesen über den Tunnel zu node1 (nur GET). Das Schema stammt aus dem Quellcode des Stripe-Shops (`cms/strapi_cms/src/api`, 41 Inhaltstypen).

| Art | Anzahl | Bausteine |
|---|---|---|
| Rechtstexte (AGB, Datenschutz, Impressum, Widerruf, Zahlung/Versand, Batteriegesetz, Verpackungsverordnung) | 7 | Text. 6 davon sind schon übernommen |
| Ratgeber/Info (z. B. Schließzylinder richtig messen, kürzen/verlängern, Systembeschreibungen KESO, Tür-DIN-Richtung, Schließanlagen-Information) | 19 | Text (29×) und Bild+Text (19×) |
| Kontakt | 1 | Kontaktformular + Text. Der .NET-Shop hat ein eigenes Formular |
| Startseite | 1 | 14 Sonderbausteine (Hero, Kundenkategorien, Konfigurator-Leitfaden, Partner, Referenzen, Trusted Shops …) |
| FAQ / News | 15 / 3 | Sammlungen mit Rich-Text |
| Ohne Inhalt (404) | 6 | erster Schlüssel, Glossar, Anfahrt, Schließplan-Vorlage, Upload-Formular, Video-Produktinfo |

## Wie übernehmen
- **Ratgeber- und Infoseiten:**
  - Text → Baustein „Text“, Bild+Text → zwei Spalten (Bild, Text).
  - Bilder aus der Strapi-Medienablage in die Medienbibliothek des .NET-Shops.
  - Als **Entwurf** einspielen; das SN24-Team prüft und veröffentlicht.
- **Adressen:** Der Stripe-Shop zeigte die Seiten unter `/{slug}` mit demselben Slug wie in Strapi (`(singlepages)/[singlepage]`). Wir übernehmen den Slug, dann bleiben die Adressen aus der Zeit 15.06.–14.10. ohne Weiterleitung gültig (Prüfliste B-10). Produktadressen `/shop/{slug}` leitet der .NET-Shop schon um.
- **FAQ (15):** als eine Seite „Häufige Fragen“ mit Überschrift + Text je Frage.
- **News (3):** nicht nachbauen (Entscheidung F-47/F-48, Bericht 21), Adressen `/news/…` auf passende Seiten umleiten.
- **Startseite:** nicht automatisch; die .NET-Startseite hat eigene Bausteine. Einzelne Texte bei Bedarf von Hand.

## Wechselwirkung mit den JTL-Seiten
Zu mehreren Themen gibt es eine JTL- und eine Strapi-Fassung (z. B. „Schließzylinder richtig messen“). Das Team sollte pro Thema **eine** Quelle wählen. Mein Vorschlag:
- Strapi ist die neuere Fassung, vor allem bei den Rechtstexten.
- Die JTL-Adressen leiten auf die gewählte Seite um.
- Übernommen wird alles nur als Entwurf, damit das Team in Ruhe vergleichen kann.

## Nächster Schritt
Skript `~/repos/tools/ops-tools/siseshop/strapi_inhalte.py`, nach dem Muster von `rechtstexte_strapi.py`: nur lesend aus Strapi, Entwürfe im .NET-Shop (lokal, danach Test). Erst nach der Entscheidung des Teams.

## Umsetzung (19.11.2026, lokal)
`~/repos/tools/ops-tools/siseshop/strapi_inhalte.py`, Probelauf ohne `--apply`. Nur lesend aus Strapi, im Shop ausschließlich **Entwürfe**.

**Ergebnis im lokalen Shop:**
- **Neu (3):** `/gebrauchsanleitung-fuer-schliesszylinder`, `/schliessanlagen-konfigurator-anleitung` und `/faq` (Häufige Fragen, 15 Fragen als Akkordeon).
- **Schon vorhanden (17):** Diese Seiten gibt es als überarbeitete JTL-Fassung („Prüfung offen“) unter **derselben Adresse**. Sie bleiben unverändert. Die Strapi-Fassung liegt als Vergleichsentwurf daneben, Adresse `…-strapi`, Titel „[Strapi] …“, unveröffentlicht und damit öffentlich nicht erreichbar.
- **Leer in Strapi (6):** übersprungen.
- **Bilder:** 20 Bilder aus Strapi in der Medienbibliothek (`strapi_*`). Strapi auf node1 wird abgeschaltet, deshalb kopiert.

**Dabei behoben:**
- Bilder in Spalten wurden auf Spaltenbreite gezogen und dadurch unscharf. Ursache: Die Spalte ist ein Flex-Container; Korrektur in `shop.css`.
- Die erste Strapi-Überschrift doppelte den Seitentitel. Sie fällt jetzt weg, damit es genau eine H1 gibt (SEO).

**Für das Team:** Je Thema eine Fassung wählen. Strapi gewählt → Bausteine in die Hauptseite übernehmen oder die Hauptseite löschen und den Vergleichsentwurf umbenennen. JTL gewählt → Vergleichsentwurf löschen.

**Test (20.11.2026):** Dieselbe Übernahme per Port-Forward auf die Test-API: 20 Entwürfe, 20 Bilder, nichts veröffentlicht. Die 17 JTL-Seiten im Test sind unverändert. Das SN24-Team kann jetzt in der Testversion vergleichen.
