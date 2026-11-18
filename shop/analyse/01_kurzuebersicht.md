# 01 – Kurzübersicht: Webshop Sicherheit und Service

> **Stand:** 07.10.2026 · Details in den Berichten 00–08 · **vertraulich** (enthält Hinweise auf offene Sicherheitslücken)

## Lage
- Der Shop ist seit Juni 2026 live, der Dienstleister ist insolvent, zwei Ex-Entwickler arbeiten als Freelancer weiter. Seit dem Go-Live sind **65 % aller Commits Fehlerbehebungen** (vorher 26 %).
- Technik: SvelteKit-Frontend (reine SPA), 6 Python/FastAPI-Dienste, 4× MongoDB, Strapi, JTL-Wawi + „Custom-API“ (direkter SQL-Zugriff, Code nicht im Repo), Docker auf Servern des Dienstleisters.
- **Keine Tests, kein CI/CD, keine Backups.** Der Produktionsstand ist aus dem Repo nicht reproduzierbar. Das Backend hängt an einer Bibliothek aus der **GitLab-Gruppe eines Ex-Entwicklers** (öffentlich, jederzeit entziehbar; Kopie seit 07.10. unter `referenz/svc_helper` gesichert, Rechte ungeklärt).

## Kritisch – sofort handeln
1. **`mark_paid` ist öffentlich:** Bestellungen lassen sich ohne Login als „bezahlt“ markieren und gehen so an JTL.
2. **Kundendaten offen:** Bestellungen, Adressen und Auftrags-PDFs sind über fortlaufende Nummern ohne Login abrufbar → Datenschutzvorfall prüfen (Art. 33 DSGVO).
3. **PDF-Dienst öffentlich** und für Codeausführung missbrauchbar.
4. **Zugangsdaten im Git:** JTL, Custom-API, SMTP, JWT-Schlüssel, Strapi → alle rotieren. **Live bestätigt (09.10.):** Login-JWT-Schlüssel und DB-Passwörter sind die Repo-Standardwerte → Anmeldungen fälschbar (Bericht 12).
5. **Stripe-Betrag ≠ Bestellbetrag** möglich: Ein alter Zahlungsvorgang wird wiederverwendet, auch ohne Manipulation.
6. JTL-Server: **Remote Desktop (3389) aus dem Internet erreichbar**, die APIs laufen über unverschlüsseltes HTTP.

## Warum die Preise falsch sind
- **Sieben unabhängige Preisberechnungen** (Frontend, Backend, ERP-Übergabe). Kleiner (`/shop`) und großer (`/configurator`) Konfigurator rechnen unterschiedlich.
- **Mengen, Konfig-Artikel und Versandkosten** kommen ungeprüft aus dem Browser. Sonderpreise gibt es nur im Frontend.
- **MwSt:** fest 19 % im Backend, Rückfall 1,19 an 15 Stellen im Frontend, **1 %**, wenn der Steuerabruf ausfällt.
- Der große Konfigurator **verliert Aufpreise** beim Zusammenlegen identischer Positionen. Der Schließcode wird per Regex aus dem Artikelnamen geraten, und der Schließplan erreicht die Bestellung nicht.
- **Die Regeln sind nirgends eindeutig festgelegt.** Sie stecken verteilt in Code, JTL-Daten und Köpfen.

## Bewertung der Schuldzuweisungen
- **„JTL ändert die API undokumentiert“: nicht haltbar.** Live geprüft (08.10.) liefert JTL mit und ohne `/v1` identische Antworten. Die Ursachen liegen im eigenen Code: Fehler werden still verschluckt, ein einziges `null` verwirft ganze Datenblöcke, Artikel ohne Kategorie kommen nie an, und der Auftragsabgleich ist seit 17.08. abgeschaltet. Das alles ist im eigenen Code.
- **„Das Backend ist schuld“: teilweise.** Frontend und Backend haben beide Anteil (doppelte und widersprüchliche Logik).

