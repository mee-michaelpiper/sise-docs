# 08 – Umsetzungsplan Phase 1: .NET-Shop-API (2 Wochen + Testphase)

> **Stand:** 07.10.2026 · **Grundlage:** Berichte 00–07, Abstimmung vom 07.10.2026
> **Vorbehalt:** Der Plan wird verbindlich, sobald die JTL-Schnittstelle abgefragt werden kann (IP-Freigabe). Tag 1 ist der erste Tag mit JTL-Zugang.

## 1. Ziel

Nach Phase 1 gilt:
1. **Kein falscher Zahlbetrag mehr.** Den Betrag, den Stripe abbucht und der an JTL geht, berechnet ausschließlich die neue .NET-API aus Artikelpreisen und Mengen.
2. **Die kritischen Sicherheitslücken sind geschlossen:** `mark_paid`, offene Bestelldaten, PDF-Dienst und Secrets (Bericht 04).
3. **FastAPI ist für Katalog, Warenkorb, Bestellung und Zahlung abgelöst.** Das Frontend läuft unverändert bzw. minimal angepasst gegen die neue API.
4. **Fehler werden sichtbar statt verschluckt:** JTL-Abgleich und Auftragsübergabe melden Probleme (Log und Sync-Probleme wie im SiSe Portal).

**Bewusst nicht in Phase 1:**
- Konfigurationsregeln ins Backend (→ Phase 2 nach Fachworkshop)
- Wechsel auf esERP (→ Phase 3)
- Neubau von Auth, Mail und PDF (bleiben als Python-Dienste, nur intern erreichbar)
- Frontend-Umbau über das Nötigste hinaus

## 2. Grundprinzip: Preisautorität ohne Konfigurationsregeln

Der Konfigurator im Frontend entscheidet weiterhin, **welche** Artikel eine Konfiguration ergibt: Grundzylinder, Längen, Zusatzschlüssel, Konfig-Artikel. Die API entscheidet, **was sie kosten**:

```
Positionspreis = Artikelpreis aus JTL (Kundengruppe aus Session, Sonder-/Kanalpreis nach JTL-Regeln) × Menge
MwSt           = Satz der Steuerklasse des Artikels (DE → Lieferland), kein Rückfallwert
Rundung        = je Position auf Cent (decimal, kaufmännisch)
Versand        = serverseitig aus Versandart, Lieferland und Gewicht bzw. Versandklasse; Freigrenze serverseitig
Gesamt         = Σ Positionen + Versand
```

Diese Rechnung ist **eindeutig**, auch solange die Konfigurationsregeln noch nicht festgeschrieben sind. Die API prüft zusätzlich alles, was sich aus den JTL-Daten eindeutig ergibt:
- Menge > 0, ganzzahlig
- Artikel existiert, ist aktiv und im Shop bzw. Konfigurator freigegeben
- Konfig-Artikel gehört laut JTL-Konfiggruppe zum Grundartikel, und die Min/Max-Mengen der Gruppe werden eingehalten

Weicht der vom Frontend gezeigte Preis ab, wird das protokolliert und dem Kunden **vor** der Zahlung der korrekte Betrag angezeigt.

## 3. Voraussetzungen (Tag 0)

| # | Voraussetzung | Verantwortlich | Status |
|---|---|---|---|
| V1 | Sofortmaßnahmen aus Bericht 00, Abschnitt 4: Traefik-Allowlist, offene Endpunkte sperren, Ports schließen | Betrieb | offen |
| V2 | Produktionsstand gesichert: Compose/Stack, `.env`, Traefik, Images, DB-Dumps aller Mongo und MariaDB, Uploads | Betrieb | offen |
| V3 | Secrets rotiert und im Key Vault: JTL-Token, Custom-API-Token, JWT-Schlüssel, SMTP, Strapi, Stripe | IT | offen |
| V4 | IP-Freigabe bei ecomdata für Entwickler-IP und Ziel-Egress-IP (AKS bzw. Server), Ports 50443 und 58080 | ecomdata | angefragt |
| V5 | Zugang zum Server, zum Server-Traefik und zu den Logs | Projektleitung | offen |
| V6 | Stripe: Zugang zum Dashboard, Test-Keys, Klärung Live-Keys, Webhook-Endpunkt für die neue API | Projektleitung | offen |
| V7 | Neues Repo in Azure DevOps (`Meesenburg.SiseShop`), Branch-Policy (PR + Review), Pipeline-Agent | Projektleitung | offen |
| V8 | Entscheidung Betriebsort Phase 1 (Abschnitt 9, E1) | Projektleitung | offen |
| V9 | Ansprechpartner Fachbereich für Soll-Preise (Testfälle) und den Fachworkshop | Projektleitung | offen |

