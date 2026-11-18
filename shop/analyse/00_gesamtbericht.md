# Webshop Sicherheit und Service (SN24) – Gesamtanalyse

> **Stand:** 07.10.2026 · **Code-Stand:** `main` @ `9cc4a13` (PR 938, 05.10.2026), Repo `Meesenburg-Group/Sicherheit und Service/WebshopSicherheitUndService`
> **Methode:** statische Analyse des kompletten Repos inkl. Git-Historie (308 Commits, alle Branches), keine Änderungen am Repo. Live-Abfragen nur lesend; die JTL-API war vom Analyse-Rechner aus nicht erreichbar (siehe Abschnitt 8).
> **Zweck:** Diese Analyse soll nicht wiederholt werden müssen. Sie ist die Arbeitsgrundlage für die Stabilisierung (4 Wochen) und die Ablösung des FastAPI-Backends durch eine .NET-API.
> Secrets sind in allen Dokumenten nur maskiert (erste 4 Zeichen + „…“).

## Dokumente

| Nr. | Dokument | Inhalt | Befunde |
|---|---|---|---|
| 00 | dieses Dokument | Gesamtbild, Bewertung, Sofortmaßnahmen, Zeitplan, Zielarchitektur | – |
| 01 | [Kurzübersicht](01_kurzuebersicht.md) | die wichtigsten Punkte auf 1–2 Seiten | – |
| 02 | [Konfiguratoren & Preislogik](02_konfiguratoren_preislogik.md) | beide Konfiguratoren, alle Preisformeln, 18 durchgerechnete Fehlerszenarien, API-Entwurf, 30 Pflicht-Testfälle | K-01 … K-29 |
| 03 | [Backend & JTL-Anbindung](03_backend_jtl.md) | alle Services/Endpunkte, JTL- und Custom-API-Endpunkttabellen, Bestellfluss, Bewertung „JTL hat geändert“ | B-01 … B-45 |
| 04 | [Sicherheit](04_sicherheit.md) | OWASP-orientiertes Audit, Secrets-Inventar, priorisierte Sofortmaßnahmen | S-01 … S-29 |
| 05 | [Frontend & CMS](05_frontend_cms.md) | Technik-Inventar, API-Vertrag (26 Endpunkte), Businesslogik im Frontend, Strapi, Branches | F-01 … F-37 |
| 06 | [Infrastruktur & Prozess](06_infrastruktur_prozess.md) | Topologie (Mermaid), Deployment, Konfiguration, AKS-Readiness, Abhängigkeiten zum Alt-Dienstleister, Prozesskennzahlen | I-01 … I-27 |
| 07 | [esERP als führendes ERP](07_eserp_bewertung.md) | Bewertung esERP statt JTL, Zielbild, Prüfliste Auftrags-API, Zahlung/Mahnwesen, Fragen an es2000/com4 | – |
| 08 | [Umsetzungsplan Phase 1](08_plan_phase1.md) | 2-Wochen-Plan: Arbeitspakete, Tagesplan, Testphase, Umschaltung, Entscheidungen, Risiken | – |
| 09 | [Forensik-Anleitung](09_forensik_anleitung.md) | Server und Logs sichern, Auswertung der Zugriffe, DSGVO-Dokumentation, Systeme außerhalb von Meesenburg (**vertraulich**) | – |
| 10 | [Briefing kritische Themen](10_briefing_kritische_themen.md) | DSGVO, Finanzen, Sicherheit, Verträge – nach Adressat, Fristen, offene Fragen (**vertraulich**) | – |
| 11 | [Zahlungshistorie](11_zahlungshistorie.md) | Go-Live-Eingrenzung, Zahlungsfluss seit 2025, Risikozeiträume für Falschbuchungen, Abgleich-Anleitung | – |
| – | [`../README.md`](../README.md), `../src/` | .NET-10-API + Prüf-Oberfläche im SiSe-Portal-Look (siehe Abschnitt 7) | – |

---

## 1. Kurzfazit

