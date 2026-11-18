# Prüfnachweis K-01 – Bestellung ohne Zahlung als „bezahlt“ markierbar

**Vertraulich · interne Sicherheitsprüfung · nicht für externe Dienstleister**
Vorlage zum Ausfüllen. Platzhalter in `<…>` ersetzen, Felder mit ☐ ankreuzen.

---

## 1. Rahmen der Prüfung

| Feld | Eintrag |
|---|---|
| Befund-ID | K-01 (Analyse 02 / 04, Endpunkt `PATCH /api/order/{id}/mark_paid`) |
| Prüfer | `<Name, Rolle>` |
| Datum / Uhrzeit (Europe/Berlin) | `<TT.MM.JJJJ, HH:MM>` |
| Umgebung | ☐ lokaler Stack  ☐ Staging/Test  — Host: `<testhost>` |
| Produktion berührt? | ☐ nein (Pflicht) |
| Zahlungsmodus | ☐ Stripe-Testmodus |
| Datenbasis | ☐ nur eigene Testbestellung |
| Autorisierung | beauftragt durch `<GF/CTO>`, Mail vom `<TT.MM.JJJJ>` |
| Zweck | Nachweis der praktischen Ausnutzbarkeit zur Priorisierung der Behebung |

## 2. Vorbedingung

| Feld | Eintrag |
|---|---|
| Test-Bestellnummer | `<order_id>` |
| Angelegt über | normalen Checkout, **ohne** Bezahlung |
| Zahlungsmethode vor dem Test | `<z. B. PayPal>` |
| Zahlstatus vor dem Test | `<z. B. offen / nicht bezahlt>` |
| Offener Betrag vor dem Test | `<Betrag> €` |

## 3. Durchgeführte Aktion

**Gesendeter Request** (ohne Authentifizierung – genau das ist der Befund):

```
PATCH https://<testhost>/api/order/<order_id>/mark_paid
Content-Type: application/json

{"status": "paid", "intent_id": "demo_fake_intent_0001"}
```

| Feld | Eintrag |
|---|---|
| Authentifizierung mitgesendet? | ☐ nein |
| Uhrzeit des Requests | `<HH:MM:SS>` |
| Werkzeug | ☐ curl  ☐ Postman  ☐ `<…>` |

## 4. Beobachtetes Ergebnis

**Antwort des Servers:**

```
HTTP <Status, erwartet 200>
{"detail": "updated", "id": "<order_id>"}
```

**Logzeile im `business_api`-Container** (Beleg, dass der Request serverseitig verarbeitet wurde):

```
mark_paid: Order <order_id> -> {'status': 'paid', 'intent_id': 'demo_fake_intent_0001'}
```

**Zustand der Bestellung nach dem Test:**

| Feld | Vorher | Nachher |
|---|---|---|
| Zahlstatus | `<offen>` | `<bezahlt>` |
| Offener Betrag | `<Betrag> €` | `0,00 €` |
| Zahlungsmethode | `<PayPal>` | `<Unbekannt>` |
| Status (Anzeige) | `<…>` | `<Unbekannt>` |
| Übergabe an ERP | – | ☐ als „bezahlt“ übertragen |

## 5. Bewertung

- **Haupteffekt:** Eine unbezahlte Bestellung wurde **ohne Anmeldung** als „bezahlt“ markiert und als bezahlt an das ERP übergeben. Ein Angreifer kann Ware ohne Zahlung auslösen.
- **Nebeneffekt (Datenintegrität):** Durch das vollständige Zurückschreiben des Datensatzes (`insert_or_replace_one`) ging die **tatsächliche Zahlungsmethode verloren** (`<PayPal>` → „Unbekannt“). Der Endpunkt fälscht nicht nur den Status, er korrumpiert Bestelldaten.
- **Voraussetzung für den Angriff:** Kenntnis einer gültigen (fortlaufenden) Bestellnummer und die Fähigkeit, einen HTTP-Request zu senden. Kein Abfangen, kein Spezialwerkzeug.
- **Schweregrad:** Kritisch.

## 6. Empfohlene Sofortmaßnahme

- Endpunkt `mark_paid` über Traefik **nicht** öffentlich erreichbar machen (Allowlist / nur internes Docker-Netz).
- Zahlungsbestätigung ausschließlich über den **Stripe-Webhook mit Signatur- und Betragsprüfung**; `intent_id` serverseitig gegen Stripe verifizieren.
- Beim Statuswechsel nur die Zahlungsfelder ändern (gezieltes Update statt vollständigem Replace), damit keine Daten verloren gehen.
- Siehe Gesamtbericht, Abschnitt 4 (Sofortmaßnahmen) und 5 (Stabilisierung).

## 7. Nachweis-Anhänge

☐ Screenshot Request/Response  ☐ Screenshot Logzeile  ☐ Screenshot Bestellung vorher  ☐ Screenshot Bestellung nachher

---

*Erstellt am `<TT.MM.JJJJ>` · Prüfer `<Name>` · Unterschrift / Freigabe: ______________________*