## 4. Zielarchitektur Phase 1

Der Browser (SvelteKit-SPA) ruft weiter dieselben Pfade `/api/...` auf. Traefik (Server, Allowlist) verteilt so:

| Pfad | Ziel in Phase 1 |
|---|---|
| `/api/items/*`, `/api/cart/*`, `/api/order/*`, `/api/user/`, `/api/payment/*`, `/api/configurator/`, `/api/contact`, `/api/upload/*` | **Shop-API (.NET 10)** |
| `/api/auth/*` | auth_svc (Python, unverändert, Secrets rotiert) |
| `_check_page`, `sitemap.xml` | business_api (Python, nur diese Pfade, bis Phase 2) |
| mail_svc, pdf_svc | **keine Route** (nur intern) |

Innerhalb der Shop-API:
- **Lesemodell** (Mongo) mit Artikeln, Preisen und Konfigurationsdaten, befüllt vom **Sync-Worker** aus JTL-REST (`/v1`) und Custom-API (nur lesend)
- **Bestellungen** (Mongo), **Auftrags-Outbox** → JTL-REST `/salesOrders` (+ Custom-API-Schritte)
- **Stripe:** PaymentIntents und Webhook

- **Anmeldung:** Die Shop-API prüft das vorhandene Auth-Cookie (JWT) mit dem **neuen, rotierten** Schlüssel aus dem Key Vault. Benutzer-ID und Kundengruppe kommen aus dem Token bzw. dem Benutzerprofil, **nie** aus dem Request-Body.
- **Kompatible Fassade:** Pfade, Feldnamen und Fehlerformate entsprechen dem Ist-Vertrag (Bericht 05, Abschnitt 3 und 8.3), inklusive der Altlasten (`/api/items/catagories`, PascalCase-Bestellstruktur, `detail`/`msg`).
- **Aufbau wie im ES-Projekt:** Apis → Services → Brokers, Minimal APIs, ProblemDetails, Resilience, Key Vault, OpenTelemetry/Mongo-Log. ERP-Zugriff nur über `IErpCatalogBroker`, `IErpOrderBroker` und `IErpCustomerBroker`, damit Phase 3 (esERP) nur einen Broker austauscht.

## 5. Arbeitspakete

Aufwand in Personentagen (PT), grob geschätzt.

