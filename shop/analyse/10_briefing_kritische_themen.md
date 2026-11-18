# 10 – Briefing: kritische Themen Webshop SN24 (interne Information am 08.10.2026)

> **Stand:** 07.10.2026 · **VERTRAULICH – nur Need-to-know, nicht per Mail/Teams-Chat weitergeben** · **Keine Rechtsberatung.** Die rechtliche Bewertung treffen Datenschutzbeauftragter (DSB) und Rechtsabteilung.
> **Grundlage:** Berichte 00–09, `TODO.md`, Code-Stand `9cc4a13`. Fundstellen `Datei:Zeile` beziehen sich auf das Shop-Repo `webshop/`. Secrets werden nicht genannt.
> **[neu]** = Erkenntnis aus der Code-Prüfung für dieses Briefing, die so nicht in den Berichten steht. **[zu prüfen]** = aus dem Repo nicht belegbar.

## 1. Die wichtigsten Botschaften

1. **Kundendaten sind ohne Login abrufbar.** Über fortlaufende Nummern lassen sich Bestellungen mit Name, Anschrift, E-Mail, Telefon und Auftrags-PDF abrufen. Ob das jemand ausgenutzt hat, wissen wir noch nicht. Die Lücke ist seit **07.10.2026** bekannt. Der DSB sollte deshalb vorsorglich mit einer Frist bis **Sa 10.10.2026** planen (72 h nach Art. 33 DSGVO).
2. **Bei einem Schließanlagen-Anbieter geht es nicht nur um Adressdaten.** Offen liegen auch die Zuordnung „wer hat welches Schließsystem an welcher Adresse“, gespeicherte Schließpläne und hochgeladene **Sicherungskarten**. Sicherungskarten berechtigen zur Nachbestellung von Schlüsseln.
3. **Zahlungsdaten im ERP sind nicht verlässlich – nicht erst seit August.** Seit dem 20.08.2026 (Merge von `mark_paid`) kann jeder ohne Login eine Bestellung als „bezahlt“ markieren. Schon vorher konnten Testzahlungen, Doppelzahlungen und fehlgeschlagene Zahlungen in JTL gebucht oder bezahlte Zahlungen gar nicht gebucht werden (Kap. 6.2, Bericht 11). Der Abgleich muss deshalb **ab Februar 2026** laufen. Der Stripe-Betrag kann vom Bestellbetrag abweichen. JTL bucht trotzdem immer die volle Bestellsumme als Stripe-Zahlung. Die Buchhaltung muss alle Shop-Aufträge gegen Stripe abgleichen.
4. **Live-Prüfung 07.10.2026:** Die **Mailpit-Weboberfläche ist ohne Login aus dem Internet erreichbar** (Port 8025). Was darin liegt, muss morgen eine befugte Person prüfen; danach Port schließen. RabbitMQ und die Datenbanken sind von außen nicht erreichbar. Der Server liegt bei **Hetzner** (RZ Falkenstein).
5. **Der Server kann übernommen werden.** Der PDF-Dienst ist öffentlich erreichbar und erlaubt Codeausführung. Außerdem liegen produktive Zugangsdaten (ERP, Mailpostfach, CMS) im Git. Die Reihenfolge ist deshalb: **erst sichern, dann sperren, dann Zugangsdaten tauschen**.
6. **Personenbezogene Daten gehen an Stellen ohne geklärten Vertrag zur Auftragsverarbeitung (AVV).** Dazu gehören:
   - eine Rückfall-Adresse beim Freelancer,
   - ein Test-Werkzeug, das Produktivdaten an feste Adressen schickt,
   - **eine Blindkopie (BCC) jeder Bestellbestätigung an `info@sicherheitstechnik-nord.de`** [neu]. Das ist zugleich das Absender- bzw. SMTP-Konto des Shops (`Readme.md`, `MAIL_SMTP_USER`), also vermutlich das eigene Shop-Postfach („Sicherheitstechnik Nord“ = SN24). Unkritisch, **wenn** Domain und Postfach Meesenburg gehören,
   - der SMTP-Anbieter, der unbekannte Shop-Hoster und die Systeme beim insolventen Dienstleister,
   - Chat- und Bewertungs-Widgets, die schon vor der Einwilligung laden.
7. **Der Betrieb hängt an Dritten.** Es gibt keine Backups, und der Produktionsstand liegt nur auf fremden Servern. Hinzu kommen eine Kernbibliothek mit ungeklärten Rechten, die Custom-API ohne Quellcode und das Zertifikatskonto auf einer Adresse des Ex-Dienstleisters.
8. **Preise und MwSt sind fehleranfällig.** Der angezeigte Preis kann vom abgebuchten abweichen, und die MwSt ist fest auf 19 % gesetzt. Belegbar sind Zeiträume mit Über- bzw. Unterberechnung. Daraus können Verbraucher- und Wettbewerbsrisiken sowie Korrekturen gegenüber Kunden folgen.
9. **Die Ursachen sind strukturell:** keine Prüfungen im Backend, keine Tests, kein Review. Es geht nicht um Fehler einzelner Personen. Die Freelancer werden **erst nach Sicherung und Sperrung** informiert.

## 2. Nach Adressat

### 2.1 Geschäftsführung

| Thema | Sachverhalt (kurz) | Risiko | Frist | Entscheidung/Aktion erbeten | Beleg |
|---|---|---|---|---|---|
| Möglicher Datenschutzvorfall | Bestellungen, Kundendaten und Auftrags-PDFs sind ohne Login abrufbar | Meldepflicht, Bußgeld, Reputation; Sicherheitsbezug (Schließanlagen) | 08.10. | DSB sofort beauftragen; Budget/Mandat für Forensik | S-01, S-09, 09 Kap. 6 |
| Zahlungsintegrität | „bezahlt“ ist ohne Login setzbar; Stripe-Betrag ≠ Bestellbetrag | Warenabgabe ohne Zahlung, falsche Buchungen | 24 h | Abgleich JTL ↔ Stripe beauftragen (Buchhaltung + IT) | K-01, K-02, S-16 |
| Server-Übernahme | PDF-Dienst öffentlich mit Codeausführung; Zugangsdaten im Git | Kompromittierung, Datenabfluss, Phishing über das Shop-Postfach | sofort nach Sicherung | Sperrung freigeben, auch wenn Teilfunktionen ausfallen (z. B. Bestellsuche für Gäste) | S-03, S-04 |
| **Kein eigener Serverzugang** | Die Shop-Server liegen bei einem unbekannten Hoster, Zugang haben vermutlich nur die Freelancer | Sichern und Sperren ist ohne sie evtl. nicht möglich | 08.10. | Weg zum Zugang festlegen: Hoster direkt oder Freelancer mit Minimalinfo; Rückfall Wartungsmodus | 06 A6, 09 Kap. 1 |
| Abhängigkeit vom Insolvenzverfahren | Strapi, Nextcloud, Gitea, Registry, LE-Account, Code-Rechte | Ausfall, Datenverlust, Rechtsunsicherheit | 48 h | Mandat an Einkauf/Recht für Insolvenzverwalter; Rolle Freelancer (E5) | I-04, I-05, 06 Kap. 6 |

### 2.2 Datenschutzbeauftragter

