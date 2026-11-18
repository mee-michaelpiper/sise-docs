# JTL-Shop: Wie er rechnet und was eingestellt ist

Begriffe: **JTL-Shop** = JTL-Shop 5.5.2 bei Timme (Referenz) · **Stripe-Shop** = Python-Backend auf node1 (offline) · **.NET-Shop** = unsere Migration.

Stand: 27.10.2026 · Quelle: JTL-Shop bei Timme Hosting (`k69g81.meinserver.io`, Webseite `web33`, Datenbank `c1w33db1`), nur lesend.
Zugang: SSH-Benutzer `c446308mpiper` (Jailkit, Schlüssel `~/.ssh/timme_shop`, Eintrag `timme-shop` in `~/.ssh/config`).
Abfragen: feste SELECTs in einer schreibgeschützten Transaktion, Zugangsdaten nur serverseitig, keine Kunden- und Bestelltabellen, Werte mit Schlüssel- oder Passwortnamen maskiert.

## Kurzfassung

- **JTL-Shop 5.5.2**, Template NOVA/NOVAChild, **PHP 8.2** (im Jail nur 8.2 vorhanden, Stand 8.2.35 vom 21.10.2026 → gegen PHP2Shell abgesichert).
- Der JTL-Shop rechnet **mit dem Standard-Kategorierabatt von JTL**. Es ist kein Plugin daran beteiligt. Unsere Beispiele aus dem Vergleich gehen auf den Cent auf (siehe unten).
- **Gäste** bekommen die **Standard-Kundengruppe „Endkunden“ (1)**, nicht „Gast“ (3).
- **1.041 Artikel** sind für Endkunden und Gäste über die **Sichtbarkeit je Kundengruppe** ausgeblendet. Das kennt der .NET-Shop noch nicht.
- Versandarten, Zahlarten, Gutscheine und Gratisgeschenke sind **im Shop selbst** eingestellt, nicht in der Wawi.

## 1. Preisregel (JTL-Shop 5.5, Kerncode)

Quelle: `includes/src/Catalog/Product/Artikel.php` (Kategorierabatt, Aufpreise) und `includes/src/Filter/Items/PriceRange.php` (Preisformel).

```
Rabatt %   = MAX( höchster Kategorierabatt aller Kategorien des Artikels (Variantenkind: die des Vaters),
                  Rabatt der Kundengruppe (alle 0 %), individueller Kundenrabatt (nur angemeldet) )
Preis netto = MIN( Listenpreis netto × (100 − Rabatt %) / 100 ,  Sonderpreis netto (falls aktiv und gültig) )
Aufpreis    = Aufpreis netto (je Kundengruppe, falls gepflegt, sonst allgemein) × (100 − Rabatt %) / 100
Brutto      = Netto × (1 + MwSt), auf Cent
```

- Der wirksame Rabatt je Artikel und Kundengruppe steht fertig in `tartikelkategorierabatt`. Diese Tabelle berechnet der Abgleich (dbeS) aus den Kategorie-Rabatten der Wawi.
- **Der Rabatt gilt auch für Variationsaufpreise.**
- **Sonderpreis und Rabatt werden nicht addiert.** Es gilt der niedrigere der beiden Preise (29 Artikel haben beides).
- **Mehrere Rabatt-Kategorien:** Es gilt der höchste Rabatt (14 Artikel liegen in Kategorien mit unterschiedlichen Sätzen).

**Nachgerechnet:**

| Artikel | Listenpreis netto | Rabatt Endkunden | Ergebnis brutto | Stripe-Shop | .NET-Shop heute |
|---|---|---|---|---|---|
| Keso 8000 Omega² 81.F16 (41166) | 299,02 € | 35 % | **231,29 €** | 231,29 € | 355,83 € |
| Traka21 (33835) | 1.928,57 € | 10 % | **2.065,50 €** | 2.065,50 € | 2.295,00 € |
| Sicherheitswinkelschließblech (7) | 24,92 € | 10 % | 26,69 € | – | 29,65 € |

**Rabatte je Kundengruppe im Shop:**

| Kundengruppe | Artikel mit Rabatt | Spanne |
|---|---|---|
| Endkunden (1) | 209 | 10 bis 35 % |
| Händler (2) | 212 | 10 bis 40 % |
| Gast (3) | 209 | 10 bis 35 % |
| Google lokal (7), Google Shopping (13) | je 121 | 35 % |
| Meesenburg GmbH & Co KG (17) | 129 | 35 bis 61 % |

## 2. Kundengruppen

| Id | Name | Standard | Netto-Preise |
|---|---|---|---|
| 1 | Endkunden | **ja** (gilt für Gäste) | nein |
| 2 | Händler | nein | ja |
| 3 | Gast | nein | nein |
| 7 | Google lokal | nein | nein |
| 13 | Google Shopping | nein | nein |
| 17 | Meesenburg GmbH & CoKG | nein | ja |