| AP | Inhalt | Ergebnis / Abnahme | PT |
|---|---|---|---|
| **AP0** Absicherung | V1–V3 umsetzen bzw. begleiten; Forensik anstoßen: Logs auf die gesperrten Pfade prüfen, „bezahlt“-Aufträge in JTL gegen Stripe abgleichen, besonders in den Zeiträumen mit Fehlberechnung (Bericht 02) | Liste auffälliger Aufträge, Sperren aktiv | 2 |
| **AP1** Projektbasis | Repo, Solution (aus dem POC), CI-Pipeline (Build, Tests, gitleaks, Container-Build), Key Vault, Mongo, Container, Health-Endpunkte, Logging; Schnittstellen-Stand JTL speichern (POC-Funktion) | Pipeline grün; API läuft im Zielbetrieb mit `/health` | 1,5 |
| **AP2** JTL-Abgleich (Worker) | Artikel, Kategorien, Hersteller, Steuerklassen/-sätze, Preise (Kanal-, Sonder-, Kundengruppenpreise), Variationen inkl. Aufpreise, Bilder-Metadaten, Merkmale; aus der Custom-API (nur lesend) Konfiggruppen, Konfig-Artikel, Variationswerte, Versandarten, Einheiten. **Nur `/v1`**, HTTP-Status ausgewertet, tolerantes Mapping, **kein stilles Verwerfen**; Fehler je Artikel als Sync-Problem; neuer Stand wird erst nach vollständigem Lauf aktiv (atomarer Wechsel); Delta-Abgleich nach `changedSince` mit Merker erst nach Erfolg; **alle** Artikel, nicht nur Artikel mit Kategorie | Abgleich vollständig; Zahl der Artikel = JTL-Zählung; Sync-Probleme sichtbar | 3 |
| **AP3** Katalog-Fassade | `POST /api/items/items/` mit Filter-DSL (**Whitelist** von Feldern und Operatoren, Sichtbarkeitsfilter erzwungen), `/api/items/catagories[/]`, `/api/items/manufacturers/`, `/api/items/shipping/`; **kein EK, keine fremden Kundengruppenpreise** in Antworten; Kundengruppe aus der Session | Vergleichstest gegen die Alt-API mit Stichproben-Requests (gleiche Struktur, korrigierte Inhalte) | 2,5 |
| **AP4** Preisdienst | Preisfindung je Artikel/Kundengruppe nach den JTL-Daten; MwSt aus Steuerklasse und Lieferland (DE in Phase 1, andere Länder ablehnen oder klar festlegen); Cent-Rundung je Position; Versand serverseitig inkl. Freigrenze und Speditions-Versandklassen; Geschenk-Berechtigung serverseitig; `quote`-Berechnung für Warenkorb und Bestellung | Unit-Tests für alle eindeutigen Testfälle aus Bericht 02 (Soll-Preise vom Fachbereich bestätigt) | 2,5 |
| **AP5** Warenkorb & Konfiguration speichern | `/api/cart/{id}` (GET/POST/DELETE) nur für den eigenen Benutzer bzw. Gast-Token; `/api/configurator/` (GET/POST), `user_id` aus der Session; Speicherung des Schließplans (Matrix) mit Referenz | Kein Zugriff auf fremde Warenkörbe/Konfigurationen (Test) | 1,5 |
| **AP6** Bestellung | `POST /api/order/` (multipart, Ist-Vertrag); Validierung (Abschnitt 2); Preise **nur** aus AP4; Bestellung als **eingefrorenes Angebot** (Version + Betrag); Schließplan-Referenz und Matrix werden mitgespeichert (K-08); Anhänge mit Typ- und Größenprüfung | Bestellung mit manipulierten Mengen/Preisen/Versandkosten wird abgelehnt bzw. korrigiert (Test) | 2 |
| **AP7** Zahlung (Stripe) | `POST /api/payment/intents/`: PaymentIntent **je eingefrorenem Angebot**, Betrag aus der Bestellung; bei geänderter Bestellung neuer Intent oder Betrag aktualisieren (K-02); Webhook mit Signatur, Idempotenz und **Betragsabgleich**; Bestellung gilt nur nach erfolgreichem Webhook als bezahlt; **`mark_paid` entfällt**. **Stripe-Referenzen sauber zuordnen:** `metadata` mit Shop-Bestellnummer, Bestell-ID und Angebotsversion, `description` = Shop-Bestellnummer, `statement_descriptor_suffix`; PaymentIntent-ID und Charge-ID an der Bestellung; dieselbe Referenz in der JTL-Zahlung (`cExternalTransactionId`) | Testmodus: Zahlung, Abbruch, Betragsabweichung, doppelter Webhook; jede Zahlung eindeutig einer Bestellung zuordenbar | 2 |
| **AP8** Auftragsübergabe an JTL | Outbox mit Wiederholung; `POST /salesOrders` (REST) mit Shop-Bestellnummer als externer Referenz (keine Doppelanlage); notwendige Custom-API-Schritte (Positionspreise, Konfig-/Variationszuordnung, Versandtext) aus `raw_order.py` 1:1 übernommen, aber **mit Prüfung der Antworten**; Zahlungsbuchung erst nach Webhook; Fehler → Sync-Problem + Alarm statt „gilt als angelegt“ | Testauftrag in JTL vollständig und betragsgleich (Shop = Stripe = JTL) | 2,5 |
| **AP9** Konto & Bestellsuche | `/api/user/` (aus der Session), `/api/order/?user_id` nur eigene Bestellungen, `/api/order/single_order` nur mit Anmeldung oder Bestellnummer **plus** E-Mail, serverseitig geprüft, mit Rate-Limit; `payment_methods`, `shipping_methods` | Kein Zugriff auf fremde Bestellungen (Test) | 1 |
| **AP10** Sonstiges | `/api/contact[/]` mit serverseitigem Spam-Schutz und Rate-Limit; `/api/upload/security_card` mit Typprüfung, Größenlimit und Ablage | Funktion + Missbrauchstests | 1 |
| **AP11** Frontend (minimal) | Anzeige des korrigierten Preises vor der Zahlung (bei Abweichung); Bestellsuche sendet die E-Mail mit; Fehlertexte aus ProblemDetails; Payload enthält die Schließplan-Referenz; **keine** Umbauten der Konfiguratoren | Kernabläufe funktionieren unverändert | 1,5 |
| **AP12** Tests & Schattenbetrieb | Vertragstests gegen den Ist-Vertrag; E2E Checkout (Stripe-Testmodus, Vorkasse); **Schattenbetrieb:** echte Warenkörbe parallel durch die neue Preisberechnung, Abweichungen gegen die Alt-Berechnung protokollieren und erklären | Abweichungsliste: jede Differenz begründet (Fehler alt oder neu) | 2 |
| **AP13** Betrieb & Umschaltung | Deployment Zielbetrieb, Alarme (Sync-Probleme, Auftragsübergabe, Webhook-Fehler), Traefik-Umschaltung je Pfadgruppe, Rückfallplan | Umschaltung geprobt, Rückfall in < 5 Min. | 1,5 |
| | **Summe** | | **≈ 27 PT** |

