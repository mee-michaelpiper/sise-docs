# 23 AKS-Kapazität für den produktiven .NET-Shop

Stand 17.11.2026. Frage: Um wie viel muss das AKS wachsen, wenn der .NET-Shop der produktive Shop wird und der Shop-Admin ins SiSe-Portal wandert (Bericht 22, §3)?

Grundlage: `kubectl get nodes`, `kubectl top nodes` und `kubectl top pods -A` am 17.11.2026 gegen 11:20, nur lesend. `az aks` liefert mit meinem Zugang keine Abo-Daten (Pool-Einstellungen, Ausgangs-Typ). Diese Werte fehlen deshalb und sind unten als offen markiert. Preise sind Listenpreise (Pay-as-you-go, westeurope, Linux) aus dem Gedächtnis, gerundet. Vor der Bestellung mit dem Azure-Preisrechner bzw. dem eigenen Vertrag prüfen.

## 1. Heute

3 Knoten `Standard_D2ds_v5` (2 vCPU, 8 GB, nutzbar je 5,65 GiB und 1,9 vCPU), ein Pool `agentpool`.

| Knoten | CPU | Speicher |
|---|---|---|
| …000000 | 12 % | 82 % (4,6 GiB) |
| …00000u | 10 % | **103 %** (5,9 GiB) |
| …00000v | 10 % | 51 % (2,9 GiB) |

- Wie am 04.11. (Bericht 20, §7): CPU fast frei, Speicher knapp. Ein Knoten liegt über 100 %.
- Größte Verbraucher: `es-api-sapdata` 2 × 1,2–1,3 GiB, `aspire-dashboard-eserp` 1,1 GiB, `es-dataworker` 0,7 GiB, `es-web` 2 × 0,5 GiB. Dazu `default/backend-api-identity` 0,5 GiB (gehört nicht zu ES, Herkunft klären).
- Ingress: **ein** ingress-nginx-Pod für alles. Fällt er aus oder wird er neu gestartet, sind Portal und Shop gemeinsam weg.

**Folge:** In den heutigen Pool passt der Shop nicht. Er braucht einen eigenen Knotenpool (wie in Bericht 22, V-1).

## 2. Bedarf des Shops in Produktion

Die Dienste wie im Repo: öffentlicher Shop (`Meesenburg.Shop.Portal`), Shop-API, Worker. Mongo läuft im Rechenzentrum (Bilder in GridFS), braucht also keinen Speicher im AKS. Mailpit gibt es in Produktion nicht.

| Dienst | Pods | Request Speicher | Limit Speicher | Request CPU | Begründung |
|---|---|---|---|---|---|
| Shop (öffentlich, Blazor) | 2 | 512 MiB | 1 GiB | 250 m | Zwei Pods für Ausfallsicherheit und Updates ohne Unterbrechung. Statische Seiten, interaktiv nur Warenkorb/Kasse/Konfigurator/Konto |
| Shop-API | 2 | 512 MiB | 1 GiB | 250 m | Katalog-Abfragen, Preise, Bestellungen. Spitzen beim Umrechnen der Bilder (WebP), danach aus GridFS |
| Worker | 1 | 384 MiB | 768 MiB | 100 m | Abgleich mit der Wawi, Outbox, Bild-Vorladen. Ein Pod reicht, die Outbox wiederholt |
| **Summe** | 5 | **≈ 2,4 GiB** | ≈ 4,8 GiB | ≈ 1,1 vCPU | Lokal im Leerlauf 80–110 MiB je Dienst; die Requests sind bewusst großzügig, damit der Scheduler nicht wie bei ES überbucht |

Zusätzlich im selben Pool, solange es ihn gibt: die **Testversion** aus Bericht 22 (je 1 Pod, ≈ 1,2 GiB).

**Shop-Admin im SiSe-Portal:** läuft in den bestehenden `es-web`-Pods mit (RCL bzw. direkt im Portal, E-T2). Schätzung: + 100–200 MiB je `es-web`-Pod, also + 0,2–0,4 GiB im ES-Pool. Das ist klein, kommt aber zu einem Pool hinzu, der heute schon über 100 % liegt (siehe §4).

