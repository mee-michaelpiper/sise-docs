# Moderationsblatt – Sicherheits-Demo

**Testsystem · Stripe-Testmodus · nur eigene Testbestellungen · autorisiert**
Grundtechnik für die GF: „Nur der Browser, den jeder hat. Keine Spezialsoftware.“
Werkzeuge: **Application → Local Storage** (Werte setzen) · **Network → Rechtsklick → Copy as fetch**, in der Console `body` ändern, Enter (Request wird nicht abgefangen, sondern als geänderte Kopie neu geschickt).

| # | Station | Handgriff (Stichwort) | Kernsatz | Ref |
|---|---|---|---|---|
| 0 | Ehrlicher Kauf | Normal bestellen, Testkarte 4242…, Betrag + Bestellnr. zeigen | „Merken Sie sich diesen Betrag.“ | – |
| 1 | Ein Produkt, zwei Preise | Gleiche Schließanlage in `/shop/` **und** `/configurator/` – zwei Preise | „Niemand manipuliert – trotzdem stimmt der Preis nicht. Das sind die Verluste.“ | 02 §2.5–2.7 |
| 2 | Warenkorb schönt sich | Konstellation zeigen, in der Anzeige ≠ Buchung (Toleranz ±0,01 €) | „Kunde sieht anderen Preis als gebucht wird – auch rechtlich heikel.“ | K-06 |
| 3 | Versand frei Haus | Warenkorb **unter** Freigrenze · `/api/order/` ist multipart → im `payload`-JSON `shippingMethod.GrossPrice`=0 (Proxy/Postman) | „Der Server glaubt mir die Versandkosten.“ | K-03 |
| 4 | Mengen & Fremd-Positionen | Gleicher Request → `quantity` / `config_group_items` ändern → resend | „Das Backend prüft nicht, ob das zusammengehört – es vertraut dem Browser.“ | K-04 |
| 5 | Jeder wird Großkunde | Local Storage: `customer_group_id` = B2B-ID setzen → neu laden → Sonderpreis | „Eine Zeile im Browser macht jeden zum Großkunden.“ | K-05 |
| 6 | **Bezahlen ist optional** | Eigene Bestellung **nicht** zahlen → `PATCH /api/order/<id>/mark_paid` (`status:paid`, `intent_id`) → Status „bezahlt“ | „Ware wird versandt. Bezahlt wurde nie. Im JTL steht trotzdem eine Zahlung.“ | K-01 |
| 7 | Auch ehrlich kostet | Zahlvorgang erzeugen, dann Versand/Warenkorb ändern, zahlen → Betrag ≠ ERP | „Selbst ohne Angreifer stimmt die Buchhaltung nicht.“ | K-02 |

**Schluss (Folie, 3 Zeilen):** Was: Preise falsch · Zahlung umgehbar · Kundendaten offen. — Warum: keine zentrale Preisberechnung · keine Berechtigungsprüfung · Logik im Browser. — Brauchen: Sofortmaßnahmen 24–72 h + 4 Wochen Stabilisierung.

**Nicht live zeigen:** fremde Kundendaten (S-01) und PDF-Codeausführung (S-03) – nur benennen.