1. **Der Shop ist nicht „fast fertig“, sondern strukturell unsicher und fehleranfällig.** Die Probleme seit Juni sind keine Einzelfehler. Sie folgen aus drei Grundmustern, die sich durch alle Schichten ziehen:
   - **Es gibt keine einzige Stelle der Wahrheit für Preise.** Mindestens sieben unabhängige Berechnungen existieren in Frontend, Backend und ERP-Übergabe.
   - **Fehler werden still verschluckt**, statt gemeldet zu werden.
   - **Die Backend-Dienste prüfen keine Berechtigungen.**
2. **Es gibt akute, live ausnutzbare Sicherheitslücken.** Dazu gehören:
   - Bestellungen lassen sich ohne Login als „bezahlt“ markieren.
   - Kundendaten und Auftrags-PDFs sind über fortlaufende Nummern ohne Login abrufbar.
   - Der PDF-Dienst ist öffentlich erreichbar und erlaubt Codeausführung.
   - Produktive Zugangsdaten liegen im Git.

   → **Sofortmaßnahmen innerhalb 24–72 h** (Abschnitt 4), Datenschutzbeauftragten einbinden (Art. 33 DSGVO).
3. **Die Aussage „JTL ändert die API undokumentiert“ ist nicht haltbar.** Einige Ursachen liegen im eigenen Code:
   - Ein Teil der Detailabfragen ruft JTL **ohne Versionspräfix** auf (`/items/{id}/…` statt `/v1/…`). *Nachtrag 08.10.2026: Live geprüft liefern beide Varianten heute identische Antworten – das ist kein Erklärungsgrund für die bisherigen Probleme, nur ein Zukunftsrisiko.*
   - Die Python-Modelle sind so streng, dass ein einziges `null` ganze Datenblöcke verwirft – **still**, weil weder HTTP-Status noch Validierungsfehler ausgewertet werden.
   - Fällt der Steuersatz-Abruf aus, rechnet das Backend mit **1 % MwSt.**
   - Artikel ohne Kategorie werden **nie** synchronisiert.
   - Der Abgleich von Aufträgen und Kunden aus JTL ist **seit 17.08.2026 abgeschaltet**.
   - Die Rohmodelle wurden seit November 2025 nicht angepasst. Echte Feldänderungen bei JTL hätten das erzwungen.
4. **Die Aussage „Das Backend ist schuld“ trifft teilweise zu, greift aber zu kurz.** Das Backend rechnet den Warenpreis zwar selbst, übernimmt aber Mengen, Konfig-Artikel und Versandkosten ungeprüft vom Client. Das Frontend wiederum enthält Preis-, Rabatt-, Versand-, Geschenk- und MwSt-Logik, die dort nicht hingehört. Beide Seiten haben Anteil.
5. **Die Ablösung des FastAPI-Backends ist richtig und dringlich.** Es gibt drei Gründe:
   - Sicherheit und Datenhoheit.
   - Ohne die Bibliothek `svc_helper` lässt sich kein Backend-Image bauen. Sie liegt als **öffentliches Repo in der GitLab-Gruppe eines Ex-Entwicklers** (`gitlab.com/vitandi1/svc_helper`) und kann jederzeit privat gestellt oder gelöscht werden. Eine Kopie des genutzten Stands ist seit 07.10.2026 unter `referenz/svc_helper` gesichert.
   - Der tatsächliche Produktionsstand ist aus dem Repo nicht reproduzierbar. Er liegt nur auf den Servern des Dienstleisters.

   Der Weg sollte schrittweise („Strangler“) sein, nicht als Komplett-Neubau in einem Schritt (Abschnitt 5).

## 2. Technik-Inventar (Kurzform)