## 3. Empfehlung und Kosten

**Eigener Knotenpool `shop` mit 2 × `Standard_D2ds_v5`**, Taint `workload=siseshop`, möglichst auf zwei Zonen verteilt.

- Verfügbar: 2 × 5,65 GiB = 11,3 GiB, belegt: ≈ 2,4 GiB Produktion + 1,2 GiB Test = 3,6 GiB. Fällt ein Knoten aus, trägt der andere alle Produktions-Pods allein.
- 1 × `D4ds_v5` kostet etwa gleich viel, hat aber keine Ausfallsicherheit (Knoten-Update = Shop offline). Für einen produktiven Shop nicht empfohlen.
- Autoscaler mit min 2 / max 3 ist möglich, nötig nur bei Kampagnen-Last.

| Posten | Monatlich (Liste, grob) | Bemerkung |
|---|---|---|
| 2 × D2ds_v5 | ≈ 190–210 € | ≈ 95–105 € je Knoten |
| OS-Datenträger 2 × 128 GB (falls nicht ephemeral) | ≈ 20–40 € | Abhängig vom Pool-Typ, offen |
| Feste Ausgangs-IP für ecomdata (V-2) | ≈ 5–40 € | 5 € für eine öffentliche IP am Load Balancer; ≈ 35–40 € plus Traffic, falls ein NAT-Gateway nötig ist. Ausgangs-Typ des Clusters offen |
| Zweiter ingress-nginx-Pod | 0 € | Passt in die bestehenden Knoten, aber erst nach §4 |
| **Summe** | **≈ 215–290 € / Monat** | ≈ 2.600–3.500 € / Jahr. Mit 1 Jahr Reserved Instance auf die Knoten etwa 30–40 % weniger |

Dem stehen Einsparungen gegenüber, sobald der Stripe-Shop und der JTL-Shop abgelöst sind: Hetzner node1 (und node2/node3, falls nicht mehr gebraucht) sowie Timme. Die Beträge dafür liegen mir nicht vor.

## 4. Unabhängig vom Shop: ES-Pool entlasten

Der ES-Pool ist schon heute zu voll. Vor dem Admin-Umzug (§2) eine dieser Maßnahmen, Reihenfolge nach Nutzen:
1. `aspire-dashboard-eserp` in Produktion abschalten oder auf Abruf (spart 1,1 GiB). Logs liegen ohnehin in Mongo.
2. Requests realistisch setzen (Bericht 20: Limits bis 697 % überbucht), damit der Scheduler nicht einen Knoten über 100 % packt.
3. `default/backend-api-identity` klären (0,5 GiB, kein ES).
4. Erst wenn das nicht reicht: 4. Knoten im ES-Pool (≈ 100 € / Monat).

## 5. Risiken, die keine Knotenfrage sind

| # | Thema | Warum wichtig |
|---|---|---|
| R-1 | Shop hängt am VPN zum Rechenzentrum (Mongo) | Heute (16./17.11.) gab es mehrere VPN-Ausfälle. Für ES ist das ärgerlich, für einen öffentlichen Shop heißt es: Shop offline. Optionen: Mongo-Replika in Azure, Atlas/Cosmos für den Shop, oder Ausfallsicherheit des VPN durch die IT bestätigen lassen |
| R-2 | Ein Ingress-Pod für Portal und Shop | Zweiten Replica plus PodDisruptionBudget. Optional eigener Ingress für den Shop |
| R-3 | Feste Ausgangs-IP für ecomdata (V-2) | Ohne sie keine Preise, kein Bestand. Weiterhin der Engpass |
| R-4 | Deploy aus der Arbeitskopie (Bericht 22, §1) | Für einen Shop mit Zahlungen nicht tragbar. T-3 (Release nur aus Git-Tag) vor dem Livegang |

## 6. Offen (braucht IT bzw. einen Zugang mit Abo-Rechten)
- Pool-Einstellungen (Autoscaler, Zonen, OS-Datenträger) und Ausgangs-Typ des Clusters (`az aks show`).
- Ob es Reservierungen oder einen Rahmenvertrag mit anderen Preisen gibt.
- Erwartete Besucherzahlen bzw. Kampagnen (bei Spitzen den Autoscaler auf max 3–4).