Ein Login im Shop ist für keine Gruppe Pflicht (`cShopLogin = N`).

## 3. Sortiment und Sichtbarkeit

- Im Shop: **6.279 Artikel**. Davon sind 2.866 einzelne Artikel, 20 Vaterartikel und 3.393 Variantenkinder.
- **Ausgeblendet über die Sichtbarkeit je Kundengruppe** (`tartikelsichtbarkeit`, kommt aus der Wawi):

  | Kundengruppe | ausgeblendete Artikel |
  |---|---|
  | Endkunden (1) | 1.041 |
  | Händler (2) | 1.042 |
  | Gast (3) | 1.041 |
  | Google lokal (7), Google Shopping (13) | je 2.936 |
  | Meesenburg GmbH & Co KG (17) | 45 |
- **502 Artikel ohne Preis** für Endkunden. Die Einstellung „Artikel mit Preis 0 anzeigen“ ist aus (`global_preis0 = N`).
- **Plugin `sd_products_hider_5` ist ausgeschaltet.** Ausgeblendet wird also nur über die Sichtbarkeit aus der Wawi.
- „Angebot anfordern“ (`netzdingeDE_angebot_anfordern`) ist aktiv, als Knopf im Warenkorb. Die Plugin-Tabelle enthält 2.017 Anfragen. Gelesen habe ich die Inhalte nicht.

## 4. Staffel- und Sonderpreise, Grundpreis

- **Staffelpreise** gibt es für wenige Artikel, rund 120 Staffelzeilen je Kundengruppe 1/2/3. Beispiel Artikel 141: Grundpreis, dann ab 5 Stück 34,49 € netto, ab 10 Stück 32,68 € netto.
- **Sonderpreise:** 107 insgesamt, davon 58 als aktiv markiert und 23 aktuell im Zeitraum. Keiner ist auf den Bestand begrenzt.
- **Grundpreis:** nur **3 Artikel** haben eine Grundpreis-Angabe (351, 32429, 72674; alle ml → je 1 l).
- **Aufpreise je Kundengruppe:** 7 Variationswerte haben für die Gruppen 1, 2 und 3 einen eigenen Aufpreis.

## 5. Versand (im Shop gepflegt, eigene Ids)

- Die Versandarten des Shops haben **eigene Ids** und eigene Preise. Sie sind nicht identisch mit den Wawi-Versandarten, die wir übernommen haben.
- **Liefergebiet:** Deutschland, außerdem EU (GLS, DHL International 16,90 €, frei ab 500 € bei GLS), Schweiz (32 €), weltweit (32 €) und Spedition EU (49,90 €).
- **Abholung Fachmarkt Flensburg:** 5,90 € bis 10 € Warenwert, darüber 0 €, über eine Staffel umgesetzt. Das deckt sich mit der Wawi.
- **Metabo-Stückgutspedition:** 0 € (wie Wawi).
- **DE-Paketversand:** 7,50 € (GLS/UPS) bzw. 8,50 € (DHL), frei ab 99 €.
- **MwSt. auf Versand:** höchster Steuersatz im Warenkorb (`bestellvorgang_versand_steuersatz = HS`).
- **Zuschläge:** keine Versandzuschläge und keine Aufpreise je Zahlart.

## 6. Zahlarten (aktiv)

Vorkasse/Überweisung (Gruppen 1, 2, 3, 7, 13), PayPal Checkout, PayPal Kreditkarte, Rechnungskauf mit Ratepay, Apple Pay, Google Pay (über PayPal Commerce), Kreditkarte, SEPA-Lastschrift, giropay, SOFORT, Paydirekt, iDEAL, eps, Maestro, Bluecode (über GiroCheckout), Barzahlung (bis 500 €), Nachnahme, Rechnung (nur Händler und Meesenburg), Lastschrift (nur Meesenburg).

Hinweise:
- Amazon Pay und easyCredit sind als Zahlart noch aktiv, ihre Plugins aber ausgeschaltet.
- giropay, SOFORT und Paydirekt gibt es am Markt nicht mehr.

## 7. Gutscheine, Gratisgeschenke, Mails, Widerruf

- **Gutscheine:** **10 aktive Standard-Kupons** im Shop (Werte bis 7, gültig bis Anfang November 2026). Das Gutscheinfeld wird im Warenkorb angezeigt. Das Plugin `jtl_vouchers` ist aus.
- **Gratisgeschenke:** aktiv. **8 Artikel** sind „Gratisgeschenk ab 200 €“ Warenwert, zum Beispiel Keso-Langschlüssel und ein USB-Stick. Je Bestellung gibt es eines, das ist JTL-Standard.
- **Mail-Absender:** `info@sicherheitstechnik-nord.de`, Name „Meesenburg GmbH | sicherheit.meesenburg,com“ (mit Tippfehler).
- **Widerruf:** Das Plugin `css_WithdrawalForm` (Widerrufsformular) ist aktiv.