| Thema | Sachverhalt (kurz) | Risiko | Frist | Entscheidung/Aktion erbeten | Beleg |
|---|---|---|---|---|---|
| Offene Endpunkte (IDOR) | `single_order` existiert seit 11/2025, also live seit Go-Live; `/maildata/*` mit Auftrags-PDF seit **09.09.2026** [neu]; `order?user_id=`; Warenkörbe; Schließpläne | Unbefugte Offenlegung, Art. 32/33/34 | bis 10.10. | Bewertung Meldepflicht; Forensik nach 09 Kap. 5 | S-01, S-09; `…/endpoints/mail_data.py:8-32` (Commit `c94dd9e`), `…/endpoints/order.py:102-107` |
| Kreis der Betroffenen | Der Abruf liest neben Shop-Bestellungen auch die aus JTL gespiegelten Aufträge (`order_erp`) [neu] | Mehr Betroffene als nur Shop-Kunden | 08.10. | Anzahl Datensätze `order`, `order_erp`, `user`, `configurations` ermitteln | `…/jtl_helper/mail_data.py:58-79`; `…/utils/order.py:166-177` |
| Sicherungskarten | **Sicherheitskritisch:** Wer Karte (bzw. Bild) und Kundendaten hat, kann Schlüssel nachbestellen. Für einen Sicherheitsfachhändler ein Vertrauens- und Haftungsthema, zusätzlich DSGVO (personenbezogen, Speicherbegrenzung Art. 5 Abs. 1 e, angemessener Schutz Art. 32). Weg 1, Upload-Formular: ohne Login und ohne Prüfung, Base64 über RabbitMQ → Mail an `sicherheit@meesenburg.com` über externen SMTP (seit 09.09.2026). RabbitMQ ist laut Portprüfung vom 07.10.2026 **von außen nicht erreichbar**. Weg 2, Warenkorb: **unverschlüsselt** im business-Container (`resources/order_attachments`), **ohne Löschregel**, aber auch **ohne Volume** (bei Neuaufbau des Containers verloren), per **HTTP** an JTL [neu]. Kein Abruf-Endpunkt im Shop; Zugriff nur mit Server-, Postfach-, MQ- oder JTL-Zugang. Zugriffskreis auf dem Server ist unbekannt (Hoster, Freelancer, Ex-Dienstleister) | Missbrauch für Schlüssel-Nachbestellung; Mitlesen bei der HTTP-Übertragung | 08.10. | Zugriffskreis Postfach/Server/JTL festlegen; Löschregel (z. B. nach Versand); Übertragung nur verschlüsselt; **business-Container vor Neuaufbau sichern** | S-12, I-12; `…/endpoints/upload_security_card.py:17-53`; `mail_svc/…/mail_templates.py:131-148`; `…/utils/file.py:14,45-82,94-140`; `business/…/core/config.py:55` |
| Externe Mail-Empfänger | **BCC jeder Bestellbestätigung inkl. PDF an `info@sicherheitstechnik-nord.de`** seit 07.09.2026 [neu], dieselbe Adresse wie Absender/SMTP-Konto des Shops, vermutlich eigenes Postfach (Inhaberschaft klären). Rückfall an `c.seidel@neogris.com` greift bei Bestellbestätigungen ohne E-Mail. Das lokale Test-Tool holt Produktivdaten über die öffentlichen `/api/maildata` und schickt sie an feste Adressen, darunter die des Freelancers | Übermittlung an Dritte ohne AVV | 08.10. | Inhaber der Domain/des Postfachs klären; Mail-Logs beim SMTP-Anbieter anfordern | `mail_templates.py:252-253,269,284-285`; `mail_svc/src/mail/main.py:20-24,139-177`; I-19 |
| Logs/Queue mit PII | Das Access-Log enthält Session-Cookies, Reset-Links und Query-Parameter, **ohne Rotation**. Stripe-Events werden vollständig geloggt. Das **Klartext-Passwort** neuer Kunden steht in der RabbitMQ-Nachricht; die MQ-Oberfläche hat einen Standard-Login (Port 11010) | Kontoübernahme; Passwort-Wiederverwendung | nach Sicherung | Logs als sensibel einstufen; Aufbewahrung festlegen; Queue leeren nach Sicherung | S-14, S-07; `traefik/traefik.yml:35,38-58`; `auth_svc/…/utils/auth.py:294` |
| Mailpit | **Bestätigt (Portprüfung 07.10.2026): Mailpit-Weboberfläche auf Port 8025 ist ohne Login aus dem Internet erreichbar** (HTTP 200, keine Anmeldung; Inhalt bewusst nicht angesehen), SMTP-Port 1025 ebenfalls offen. Ob Produktivmails dort landen, hängt von der Deploy-Variante ab: Der Swarm-Stack liest nur die `.env`; die Readme-Variablen `MAIL_SMTP_*` (einfacher Unterstrich) werden vom Mail-Dienst nicht gelesen (erwartet `MAIL__SMTP_*`) → Standard `smtp_host = mailpit`. Die Compose-Variante übersetzt korrekt. **Indiz:** Erhalten Kunden ihre Bestellbestätigungen, gehen die Produktivmails wohl nicht an Mailpit | Offenlegung aller dort liegenden Mails (Bestellbestätigungen mit PDF, Passwort-Reset-Links, Sicherungskarten) | **08.10., sofort nach Sicherung** | Mailpit-Daten sichern, Inhalt durch eine befugte Person prüfen lassen, Ports 8025/1025 schließen bzw. Mailpit aus Produktion entfernen; Ergebnis in die DSGVO-Bewertung | I-08, S-07; `docker-stack.yml:660-666`; `mail_svc/…/core/config.py:11-15`; `compose/services/backend/mail_svc.yml:22-26`; `Readme.md:27-34` |
| Tracking/Consent | GTM lädt erst nach Einwilligung (gut). **Ohne** Einwilligung laden Userlike-Chat (CDN AWS Irland), eTrusted, Trusted Shops, Händlerbund und das GTM-noscript. Gehashte E-Mail/Name/Telefon/Adresse gehen in den dataLayer und nur nach Einwilligung an Google. Attributions-Cookies laufen in Prod nicht (statische SPA) [Präzisierung zu S-24]. Was der GTM-Container enthält (Server-Side?) ist unbekannt | TDDDG/DSGVO, Abmahnung | 2 Wochen | Widgets hinter Einwilligung; GTM-Container prüfen | S-24, F-09, F-24; `frontend/src/app.html:13-73`; `…/utils/tracking.ts:291-309` |
| Chatbot | Branch `feature/new_chatbot` = **nur neue Userlike-Widget-ID** (1 Zeile, Commit `12ee1ef`, über PR 567 am 27.04.2026 live). Kein eigener Bot, keine KI-Anbindung im Code; Chat-Inhalte gehen an Userlike [neu] | Drittanbieter ohne Einwilligung/AVV | 2 Wochen | AVV Userlike prüfen, ebenso aktivierte KI-Funktionen dort [zu prüfen] | `app.html:46-50`; 05 Kap. Branches |
| Drittland/Dienstleister | Stripe (USA), Google (USA); ecomdata (JTL, Verbindung über **HTTP**), SMTP `k69g…meinserver.io`, Shop-Hoster unbekannt, Nextcloud-Dumps beim Insolvenzverwalter (Inhalt?) | AVV/Transfergrundlage fehlt evtl. | 2 Wochen | AVV-Liste anlegen (Kap. 4) | S-13, 09 Kap. 7 |
| Löschkonzept | **Im Code gibt es keine Lösch- oder Aufbewahrungslogik:** keine TTL, keine Kontolöschung, Anhänge werden nie entfernt [neu]. Der Alt-Shop-Kundendump `tkunde.sql` liegt an unbekanntem Ort; der Schlüssel zum Entschlüsseln steht im Repo | Art. 5 Abs. 1 lit. e, Art. 17 | 4 Wochen | Löschfristen festlegen; Verbleib des Dumps klären | `auth_svc/…/migrate_jtl_data.py:312-313`; S-04 |

### 2.3 IT-Sicherheit / ISB

| Thema | Sachverhalt (kurz) | Risiko | Frist | Entscheidung/Aktion erbeten | Beleg |
|---|---|---|---|---|---|
| Beweissicherung | Logs, `.env`, Stack, DBs, Images; **Anhänge liegen im Container ohne Volume** und sind bei einem Redeploy weg | Beweis- und Datenverlust | vor jeder Änderung | Checkliste abarbeiten, Prüfsummen bilden, verschlüsselt ablegen | 09 Kap. 3–4, I-12; `docker-stack.yml:270-276` |
| Öffentliche interne Routen | `PathPrefix(/api)` leitet alles nach außen, u. a. `mark_paid`, `send_order_to_system`, `maildata`, `pdf`, Doku-Seiten | Ausnutzung | direkt nach Sicherung | Traefik-Allowlist | S-15, S-02; `docker-stack.yml:304,655` |
| Codeausführung (RCE) | `template_string` rendert Jinja ohne Sandbox; WeasyPrint lädt beliebige URLs (SSRF) | Übernahme des Containers, die gesamte `.env` ist lesbar | direkt nach Sicherung | `/api/pdf` sperren | S-03; `pdf_svc/…/template_handler.py:69` |
| Offene Ports | **Portprüfung von außen am 07.10.2026** (`sicherheit.meesenburg.com` = 136.243.107.86, Hetzner Online, RZ Falkenstein fsn1-dc8): **offen** 22 (SSH), 80/443, **1025/8025 (Mailpit)**, **10002 (Frontend direkt)**; **nicht erreichbar** RabbitMQ (11010–11012, 5672, 15672), Valkey (11020, 6379), mongo-express/Redis-Commander (11021–11025), MongoDB (27017), MariaDB (3306) | Datenzugriff ohne Login (Mailpit) | direkt nach Sicherung | Mailpit- und Frontend-Direktport schließen; SSH nur per Schlüssel bzw. IP-Freigabe | S-07; Live-Prüfung 07.10.2026 |
| Zugangsdaten | JTL, Custom-API, SMTP, Strapi im Repo; JWT-Schlüssel `foob…`/`abc1…` | Vollzugriff auf ERP und Postfach; Token-Fälschung | 24 h nach Sicherung | Rotation: JTL → Custom-API → SMTP → JWT → Strapi → DB/MQ → Stripe | S-04, S-05 |
| JTL-Server (ecomdata) | RDP 3389 aus dem Internet erreichbar; REST und Custom-API über HTTP | Einbruch ins ERP, Abhören | 72 h | ecomdata: VPN, TLS, Prüfung der RDP-Logs | S-13, 04 Kap. 6 |
| Passwort-Reset | Token ohne Ablauf, Refresh-Token als Reset nutzbar | Kontoübernahme | 1 Woche | Hotfix (E2) | S-08 |
| Lieferkette | `svc_helper` (JWT-Prüfung) in fremder GitLab-Gruppe; Strapi im Dev-Modus mit Default-Login | Manipulation, Ausfall | 1 Woche | Kopie ins eigene Repo; Strapi-Admins prüfen | S-25, S-11 |

### 2.4 Buchhaltung / Finanzen

| Thema | Sachverhalt (kurz) | Risiko | Frist | Entscheidung/Aktion erbeten | Beleg |
|---|---|---|---|---|---|
| `mark_paid` öffentlich | Geschrieben 17.08., **im Hauptzweig seit 20.08.2026** (PR 808), ohne Login und ohne Stripe-Prüfung. Weitere Risikozeiträume davor: Kap. 6.2 / Bericht 11. JTL bucht „Stripe“ über die volle Bestellsumme | Ware ohne Zahlung | 24 h | Jeden JTL-Zahlungseingang „Stripe“ **seit Februar 2026** gegen Stripe abgleichen (Kap. 6.2) | K-01; `…/endpoints/order.py:110-138` (Commit `ef0fe86`) |
| Betrag Stripe ≠ Bestellung | Ein alter PaymentIntent wird wiederverwendet; JTL bucht `TotalGrossAmount`, nicht den tatsächlich gezahlten Betrag | Unterzahlung, offene Differenzen | 24 h | Differenzliste je Auftrag | K-02; `payment_svc/…/intents.py:30-33`; `…/utils/order.py:566-583` |
| Buchung ohne Erfolg | Die Zahlung wird in JTL gebucht, sobald eine Intent-ID gesetzt ist, **unabhängig vom Status**. Auch `failed`/`canceled` mit Zahlungsmethode führt zur Buchung [neu; wie oft das vorkam, ist zu prüfen] | Scheinbar bezahlte Aufträge | 24 h | Im Abgleich besonders prüfen | `…/utils/order.py:613-617`; `…/endpoints/order.py:126-135`; `payment_svc/…/webhook.py:88-105` |
| Keine Referenz in Stripe | Der PaymentIntent wird ohne Bestellnummer/Metadaten angelegt [neu]. Zuordnung nur über die Payment-DB bzw. `cExternalTransactionId` in JTL | Abgleich aufwendig, Mahnungen | 24 h | Export der Payment-DB in die Sicherung aufnehmen | `payment_svc/…/intents.py:36`; 07 Kap. 4.2 |
| Fehlende Aufträge | Bestellungen gelten als „in ERP angelegt“, obwohl JTL keine ID geliefert hat | Bezahlt, aber nie geliefert/fakturiert | 1 Woche | Abgleich Shop-Bestellungen ↔ JTL | B-13 |
| Zeiträume mit Fehlberechnung | 17.08.–09.09. (q-fache Zylinder-Aufpreise), 08.–09.09. (n-fach), bis 23.09. (D/E/F-Aufpreise fehlen [datenabhängig]); Aufpreise ab dem 2. identischen Stück fehlen | Über- und Unterberechnung | 2 Wochen | Betroffene Aufträge identifizieren, Korrekturentscheid | 02 Kap. 2.10, K-07, K-10 |
| MwSt | Fest 19 % in Backend, ERP-Übergabe **und Bestellbestätigung**; 1 % bei Ausfall des Steuerabrufs (Anzeige/Feed) | Falsche Steuerausweise | 2 Wochen | Prüfen, ob 7 %-/Auslandsfälle vorkamen | K-12, B-06; `…/jtl_helper/mail_data.py:204,234` |
| Stripe-Konto | Im Repo nur Test-Keys. **Das Live-Frontend nutzt einen Live-Key** (`pk_live_51SK…`, Prüfung 07.10.2026); er stimmt nicht eindeutig mit den Test-Keys der Readme überein → Konto-Zuordnung offen. Inhaber des Live-Kontos und Dashboard-Zugang (dashboard.stripe.com) unklar. Praktischer Test: Kommen **Stripe-Auszahlungen auf einem Meesenburg-Bankkonto** an? | Kein Zugriff auf Zahlungsdaten | 08.10. | Zugang sicherstellen (V6) | S-04 #4–6, 08 V6 |