## 7. Nachtrag 17.11.2026 (nach Rückfragen M. Piper)

- **Aspire-Dashboard abgeschaltet** (`kubectl scale … --replicas=0`, auf Wunsch). Knoten …00000u danach 91 % statt 103–109 %. Zurück mit `--replicas=1`. Das Dashboard war über den OAuth-Pod geschützt.
- **Pool-Einstellungen** (Abo `sub-kubernetes-prod-sva`): `agentpool` ist System-Pool, Autoscaler **3–5**, Zonen 1–3, Managed-OS-Disk 128 GB. Der Autoscaler skaliert nach **Requests**, nicht nach Verbrauch. Weil die Requests in ES zu niedrig sind (30–43 %), greift er trotz > 100 % Verbrauch nie.
- **Ausgangs-IP:** `outboundType = loadBalancer`, eine statische Standard-IP **20.31.106.131**. Das ist die Ausgangs-IP aller Pods im Cluster, auch des Shops → bei ecomdata für REST 50443 und SQL 50000 freigeben (V-2). Die IP gehört zur AKS-verwalteten Knoten-Ressourcengruppe; sie bleibt, solange niemand die Ausgangskonfiguration ändert. Robuster: IT legt eine eigene IP an und hängt sie als `outboundIPs` an den Cluster.
- **`backend-api-identity`** (default, 0,5 GiB) ist eine On-Prem-Identity-API von M. Piper und bleibt.
- **Mongo bleibt im Rechenzentrum** (VPN laut M. Piper stabil). R-1 entsprechend herabgestuft.
- **Redis für Artikeldaten?** Vorschlag: nein, stattdessen Katalog im Speicher je Pod mit „letztem Stand“ (siehe Antwort im Chat vom 17.11.).
- **Speicheroptimierte Knoten:** E-Serie (`E2ds_v5`, 2 vCPU, 16 GB) bei gleicher CPU. Siehe Abwägung im Chat vom 17.11.

## 8. Umgesetzt und entschieden (17.11.2026, nachmittags)

- **Ingress hochverfügbar (R-2 erledigt):** `helm upgrade ingress-nginx` (Chart 4.12.1 wie bisher, `--reuse-values`) mit `controller.replicaCount=2`, `controller.minAvailable=1` (PDB) und `topologySpreadConstraints` je Knoten. Probelauf vorher: nur PDB, Replicas, Verteilung geändert. Pods laufen auf …000000 und …00000v, Portal danach 200. Helm-Revision 6; zurück mit `helm rollback ingress-nginx 5 -n ingress`.
- **Reservierungen:** `az reservations reservation-order list` liefert für M. Piper nichts. Reservierungen sieht nur, wer Rechte auf die Reservierung bzw. die Abrechnung hat → im Azure-Portal unter „Reservierungen“ bzw. „Sparpläne“ mit Abrechnungsrechten prüfen.
- **Knotenpools (Vorschlag M. Piper):** `agentpool` (D2ds_v5, System) für System-Pods, `default` und den Shop; neuer Pool mit `E2ds_v5` für ES (nodeSelector in den ES-Deployments). E2ds stellt M. Piper bereit.

## 9. Knotentyp und Besucherzahlen (17.11.2026)

**Preise** (öffentliche Azure-Preisliste, westeurope, Linux, Pay-as-you-go, abgerufen 17.11.2026; ×730 h):

| Typ | vCPU | RAM | lokale Platte | €/Monat |
|---|---|---|---|---|
| D2ds_v5 (heute) | 2 | 8 GB | ja | ≈ 89 |
| E2ds_v5 | 2 | 16 GB | ja | ≈ 113 |
| E2ads_v7 (AMD) | 2 | 16 GB | ja (NVMe) | ≈ 115 |
| **E2as_v7 (AMD)** | 2 | 16 GB | nein | **≈ 94** |

