# 19 Kundenkonten und Händlerpreise (F-17, F-05) – Analyse und Plan

Stand: 30.10.2026, Umsetzung K1/K3 04.11.2026 (Abschnitt 7). Grundlage: Workshop 29.10.2026 (F-17 B: Konten von Anfang an, Anmeldung wie bisher, gleiche Kundennummern, Abgleich mit der Kundenübernahme des Stripe-Shops; F-05: Händler sehen ihre Preise, die KG später). Alle Zahlen stammen aus lesenden Abfragen gegen die JTL-Shop-Datenbank (Timme) und die Wawi (ecomdata). Es wurden nur Zählungen ausgegeben, keine Kundendaten.

## 1. Kurzfassung

- **Der JTL-Shop hat 8.141 Kundenkonten.** 6.928 Konten haben ein bcrypt-Passwort. 1.213 Konten haben noch ein Altverfahren (SHA1/MD5, zuletzt 2021 geändert). Der .NET-Shop kann alle drei Verfahren prüfen wie der JTL-Shop. **Kein Kunde muss ein neues Passwort vergeben.**
- **Die Kundennummer kommt aus der Wawi**, nicht aus dem Shop. Die Wawi-Tabelle `tInetKundeShop` ordnet jedes Shop-Konto genau einem Wawi-Kunden zu. 6.639 Konten haben so einen Wawi-Kunden mit Kundennummer. Die übrigen 1.502 Konten haben nie bestellt, ihr Wawi-Kunde entsteht erst mit der ersten Bestellung.
- **Die Zuordnung über die E-Mail-Adresse ist unsicher.** In der Wawi gehören 944 E-Mail-Adressen zu mehreren Kunden. Der Stripe-Shop hat genau so zugeordnet. Der .NET-Shop nutzt die feste Zuordnung aus `tInetKundeShop`.
- **Der Stripe-Shop hat nur die bcrypt-Konten übernommen.** Die 1.213 Konten mit Altverfahren konnten sich dort nicht anmelden. Seit Juni hat der Stripe-Shop 491 neue Wawi-Kunden angelegt, für die es im JTL-Shop kein Konto gibt.
- **Händlerpreise unterscheiden sich kaum von Endkundenpreisen.** Nur 15 Artikel haben einen anderen Grundpreis, die Kategorie-Rabatte reichen bis 45 % statt 40 %. Der Hauptunterschied ist die Anzeige netto statt brutto. Bei der KG liegen die Kategorie-Rabatte bei 35–61 %, daher das Problem mit der Außendienst-Sicht.

## 2. Befunde JTL-Shop (Tabelle `tkunde`)

| Thema | Befund |
|---|---|
| Konten | 8.141 registriert (dazu 1 gesperrt), 8.573 Gastkunden ohne Passwort |
| Passwörter | 6.928 bcrypt (`$2y$10$`), 1.088 SHA1-Altverfahren (JTL 3/4, Salz im Hash), 125 MD5 ohne Salz. Die Altverfahren wurden zuletzt am 14.07.2021 geändert, kein solches Konto hat sich seither angemeldet |
| Prüfung im JTL-Shop | `PasswordService::verify`: Länge 32 → MD5, Länge 40 → SHA1-Altverfahren, sonst `password_verify`. Beim erfolgreichen Login stellt der Shop auf bcrypt um (`needsRehash`). Fehlversuche zählt `nLoginversuche` |
| Anmeldename | E-Mail-Adresse (`cMail`, nur registrierte Konten). 12 Adressen kommen doppelt vor (26 Konten). Der JTL-Shop nimmt dann das erste Konto |
| Verschlüsselte Felder | Nachname, Firma und Straße stehen XTEA-verschlüsselt in der Datenbank (Schlüssel in der Shop-Konfiguration). Der .NET-Shop holt Name und Adressen deshalb aus der Wawi |
| Aktivität | Anmeldung im letzten Jahr: 308 Konten, vor 1–3 Jahren: 154, nie (seit es das Feld gibt): 7.679. Bestellungen der letzten 12 Monate: 428 von 382 Konten, 839 als Gast |
| Kundengruppen | 8.095 Endkunden, 44 Händler, 2 KG |
| Kundennummer | Im Shop fast immer leer (8.080 Konten), sie entsteht in der Wawi |
| Sonstiges | Keine Zwei-Faktor-Anmeldung, keine Kundenrabatte, kein Guthaben, keine kundenindividuellen Preise. 69 Kunden haben gespeicherte Lieferadressen (78). Registrierung mit Captcha, Firma und USt-IdNr. optional |