| Bereich | Technik | Bemerkung |
|---|---|---|
| Frontend | SvelteKit 2.17 / Svelte 5.28, TypeScript, adapter-static (reine SPA, kein SSR), nginx, Paraglide i18n | 8 Dateien > 1.000 Zeilen, ~50 verstreute `fetch`-Aufrufe, kein zentraler API-Client, 1 Dummy-Test |
| Backend | Python, FastAPI, 6 Microservices (auth, business, items, mail, payment, pdf), Pydantic v2, httpx, uv | Kernbibliothek `svc_helper` nicht im Repo (öffentliches GitLab-Repo eines Ex-Entwicklers, Kopie gesichert), Testabdeckung < 10 %, items/mail/pdf ohne Tests |
| Daten | 4× MongoDB, MariaDB (Strapi), Valkey, RabbitMQ | je eine Instanz auf Host-Verzeichnissen, **keine Backups**, keine Healthchecks (außer Valkey) |
| CMS | Strapi 5 (package.json 5.30.1, Lockfile 5.6.0), Development-Modus | 41 Single-Types, `.env` mit Secrets eingecheckt |
| ERP | JTL-Wawi REST-API (eazybusiness, `http://…:50443`) + „Custom-API“ (`http://…:58080`, direkter SQL-Lese-/Schreibzugriff auf die JTL-DB, Code nicht im Repo) + 16 Trigger in der JTL-DB (`trigger_up.sql`) | unverschlüsselt (HTTP), Custom-API schreibt u. a. Zahlungen direkt in `tZahlung` |
| Zahlung | Stripe (PaymentIntents, Webhook mit korrekter Signaturprüfung) | PaymentIntent wird bei geändertem Warenkorb nicht angepasst |
| Betrieb | Docker Compose / Swarm (`node1..3`), Traefik, Registry `zot.local:5000` | kein CI/CD, manuelle Deploys, Let's-Encrypt-Account beim insolventen Dienstleister |

## 3. Wichtigste Befunde (dokumentübergreifend)

| # | Schwere | Befund | Details |
|---|---|---|---|
| 1 | Kritisch | `PATCH /api/order/{id}/mark_paid` öffentlich ohne Auth und ohne Stripe-Abgleich – Bestellung gilt als bezahlt und wird mit vollem Betrag als Stripe-Zahlung ins ERP gebucht | K-01, S-02, B |
| 2 | Kritisch | Bestellungen, Kundendaten, Auftrags-PDFs und Warenkörbe ohne Login abrufbar (fortlaufende Nummern); E-Mail-Prüfung der Bestellsuche nur im Browser | S-01, S-09, F |
| 3 | Kritisch | PDF-Service öffentlich: Template-Injection (Codeausführung) und SSRF | S-03 |
| 4 | Kritisch | Produktive Secrets im Repo und in der Historie (JTL-Token, Custom-API-Token, SMTP, Strapi, JWT-Schlüssel, TLS-Keys) | S-04, S-05, B-18, I-02 |
| 5 | Kritisch | Stripe-Betrag und Bestellung laufen auseinander (alter PaymentIntent wird wiederverwendet) – auch ohne Manipulation | K-02, S-06 |
| 6 | Kritisch | Mengen (auch 0/negativ), Konfig-Artikel und Versandkosten kommen ungeprüft vom Client | K-03, K-04, S-06 |
| 7 | Hoch | Mindestens 7 unabhängige Preisberechnungen; kleiner (`/shop`) und großer (`/configurator`) Konfigurator rechnen unterschiedlich | 02 Kap. 2.5–2.7 |
| 8 | Hoch | Großer Konfigurator verliert Aufpreise (Zusammenlegen identischer Positionen, Längen-Variationen ignoriert), Schließcode per Regex „geraten“, Schließplan erreicht die Bestellung nicht | K-07 … K-10 |
| 9 | Hoch | Sonderpreise/Kundengruppen nur im Frontend (`localStorage`) – Kunde sieht anderen Preis als abgebucht; Kundengruppe ist manipulierbar | K-05, F |
| 10 | Hoch | MwSt fest 19 % im Backend, Fallback 1,19 an ≥ 15 Stellen im Frontend, 1 % bei Ausfall des Steuerabrufs im items_svc; Gleitkomma ohne Cent-Rundung | K-12, B, F |
| 11 | Hoch | Stille Fehlerbehandlung im JTL-Abgleich: HTTP-Status ignoriert, Validierungsfehler verschluckt, leere Ergebnisse überschreiben gute Daten | B-01, B-07 |
| 12 | Hoch | Abgleich Aufträge/Kunden JTL → Shop seit 17.08.2026 deaktiviert; Bestände werden nicht aus JTL gelesen und nicht geprüft („immer auf Lager“) | B |
| 13 | Hoch | Keine Backups, keine Redundanz, kein CI/CD; Produktionsstand nicht reproduzierbar | I-01, I-03, I-06, I-09 |
| 14 | Hoch | Abhängigkeiten zum insolventen Dienstleister: `svc_helper` (GitLab-Gruppe eines Ex-Entwicklers), Strapi-Instanz, Nextcloud (Assets/DB-Dumps), Gitea, Let's-Encrypt-Account, Registry/Server | I-04, I-05 |
| 15 | Hoch | JTL-Server: RDP (3389) aus dem Internet erreichbar; JTL-REST und Custom-API nur per HTTP | 04 Abschn. 6, S-13 |