- `az vm list-skus` (Abo sub-kubernetes-prod-sva): E2ads_v7 und E2as_v7 in westeurope in allen 3 Zonen ohne Einschränkung; nur Gen2, NVMe-Controller. Ephemerer OS-Datenträger nur mit „d“.
- **Lokaler Speicher wird nicht gebraucht:** Kein Pod außerhalb von kube-system nutzt emptyDir oder PVC; nur akv2k8s liest per hostPath die Azure-Konfiguration. Ohne „d“ braucht der Knoten einen verwalteten OS-Datenträger (64–128 GB, ≈ 5–18 €).
- **Offen:** ob AKS die v7-Größen schon als Knotenpool annimmt (NVMe/Gen2) → beim Anlegen prüfen, sonst E2as_v6/E2ads_v6.

**Besucherzahlen** (M. Piper): ≈ 15.000/Tag, 105.000/Woche, 450.000/Monat, Spitze 50 gleichzeitig. Das bestätigt §2: 50 gleichzeitige Besucher sind für 2 Shop-Pods wenig (interaktive Verbindungen nur in Warenkorb/Kasse/Konfigurator/Konto). Neu dazu: **ausgehender Traffic** (Internet-Egress, die ersten 100 GB frei, danach grob 0,08 €/GB): bei 1–2 MB je Besuch 450–900 GB/Monat ≈ 30–65 €/Monat, unabhängig davon, ob Bilder aus AKS, Blob oder CDN kommen.

**Bilder in Blob Storage** (geplant: TODO, Bericht 14 V8/E-E, Bericht 21 A-10): Speicher und Abrufe kosten wenige Euro. Vorteil: Bilder laufen nicht mehr über VPN/GridFS und nicht durch die API-Pods; WebP-Größen einmal erzeugen und dort ablegen. Ein CDN/Front Door (≈ 30 €/Monat Grundgebühr) lohnt bei 50 Gleichzeitigen noch nicht, die Bilder haben bereits `Cache-Control: public, max-age=604800`.

## 10. Passt der Shop auf den D2-Pool? (17.11.2026, nach dem ES-Umzug)

ES läuft seit 17.11. auf `agentpool2` (E2as_v7). Auf `agentpool` (3 × D2ds_v5, System) bleiben System-Pods, ingress, cert-manager, external-dns, akv2k8s und `default` (Identity-API, Identity-Portal, ai-policy).

| | Verbrauch | Requests |
|---|---|---|
| agentpool heute (3 Knoten, 17 GiB nutzbar) | ≈ 7 GiB (31–50 % je Knoten) | ≈ 4,4 GiB (21–29 %) |
| + Shop Produktion | ≈ 1–1,5 GiB (Schätzung) | 2,4 GiB |
| + Shop Test | ≈ 0,6 GiB | 1,2 GiB |
| **Summe** | **≈ 9 GiB (≈ 55 %)** | **≈ 8 GiB (≈ 47 %)** |

→ **Der Shop passt auf den D2-Pool.** Empfehlung: dort lassen, nicht auf den E2-Pool. Grund: Trennung von ES (sapdata darf bis 3 GiB, der Worker hat Spitzen); fällt ES-seitig Speicher knapp, trifft es den öffentlichen Shop nicht. Der Autoscaler (3–5) greift, weil die Shop-Pods Requests haben.

**Speicherverhalten von ASP.NET/Blazor Server:**
- .NET 9/10 nutzt bei Server-GC standardmäßig DATAS (passt die Heap-Größe der Last an und gibt Speicher zurück). Ohne Container-Limit orientiert sich die GC am Knoten (8 GB) und räumt spät auf → deshalb **immer ein Limit setzen** (1 GiB); die GC zielt dann auf ≈ 75 % davon.
- Bei Bedarf zusätzlich `DOTNET_GCConserveMemory=5`; für den Worker reicht Workstation-GC (`DOTNET_gcServer=0`).
- Blazor-Server-Verbindungen kosten je nach Seite grob 0,1–1 MB; bei 50 gleichzeitigen sind das < 50 MB. Interaktiv sind im Shop nur Warenkorb, Kasse, Konfigurator und Konto.
- Prüfen statt schätzen: in der Testversion (eigener Namespace) mit Limit laufen lassen und einen Lasttest mit 50–100 gleichzeitigen Besuchern fahren, dabei `kubectl top` beobachten.
