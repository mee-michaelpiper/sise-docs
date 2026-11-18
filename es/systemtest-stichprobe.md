# Systemtest – feste Stichprobe

Gezogen am 14.10.2026 aus **MEP** (nur lesend, `~/repos/sap-tools/SapRead`), Zufall mit festem Startwert 20261014. Vorbelegt in `Meesenburg.ES.Web/appsettings.Development.json` → `SystemTest:*`, genutzt von `/dev/systemtest`.

| Liste | Auswahl |
|---|---|
| 50 Kunden | `YDD_CUSTOMERS_V2`, ORGANISATIONID 298 (Grundlage des Kundenabgleichs), nur übertragbare nach `SapCustomerExtensions.GetInvalidReason` (nicht VK-Büro 2900/7900/leer, Nummer < 19.999.999). 44 aktive (Status leer/A, Kontengruppe GBMD), je Kombination Verkaufsbüro × Rechnungsweg mindestens einer (11 Büros, Rechnungsweg 0/E/Z/4/X, 12 Zahlungsbedingungen), dazu 6 mit Status L/K (inaktiv/gesperrt im esERP) |
| 50 Artikel | `MVKE` VKORG 298, PRAT8 = X (ES-freigegeben, wie im Artikelabgleich), ohne 800er/500er (`EsArticleNumbers.IsExcludedFromSync`) |
| 50 Lieferanten | `YDH_SD_ES_LIEFER`, BUKRS 298, nicht gesperrt (wie im Lieferantenabgleich), nur numerische Nummern |

Korrigiert 14.10.2026 nach dem ersten Lauf: 7 Kunden (VK-Büro 2900/7900/leer), 3 Artikel (800er) und die Lieferantenliste (Lesefehler, z. B. „30469“ aus `K0130469`) ersetzt.

Wenn ein Datensatz in SAP wegfällt (gelöscht, nicht mehr freigegeben), meldet der Systemtest ihn als „nicht gefunden“ bzw. „nicht freigegeben“ – dann hier und in den appsettings ersetzen.

## Kunden

| Kunde | Status | VK-Büro | Rechnungsweg | ZTERM |
|---|---|---|---|---|
| 234523 | A | 4900 | E | A018 |
| 123420 | A | 1900 | E | A011 |
| 14511 | A | 1900 | 0 | A125 |
| 10053155 | – | 4900 | 0 | A024 |
| 261489 | A | 8900 | E | A018 |
| 10095426 | – | 8900 | 0 | A014 |
| 10074930 | – | 5900 | 0 | A014 |
| 10054897 | – | 3800 | E | A014 |
| 102875 | – | 7910 | 0 | A014 |
| 10059951 | – | 3800 | 0 | A014 |
| 10051477 | – | 3900 | 0 | A014 |
| 10100023 | – | 4900 | Z | A014 |
| 98310 | A | 1900 | Z | A249 |
| 150676 | A | 1900 | 4 | A404 |
| 208329 | A | 7910 | E | A024 |
| 10048464 | – | 5900 | E | A014 |
| 10056187 | – | 7910 | Z | A014 |
| 113232 | – | 8900 | Z | A024 |
| 10048306 | – | 5900 | Z | A014 |
| 19513 | – | 4900 | 4 | A404 |
| 10052433 | – | 3900 | E | A014 |
| 10099937 | – | 2950 | Z | A014 |
| 10076583 | – | 3800 | Z | A014 |
| 10046063 | – | 5900 | X | A014 |
| 5797 | A | 4900 | E | A129 |
| 10014252 | – | 1900 | E | A014 |
| 10047311 | – | 5900 | 0 | A269 |
| 10057594 | – | 3800 | 0 | A014 |
| 10051426 | – | 1900 | 0 | A014 |
| 80609 | A | 1900 | 0 | A249 |
| 77981 | A | 4900 | 0 | A018 |
| 249277 | A | 4900 | E | A018 |
| 76360 | A | 1900 | E | A018 |
| 10025091 | – | 1900 | E | A014 |
| 2819 | A | 1900 | 0 | A125 |
| 66735 | A | 4900 | 0 | A129 |
| 10069558 | – | 5900 | 0 | A014 |
| 10081775 | L | 2950 | E | A129 |
| 10080865 | L | 2950 | 0 | A014 |
| 10079545 | L | 2950 | 0 | A151 |
| 10080410 | L | 2950 | E | A014 |
| 261240 | K | 4900 | E | A018 |
| 231113 | K | 1900 | E | A018 |
| 10022580 | – | 8900 | 0 | A014 |
| 216958 | A | 4900 | E | A018 |
| 10053477 | – | 4900 | 0 | A014 |
| 235543 | A | 4900 | E | A018 |
| 265066 | A | 8900 | E | A018 |
| 217171 | A | 4900 | E | A018 |
| 12072 | A | 1900 | E | A018 |

## Artikel

7000418917, 9050327, 224639, 9050455, 310734, 225418, 393306, 136559, 9209150, 9253066, 129617, 117730, 6390, 261230, 9050401, 9230510, 9131971, 265246, 9050260, 129704, 432853, 116335, 293003, 9277411, 9127560, 169525, 402154, 142146, 350178, 403401, 9253038, 407972, 9050445, 116576, 402155, 325122, 265206, 9306167, 9350332, 416105, 265179, 401067, 9050344, 9230186, 431452, 9203400, 325153, 9053271, 9050385, 9271102

## Lieferanten

4312473, 217824, 4317055, 4314895, 4310949, 4315085, 4314822, 29818, 258461, 13407, 97619, 4315261, 98059, 4311017, 92929, 4316420, 7629, 4856, 76937, 4314086, 4311047, 2760264, 4314522, 4317441, 242822, 154662, 97875, 4311753, 248473, 191701, 4313444, 180290, 107184, 124522, 4313040, 3385, 4313730, 198869, 4314868, 4313424, 167951, 208505, 213417, 3145, 4312745, 12979, 4313177, 9671, 209301, 12995
