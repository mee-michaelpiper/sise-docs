**Betreff:** esERP-API: Listen mit „geändert seit“ für die Portal-Suche

Hi,

ich baue im SiSe-Portal gerade eine zentrale Suche über Kunden, Artikel, Angebote, Aufträge und Rechnungen. Die Daten ziehe ich alle 15–30 Minuten in einen eigenen Suchindex – und dafür fehlen mir in der API noch ein paar Sachen:

1. **Angebote und Aufträge als Kopf-Liste.** Aktuell komme ich nur über die Positionen je Nummer ran. Ich brauche `GET /angebote` und `GET /auftraege`, gepaged, mit den Kopfdaten (Felder siehe unten).
2. **„Geändert seit“ für alle Listen.** Ein Parameter `geaendertSeit` für Kunden, Artikel, Lieferanten, Angebote, Aufträge und Rechnungen. Dann hole ich nur die Änderungen statt jedes Mal alles.
3. **Löschungen und Stornos.** Ich muss mitbekommen, was weg ist, sonst bleibt es in meiner Suche stehen. Entweder ein eigener Endpunkt oder der Datensatz bleibt mit Löschkennzeichen in der Liste – wie es für dich einfacher ist.
4. **Sauberes Paging.** Stabile Sortierung, `totalCount` in der Antwort, 1.000 Einträge pro Seite in ein paar Sekunden.

Erst mal auf esapi-test mit Swagger, dann teste ich direkt. Am wichtigsten sind mir 1 und 2, der Rest kann danach kommen. Sag mir kurz, was davon geht und bis wann.

Danke dir!
Michael

---

**Anforderungen im Überblick**

| # | Anforderung | Details | Prio |
|---|---|---|---|
| 1 | `GET /angebote`, `GET /auftraege` | gepaged (`pageNumber`, `pageSize` bis 1.000); Felder: Nummer, Variante (Angebot), Kunde Nr. + Name, Datum, Status, Sachbearbeiter (Pers.-Nr./Mail), Projekt/Kommission/Bezeichnung, Netto gesamt, Änderungszeitpunkt | hoch |
| 2 | Parameter `geaendertSeit` | für `/kunden`, `/artikel`, `/lieferanten`, `/angebote`, `/auftraege`, Rechnungslisten; Zeitstempel mit Uhrzeit, sortiert nach Änderung; Änderungszeitpunkt mit Uhrzeit und Zeitzone in jedem Datensatz | hoch |
| 3 | Löschungen/Stornos | `GET /…/geloescht?seit=…` mit Nummern **oder** Löschkennzeichen/Status in der Liste | mittel |
| 4 | Paging | stabile Reihenfolge, `totalCount`, 1.000 Einträge/Seite in wenigen Sekunden | mittel |
| 5 | Bereitstellung | zuerst esapi-test, Swagger-Beschreibung ergänzt | – |