### 2.5 Einkauf / Recht (Verträge, Insolvenzverwalter)

| Thema | Sachverhalt (kurz) | Risiko | Frist | Entscheidung/Aktion erbeten | Beleg |
|---|---|---|---|---|---|
| Insolvenzverwalter | Strapi „Global“, Nextcloud (Assets, DB-Dumps), Gitea (Issues/PRs), Registry, Loki, Server/Hoster, Domains/DNS | Abschaltung = Datenverlust | 48 h | Herausgabe/Übernahme vereinbaren; Daten sichern lassen | 06 Kap. 6, 09 Kap. 7 |
| Nutzungsrechte Code | Für `svc_helper` steht in der LICENSE (MIT) ein fremder Copyright-Vermerk; der Quellcode der Custom-API fehlt; die Rechte am Shop-Code sind zu klären | Weiterbetrieb rechtlich unsicher | 48 h | Schriftliche Rechteübertragung (Inhaber bzw. Verwalter) | 00 Kap. 10, B-29, I-05 |
| Freelancer | Zugriff auf Server und Kundendaten ohne bekannten AVV; Vertragspartner unklar (Person, neogris oder Alt-Dienstleister). Seit August committen zwei Personen: Christopher Seidel (`c.seidel@neogris.com`, neogris.com ist laut Seitentitel seine persönliche CV-Website) und Dominic Bräuer (gmail). Die Identität „vitandi“ (`vitandi@neogris.com`, GitLab-Gruppe von `svc_helper`) ist nach den Indizien in Kap. 6.3 sehr wahrscheinlich ebenfalls Christopher Seidel | Art. 28 DSGVO; Wissens- und Zugangsabhängigkeit | nach Sicherung | AVV, Vertraulichkeit, Übergabe von Zugängen und Rechten, Rolle (E5) | TODO Forensik; I-22 |
| Dienstleister-AVV | ecomdata, Shop-Hoster, SMTP-Anbieter (`meinserver.io`), Inhaber `sicherheitstechnik-nord.de`, Stripe, Google, Userlike, Trusted Shops, CookieFirst, Händlerbund | Fehlende Rechtsgrundlage | 2 Wochen | Bestand erheben, Lücken schließen | Kap. 2.2 |
| Preisangaben/Wettbewerb | Angezeigter ≠ abgebuchter Preis (Sonderpreise nur im Frontend), Streichpreise teils ×1,19 falsch, Gutscheinfeld ohne Wirkung, Mock-Daten live, Google-Feed immer „auf Lager“ | Abmahnung (PAngV/UWG) | 2 Wochen | Rechtliche Bewertung; ggf. Funktionen abschalten | K-05, K-23, K-24, F-14, B-21 |
| Barrierefreiheit | BFSG-Stand unbekannt (Pflicht seit 06/2025) | Abmahnung | später | Prüfung beauftragen | F-37 |

### 2.6 Vertrieb / Kundenservice

| Thema | Sachverhalt (kurz) | Risiko | Frist | Entscheidung/Aktion erbeten | Beleg |
|---|---|---|---|---|---|
| Falsch berechnete Aufträge | Siehe Zeiträume in 2.4 | Reklamationen, Rückforderungen | nach Abgleich | Vorgehen festlegen (Erstattung/Nachforderung/Kulanz) | 02 Kap. 2.10 |
| Bezahlte, aber fehlende Aufträge | Der Auftrag fehlt in JTL, der Kunde wartet | Beschwerden | 1 Woche | Liste aus dem Abgleich abarbeiten | B-13 |
| Status im Kundenkonto veraltet | Versand- und Stornostatus kommen seit 17.08. nicht mehr im Shop an | Rückfragen | laufend | Bei Rückfragen JTL als führend nutzen | B-02 |
| B2B-Sonderpreise | Angezeigter Preis ≠ abgebuchter Preis | Reklamation | laufend | Sprachregelung | K-05, B-10 |
| Neuanmeldung nach Rotation | Ein neuer JWT-Schlüssel beendet alle Sitzungen | Rückfragen | mit Rotation | Hinweistext | S-05 |
| Mögliche Benachrichtigung Betroffener | Nur nach Entscheidung des DSB (Art. 34) | Anfragen | nach DSB | Textbausteine und Hotline vorbereiten, **keine Details zu Lücken** | 09 Kap. 6 |

## 3. Fristen und Reihenfolge

| Wann | Schritt | Hinweis |
|---|---|---|
| **07.10.2026** | Kenntnis der Lücke durch Code-Analyse | Uhrzeit dokumentieren (Art. 33 Abs. 5) |
| **08.10. vormittags** | GF, DSB und ISB informieren; Zugangsweg zum Server entscheiden | Kap. 5 |
| 08.10. | **Sichern** (09 Kap. 3), Prüfsummen bilden, verschlüsselt ablegen | Vorher nichts neu ausrollen; nur `docker restart` ist unkritisch |
| direkt danach | **Sperren:** Traefik-Allowlist, Ports schließen, Bestellsuche für Gäste deaktivieren (nur `single_order` **mit** `is_jtl_number=true` sperren, siehe 6.4 – den ganzen Endpunkt zu sperren, legt den Checkout lahm) | |
| ≤ 24 h nach Sicherung | **Zugangsdaten tauschen** (Reihenfolge 2.3); Abgleich `mark_paid`/JTL ↔ Stripe starten | Neuer JWT-Schlüssel macht geloggte Cookies wertlos |
| **bis Sa 10.10.2026** (vorsorglich) | DSB entscheidet über Meldung an die Aufsichtsbehörde; Angaben können nachgereicht werden (Art. 33 Abs. 4) | Wochenende → Erreichbarkeit des DSB klären |
| nach Sicherung und Sperrung | Freelancer informieren (Zugänge, AVV, Übergabe) | Kap. 5 |
| 48 h | Insolvenzverwalter: Rechte, Exporte, LE-Account, Domains | |
| 72 h | ecomdata: RDP nur per VPN, TLS, IP-Freigabe | |
| 1–2 Wochen | Hotfix Passwort-Reset, Consent für Widgets, AVV-Bestand, Korrekturen gegenüber Kunden | |

## 4. Was wir (noch) nicht wissen – morgen erfragen

1. **Wer betreibt die Shop-Server, und wer hat Root-Zugang?** Laut IP-Abfrage liegt `sicherheit.meesenburg.com` bei **Hetzner Online (RZ Falkenstein)**: Auf wessen Hetzner-Konto (Dienstleister, Freelancer, Meesenburg)? Gibt es einen Vertrag? Ist eine Sicherung ohne die Freelancer möglich?
2. **Produktive Konfiguration:** Welche MAIL-Variablen sind gesetzt (Mailpit?), welcher JWT-Schlüssel, sind Stripe-Live-Keys aktiv, stimmen die Strapi-Secrets? Sind die Ports von außen erreichbar?
3. **Logs:** Gibt es Access-Logs seit Go-Live? Wo liegt Loki/Promtail, und wie lange werden Logs aufbewahrt?
4. **Betroffenenkreis:** Wie viele Datensätze gibt es in `order`, `order_erp`, `user`, `configurations`? Wie viele Sicherungskarten?
5. **Wem gehören `sicherheitstechnik-nord.de` (BCC und Absender), `sn-24.de` und das SMTP-Konto bei `meinserver.io`?** Wer liest `sicherheit@meesenburg.com`?
6. Wurde das Mail-Test-Werkzeug gegen Produktion genutzt? Antwort aus den Mail-Logs des SMTP-Anbieters.
7. **AVVs:** Gibt es sie mit Freelancern, Hoster, ecomdata, SMTP-Anbieter, Userlike, Trusted Shops, CookieFirst, Stripe, Google?
8. Was enthalten der GTM-Container (Tags, Server-Side, Enhanced Conversions) und die CookieFirst-Kategorien? Sind bei Userlike KI-Funktionen aktiv?
9. Wo liegt der Alt-Shop-Kundendump `tkunde.sql`, und was liegt in Nextcloud `SN24/repo/dbs/`?
10. Wer hält das Stripe-Live-Konto, und wer hat Dashboard-Zugang?
11. Wer ist Ansprechpartner beim Insolvenzverwalter? Wie lange laufen die Systeme des Dienstleisters noch?
12. Sind Kundenreklamationen zu Preisen oder fehlenden Lieferungen bekannt?

## 5. Vorschlag für die Kommunikation

1. **Projektleitung → Geschäftsführung** (08.10. früh, persönlich): dieses Briefing; Entscheidungen aus 2.1.
2. **GF/Projektleitung → DSB und ISB gemeinsam** (direkt danach): Start der 72-h-Bewertung, Forensik, Dokumentation nach 09 Kap. 6.
3. **→ IT-Betrieb** (Need-to-know): Sichern und Sperren. Keine Weitergabe an Externe.
4. **→ Buchhaltung** (Stripe-Abgleich) und **→ Einkauf/Recht** (Insolvenzverwalter, AVVs, Rechte).
5. **→ Vertrieb/Kundenservice:** nur Sprachregelung und erwartbare Kundenfragen, keine technischen Details.
6. **Externe für den Zugang** (Hoster/ecomdata): nur das Anliegen (Zugang, Sicherung, Firewall), keine Details zu den Lücken.
7. **Freelancer: erst nach Sicherung und Sperrung.** Das Gespräch bleibt sachlich und ohne Vorwürfe. Es geht um Übergabe von Zugängen, AVV, Vertraulichkeit und die künftige Rolle (nur PR mit internem Review). Falls der Zugang nur über sie möglich ist: Sicherung anfordern, ohne die Lücken zu nennen. Diese Entscheidung trifft die GF.
8. **Aufsichtsbehörde und Betroffene:** nur nach Entscheidung des DSB.

**Grundsätze:** schriftliche Details nur in verschlüsselten Ablagen; keine Schuldzuweisungen; Formulierungen wie „strukturelle Mängel aus der Übernahme“.

## 6. Rückfragen und Antworten (07.10.2026, abends)