## Plan
| Phase | Dauer | Inhalt |
|---|---|---|
| **0** | sofort | Lücken sperren, Produktionsstand sichern, Secrets rotieren, Aufträge gegen Stripe abgleichen |
| **1** | 2 Wochen + 3–5 Tage Test | .NET-API ersetzt FastAPI für Katalog, Warenkorb, Bestellung, Zahlung. **Die API berechnet den Zahlbetrag** aus Artikelpreisen × Mengen; der Konfigurator bleibt vorerst im Frontend. Fehler werden sichtbar. Umschaltung stufenweise über Traefik mit Rückfall. (Bericht 08) |
| parallel | – | **Fachworkshop:** Konfigurationsregeln und Soll-Preise verbindlich festlegen |
| **2** | danach | Konfigurationsregeln in die API (mit Tests), Frontend vereinfachen, Auth/Mail/PDF ablösen, AKS |
| **3** | danach | **Wechsel JTL → esERP** |

## esERP statt JTL (Bericht 07)
- **Machbar.** SAP bleibt außen vor. Shop-Artikel werden als esERP-eigene **500er** angelegt, die der SAP-Abgleich nicht anfasst. Bestand und DMS liegen in esERP, ein Shop-Listenpreis ist möglich (einer der 4 Listenpreis-Codes). com4 kann die nötigen esapi-Endpunkte bauen.
- **Konfigurationsartikel gibt es in esERP nicht.** Kaskaden sind nur starre Sets. Die Regeln liegen deshalb in der Shop-API und verweisen auf 500er-Artikelnummern. Das war ohnehin das Ziel.
- **Zahlung:** Die Buchhaltung läuft in SAP (CSV-Export aus esERP, eigener Code). Ein „Stripe – bezahlt“-Kennzeichen mit Ausgleich gegen ein Stripe-Verrechnungskonto verhindert Mahnungen. **Stripe-Referenzen müssen durchgängig zuordenbar sein** (Shop-Bestellnummer in Stripe, JTL/esERP und SAP).

## Nächste Schritte
1. **Erst sichern** (Logs, Server, Datenbanken – Bericht 09), dann Sofortmaßnahmen (Phase 0) umsetzen, dann die Freelancer informieren.
2. IP-Freigabe bei ecomdata → JTL-Schnittstellen-Stand mit dem POC speichern.
3. Entscheidungen E1–E6 aus Bericht 08 treffen (Betriebsort, Auth, Länder, Rollen).
4. Termin Fachworkshop und Ansprechpartner für Soll-Preise festlegen.
5. com4: Auftrags-API, Bestand, Dokument-Upload; es2000: Neuerungen 3.4, Konfigurationsartikel.
6. SAP-Fibu: Stripe-Verrechnungskonto und Ausgleichslogik.

## Dokumente
| Nr. | Inhalt |
|---|---|
| 00 | Gesamtbericht: Bewertung, Sofortmaßnahmen, Zeitplan, Zielarchitektur |
| 02 | Konfiguratoren & Preislogik (29 Befunde, 30 Testfälle) |
| 03 | Backend & JTL-Anbindung (45 Befunde, Endpunkt-Tabellen) |
| 04 | Sicherheit (29 Befunde, Secrets-Inventar) – **vertraulich** |
| 05 | Frontend & CMS (37 Befunde, API-Vertrag) |
| 06 | Infrastruktur & Prozess (27 Befunde, Topologie) |
| 07 | esERP als führendes ERP |
| 08 | Umsetzungsplan Phase 1 |
| 09 | Forensik-Anleitung: sichern, auswerten, dokumentieren – **vertraulich** |
| 10 | Briefing kritische Themen (DSGVO, Finanzen, Sicherheit, Verträge) nach Adressat – **vertraulich** |
| 11 | Zahlungshistorie: Risikozeiträume für Falschbuchungen, Abgleich |
| 13 | **JTL-Schnittstelle: Referenz für das C#-Backend** (Versionen, Header, verifizierte Fallstricke, genutzte Endpunkte) – lebendes Dokument |
| 12 | Zugangsdaten tauschen und Entwickler aussperren (Hetzner, JTL/ecomdata, DevOps, Drittdienste) – **vertraulich** |
