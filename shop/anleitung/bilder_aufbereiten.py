"""Screenshots für die Anleitung aufbereiten (24.11.2026).

- Namen von Kolleginnen und Kollegen unkenntlich machen (die Anleitung geht an das SN24-Team)
- Artikelseite mit nummerierten Markierungen versehen („was kommt woher“)
- Menü-Ausschnitt erzeugen

Aufruf: python3 bilder_aufbereiten.py   (im Ordner anleitung/, braucht Pillow)
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HIER = Path(__file__).parent / "bilder"
FONT = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"


def schwaerzen(datei: str, boxen: list[tuple[int, int, int, int]], ersatz: str = "Mitarbeiter/in") -> None:
    """Box mit Hintergrundfarbe der Umgebung übermalen und neutralen Text hineinschreiben."""
    pfad = HIER / datei
    bild = Image.open(pfad).convert("RGB")
    zeichnen = ImageDraw.Draw(bild)
    schrift = ImageFont.truetype(FONT, 11)
    for x0, y0, x1, y1 in boxen:
        farbe = bild.getpixel((x1 + 4, (y0 + y1) // 2))
        zeichnen.rectangle((x0, y0, x1, y1), fill=farbe)
        zeichnen.text((x0 + 2, y0 + 1), ersatz, fill=(110, 116, 128), font=schrift)
    bild.save(pfad)


def markieren(quelle: str, ziel: str, punkte: list[tuple[int, int, int]]) -> None:
    """Orange Kreise mit Nummern (wie im Text der Anleitung)."""
    bild = Image.open(HIER / quelle).convert("RGB")
    zeichnen = ImageDraw.Draw(bild)
    schrift = ImageFont.truetype(FONT, 20)
    for nummer, x, y in punkte:
        r = 17
        zeichnen.ellipse((x - r, y - r, x + r, y + r), fill=(226, 67, 18), outline=(255, 255, 255), width=3)
        text = str(nummer)
        breite = zeichnen.textlength(text, font=schrift)
        zeichnen.text((x - breite / 2, y - 12), text, fill="white", font=schrift)
    bild.save(HIER / ziel)


if __name__ == "__main__":
    # Namen in der Inhaltsliste, im Editor-Kopf und in der Fußzeile des Editors
    schwaerzen("09-inhalte-liste.png", [(1280, 213, 1400, 234)])
    schwaerzen("10-inhalt-editor.png", [(530, 34, 640, 50)])
    schwaerzen("11-inhalt-bausteine.png", [(404, 740, 520, 756)])

    markieren("02-shop-artikelseite-roh.png", "02-shop-artikelseite.png", [
        (1, 740, 240),   # Name, Hersteller, Artikelnummer, EAN
        (2, 740, 362),   # Preis
        (3, 248, 210),   # Bilder
        (4, 740, 500),   # Kurzbeschreibung
        (5, 740, 640),   # Konfiguration
        (6, 226, 158),   # Kategorie-Pfad
    ])

    # Menü links aus der Inhaltsliste
    Image.open(HIER / "09-inhalte-liste.png").crop((0, 0, 200, 570)).save(HIER / "01-menue.png")
    print("fertig")