### 6.1 Haben wir schon Zugriff auf die MongoDBs?
**Nein.** Die Datenbanken sind von außen nicht erreichbar (Portprüfung, Kap. 2). Zugriff gibt es erst über den Server (Hetzner-Zugang bzw. SSH) oder über jemanden, der ihn hat. Die öffentlichen Shop-Endpunkte liefern zwar Daten, sind aber **kein** zulässiger Weg für Auswertungen.

Es gibt vier Instanzen, je Microservice eine (kein Cache):

| Instanz | Inhalt |
|---|---|
| `items_mongo` | Artikel-Kopie aus JTL (`items`, `item_globals`, `attribute_collections`) – am ehesten ein Lesemodell bzw. Cache |
| `business_mongo` | `order` (Shop-Bestellungen), `order_erp` (gespiegelte JTL-Aufträge), `user`/`user_erp` (Kundenprofile), `carts`, **`configurations`** (gespeicherte Schließanlagen inkl. Schließmatrix und Objektadresse), `payment_log_erp` |
| `auth_mongo` | Anmeldedaten (`user`, `user_erp`: Passwort-Hashes, Tokens) |
| `payment_mongo` | Stripe-Zahlungsvorgänge (`payment`) |

Eine Instanz mit getrennten Datenbanken würde reichen. Die neue .NET-API kommt mit einer Datenbank aus.

### 6.2 `mark_paid` – erst seit 17.08.2026? Wie war es vorher? (Details: Bericht 11)
- **`mark_paid`** wurde am 17.08.2026 geschrieben (`ef0fe86`) und am **20.08.2026 in den Hauptzweig übernommen** (PR 808, `285d351`). Für diese Lücke gilt also: ab Deploy nach dem 20.08.
- **Go-Live (Relaunch):** zwischen **17.06. und 23.06.2026** (letzter PR davor 17.06., PR 713 „First Fixes and Changes after Relaunch“ am 23.06.). Vorbereitung 13.05.–13.06. (GTM, CookieFirst, Merchant-Feed, Trusted Shops). **Bestellungen gegen die echte JTL-Wawi** vermutlich schon seit **Februar 2026** (`8fbc713`, 09.02.2026: Stripe-Keys im „live frontend“) – also ein langer Softstart.
- **Vor `mark_paid`** gab es keinen öffentlichen Weg, eine Bestellung als bezahlt zu setzen. Gebucht wurde über den signaturgeprüften Stripe-Webhook → RabbitMQ → Worker → Custom-API. **Aber auch vorher galt nie „gebucht = in Stripe erfolgreich bezahlt“:**

| Zeitraum | Risiko | ohne Manipulation? |
|---|---|---|
| ab Feb. 2026 bis Umstellung auf Stripe-Live (Datum unbekannt) | **Testzahlungen** (Stripe-Testmodus) wurden in der echten Wawi als Zahlung gebucht | ja |
| 07.11.2025 – 20.08.2026 | Worker prüft den Zahlungsstatus nicht (`payment.py`, `update_payment_details`): auch fehlgeschlagene/abgebrochene Zahlungen mit Zahlart wurden voll gebucht (vermutlich selten) | ja |
| bis 20.08.2026, v. a. bis 19.07.2026 | Jeder Seitenaufruf erzeugte einen neuen PaymentIntent → **echte Doppelzahlungen** möglich | ja |
| bis 20.08.2026 | keine Idempotenz: Webhook-Wiederholungen konnten **doppelt gebucht** werden | ja |
| bis 22.07. (Link) bzw. 20.08.2026 (Amazon Pay, Revolut Pay) | **bezahlt, aber nie in JTL gebucht** → mögliche Mahnungen an zahlende Kunden | ja |
| ab 20.08.2026 | öffentliches `mark_paid`; Intent-Wiederverwendung (gezahlter ≠ gebuchter Betrag); auch failed/canceled lösen `mark_paid` aus | teils |

**Abgleich (Buchhaltung):** **Alle** JTL-Zahlungen mit Name „Stripe“ seit Februar 2026 über die Stripe-Referenz (`cExternalTransactionId` = PaymentIntent-ID) im Stripe-Dashboard nachschlagen: existiert, `succeeded`, gezahlter Betrag = gebuchter Betrag, keine Doppelungen. Umgekehrt jede erfolgreiche Stripe-Zahlung einer JTL-Buchung zuordnen (findet nicht gebuchte Zahlungen und Doppelzahlungen).

### 6.3 Ist „vitandi“ dieselbe Person wie Christopher Seidel? (Indizien)
Belegt aus eigenen Systemen (Azure DevOps, Git, gesicherter `svc_helper`-Klon) sowie öffentlich zugänglichen Angaben:

1. **Gleiche Domain:** `vitandi@neogris.com` und `c.seidel@neogris.com`; neogris.com ist laut Seitentitel die persönliche CV-Website von Christopher Seidel.
2. **Klarname mit vitandi-Adresse:** Im Repo `svc_helper` (GitLab-Gruppe `vitandi1`) stehen **4 Commits mit Autor „Christopher Seidel <vitandi@neogris.com>“**, die übrigen 30 als „vitandi <vitandi@neogris.com>“.
3. **Azure-DevOps-Pushes:** Alle **9 vitandi-Commits seit August 2026** (darunter `ef0fe86` mit `mark_paid`) wurden über das DevOps-Konto **„Christopher Seidel (Extern)“** hochgeladen, teils Minuten nach dem Commit. Ein DevOps-Konto „vitandi“ gibt es nicht. Die älteren vitandi-Commits kamen mit dem Migrations-Push von „ICS Christopher Seidel“ (05.03.2026).
4. **Zeitliche Deckung:** „Christopher Seidel (Extern)“ committet 17.08.–09.09.2026, „vitandi“ in denselben Tagen (17.08., 20.08., 21.08., 25.08., 07.09., 09.09.).
5. **Code-Hinweise:** Test-Mailadresse `User.CHRIS = c.seidel@neogris.com` und Rückfall-Empfänger im Mail-Dienst.

**Einschätzung:** Mit hoher Wahrscheinlichkeit dieselbe Person; „vitandi“ ist ein Pseudonym. Für das Gespräch reicht eine neutrale Frage („Wer steht hinter vitandi / der GitLab-Gruppe vitandi1?“) – die Belege müssen nicht vorgelegt werden. Einen Vorwurf begründet das Pseudonym nicht.

### 6.4 Bestellsuche für Gäste deaktivieren
- **Seite:** Die Route ist `/order-lookup` (Formular: Bestellnummer + E-Mail), verlinkt nur von der Erfolgsseite nach dem Checkout. **Direkt aufgerufen liefert sie 404**, weil die Seitenprüfung `_check_page` nur `/shop/order-lookup` erlaubt (`check_page.py:126-133`). Über den Link nach dem Checkout funktioniert sie (Navigation im Browser ohne Serveranfrage).
- **Das Risiko liegt nicht in der Seite, sondern im API-Endpunkt**, der unabhängig davon direkt aufrufbar ist: `GET /api/order/single_order?number=<JTL-Nr>&is_jtl_number=true`.
- **Derselbe Endpunkt** wird auch von Zahlungs- und Erfolgsseite genutzt (`?number=<id>` **ohne** `is_jtl_number`). Ihn ganz zu sperren, legt den Checkout lahm.
- **Sofortlösung (Traefik):** nur Anfragen mit `is_jtl_number=true` sperren, z. B. Router-Regel `Path(`/api/order/single_order`) && Query(`is_jtl_number`, `true`)` mit höherer Priorität auf eine Sperre (403). Den Link auf der Erfolgsseite bei Gelegenheit entfernen. Der Abruf per interner Nummer im Checkout bleibt bis zur neuen API offen (Rest-Risiko, S-01).

### 6.5 GTM-Container und Stripe-Konto – was können wir ohne Zugang herausfinden?
- **GTM-Container `GTM-K49HM8V`:** Die Konfiguration wird an jeden Besucher ausgeliefert und ist damit öffentlich lesbar (`googletagmanager.com/gtm.js?id=GTM-K49HM8V`, Abruf 07.10.2026). Enthalten sind u. a.:
  - Google Analytics 4 (Mess-ID `G-KFQK…`, 12 Ereignis-Tags)
  - Google-Ads-Conversion-Tracking (4 Tags), Remarketing, Conversion-Linker
  - **„Enhanced Conversions“ bzw. „vom Nutzer bereitgestellte Daten“** (Tags `__awec`, `__awud`): gehashte E-Mail/Telefon/Adresse an Google – laut Code nur nach Einwilligung
  - 13 pausierte Tags

  **Nicht** ohne Zugang erkennbar: wer das Google-Konto besitzt und Zugriff hat (tagmanager.google.com). → Inhaber erfragen, Zugang auf Meesenburg übertragen lassen.
- **Stripe:** Im Live-Frontend steckt ein Live-Key (`pk_live_51SK…`), es läuft also ein **Live-Konto**. Wem es gehört, ist ohne Zugang nicht feststellbar. **Praktische Prüfung durch die Buchhaltung:** Gehen Stripe-Auszahlungen auf ein Meesenburg-Bankkonto (Verwendungszweck „STRIPE“)? Dashboard-Zugang: dashboard.stripe.com, Konto-Inhaber muss Zugang gewähren.

### 6.6 Landen aktiv Dumps oder Backups irgendwo?
- **Im Repo ist keine automatische Sicherung definiert** (kein `mongodump`, keine Cron-/Backup-Container, kein restic/rclone/Nextcloud-Upload). Die Nextcloud-Dumps aus der Readme sind manuelle Entwicklungsstände.
- **Ohne Serverzugang nicht prüfbar:** Cronjobs auf dem Server, Backup- bzw. Snapshot-Funktion bei Hetzner (im Hetzner-Konto sichtbar), Inhalte der Nextcloud des Dienstleisters.
- **Jetzt schon erfragbar:** bei Hetzner bzw. dem Kontoinhaber (Backups/Snapshots aktiv?), bei Freelancer/Insolvenzverwalter (welche Dumps liegen wo?).

### 6.7 Wer hat die Zahlungslogik seit 11/2025 geändert?
Ausgewertet: alle 56 Commits seit 01.11.2025 an Zahlungsdienst, Zahlungsbuchung, Bestell-Endpunkten sowie Checkout- und Zahlungsseiten; dazu Ersteller, Abschließer und Freigaben der Pull Requests in Azure DevOps (bei Squash-Merges geht der Autor in Git verloren).