**Prozess:** Seit dem Go-Live sind 65 % der Commits „fix“-Commits (vorher 26 %, im September 15 von 16). Es gab 34 direkte Pushes auf `main`, Reviews sind nicht nachweisbar, und die Mengen-Skalierung der Konfig-Artikel wurde von zwei Entwicklern getrennt in Frontend und Backend geändert. Daraus entstanden belegbare Zeiträume mit Über- bzw. Unterberechnung: 17.08.–09.09., 08.–09.09. und vor dem 23.09.2026.

## 4. Sofortmaßnahmen (vor jeder Weiterentwicklung)

| Wann | Maßnahme | Wer |
|---|---|---|
| **heute** | Traefik: öffentliche Routen auf Allowlist reduzieren. Sperren u. a. `/api/pdf/*`, `…/mark_paid`, `…/send_order_to_system/*`, `/api/maildata/*`, `/api/*/docs`, `openapi.json`, Demo-Routen | Betrieb |
| **heute** | `single_order`, `order?user_id=`, `cart/{id}` serverseitig an Login/Eigentum binden (oder vorübergehend sperren; die Bestellsuche für Gäste deaktivieren) | Backend |
| **heute** | Host-Ports von Mailpit, RabbitMQ, Valkey, mongo-express, Redis-Commander, Frontend-Direktport schließen; `db_viewer.yml` aus dem Deploy | Betrieb |
| **heute** | **Produktionsstand sichern:** Compose/Stack-Dateien, `.env`, Traefik-Konfiguration, Images aus `zot.local`, Datenbank-Dumps (alle Mongo, MariaDB), Uploads/Anhänge von den Servern holen | Betrieb |
| 24 h | Alle Secrets rotieren: JTL-Token → Custom-API-Token → SMTP → JWT-Schlüssel → Strapi → DB/MQ → Stripe. Prüfen, ob Live-Keys in Prod aktiv sind. Neue Werte in den Key Vault, nicht ins Repo | IT |
| 24 h | Forensik: Access-Logs nach Zugriffen auf die o. g. Pfade durchsuchen; **alle „bezahlt“-Aufträge in JTL gegen Stripe abgleichen**, besonders in den Zeiträumen mit Fehlberechnung (Abschnitt 3); DSGVO-Bewertung | IT + DSB + Buchhaltung |
| 48 h | Nutzungsrechte an `svc_helper` schriftlich klären (Code ist gesichert), Custom-API-Quellcode einfordern (Freelancer/Insolvenzverwalter); Let's-Encrypt, Domains, Strapi-Instanz, Nextcloud-Inhalte übernehmen | Projektleitung |
| 72 h | ecomdata: RDP nur per VPN, TLS für JTL-REST/Custom-API oder Site-to-Site-VPN; IP-Freigabe für die künftige AKS-Egress-IP | IT |
| 72 h | Branch-Policy `main` in Azure DevOps: PR-Pflicht, 1 Reviewer (intern), Build-Validierung | Projektleitung |

## 5. Weg zum stabilen Shop in 4 Wochen

**Leitlinie:** Der Shop soll in 4 Wochen ohne Logikfehler laufen. Ein vollständiger Neubau aller sechs Python-Dienste ist in dieser Zeit nicht seriös machbar. Deshalb ersetzt eine .NET-API zuerst die **geschäftskritischen** Teile: Katalog, Preise, Warenkorb, Bestellung und Zahlung. Weniger kritische Dienste (Mail, PDF, Auth) bleiben vorübergehend, aber gesperrt bzw. nur intern erreichbar. Traefik leitet Pfad für Pfad auf die neue API um („Strangler“). Das Frontend bleibt weitgehend, bekommt aber einen zentralen API-Client und **rechnet nichts mehr selbst**.