**Kapazität:** 27 PT in 10 Arbeitstagen heißt **3 Personen** oder 2 Personen mit vollem Fokus plus Unterstützung durch Betrieb und Fachbereich (AP0, Testfälle). Engpass ist AP8 (Auftragsübergabe), weil die Custom-API-Schritte nur aus dem Python-Code bekannt sind.

## 6. Tagesplan

| Tag | Schwerpunkt | Meilenstein |
|---|---|---|
| 0 | V1–V9, AP0 | Shop abgesichert, Zugänge da |
| 1 | AP1, Schnittstellen-Stand JTL speichern, AP2 Beginn | Pipeline grün, JTL-Live-Abfrage |
| 2 | AP2 | Vollabgleich Artikel + Stammdaten |
| 3 | AP2 (Delta, Custom-API, Sync-Probleme), AP4 Beginn | Lesemodell vollständig |
| 4 | AP3, AP4 | Katalog-Fassade, Preisdienst mit Tests |
| 5 | AP3 Vergleichstest, AP5 | **Zwischenabnahme:** Katalog im Schattenbetrieb |
| 6 | AP6, AP9 | Bestellung mit Validierung |
| 7 | AP7 | Stripe end-to-end im Testmodus |
| 8 | AP8 | Auftrag in JTL betragsgleich |
| 9 | AP10, AP11, AP12 | Schattenbetrieb Preise, E2E |
| 10 | AP12, AP13 | **Abnahme Phase 1**, Umschaltung geprobt |

## 7. Testphase und Umschaltung (3–5 Arbeitstage)

1. **Schattenbetrieb Katalog und Preise** (ab Tag 5 bzw. 9): Die Alt-API bleibt aktiv. Die neue API rechnet jeden echten Warenkorb parallel mit, Abweichungen werden protokolliert.
2. **Interner Testkauf** mit Testkunden (B2C, B2B mit Kundengruppe, Gast) in Stripe-Live mit kleinen Beträgen und Storno, inklusive Prüfung in JTL.
3. **Umschaltung in Stufen** über Traefik: Katalog (`/api/items`) → Warenkorb/Konfiguration → Bestellung/Zahlung/Konto. Jede Stufe 1 Tag beobachten.
4. **Rückfall:** Traefik-Route zurück auf die Alt-Dienste. Bestellungen, die in der neuen API entstanden sind, bleiben über die Outbox in JTL nachvollziehbar.
5. **Abnahmekriterien:**
   - Betrag Shop = Stripe = JTL bei allen Testkäufen
   - keine offenen Abweichungen im Schattenbetrieb ohne Begründung
   - kein Zugriff auf fremde Daten (Tests AP5/AP9)
   - Sync-Probleme und Alarme funktionieren
   - Rückfall geprobt