| Zeitraum | Wer | Was |
|---|---|---|
| Nov. 2025 – Feb. 2026 | **M. Schultz („Matz“)** und „mahmud“ (beide Alt-Dienstleister, letzte Commits Feb. 2026) | **Grundaufbau** des Zahlungsdienstes (`3dca370` „Payment-Service“, 07.11.2025) und der Zahlungsbuchung in JTL (`2b10da9`, 01.12.2025). Hier entstanden die Buchung **ohne Statusprüfung** und die **mehrfachen Zahlungsvorgänge** (Risiken R2/R3, Bericht 11). |
| Feb. 2026 | M. Schultz | Stripe-Keys im „live frontend“ (`8fbc713`, 09.02.2026) |
| März – Sept. 2026 | **Christopher Seidel** (auch als „vitandi“) | Mehrheit der Zahlungsänderungen (24 Commits): u. a. Vorkasse statt Bar (PR 647), Zahlungs-Fix nach Relaunch (PR 764, 19.07.), Zeitzone/Worker (PR 769, 22.07.), **neue Checkout-Logik mit `mark_paid`** (PR 808, 20.08.) |
| Mai/Juni und Sept. 2026 | **Dominic Bräuer** | v. a. Tracking/Consent im Checkout-Umfeld (PR 612, 616, 669) und Konfigurator-Preis-Fixes (PR 935, 938) |

**Freigaben:** In **keinem** der geprüften PRs (591, 612, 647, 669, 713, 720, 764, 769, 807, 808, 935, 938) gibt es eine Freigabe durch einen Reviewer. Alle wurden von Christopher Seidel abgeschlossen – seine eigenen PRs (einschließlich PR 808 mit `mark_paid`) ebenso wie die von Dominic Bräuer.

**Einordnung:** Die Risiken stammen nicht von einer Person: Die Grundprobleme der Zahlungsbuchung liegen im Ursprungsentwurf (Alt-Dienstleister, 11/2025) und wurden später nicht behoben; `mark_paid` kam im August hinzu. Die gemeinsame Ursache ist **fehlendes Vier-Augen-Prinzip** (keine Reviews, kein Test, keine Branch-Policy). → Branch-Policy mit Pflicht-Review durch Meesenburg (Bericht 08, V7/E5).

### 6.8 Live-Test 08.10.2026: Bestellungen kommen nicht in JTL an
**Test:** Zwei Bestellungen mit abgelehnter Zahlung; eine davon anschließend per `mark_paid` als bezahlt markiert. **Ergebnis:** Beide Bestellungen im Shop-Konto sichtbar, Bestellbestätigung mit PDF per Mail erhalten, **in JTL keine der beiden**.

**Ursache im Code** (`business/…/utils/order.py:602-637`, `payment_svc/…/utils/webhook.py:85-103`):
- Bei Stripe-Zahlung wird die Bestellung zunächst **nur in der Shop-Datenbank** gespeichert. Die Kontoübersicht liest von dort, nicht aus JTL.
- Der Stripe-Webhook ruft `mark_paid` **auch bei „failed“** auf. Das startet `send_order_to_systems`.
- Darin läuft die JTL-Übergabe in `with suppress(Exception)`: Scheitert sie, bricht sie **still** ab. Der Mail-Block ist davon getrennt und **versendet die Bestellbestätigung trotzdem**.

**Folgen:**
1. Die JTL-Übergabe ist beim Test gescheitert; die Ursache ist nur mit den Logs des business-Containers feststellbar (Serverzugang). Mögliche Gründe: bereits getauschte Zugangsdaten, JTL vom Server nicht erreichbar (IP-Freigabe), falscher Variablenname des Custom-API-Tokens (B-30), Datenfehler.
2. **Möglicherweise betrifft das auch echte Kundenbestellungen** (bezahlt, bestätigt, aber nie in JTL). **Sofort prüfen, ohne Serverzugang:** Bestellnummern aus dem BCC-Postfach `info@sicherheitstechnik-nord.de` (Kopie jeder Bestätigung seit 07.09.2026) gegen JTL abgleichen; letzter Shop-Auftrag in JTL; erfolgreiche Stripe-Zahlungen ohne JTL-Auftrag.
3. **Kunden erhalten eine Bestellbestätigung auch bei abgelehnter Zahlung.**
4. **Testbestellungen sind „scharf“:** Sie stehen mit `_in_erp_created = false` in der Shop-Datenbank und würden bei jedem späteren Auslöser (`send_order_to_system`, neuer Webhook) nachträglich an JTL gesendet – bei der als bezahlt markierten inkl. Stripe-Zahlungsbuchung über die volle Summe. → Bestellnummern notieren, nach der Sicherung gezielt bereinigen; nichts manuell nachschieben.

### 6.9 Werden nicht übertragene Bestellungen gesammelt nachgeschoben? Kann man die Übertragung von außen auslösen?
- **Kein Sammel-Nachschub (laut Repo):** `send_order_to_systems(order)` bearbeitet nur die übergebene Bestellung. Die Worker-Schleife über „Bestellungen in Bearbeitung“ (`jtl_helper/worker.py:156-163`) gibt nur Status aus; der JTL-Abgleich ist abgeschaltet (`update_runner: False`). Stripe wiederholt alte Webhooks nicht, weil der Handler auch bei Fehlern „OK“ meldet.
- **Von außen ohne Anmeldung auslösbar**, mit einer Bestell-ID. Als ID akzeptiert der Code die interne Shop-ID (GUID), die JTL-Datenbank-ID oder die fortlaufende Bestellnummer (`utils/order.py:142-177`).

| Aufruf | laut Repo (`main`) | **live beobachtet (08.10.2026)** |
|---|---|---|
| `PATCH /api/order/{id}/mark_paid` | aktiv: Status setzen + `send_order_to_systems` | – |
| `GET /api/order/send_order_to_system/{id}` | aktiv: `send_order_to_systems`; unbekannte ID → 404 „order not found“ | Antwort **`{"detail": false}`** – kommt im Repo-Code nicht vor |
| `GET /api/order/send/{id}` | **auskommentiert** (`order.py:166-204`) → müsste 404 liefern | Antwort **`{"detail": "ok"}` bei beliebiger Eingabe** – Endpunkt live offenbar aktiv |
| `GET /api/order/send_payed/{id}`, `send_mail/{id}` | auskommentiert | nicht geprüft |

- **Wichtig: Der Produktionsstand weicht vom Repo ab** (bestätigt I-01). Das Verhalten der Live-Endpunkte ist aus dem Repo nicht sicher vorhersagbar. → **Code aus den laufenden Containern sichern** (Bericht 09) und **keine weiteren Testaufrufe** dieser Endpunkte auf dem Live-System; die bereits erfolgten Aufrufe (IDs, Uhrzeit) dokumentieren und in JTL auf neue/doppelte Aufträge prüfen.
- **Sperre (Traefik, nach der Sicherung):** alles unter `/api/order/` außer `POST /api/order/`, `GET /api/order/?user_id=`, `GET /api/order/single_order` (ohne `is_jtl_number`), `/api/order/payment_methods`, `/api/order/shipping_methods`. `mark_paid` nutzt der Zahlungsdienst intern über das Docker-Netz, nicht über Traefik.
- **Forensik:** Aufrufe von `/api/order/send…` im Access-Log suchen; in JTL nach **doppelten Aufträgen** und **mehrfachen Stripe-Zahlungsbuchungen je Auftrag** suchen.

### 6.10 Sichtung JTL-Server (ecomdata, `c10330`) am 08.10.2026
Zugang per RDP mit eigenem Konto (`nord4`, ohne Admin-Rechte), nur lesende Abfragen.