| Woche | Ziel | Ergebnis |
|---|---|---|
| **1** | Absichern, Fakten schaffen | Sofortmaßnahmen umgesetzt. POC live gegen JTL (IP-Freigabe): Schnittstellen-Stand speichern, damit jede künftige Änderung belegbar ist. **Golden-Tests** für Preise: die 30 Pflicht-Testfälle aus Dokument 02 mit Soll-Preisen aus JTL (vom Fachbereich bestätigt). Repo-Bereinigung: Secrets raus, CI-Pipeline (Build + Tests + gitleaks). |
| **2** | .NET-API: Katalog + Preis-Engine | Artikel-Lesemodell aus JTL ohne stilles Verwerfen, nur `/v1`, Fehler als Sync-Probleme sichtbar. **Eine** Preis-Engine (decimal, Cent-Rundung je Position, MwSt aus Steuerklasse und Lieferland, Kundengruppe aus der Session, Sonderpreise, Konfig-Aufpreise, Versand, Geschenke). Endpunkte `POST /pricing/quote` (Warenkorb oder Konfiguration → fertig berechnetes Angebot) und Schließplan-Prüfung. Alle Golden-Tests grün. |
| **3** | Frontend auf die neue API, Bestellung/Zahlung | Beide Konfiguratoren und der Warenkorb zeigen nur noch Preise aus `/pricing/quote`; 15+ MwSt-Fallbacks und Mock-Daten entfernt. Bestellung serverseitig aus dem letzten Angebot (eingefroren), PaymentIntent je Angebot, Zahlungsbestätigung nur per Stripe-Webhook mit Betragsabgleich, Schließplan in der Bestellung. Traefik leitet `/api/items`, `/api/cart`, `/api/order` auf .NET. |
| **4** | Betrieb im AKS, Abnahme | Container im AKS (Ingress, Key Vault CSI, Managed Mongo/Cosmos DB oder in-cluster mit Backups, Monitoring/Alerts wie im SiSe Portal), Lasttest, Abnahme mit Fachbereich anhand der Golden-Tests, Cutover mit Rückfallplan. Danach: Auth/Mail/PDF schrittweise nachziehen. |

### 5a. Angepasster Zeitplan (Abstimmung 07.10.2026)

> Detaillierter Plan mit Arbeitspaketen und Tagesplan: [Bericht 08](08_plan_phase1.md). Die Wochentabelle oben gilt als Ausgangsfassung; maßgeblich ist dieser Abschnitt.

Phase 1 soll in **2 Wochen** fertig sein, danach folgt eine kurze Testphase. Die genaue Planung erfolgt, sobald die JTL-Schnittstelle abgefragt werden kann (IP-Freigabe). Dafür wird der Umfang geschärft: Die .NET-API wird **Herrin über den Zahlbetrag, noch nicht über die Konfiguration**.

| Phase | Inhalt |
|---|---|
| **1 (2 Wochen + Test)** | .NET-API ersetzt FastAPI für Katalog, Warenkorb, Bestellung und Zahlung. Der Konfigurator im Frontend bleibt vorerst, schickt aber nur noch **aufgelöste Artikel + Mengen**. Das Backend rechnet daraus selbst: Preis je Artikel aus dem ERP × Menge, MwSt, Versand. Mengen ≤ 0 und unbekannte Artikel werden abgelehnt. Der Stripe-Betrag kommt nur aus dieser Berechnung, `mark_paid` entfällt. Abweichungen zum Frontend-Preis werden protokolliert und dem Kunden vor der Zahlung korrigiert angezeigt. Dazu kommen die Sofortmaßnahmen aus Abschnitt 4. Ergebnis: **kein falscher Zahlbetrag mehr**, auch wenn die Konfigurationsregeln noch nicht festgeschrieben sind. |
| **parallel** | **Fachworkshop Regeln:** Schließcode B–F, erlaubte Längen je Zylindertyp, Schlüsselanzahl/Zusatzschlüssel, Aufpreise, Geschenke, Versand. Grundlage sind die Formeln, Widersprüche und 30 Testfälle aus Bericht 02. Ergebnis: eine verbindliche Spezifikation mit Soll-Preisen. |
| **2** | Konfigurationsregeln in die Shop-API (Konfigurationsmodell mit Verweis auf Artikelnummern, Pflege über das SiSe Portal), Testfälle als automatische Tests, Frontend vereinfacht |
| **3** | Wechsel JTL → esERP (siehe Bericht 07): 500er-Artikel in esERP, Auftrags-API, Bestand, DMS, Zahlungskennzeichen im Fibu-Export |