## 7a. Konfiguratoren (27.10.2026)

- **Kleiner Konfigurator = JTL-Standard „Konfigurationsgruppen“:** 470 Gruppen, 3.167 Konfig-Artikel, an **4.039 Artikeln** (3.707 für Endkunden sichtbar). Häufigste Gruppen: „Schlüssel“ (2.340 Artikel, Mehrfachauswahl 1–100), Farbkappen, Schließfunktion, Zylinderlänge innen/außen (Pflicht, Auswahlliste).
- **Großer Konfigurator = Plugin `sd_schliessanlagen_konfigurator`** (aktiv). Fachlich die Referenz für Schließanlagen; der Stripe-Shop hat dafür eine eigene, modernere Oberfläche (Svelte), rechnet aber fehlerhaft (Bericht 02).
- **Folge für den .NET-Shop:** Er kennt die Konfigurationsgruppen noch nicht. 3.508 dieser Artikel sind dort heute ohne Konfiguration bestellbar → sperren bis zum Konfigurator (F-34, Plan S9a).

## 8. Anzeige

- Verfügbarkeit statt Bestandszahl. In Listen eine Ampel, Lieferstatus im Bestellvorgang.
- Rabatt und Sonderpreis werden auf der Artikelseite angezeigt.

## Folgen für den .NET-Shop

1. **Sichtbarkeit je Kundengruppe** übernehmen (Wawi-Tabelle der Artikelsichtbarkeit), sonst zeigt der .NET-Shop rund 1.000 Artikel, die Endkunden heute nicht sehen.
2. **Kategorie-Rabatt nach JTL-Regel** umsetzen, falls der Fachbereich bei F-01 Option A wählt. Die Regel und die Testfälle stehen oben. Gäste sind dann Kundengruppe 1.
3. **Versandarten:** Der JTL-Shop pflegt sie selbst. Wir sollten entscheiden, ob die Wawi-Versandarten (heute im .NET-Shop) oder die Shop-Konfiguration maßgeblich ist (Ids, EU/CH/Welt).
4. **Staffelpreise, Sonderpreise, Gratisgeschenke und Gutscheine** sind im JTL-Shop in Gebrauch, wenn auch in kleinem Umfang.

## Umsetzung im .NET-Shop (27.10.2026)

Die Regeln aus Abschnitt 1 bis 4 sind im .NET-Shop umgesetzt (Plan 14, Abschnitt 5a, P1 bis P6) und mit dem JTL-Shop abgeglichen:

- **Kategorie-Rabatt:** 205 von 205 Rabatt-Artikeln haben auf den Cent denselben Endpreis. 3 weitere haben in beiden Shops keinen Preis.
- **Gegenrichtung (28.10.2026):** 24 Artikel haben laut Wawi einen Kategorie-Rabatt, der JTL-Shop gewährt ihn aber nicht – seine Vorberechnung (`tartikelkategorierabatt`) wird nur beim Artikel-Abgleich gefüllt und ist für diese Artikel veraltet (z. B. 41001 Fenstergriff, Kategorie 538 mit 15 %, zuletzt abgeglichen 18.08.2025: JTL-Shop 107,21 €, .NET-Shop 91,13 €). Der .NET-Shop folgt der Wawi. Liste in `analyse/workshop/Wawi_Datenfehler.xlsx`.
- **Ausblendung:** In einer Stichprobe von 300 Artikeln, die der JTL-Shop für Endkunden ausblendet, sind alle 300 auch im .NET-Shop nicht abrufbar.
- **Zusätzlich gefunden:** Für 16 Artikel pflegt die Wawi einen **eigenen Grundpreis für die Kundengruppe** (z. B. 31625: 156,53 € statt 184,14 € netto). Der JTL-Shop nimmt diesen Preis, der .NET-Shop jetzt auch.
- **Rundung:** Der JTL-Shop rechnet mit 8 Nachkommastellen und rundet dann auf Cent. Der .NET-Shop macht es genauso (Artikel 7: 26,69 €).
- **Grundpreis (P7):** dieselben 3 Artikel, z. B. 351 Keso Pflegespray 56 ml: 248,21 € / 1 l.
- **Verfügbarkeit (P8):** 109 lagergeführte Artikel, 8 ausverkauft, wie im JTL-Shop. Die REST-API meldet die Lagerführung falsch (J-27), deshalb liest der .NET-Shop sie aus der Wawi-Datenbank. Abweichung nur bei 71529 (Wawi erlaubt Überverkauf, JTL-Shop nicht).
- **Gratisgeschenke (P10):** dieselben 8 Artikel ab 200 €, Auswahl im Warenkorb, eines je Bestellung, Menge fest.
- **Datenstand des JTL-Shops:** aktuell. Letzter Abgleich mit der Wawi am 27.10.2026, 15:39. Alte Preiszeilen ohne Artikel (z. B. 141) sind Reste und kein Maßstab.