## 8. Parallel: Fachworkshop Konfigurationsregeln (für Phase 2)

**Ziel:** eine verbindliche Spezifikation, die Phase 2 umsetzen kann.

**Grundlage:** Bericht 02 (Formeln, Widersprüche, 30 Testfälle). Zu klären:
- Schließcode B–F und Schließplan-Typen: Bedeutung, Zulässigkeit, Aufpreise
- Zylindertypen, erlaubte Längen außen/innen, Längenaufpreise
- Schlüssel: Anzahl inklusive, Zusatzschlüssel, Farbkappen, gleichschließend/verschiedenschließend
- Mengenlogik (Konfig-Artikel je Zylinder), Zusammenführen identischer Positionen
- Kundengruppen und Sonderpreise (B2C/B2B), Rabatte, Geschenke, Versand (Spedition)
- **Soll-Preise für alle Testfälle**, vom Fachbereich unterschrieben

**Ergebnis:** Regelwerk + Testfälle als Datei, Grundlage für die automatischen Tests in Phase 2.

## 9. Offene Entscheidungen (bis Tag 0)

| # | Frage | Empfehlung |
|---|---|---|
| E1 | Betriebsort Phase 1: AKS oder vorhandener Docker-Host neben den Alt-Diensten? | **AKS**, wenn die Egress-IP bei ecomdata bis Tag 0 freigegeben ist und Traefik vom Server ins AKS routen darf; sonst Docker-Host in Phase 1 und AKS mit Phase 2 |
| E2 | Auth in Phase 1 weiter über auth_svc? | Ja, mit rotiertem Schlüssel; der unsichere Passwort-Reset (S-08) wird als Hotfix im Python-Dienst gesperrt bzw. korrigiert; Neubau in Phase 2 |
| E3 | Wer übernimmt die Frontend-Anpassungen AP11? | Klein halten; über PR mit internem Review, egal wer umsetzt |
| E4 | Länder außer DE in Phase 1? | Nur DE (und ggf. AT nach Klärung der MwSt); andere Lieferländer bis Phase 2 nicht anbieten |
| E5 | Rolle der Freelancer | Nur über PR mit internem Review; Preis- und Bestelllogik entsteht intern |
| E6 | Shop-Listenpreis in esERP (für Phase 3) | Prüfen, welcher der 4 Listenpreis-Codes frei ist; jetzt schon festlegen, damit die 500er-Artikel parallel angelegt werden können |

## 10. Risiken

| Risiko | Gegenmaßnahme |
|---|---|
| IP-Freigabe verzögert sich | POC und Entwicklung mit Beispieldaten; Tag 1 verschiebt sich, nicht der Umfang |
| Custom-API-Schritte bei der Auftragsanlage unklar (Code fehlt) | Verhalten aus `raw_order.py` übernehmen und mit Testaufträgen in JTL prüfen; Quellcode beim Freelancer/Insolvenzverwalter anfordern |
| Schattenbetrieb zeigt viele Abweichungen | Jede Abweichung klassifizieren; eindeutige Fehler der Alt-Berechnung dokumentieren (Grundlage für den Fachworkshop) |
| Produktionsstand weicht vom Repo ab | V2 (Sicherung) vor jeder Änderung; Abgleich Server ↔ Repo in AP0 |
| Kapazität < 3 Personen | Reihenfolge: AP0 → AP2 → AP4 → AP6 → AP7 → AP8 sind Pflicht für das Ziel „kein falscher Zahlbetrag“; AP10/AP11 notfalls nach der Testphase |
| Abhängigkeit vom Python-Auth/Business für `_check_page`/Sitemap | Nur interne Weiterleitung dieser Pfade; Neubau in Phase 2 |
