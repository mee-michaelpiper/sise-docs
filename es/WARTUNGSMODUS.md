# Wartungsmodus SiSe Portal (es-web)

Wartungsseite oder Hinweisleiste **ohne Neustart** einschalten, z. B. wenn die Datenbank gestört ist.
Die Seite hängt bewusst nicht an Mongo, Entra oder statischen Dateien. Sie funktioniert also auch, wenn diese ausfallen.

| Wert (ConfigMap-Schlüssel) | Wirkung |
|---|---|
| `Maintenance__Enabled` | `true` = alle Seiten zeigen die Wartungsseite (HTTP 503), `/alive` und `/health` laufen weiter |
| `Maintenance__Message` | Text auf der Wartungsseite, leer = Standardtext. Offene Sitzungen sehen ihn als rote Leiste |
| `Maintenance__Until` | Freitext zur Dauer, z. B. `voraussichtlich bis 14:00 Uhr` |
| `Maintenance__Notice` | Gelbe Hinweisleiste, während das Portal nutzbar bleibt (Vorankündigung). Leer = keine Leiste |

Die Wartungsseite lädt sich jede Minute neu. Nach dem Ausschalten sehen die Benutzer das Portal also von selbst wieder.
Bis eine Änderung wirkt, dauert es bis zu etwa 1,5 Minuten: Kubernetes verteilt die ConfigMap verzögert, danach erkennt die App die Änderung in wenigen Sekunden.

Vorschau: [wartungsseite-vorschau.html](wartungsseite-vorschau.html)

## Einmalig einrichten

ConfigMap anlegen (Wartung aus):

```bash
kubectl --context aks-meesenburg-prod -n mee-eserp create configmap es-web-maintenance --from-literal=Maintenance__Enabled=false --from-literal=Maintenance__Message= --from-literal=Maintenance__Until= --from-literal=Maintenance__Notice=
```

Als Ordner in es-web einbinden. Das ist ein einmaliger Neustart. Der Patch ist strategisch, ergänzt also nur und überschreibt keine anderen Volumes. Kein `subPath`, sonst kommen Änderungen nicht im Pod an:

```bash
kubectl --context aks-meesenburg-prod -n mee-eserp patch deployment es-web --type=strategic -p '{"spec":{"template":{"spec":{"volumes":[{"name":"maintenance","configMap":{"name":"es-web-maintenance","optional":true}}],"containers":[{"name":"es-web","volumeMounts":[{"name":"maintenance","mountPath":"/etc/sise-portal/maintenance","readOnly":true}]}]}}}}'
```

## Wartung einschalten

```bash
kubectl --context aks-meesenburg-prod -n mee-eserp patch configmap es-web-maintenance --type merge -p '{"data":{"Maintenance__Enabled":"true","Maintenance__Message":"Der Datenbankserver ist gestört. Wir arbeiten daran.","Maintenance__Until":"voraussichtlich bis 14:00 Uhr"}}'
```

## Wartung ausschalten

```bash
kubectl --context aks-meesenburg-prod -n mee-eserp patch configmap es-web-maintenance --type merge -p '{"data":{"Maintenance__Enabled":"false","Maintenance__Message":"","Maintenance__Until":""}}'
```

## Vorankündigung (Portal bleibt nutzbar)

```bash
kubectl --context aks-meesenburg-prod -n mee-eserp patch configmap es-web-maintenance --type merge -p '{"data":{"Maintenance__Notice":"Heute ab 18:00 Uhr ist das Portal wegen Wartung etwa 30 Minuten nicht erreichbar."}}'
```

Entfernen: `"Maintenance__Notice":""`.

## Lokal

In `appsettings.Development.json` den Abschnitt `"Maintenance": { "Enabled": true, "Message": "…" }` setzen. appsettings werden ebenfalls ohne Neustart neu geladen.
Mit `Maintenance:Directory` lässt sich auch lokal ein Ordner wie im AKS nutzen (je Wert eine Datei).

Code: `Meesenburg.ES.Web/Maintenance/` (Optionen, Middleware mit der Seite), Leiste `Components/Layout/MaintenanceBanner.razor`.