| Beobachtung | Bedeutung |
|---|---|
| Angemeldet: `nord1`, `nord2`, `nord4`, `nord5`, `useradmin` (2 Sitzungen), `cpsadmin` (getrennt seit 29.09.) | **Terminalserver**, auf dem die Mitarbeiter in der Wawi arbeiten. RDP ist deshalb aus dem Internet offen – ohne VPN/Gateway mit zweitem Faktor ein Risiko für Passwort-Rateangriffe (durch IPBan entschärft, siehe unten) |
| Custom-API (Port 58080, 4× `python`) und JTL-REST (`JTL.Wawi.Rest`, Port 50443) laufen in **Sitzung 0** (System) | Unabhängig von Benutzer-Abmeldungen. Über welchen Dienst bzw. welche Aufgabe sie starten und ob sie **nach einem Neustart automatisch wieder laufen**, ist ohne Admin-Rechte nicht sichtbar → klären |
| `JTL-Worker` (Dienst gestoppt), aber `JTL-Worker.exe` + `JTL-Worker-Client` laufen **in der Sitzung von `useradmin`** (seit 29.09.) | Automatisierte Wawi-Abläufe laufen nur, solange diese Sitzung besteht → **nicht abmelden**; Worker später als Dienst betreiben |
| JTL-REST-Reservierungen: `https://212.60.27.117:50443/` (Benutzer `C10330\useradmin`) und `http://*:50443/api/eazybusiness/` (**Jeder**) | Shop nutzt HTTP (unverschlüsselt) |
| Dienst `SQLAgent$JTLWAWI` gestoppt; Profil `IcingaUser` vorhanden | Keine Sicherungen über den SQL Agent; Icinga deutet auf Monitoring durch ecomdata → **Backup-Konzept bei ecomdata erfragen** |
| Kein Profil `cpsadmin`, aber `Administrator` (geändert 29.09. 11:13, eine Minute nach Anmeldung von `cpsadmin`) | `cpsadmin` ist vermutlich das umbenannte eingebaute Administratorkonto (Hoster) |
| Zweite, ältere SQL-Instanz (`JTLWAWI2`, Stand 04/2025); Ordner `C:\TMP\SMIG` (geändert 25.08.2026) | Mögliche Altdaten; Zweck klären |
| Laufende Fremd-Dienste: **NinjaOne-Agent** im Ordner `meesenburggmbhecomdatacloudnbg1`, **Icinga 2**, **IPBan**, QEMU-Gastagent/Balloon | Server ist eine VM in der ecomdata-Cloud (Nürnberg), bei ecomdata unter einem Mandanten **„Meesenburg GmbH“** registriert (Vertragspartner damit wahrscheinlich Meesenburg – bestätigen). ecomdata überwacht (Icinga) und verwaltet/patcht (NinjaOne). **IPBan** sperrt IPs nach Fehlanmeldungen → RDP-Rateangriffe deutlich entschärft |
| **Kein Dienst** für Custom-API (`python`) oder `JTL.Wawi.Rest`, obwohl beide in Sitzung 0 laufen | Start vermutlich über geplante Aufgaben als SYSTEM (ohne Admin-Rechte nicht sichtbar). Ob sie **nach einem Patch-Neustart durch NinjaOne** automatisch wieder starten, ist offen → bei ecomdata erfragen; mögliche Erklärung für frühere Ausfälle |
| JTL-REST: `C:\\Program Files (x86)\\JTL-Software\\JTL.Wawi.Rest.exe` (Stand 23.04.2026). Custom-API: **auf dem ganzen Laufwerk C: keine für `nord4` sichtbaren Python-Dateien** – auch keine Python-Installation | **Code und Python-Interpreter der Custom-API liegen in einem gesperrten Benutzerprofil**, vermutlich `useradmin` (hält die Port-Reservierung, JTL-Worker läuft in seiner Sitzung), sonst `Administrator`/`cpsadmin`. Nur mit Admin-Zugang auffindbar und sicherbar |
| Weitere: `win-acme` (Let's-Encrypt-Erneuerung per Aufgabe), `C:\\ProgramData\\ssh` (OpenSSH-Server?), nur ein Laufwerk (C:) | SSH-Zugang und Zertifikats-Erneuerung bei ecomdata mit klären |
| `nord4` ohne Admin-Rechte | Admin-Zugang (`useradmin`/`cpsadmin`) bei ecomdata bzw. bisherigem Verwalter klären |

**Nicht tun:** Sitzung von `useradmin` abmelden (JTL-Worker), Server neu starten, Prozesse beenden, **sich selbst als `useradmin` anmelden** (geteiltes Konto mit aktiver Sitzung seit 08.10.2026 06:42 – Anmeldung würde sie übernehmen; Nutzer unbekannt).

**Quellcode der Custom-API:** Python wird nicht kompiliert – der Code liegt lesbar im gesperrten Profil und lässt sich mit Admin-Rechten vollständig sichern (Ordner als Archiv mit Prüfsumme; auf `.git`/weiteres Repository achten). Im selben Ordner liegt voraussichtlich die Konfiguration mit Datenbank-Zugangsdaten → verschlüsselt ablegen, danach tauschen.
**Klären (ecomdata):** Vertragspartner bestätigen; **Admin-Zugang für Meesenburg** (eigenes Konto statt `useradmin`); Pfad und Startbefehl der Python-Prozesse; Kopie des Custom-API-Ordners; Start der APIs nach Neustart und Termine der Patch-Neustarts (seit Juni); Backup-Konzept der VM; Admin-Zugang; Code der Custom-API sichern; Worker als Dienst; Datenbank-Sicherung; RDP nur über VPN/Gateway mit MFA.

### 6.11 Shop-Server (Hetzner, `node1`): Zugang, Sicherung und Befunde (09.10.2026)
**Zugang:** Anmeldung 05:32 UTC mit dem Root-Schlüssel der Entwickler (Kommentar `vitandi@work`), danach eigener Benutzer `mpiper` mit eigenem Schlüssel und sudo (05:38 UTC). Ab dann nur noch als `mpiper`, ohne interaktive Shell.

**Sicherung** (verschlüsseltes Image „Backup Shop“, `SN24_20261009_0545`, mit Prüfsummen und Protokoll):
- Stufe A: Traefik komplett inkl. Access-Logs ab 05.10., Mailpit-Datenbank (Stand bis 10.09.), `docker diff` des PDF-Containers (keine veränderten Dateien), Live-Code-Stand (Git + 62 nur lokal geänderte Dateien), Container-Konfiguration
- Stufe B: `/home/admin/SN24` komplett (Code, `.env`, Logs, Uploads), Datenbank-Dumps (Mongo business/auth/payment, MariaDB), Rohdateien aller Mongo-Datenbanken, Alloy-Konfiguration, `.ssh` von `admin`/`root`, Shell-Historien, `/etc`, `/var/log`, Docker-Logs aller 21 Container (≈ 18 GB)
- Temporäre Dateien auf dem Server wieder gelöscht; am laufenden Shop nichts verändert

**Befunde:**

| Befund | Bedeutung |
|---|---|
| Während der Sicherung war zusätzlich `root` von `217.74.152.246` angemeldet (seit 07.10. 13:27) | Aktiver Fremdzugriff; Root-Anmeldungen seit April ausschließlich mit dem vitandi-Schlüssel von vier dänischen IPs (Norlys/Telia), dieselben Netze sind auf dem JTL-Server freigegeben |
| Root-Passwort **gesperrt** (nur Schlüssel); Benutzer `admin` mit sudo ohne Passwort, Schlüssel „m.schulz“, **seit mindestens 14.04.2026 nie benutzt** | Es gibt kein Root-Passwort zu erfragen. Entwickler-Zugänge = zwei Schlüssel (root/vitandi, admin/m.schulz) → nach Abstimmung entfernen |
| Shop läuft per **Docker Compose aus `/home/admin/SN24`** (kein Swarm-Stack); Python-Code aus diesem Ordner eingebunden; Git-Stand 09.09. **+ 62 nie committete Änderungen** | Erklärt die Abweichung zwischen Live-Code und Repo; Änderungen am Ordner wirken sofort live |
| Traefik-Logs: Rotation täglich 00:00–01:00, **nur 5 Tage** | Ältere Zugriffslogs auf dem Server gelöscht |
| **Alloy sendet Logs an `85.235.65.67` (netcup)**, unverschlüsselt (HTTP), ohne Anmeldung, seit ca. Juli 2026: Traefik-Access-Log (IP, Pfade, **Cookies inkl. Anmelde-Tokens** – an einem Tag 5.395 Zeilen), alle Dienst-Logs (z. B. business 52.604 Zeilen mit E-Mail-Adressen, payment 577 Zeilen mit Namens-/Adressfeldern); Metriken an Port 9090 | **Übermittlung personenbezogener Daten an einen unbekannten Dritten** → DSB heute; Inhaber klären; Export anfordern (vermutlich einzige vollständige Log-Historie); danach Datenfluss stoppen; JWT-Schlüssel tauschen (macht geloggte Tokens ungültig) |
| Items-Datenbank: „too many open connections“ – neue Verbindungen werden abgewiesen | Verbindungsleck im Items-Dienst; Risiko für Katalog-Ausfälle; Dump nicht möglich (Rohdateien gesichert, Daten aus JTL rekonstruierbar) |
| Zwei weitere Server (`node2`, `node3`) | Geprüft, siehe 6.12 |

### 6.12 Weitere Hetzner-Server `node2` und `node3` (09.10.2026)
Beide Server sind Hetzner-Dedicated-Server (gleiche Hardware wie node1, Debian 13) und seit 10.01.2026 mit node1 zu einem **Docker Swarm** verbunden. Im Swarm laufen **keine Dienste**, er wurde nur angelegt, getestet und wieder verworfen. Jeder Server betreibt seine eigenen Compose-Projekte. Zugang: wie node1 nur der Root-Schlüssel `vitandi`, seit 09.10. zusätzlich `mpiper`.

| Server | Inhalt | Befund | Sicherung |
|---|---|---|---|
| `node2` (136.243.110.39) | **Komplette Testinstanz** `sn24_test` (17 Container, Stand 17.08. + 46 nicht committete Änderungen), eigene Datenbanken (4× Mongo, MariaDB), Traefik für `test.meesenburg.neogris.com` | Domain **neogris.com** (DK, DNS netcup), dieselbe Firma wie die Rückfall-Mailadresse des Freelancers. **Mit dem produktiven JTL verbunden** (gleiche Custom-API und REST-API wie live), Stripe nur im Testmodus. 39 Auftrags-PDFs (14.08.–02.09.). Öffentlich offen: Mailpit 8025/1025 (derzeit leer) und Frontend 10002 **ohne den Passwortschutz** der Test-Domain | **Ja** (≈ 16 GB): Code, `.env`, Datenbanken, PDFs, Logs |
| `node3` (136.243.110.31) | Projekt **`configurators`**: Svelte-Web-Components plus Proxy-API (Stand Sept. 2025), Datenquelle **Univelop** (`app.univelop.de`); Altkopien `SN24`/`SN24static` (Jan. 2026) | **`217.74.152.246` (Norlys DK) ist seit 09.10. 06:53 als root angemeldet** und hat das Projekt um 06:59/07:00 neu gebaut und gestartet. Laut README liegt die Geschäftslogik in den Konfiguratoren. Keine öffentlichen Ports außer SSH und Swarm | **Ja**, klein (39 MB + System), Beleg für aktuelle Arbeiten |

Weitere Hinweise: Die Root-Historie auf node3 zeigt einen Datenabzug per `rsync` von einem Nextcloud-Server des Alt-Dienstleisters (`138.201.221.151`, Pfad `ICS/Projekte/Meesenburg/SN24`), und unter `/home/admin/.ssh` liegt der zugehörige Schlüssel `ssh_root_static_file` sowie `git_ssh`. Die stündlichen fehlgeschlagenen SSH-Anmeldungen kommen von Internet-Scannern und sind unkritisch.

**Gesichert am 09.10.2026** (schlank, mit Prüfsummen, Image „Backup Shop“): node3 (`configurators`, Historien, `.ssh`, System-Logs) und node2 (Code ohne Bilder, Git-Diff, `.env`, DB-Dumps, Auftrags-PDFs, Traefik, Logs). **Fund auf node2:** Im Ordner `db_backups` liegen Datenbanksicherungen vom **20.04. bis 25.07.2026** (Kundenkonten, Bestellungen, Zahlungen, CMS), wahrscheinlich Produktionsstände rund um den Go-Live. Das sind die einzigen historischen Datenbankstände, gut für den Zahlungsabgleich. Gleichzeitig liegen damit **Kundendaten auf einem Testserver in Verantwortung der Freelancer** (DSB).

### 6.13 Wem gehört der Log-Server `85.235.65.67`? (09.10.2026, nur öffentliche Quellen)
Ermittelt aus öffentlichen Quellen: Whois, DNS, Zertifikatstransparenz und öffentliche Startseiten. **Loki, Prometheus und Grafana selbst wurden nicht abgefragt.**

| Anhaltspunkt | Ergebnis |
|---|---|
| IP-Whois / Reverse-DNS | netcup GmbH, Karlsruhe; Kunden-VPS `v2202403153830262265.hotsrv.de` (Kundendaten nur bei netcup) |
| Offene Ports | 80/443 (Traefik), 3100 (Loki), 9090 (Prometheus) |
| Domains auf dieser IP | `sn24.`, `cms.sn24.` und `grafana.sn24.sustainable-solutions.it` (Zertifikate 04.08. bzw. 22.08.2026) **sowie `monitoring.neogris.com` und `grafana.monitoring.neogris.com`** (Zertifikat 15.08.2026); dort läuft Grafana 12.2.9 mit Login-Seite |
| `sustainable-solutions.it` | Registrant **ICS Sustainable Solutions GmbH**, Hauptstraße 60, 24980 Schafflund (Alt-Dienstleister); Webhosting bei one.com **gesperrt** |
| `neogris.com` | Registrar in Dänemark, DNS bei netcup; Startseite ist der **Lebenslauf von Christopher Seidel**, Mailadresse `c.seidel@neogris.com` (= Rückfall-Mailadresse im Shop-Code); weitere neogris-Dienste (gitea, vikunja, uptime, `erp-demo.meesenburg.neogris.com`) auf netcup-IP `152.53.32.159` |

**Einschätzung:** Der Server wurde unter der Domain des Alt-Dienstleisters aufgebaut. Seit Mitte August wird er **unter der eigenen Domain von Christopher Seidel (neogris) betrieben**. Damit ist er sehr wahrscheinlich der Betreiber, also auch derjenige, der die Logs herausgeben und den Datenfluss bestätigen kann. Gewissheit gibt nur eine Auskunft von netcup (Vertragsinhaber), über den Insolvenzverwalter oder über ihn selbst. Auffällig ist außerdem `erp-demo.meesenburg.neogris.com`, eine weitere Meesenburg-bezogene Anwendung auf seinem Server, Inhalt unbekannt.

**Nachtrag 09.10.2026, 12:20 Uhr:** Der Datenfluss an `85.235.65.67` ist gestoppt (Alloy und cAdvisor auf node1 angehalten, Neustart deaktiviert, Konfiguration erhalten). Die Zusammenarbeit mit neogris wird gekündigt. Als erster Schritt geht eine freundliche Mail an Christopher Seidel: Export und anschließende Löschung **der Daten auf dem Monitoring-Server**, Frist 12.10. (Entwurf: `korrespondenz/2026-10-09_entwurf_mail_neogris_datenherausgabe.md`).

**Nachtrag 09.10.2026, 13:49 Uhr – Sofortsperre:** Per zusätzlicher Traefik-Regel (`zz-meesenburg-sperre-intern.yml`, ohne Neustart) sind von außen gesperrt (HTTP 403): PDF-Dienst `/api/pdf`, `/api/maildata/*` (Auftragsdaten und Auftrags-PDF), `/api/order/send*` und `/api/order/{id}/mark_paid`. Server-intern bleiben sie erreichbar, denn der Mail-Dienst ruft sie teils über die öffentliche Adresse auf. Das Frontend nutzt keinen dieser Endpunkte. **Auswertung der gesicherten Zugriffslogs 05.–09.10.** (471.893 Anfragen): Die einzigen externen Aufrufe dieser Endpunkte sind die Verifizierungstests von M. Piper am 07.10. (19:18 `mark_paid` per curl, 21:42/21:52 `maildata`). Noch offen: IDOR über `single_order`/`?user_id=` (vom Frontend genutzt), Mailpit- und Frontend-Direktports, JWT-Schlüssel.

**Nachtrag 09.10.2026 – Direktports (Auswertung der gesicherten Container-Logs):**
- **Frontend-Direktport 10002** (nginx, nur statische Dateien, leitet keine API weiter → keine Daten abrufbar): Log ab 05.10. (Container-Neustart). 293.364 Anfragen, davon 290.240 intern über Traefik; **37 externe IPs, ausschließlich Internet-Scanner** (u. a. Censys, Shodan; `185.177.72.205`/FBW Networks FR mit 2.999 Anfragen in einer Minute nach Konfig-/Credential-Dateien, alle mit der Startseite beantwortet). Kein Datenabfluss.
- **Mailpit 8025 (Web-Oberfläche):** Mailpit protokolliert **keine HTTP-Zugriffe** → ob die gespeicherten Mails (bis 10.09.2026) über die Oberfläche eingesehen wurden, **ist nicht feststellbar**. Für die DSGVO-Bewertung ist deshalb von einem möglichen Zugriff auszugehen.
- **Mailpit 1025 (SMTP):** Log ab 20.08.: 2.066 Verbindungen von außen (Scanner, Syntaxfehler), 206 intern. Mailpit speichert nur und leitet nicht weiter → kein Missbrauch als Mail-Relay.

**Nachtrag 09.10.2026, 14:08 Uhr:** Mailpit (1025/8025) und Frontend-Direktport 10002 sind von außen gesperrt (IPv4 und IPv6, Firewall auf node1, bleibt nach Neustart aktiv). Intern ist Mailpit weiter erreichbar, der Shop ist unverändert.

**Nachtrag 09.10.2026 – Risiko der PDF-Lücke (Indexierung/Crawler):**
- Abruf-URL (bis 09.10. 13:49 offen): `https://sicherheit.meesenburg.com/api/maildata/order_pdf/{Auftragsnummer}`. Die Antwort ist JSON mit dem Auftrags-PDF als Base64 (`{"pdf": "…"}`), kein direkt aufrufbares PDF. Daneben `…/api/maildata/order/{Auftragsnummer}` (Auftragsdaten als JSON) sowie `shipping`, `cancel` und `payed`. Die Auftragsnummern sind fortlaufend und damit erratbar.
- Die URLs sind **nirgends verlinkt**: nicht im Frontend, nicht in Sitemap oder Mails. Der Mail-Dienst nutzt sie nur serverseitig, das PDF geht als Anhang raus.
- **Crawler:** Zugriffslog 05.–09.10.: ca. 86.000 Bot-Anfragen (Google 44.183, Baidu, Bing, Apple, KI-Crawler …). Bots rufen `/api/items`, `/api/settings` und `/api/auth` auf (beim Rendern der Seiten), aber **keinen einzigen** Aufruf auf `/api/maildata`, `/api/pdf` oder `/api/order/*`.
- **Suchmaschinen:** Websuche nach `site:sicherheit.meesenburg.com` mit `maildata`/`order_pdf`/`api/pdf` und nach `"sicherheit.meesenburg.com/api"` ergab **keine Treffer**. Für eine verbindliche Aussage: Google Search Console → Indexierung → Seiten bzw. URL-Prüfung.
- **Einschätzung:** Eine Indexierung ist sehr unwahrscheinlich (keine Links, JSON statt PDF, keine Bot-Zugriffe). Das Restrisiko ist **gezieltes Abgreifen** durch jemanden, der den Endpunkt kennt (Repo-Zugriff: Entwickler, Gitea-Kopien). Für die Zeit vor dem 05.10. ist das nur über den netcup-Log-Export prüfbar.

**Nachtrag 09.10.2026 – Seit wann war der PDF-Pfad offen? (Git-Historie des Live-Repos, identisch mit Azure DevOps `main`)**

| Datum | Ereignis | Beleg |
|---|---|---|
| 14.03.2025 | PDF-Dienst mit öffentlicher Route `/api/pdf` angelegt (rendert beliebiges HTML; keine gespeicherten Kundendaten, aber RCE/SSRF) | Commit `176f233` (Christopher Seidel, PR #106) |
| 25.11.2025 | Auftrags-PDF für Mails eingeführt, aber **ohne** öffentlichen Endpunkt | `915dfbf` (#230) |
| **17.08.2026 07:11** | `main.py` bindet den Router `mail_data` ein (`/maildata/order`, `/order_pdf`, …), in `main` gemergt um 07:12 (PR 807). Die Datei `endpoints/mail_data.py` fehlt im Commit → sie lag **nur unversioniert auf dem Server**, sonst wäre die Business-API nicht gestartet | `7dde5b0`, Autor `vitandi <vitandi@neogris.com>` |
| 17.08.2026 10:48 | Der Mail-Dienst ruft `…/api/maildata/…` über die öffentliche Adresse auf | `ef0fe86` (vitandi) |
| 09.09.2026 07:51 | `endpoints/mail_data.py` erstmals committet (PR 853), Datei auf dem Server seit 09.09. 08:14 | `c94dd9e` (vitandi) |
| 09.10.2026 13:49 | Von außen gesperrt | Traefik-Regel |

**Ergebnis:** Der Abruf von Auftrags-PDFs und Auftragsdaten über `/api/maildata/*` war **potenziell ab 17.08.2026 morgens, sicher ab 09.09.2026** öffentlich, bis 09.10.2026 13:49 (also 4 bis 7½ Wochen). Davor gab es keinen öffentlichen Endpunkt für Auftrags-PDFs (Historie geprüft). Der Bestelldaten-Abruf über `single_order` (ohne PDF) ist älter und bleibt offen.

**Nebenbefund:** Die Commit-Adresse von „vitandi“ ist **`vitandi@neogris.com`**. Der Root-Schlüssel `vitandi@work` auf node1–3 gehört damit zu neogris (Seidel/neogris-Umfeld).

**Nachtrag 09.10.2026 – netcup-Export eingegangen (17:47 Uhr):**
- `monitoring_export.zip`, 3,1 GB, SHA-256 `7248a1aa…109790`, geliefert per SharePoint-Dateianforderung; im Image unter `netcup_export_20261009/` (Original, entpackt, Arbeitskopie).
- Inhalt: Loki-Logs (263 MB: Traefik-Zugriffslog, alle SN24-Dienste) und Prometheus-Metriken (3,3 GB, Blöcke ab Juli 2026).
- **Loki hatte eine Aufbewahrung von nur 14 Tagen** (`retention_period: 14d`, Löschung durch den Compactor aktiv). Die Logs reichen damit nur bis etwa **25.09.2026** zurück. Für den Zeitraum 17.08.–24.09. (PDF-Lücke, Bericht oben) **gibt es keine Zugriffslogs mehr**. Die Annahme, der Server enthalte die vollständige Log-Historie, ist widerlegt.
- **Abdeckung:** lückenlos vom **25.09.2026 07:44 UTC bis 09.10.2026 10:20 UTC** (925 Log-Blöcke, jeden Tag vorhanden; Ende = Stopp von Alloy).
- **Auswertung** (lokales Loki auf einer Arbeitskopie, Löschung abgeschaltet; danach Container gelöscht): Im Traefik-Zugriffslog gibt es **keinen einzigen fremden Aufruf** von `/api/maildata`, `/api/pdf`, `mark_paid` oder `/api/order/send*`. Alle 11 Treffer sind Verifizierungstests von M. Piper: 07.10. von `216.83.217.161` (`mark_paid`, `maildata/order_pdf`, `maildata/order`) und 08.10. 08:18–08:20 aus dem Homeoffice `195.250.0.170` (wilhelm.tel; `send/…` 404, `send_order_to_system/…` 200). `/api/pdf` wurde nie von außen aufgerufen.
- **Abschluss:** Erhalt bestätigt, **Löschbestätigung von neogris per Mail am 09.10.2026 erhalten** (Mail aufbewahren). Lokale Download-Kopie gelöscht; eine Kopie bleibt vorerst im SharePoint-Ordner mit Zugriff nur für M. Piper.
- **Ergebnis für die DSGVO-Bewertung:** Für 25.09.–09.10. ist **kein Missbrauch** der PDF- und Auftragsdaten-Endpunkte belegt. Für 17.08.–24.09. gibt es keine Logs mehr, aber auch keinen Hinweis auf Zugriffe oder Indexierung (keine Links, keine Bot-Aufrufe im belegbaren Zeitraum).

**Nachtrag 09.10.2026 – Umfang der PDF-/Auftragsdaten-Lücke (Datenbankstand 09.10., nur Zählungen):**
- `/api/maildata/*` sucht Aufträge in der Mongo-Sammlung `order_erp`. Diese befüllen JTL-Trigger (`trigger_up.sql`, `trg_tAuftrag_ICS` u. a.) **ohne Kanalfilter**. Danach erst kommt die Shop-Sammlung `order` (305 Shop-Bestellungen).
- `order_erp` enthält **43.787 Aufträge** (Auftragsdatum 2007–2026) von **29.155 verschiedenen Kunden-IDs**, aus allen Kanälen: `2-2-5` Shop/Altshop 24.620, `1-1-1` Wawi 10.683, `1-1-6` 4.512, `6-51-1` Amazon 1.480, `3-9` 1.018, `2-2-0` 994, `5-30-0` eBay 378, weitere < 100.
- Auftragsnummern: gemischte Formate, viele 6–7-stellig fortlaufend (z. B. `25…`, `26…`) → aufzählbar.
- Das PDF wird live aus JTL erzeugt (`/salesOrders/{id}/output/pdf`), also für jeden gespiegelten Auftrag.
- Auswertung in einem lokalen Container auf einer Kopie des Dumps, danach gelöscht.
- **DSB-Mail** (nur diese Lücke): Entwurf `korrespondenz/2026-10-09_entwurf_mail_dsb_pdf_schnittstelle.md`. Kenntnis 07.10.2026 → **72-h-Frist Art. 33 endet am 10.10.2026**.

**Nachtrag 11.10.2026 – Kontrolle node1 seit den Sofortmaßnahmen:**
- **Maßnahmen wirken:** Traefik-Sperre aktiv (extern 403), Ports 1025/8025/10002 per Firewall zu (Regeln aktiv, Dienst `mee-ports-sperre` für den Neustart aktiviert), Alloy/cAdvisor seit 09.10. 12:20 gestoppt, Shop HTTP 200. Keine neuen Benutzer, Schlüssel, sudo-Einträge oder Cronjobs; keine Container neu erstellt; Git-Stand unverändert (09.09. + 62 lokale Änderungen). Neue Dateien nur aus dem Betrieb (Auftragsanhänge, ein Artikelbild).
- **Entwickler-Anmeldung nach dem Alloy-Stopp:** `root` von `217.74.152.246` (Norlys DK, vitandi-Schlüssel) am **09.10.2026 13:12:02–13:12:14** (12 Sekunden, rund 50 Minuten nach dem Stopp des Log-Versands, vor den Sperren). Keine Befehle in der Historie gespeichert.
- **Offene Root-Sitzung seit 07.10.2026 13:27** (pts/0, `217.74.152.246`) besteht weiter, untätig (nur `-bash`). Was darin ausgeführt wird, landet erst beim Abmelden in der Historie → bei der Aussperrung zuerst diese Sitzung sichern/beenden.
- **Neustart `items_svc_worker` am 09.10. 08:08:16** (Stopp mit SIGTERM, nach 10 s erzwungen). Nicht durch Meesenburg; keine SSH-Anmeldung zu diesem Zeitpunkt → vermutlich aus der offenen Root-Sitzung.
- **Neuer Befund – Produktions-Datenbank-Dumps per HTTP bereitgestellt:** In `/home/admin/SN24/db_backups` erzeugen die Entwickler regelmäßig vollständige Dumps (auth, business, items, payment, strapi; Ordner 2026_04_20 … zuletzt **01.10.2026 13:26 und 02.10.2026 14:09**, 235 MB) und starten danach im Ordner `python3 -m http.server` (Port 8000, **unverschlüsselt, ohne Anmeldung, keine Firewall**). Laut Root-Historie mindestens 7-mal (Juni, Juli, 01./02.10.). Zweck: Herunterladen auf eigene Rechner. Damit liegen **vollständige Kundendatenbanken bei den Entwicklern**, und während der Server lief, waren sie für jeden mit Kenntnis von IP und Port abrufbar. Port 8000 ist derzeit nicht offen. Die Dumps sind in unserer Sicherung (Stufe B) enthalten. → Löschung/Herausgabe der Kopien bei den Entwicklern verlangen (AVV), DSB informieren (Kollege).

**Nachtrag 12.10.2026 – Aussperrung Hetzner:** node1–3 nur noch mit `mpiper` erreichbar (Root- und admin-Schlüssel entfernt, Root-Anmeldung und Passwortanmeldung aus). Beim Beenden der offenen Root-Sitzungen gesichert: Der Entwickler hatte am **09.10. 08:08** (während bzw. nach unserer Sicherung) `items_svc/src/jtl/worker.py` **direkt auf dem Live-Server geändert** (fest eingetragene Artikel-ID-Liste für einen manuellen Nachlauf) und den Worker neu gestartet; außerdem `~/.ssh/known_hosts` angesehen. Auf node3 hatte er am 09.10. das Projekt `configurators` umgebaut und neu gestartet.

**Nachtrag 12.10.2026 – DevOps/Entra:** Beide Entwickler entfernt, Gastkonten gesperrt. **Zuordnung:** Laut DevOps-Audit hat Christopher Seidel seinen SSH-Schlüssel „Meesenburg Neogris“ am 16.08.2026 von `217.74.152.246` angelegt. Von dieser IP kamen alle Root-Sitzungen mit dem Schlüssel `vitandi@work`. „vitandi“ (`vitandi@neogris.com`) ist damit Christopher Seidel bzw. sein Konto.

**Nachtrag 12.10.2026 – Testserver `c22910` (ecomdata, 80.243.226.47), Sichtung:**
- Windows Server 2022, **JTL-Wawi 1.10.12**, SQL Server 2022 `JTLWAWI` auf Port 50000; Test-ERP der Entwickler-Testinstanz (node2). Datenbanken nicht sichtbar (Konto `useradmin` ist kein SQL-sysadmin, nur `sa`) → ob eine Kopie der Produktivdaten vorliegt, ist offen.
- Konten: `cpsadmin` und `IcingaUser` (ecomdata), **`useradmin`** (Administrator, gleiches Schema wie c10330), `meesenburggmbh1` (RDP, M. Piper). **Offene RDP-Sitzung `useradmin` seit 08.10.2026 08:02** (4 Tage untätig, Herkunft unklar; das Security-Log reicht nur bis 08.10. 15:38 zurück).
- **Custom-API-Kopie:** Dienst `JtlDbReader` (gestoppt) → `C:\Users\useradmin\Documents\jtl_db_reader`. Dazu Python 3.13, FileZilla, WinSCP (Entwickler-Werkzeuge).
- Windows-Firewall: SQL 50000, JTL 50443/58080, RDP 3389 und **WinRM 5985 jeweils mit Remote „Any“**; der Filter liegt nur im ecomdata-Portal. SQL 50000 ist von der Homeoffice-IP aus erreichbar. 50443/58080 lauschen nicht.
- Positiv: ecomdata sichert die SQL-Datenbank (geplante Aufgaben `\Backups\SQL Full/Diff/Log Backup` unter `cpsadmin`), Icinga/NinjaOne/IPBan aktiv, NLA für RDP an.

**Nachtrag 12.10.2026 – RDP-Sitzungen nach Passwortwechsel (Ereignisse LocalSessionManager 24/25):**
- **c10330:** Die `useradmin`-Sitzung 6 (angemeldet 29.09.2026 07:22, in ihr läuft der JTL-Worker) war bis heute 08:46 **von `217.74.152.246` (Seidel) verbunden**. Seidel hatte also seit dem 29.09. eine durchgehende Admin-Sitzung auf dem Produktiv-JTL. Getrennt per `tsdiscon` am 12.10. 08:46 (ebenso Sitzungen 27 `useradmin`, 28 `nord1`, 9 `nord2`). Danach Wiederverbindungen von `37.228.146.166` (Hamburg, Geschäftskundenanschluss – Standort klären): `nord1`, `nord2` und um **09:26 Übernahme der `useradmin`-Sitzung 6, 09:27 Wechsel auf Sitzung 27 (`useradmin`)** → wer? (nur mit neuem Passwort möglich). `nord4` heute 06:37/06:58 von `45.145.239.72` (NL), 08:14 von `80.245.106.33` (Homeoffice M. Piper).
- **c22910:** Die `useradmin`-Sitzung 2 (seit 08.10.2026 08:02) war **ebenfalls von `217.74.152.246` verbunden**; getrennt am 12.10. 09:48. Passwort dort zu diesem Zeitpunkt **noch nicht rolliert** → sofort rollieren.

**Nachtrag 12.10.2026 (Ergänzung):** Auf c10330 wurden `useradmin`, `nord1`, `nord2`, `nord4` um **08:38:07** rolliert, getrennt um 08:46. Die Wiederverbindungen von `37.228.146.166` (nord1 08:50, nord2 08:48/08:57/08:58, **useradmin 09:26/09:27**) erfolgten also mit den **neuen** Passwörtern. M. Piper selbst war nicht als `useradmin` per RDP angemeldet; Klärung mit dem Kollegen am Standort läuft. Die Custom-API-Kopie auf c22910 weicht leicht von der Produktivversion ab (`main.py`, `manage_apis.py`, zusätzlich `main_old.py`, `.env`) und lief bis zum Neustart am 15.09.2026; danach startete sie nicht wieder (bestätigt das Neustart-Risiko).

**Nachtrag 12.10.2026 – RDP-Herkunft c10330 (01.09.–12.10.2026, 351 Ereignisse 21/25):**
- `37.228.146.166` (Meesenburg-Standort mit Packtisch): 308× an praktisch jedem Arbeitstag, Konten `nord1/2/4/5` **und `useradmin`**, alle 30 Arbeitstage, 06:29–22:47 Uhr, kein nächtlicher Wechsel → **feste IP sehr wahrscheinlich**; wiederkehrende Verbindung um 22:47 (automatischer Vorgang am Standort?); ein Kollege nutzt `useradmin` als Arbeitskonto.
- `217.74.152.246` (Seidel): `useradmin`, 07.09.–07.10.
- **`87.132.78.136`** (Telekom-DSL privat): `useradmin` 29.09.–07.10. (4×) – **unbekannt**, möglicherweise zweiter Entwickler.
- `87.133.85.157` (Telekom-DSL privat): `nord4` und `useradmin` 02.09.–23.09. (14×) – zu klären.
- `81.189.65.47` (Drei, Österreich): `cpsadmin` 28.09.–01.10. – ecomdata-Konto, bei ecomdata bestätigen.
- `89.58.72.x` (2provide): `nord1` von zu Hause; übrige Adressen: `nord4` (M. Piper).