## 3. Befunde Wawi

| Thema | Befund |
|---|---|
| Zuordnung | `dbo.tInetKundeShop` (Shop 5 = sicherheit.meesenburg.com): Shop-Kunde (`kInetKunde` = `tkunde.kKunde` im Shop) → Wawi-Kunde (`kKunde`), mit `nRegistriert`. Registrierte Konten: 6.672 Zeilen, jeder Wawi-Kunde höchstens einmal |
| Abgleich nach Id | 6.639 der 8.141 Shop-Konten sind zugeordnet, alle mit Kundennummer (Format `Nummer-TTMMJJJJ`, 11 nur Ziffern). 1.502 nicht zugeordnet, davon 1.500 ohne jede Bestellung |
| Kundengruppe | Bei 19 Konten weichen Shop und Wawi ab (11: Shop Endkunde, Wawi Händler; 8 umgekehrt). Maßgeblich ist die Wawi |
| Passwörter | Die Wawi speichert keine (`tinetkunde.cPasswort` leer) |
| E-Mail | 944 E-Mail-Adressen gehören zu mehreren Wawi-Kunden (siehe J-26: `m.piper@meesenburg.de` doppelt) |
| Neue Kunden 2026 | Bis Mai hatten 75–85 % der neuen Wawi-Kunden eine Shop-Zuordnung. Juli bis September: fast keine (Stripe-Shop, Herkunft „Webshop“, 491 Kunden seit Juni). Seit 19.10. wieder über den JTL-Shop |

**Preise je Kundengruppe (JTL-Shop, Abgleich aus der Wawi):**

| Gruppe | Grundpreise | Kategorie-Rabatte | Anzeige | Sichtbarkeit |
|---|---|---|---|---|
| 1 Endkunden | 8.695 Artikel | 54 Kategorien, 5–40 % | brutto | 1.042 Artikel ausgeblendet |
| 2 Händler | 15 Artikel abweichend | 55 Kategorien, 5–45 % | **netto** | 1.043 ausgeblendet |
| 17 KG | 4 Artikel abweichend (von 5.045) | 21 Kategorien, **35–61 %** | **netto** | 46 ausgeblendet |

## 4. Kundenübernahme des Stripe-Shops (Quellcode `auth_svc/migrate_jtl_data.py`)

- Quelle war ein SQL-Export der Tabelle `tkunde`. Übernommen wurden nur Konten mit bcrypt-Passwort. **SHA1/MD5-Konten wurden übersprungen** (Kommentar im Code: „old“), diese 1.213 Kunden kamen nicht mehr in ihr Konto.
- Den Nachnamen hat das Skript mit dem XTEA-Schlüssel des JTL-Shops entschlüsselt. **Der Schlüssel steht im Klartext im Quellcode, dazu die E-Mail-Adresse eines echten Kunden als Testwert.** Das ist ein Sicherheits- und Datenschutzbefund (Bericht 04/10). Der Schlüssel schützt die Adressdaten in der JTL-Shop-Datenbank und gilt damit als bekannt.
- Den Wawi-Kunden hat das Skript über die E-Mail der Rechnungsadresse gesucht. Ohne Treffer hat es die Id des JTL-Shop-Kunden als Wawi-Id eingetragen, also eine Id aus einer anderen Tabelle. Bei 944 doppelten E-Mail-Adressen sind falsche Zuordnungen wahrscheinlich.
- Beim Login stellte der Stripe-Shop bcrypt auf Argon2 um. Wer sich zwischen Juni und 19.10. dort angemeldet, registriert oder sein Passwort geändert hat, hat den aktuellen Stand nur in der Stripe-Shop-Datenbank (`auth_mongo` auf node3, Container laufen noch).
- **Nicht geprüft:** der Inhalt von `auth_mongo`. Dafür braucht es das Mongo-Passwort, und Passwörter gebe ich nicht selbst ein (siehe Entscheidung D5).