**Begründung:** Die heutigen Regeln sind **nicht eindeutig**. Es gibt sieben widersprüchliche Preisberechnungen, der Schließcode wird per Regex geraten, Regeln sind fest im Code eingetragen, und im Konfigurator stecken Mock-Daten. Sie ohne festgeschriebene Spezifikation ins Backend zu verschieben, würde die Fehler nur verlagern.

**Voraussetzungen und Risiken:**
- IP-Freigabe bei ecomdata, damit JTL vom AKS aus erreichbar ist.
- Soll-Preise vom Fachbereich, das ist die Grundlage der Golden-Tests.
- Klärung, ob die Custom-API weiterbetrieben werden darf (Code fehlt, sie schreibt direkt in die DB) oder ob ihre Lesefunktionen über die offizielle REST-API bzw. eine eigene Lese-View abgebildet werden.
- Rolle der Freelancer: Wenn sie bleiben, nur mit PR und internem Review. Die Preis-Engine entsteht bei uns.

## 6. Zielarchitektur

```
Browser (SvelteKit-SPA, unverändert bis auf zentralen API-Client)
   │  /api/*  (Ingress, nur freigegebene Routen)
   ▼
Shop-API (.NET 10, Minimal APIs, Apis → Services → Brokers wie im ES-Projekt)
   ├─ Katalog-Service ──── Lesemodell (Mongo/Cosmos) ◄── Sync-Worker ◄── IErpCatalogBroker
   ├─ Preis-Engine  (einzige Stelle für Preise, MwSt, Versand, Rabatte, Konfig-Aufpreise)
   ├─ Warenkorb/Bestellung ──► IErpOrderBroker ──► JTL heute / esERP später
   ├─ Zahlung (Stripe, Webhook)
   └─ Auth (später; bis dahin Alt-Dienst intern)
Secrets: Azure Key Vault · Logs/Monitoring: wie SiSe Portal (Mongo-Log, Sync-Probleme) · Betrieb: AKS
```

**JTL → esERP später** (Bewertung in [Bericht 07](07_eserp_bewertung.md)): Die Shop-API spricht das ERP nur über Broker-Schnittstellen an (`IErpCatalogBroker`, `IErpOrderBroker`, `IErpCustomerBroker`). Der Wechsel auf esERP ist dann ein weiterer Broker. Frontend und Preis-Engine bleiben unverändert. Das passt zu den vorhandenen es-ERP-Schnittstellen im ES-Projekt.

## 7. Proof of Concept (Hauptordner `src/`)

Ziel: Die Behauptung „JTL ändert die API undokumentiert“ **selbst prüfen können** und die Basis der neuen API zeigen.