## 5. Plan

Grundsatz (Entscheidung E-A, Bericht 14): Der .NET-Shop meldet Kunden selbst an. Anmeldename ist die E-Mail-Adresse. Jedes Konto ist fest mit einem Wawi-Kunden verknüpft (Wawi-Id und Kundennummer), nicht über die E-Mail.

| Schritt | Inhalt | Prüfung |
|---|---|---|
| **K1 Konto und Anmeldung** | Mongo-Sammlung `customers` (E-Mail, Passwort-Hash, Kundengruppe, Wawi-Id, Kundennummer, Fehlversuche, Sperre). Prüfung bcrypt, SHA1-Altverfahren und MD5 wie im JTL-Shop. Nach erfolgreichem Login neuer Hash mit Argon2id (OWASP, seit 04.11.2026; vorher PBKDF2). Anmelde-Cookie im Portal (HttpOnly, Secure, SameSite). Drosselung je IP und Konto, gleiche Antwort für unbekannte E-Mail und falsches Passwort, kurze Sperre nach Fehlversuchen. „Passwort vergessen“ per einmaligem Link (1 Stunde). Passwörter erscheinen nie in Logs, Mails oder der Outbox (Lehre aus dem Stripe-Shop) | Tests mit echten Prüfwerten je Verfahren (auf dem JTL-Server mit einem Dummy-Passwort erzeugt, ohne Kundendaten); kein Zugriff auf fremde Konten |
| **K2 Übernahme** | Admin-Job, wiederholbar: liest Konten aus dem JTL-Shop (Id, E-Mail, Hash, Gruppe, Datum) und die Zuordnung aus der Wawi (`tInetKundeShop` → Kundennummer, Gruppe). Name und Adressen aus der Wawi. Doppelte E-Mails (12) und Gruppenabweichungen (19) als Klärungsliste. Bis zum Umschalttag als Delta nach Änderungsdatum, am Umschalttag ein letzter Lauf | Zählung gegen Abschnitt 2/3, Stichproben mit Testkonto |
| **K3 Konto-Seiten und Kasse** | Anmelden, Registrieren, Passwort vergessen, Mein Konto: Bestellungen (.NET-Shop und Wawi-Aufträge der Kundennummer), Rechnungsadresse (aus der Wawi), Lieferadressen, Passwort ändern. Kasse: angemeldet sind die Daten vorbelegt, und die Bestellung geht an den **bestehenden** Wawi-Kunden. Damit entfällt die Dublettensuche über die E-Mail (J-26, Trello #69). Widerruf mit Bestellauswahl (F-24). Neue Konten bekommen ihren Wawi-Kunden bei der ersten Bestellung, wie im JTL-Shop | Fremde Bestellungen nicht abrufbar (Test) |
| **K4 Preise je Kundengruppe** | Heute berechnet der Abgleich die Preise nur für die Gastgruppe. Neu: Preise, Rabatte und Sichtbarkeit für die Gruppen 1, 2 und 17. Warenkorb und Kasse rechnen mit der Gruppe des angemeldeten Kunden. Händler sehen Nettopreise. Die KG-Gruppe ist technisch dabei, aber per Schalter aus, bis die Außendienst-Sicht gelöst ist | Händler-Artikel und Rabatt-Kategorien auf den Cent wie im JTL-Shop |
| **K5 Gegenprüfung Stripe-Shop** | Zählen in `auth_mongo`: Konten gesamt, Konten ohne JTL-Shop-Gegenstück, Argon2-Hashes (dort angemeldet oder geändert). Konten, die es nur im Stripe-Shop gibt, werden übernommen. Ihre Argon2-Hashes (argon2-cffi, gleiches Format) prüft der .NET-Shop direkt – kein neues Passwort nötig | Zahlen im Bericht |

Reihenfolge: K1 und K3 zuerst (mit Testkonten lokal), dann K2, dann K4. K5 sobald der Lesezugang geklärt ist.

## 6. Entscheidungen

| Nr. | Frage | Vorschlag |
|---|---|---|
| D1 | Wie liest der .NET-Shop die Konten aus dem JTL-Shop? (a) Timme legt einen MySQL-Lese-Login an, Zugriff per SSH-Tunnel wie bei der Wawi; (b) einmaliger Export per Skript am Umschalttag | **Entschieden 04.11.2026: b**, Meesenburg exportiert selbst einmalig |
| D2 | Für die bcrypt-Prüfung braucht .NET eine Bibliothek: `BCrypt.Net-Next` (MIT, verbreitet) | **Entschieden 04.11.2026: ja.** Neue Hashes: zuerst PBKDF2, am selben Tag auf **Argon2id** umgestellt (`Konscious.Security.Cryptography.Argon2`, MIT) |
| D3 | 1.213 Konten mit SHA1/MD5 (seit 2021 ohne Anmeldung) | Übernehmen und beim ersten Login umstellen, wie der JTL-Shop |
| D4 | 1.500 Konten ohne Bestellung und ohne Wawi-Kunden | Übernehmen; der Wawi-Kunde entsteht bei der ersten Bestellung |
| D5 | Lesezugang zu `auth_mongo` (node3) für K5 | Du liest die Zählungen selbst oder richtest einen Lese-Login ein; ich liefere die Abfragen |
| D6 | Kundengruppe bei Abweichung Shop/Wawi | Wawi ist maßgeblich |
| D7 | Der XTEA-Schlüssel des JTL-Shops steht im Stripe-Shop-Code | Als Sicherheitsbefund aufnehmen. Ein Schlüsselwechsel im JTL-Shop ist aufwendig (alle Adressen neu verschlüsseln) und entfällt mit dem Wechsel auf den .NET-Shop |

## 7. Umsetzung K1 und K3 (04.11.2026)

**Gebaut:**

| Bereich | Umsetzung |
|---|---|
| Konto | Mongo-Sammlung `customers`, Anmeldename E-Mail (eindeutig, klein geschrieben). Felder für Wawi-Kunde, Kundennummer, JTL-Kunden-Id (für K2), Kundengruppe |
| Passwörter | Prüfung wie JTL-Shop 5: bcrypt (`$2y$`), SHA1-Altverfahren (Salz im Hash, Längen in Zeichen wie `mb_strlen`), MD5. Getestet mit Prüfwerten, die auf dem JTL-Server mit Dummy-Passwörtern erzeugt wurden. Nach dem ersten erfolgreichen Login: **Argon2id** mit den OWASP-Mindestwerten (19 MiB, 2 Durchläufe, 1 Thread), Format `$argon2id$v=19$…` wie PHP und argon2-cffi (Prüfwerte aus PHP). Ältere Hashes (PBKDF2 vom 04.11. früh, schwächere Argon2-Werte) werden beim Login ersetzt. Anmeldung dauert lokal etwa 100 ms |
| Schutz | 5 Fehlversuche → 15 Minuten gesperrt. Unbekannte E-Mail, falsches Passwort und Sperre bekommen dieselbe Antwort, mit gleicher Rechenzeit. Fangfeld bei der Registrierung. Passwörter erscheinen nie in Logs oder Mails |
| Passwort vergessen | Einmal-Link, 1 Stunde gültig. Gespeichert wird nur der SHA-256-Hash. Der Link steht nur bis zum Versand im Mail-Job und wird danach gelöscht. Neues Passwort hebt eine Sperre auf |
| Anmeldung im Portal | Cookie `siseshop.konto` (HttpOnly, SameSite Lax, „Angemeldet bleiben“ 30 Tage). Blazor kann kein Cookie setzen, deshalb tauscht der Browser ein Einmal-Ticket (60 s) gegen das Cookie. Alle 5 Minuten prüft das Portal, ob das Passwort geändert wurde (SecurityStamp), und meldet dann ab |
| Seiten | `/konto/anmelden`, `/konto/registrieren`, `/konto/passwort-vergessen`, `/konto/passwort-neu`, `/konto` (Bestellungen, Daten, Rechnungs- und Lieferadresse, Passwort ändern, Abmelden). Kopfzeile zeigt „Anmelden“ bzw. den Namen |
| Kasse | Angemeldet: E-Mail des Kontos (nicht änderbar), Name, Telefon und Adressen vorbelegt. Die Bestellung gehört zum Konto |
| Wawi-Übergabe | Hat das Konto einen Wawi-Kunden, geht der Auftrag an ihn, ohne Suche über die E-Mail. Legt die erste Bestellung einen Wawi-Kunden an, wird er am Konto gespeichert |
| Widerruf | Angemeldet: Name und E-Mail vorbelegt, Bestellung aus dem Konto wählbar |
| Admin | „Kundenkonten“: Herkunft, Wawi-Kunde, Passwortverfahren, Sperre; Knopf „Testkonten anlegen“ (nur Entwicklung) |

**Testkonten** (nur Entwicklung, Passwörter in `src/Meesenburg.SiseShop.Api/appsettings.Development.json`): Endkunde (Argon2id), Händler (bcrypt), KG (SHA1-Altverfahren), Altkonto (MD5). Jeweils so gespeichert wie ein übernommenes JTL-Konto.

**Lokal getestet (04.11.2026):** falsches Passwort abgelehnt; Anmeldung mit SHA1- und MD5-Konto, danach Argon2id (zuerst PBKDF2, nach der Umstellung alle 4 Testkonten Argon2id); Sperre nach 5 Fehlversuchen; unbekannte E-Mail gleiche Antwort; Kasse vorbelegt, Bestellung S100012 am KG-Konto, erscheint unter „Meine Bestellungen“; Widerruf mit Bestellauswahl; Abmelden; „Passwort vergessen“ mit Mail in Mailpit, Link nur einmal nutzbar und nach dem Versand gelöscht. 221 Tests grün.

**Offen:** K2 (Format des Exports abstimmen), K4 Preise je Kundengruppe, K5, Wawi-Aufträge aus dem JTL-Shop unter „Meine Bestellungen“. Die Drosselung je IP ist seit 04.11.2026 je Kunde (Bericht 14). Die Login-Tickets liegen im Speicher des Portals, bei mehreren Instanzen braucht es einen verteilten Cache.

**Warum Argon2id (04.11.2026):** OWASP („Password Storage Cheat Sheet“) empfiehlt in dieser Reihenfolge Argon2id, scrypt, bcrypt, PBKDF2 (letzteres vor allem, wenn FIPS gefordert ist). Argon2id braucht viel Arbeitsspeicher je Prüfung. Das macht massenhaftes Durchprobieren auf Grafikkarten oder Spezialhardware teuer, und es gibt keine Längengrenze wie bei bcrypt (72 Bytes). Nebeneffekt: Die Argon2-Hashes des Stripe-Shops (K5) sind direkt prüfbar.