| Teil | Inhalt |
|---|---|
| `Meesenburg.Shop.JtlApi` | .NET 10 Minimal API im Stil des ES-Projekts (Apis/Services/Brokers, Scalar, ProblemDetails, Resilience, Key Vault). Liest Artikel, Preise, Varianten und Bilder aus JTL (immer `/v1`), berechnet Brutto **serverseitig** mit `decimal` und ohne stillen Rückfallwert. Dazu kommt lesender Zugriff auf die Custom-API mit einer festen Liste erlaubter GET-Pfade. |
| Schnittstellen-Prüfung | Prüft jede JTL-Antwort gegen die **exakten Erwartungen des Alt-Backends**, nämlich alle Pydantic-Felder aus `item_raw_schemas.py`, und zeigt zu jedem Treffer, welche Daten das Alt-Backend still verwerfen würde. Sie vergleicht `/v1` mit den unversionierten Pfaden des Alt-Backends und speichert Schnittstellen-Stände (nur Struktur, keine Werte), damit jede Änderung **mit Datum belegbar** ist. |
| `Meesenburg.Shop.Portal` | Blazor-Oberfläche im SiSe-Portal-Design (MudBlazor 9.9, Theme/CSS/MeePage aus `Meesenburg.ES.Web`): Artikel, Artikeldetail mit Rohdaten, Schnittstellen-Prüfung, Konfigurator-Daten (Custom-API), Verbindung. |
| Beispieldaten | Ohne JTL-Zugang läuft alles mit erfundenen, klar gekennzeichneten Beispieldaten, in die typische Fehlerfälle eingebaut sind: `null`-Datum, keine Kategorie, Rabatt 7,5 %, fehlender Steuersatz und Bestand 12,5. |
| Container | Dockerfiles (Multi-Stage, Nicht-root), lokal gebaut und getestet. |

Start und Konfiguration: siehe [`README.md`](../README.md) im Hauptordner.

## 8. Live-Prüfung am 07.10.2026

- Von der IP `216.83.217.161` aus waren die JTL-REST-API (Port 50443) und die Custom-API (Port 58080) nicht erreichbar (TCP-Timeout, vermutlich IP-Freigabe).
- **Port 3389 (RDP) des JTL-Servers war aus dem Internet erreichbar.** Darüber hinaus wurde nichts getestet.
- Alle Aussagen zu JTL-Antworten beruhen deshalb auf dem Code. Sobald die Freigabe erteilt ist, sollte als erster Schritt in der Schnittstellen-Prüfung ein Stand gespeichert werden.

## 9. Offene Fragen

1. Welche Deploy-Variante läuft tatsächlich in Produktion, und wie ist der Server-Traefik konfiguriert? (Antwort nur vom Server.)
2. Sind in Produktion Stripe-**Live**-Keys aktiv, und welcher JWT-Schlüssel ist gesetzt?
3. Wo liegt der Quellcode der Custom-API, und wer betreibt sie? Wer hält die Rechte an `svc_helper` (Code gesichert, siehe Abschnitt 10)?
4. Welche Kundengruppen und Sonderpreise gelten fachlich im Shop (B2C/B2B), und welche Länder werden beliefert? Davon hängt die MwSt-Logik ab.
5. Welche Soll-Preise gelten für die 30 Golden-Testfälle? Der Fachbereich muss sie bestätigen.
6. Wurden PRs in Azure DevOps reviewt? (Aus Git nicht nachweisbar.)

## 10. Nachträge

**07.10.2026 – `svc_helper` gesichert, Korrektur „privates GitLab“:**
- Die Bibliothek liegt in der **öffentlichen** GitLab-Gruppe `vitandi1` (https://gitlab.com/vitandi1/svc_helper), nicht in einem privaten Repo. Der Stand von `main` ist genau der im Shop genutzte Commit `5e65dee9`.
- **Gesichert** unter `referenz/svc_helper`: Klon mit vollständiger Historie, 34 Commits, Autoren „vitandi“ und Christopher Seidel.
- **Lizenz:** Die Datei `LICENSE` ist eine MIT-Lizenz, der Copyright-Vermerk lautet aber auf „Charles Marsh“. Das ist offenbar eine kopierte Vorlage, der tatsächliche Rechteinhaber ist damit nicht benannt. → **Nutzungsrechte schriftlich klären** (Inhaber bzw. Insolvenzverwalter).
- In der Gruppe liegen außerdem `ansible_roles` (zuletzt aktiv 01/2026, möglicherweise die Servereinrichtung), `webpage-template` und `ordm`.
- Für Phase 1 ist das unkritisch: Die .NET-API ersetzt `svc_helper`. Wichtig bleibt die Kopie für Hotfixes an den Python-Diensten, die vorerst weiterlaufen (auth, mail, pdf).
