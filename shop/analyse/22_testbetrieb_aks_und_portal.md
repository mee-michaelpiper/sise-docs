# 22 Testbetrieb im AKS und Shop-Admin im SiSe-Portal

Stand 17.11.2026. Entscheidung: Wir machen mit dem .NET-Shop weiter. Als Nächstes kommt eine Testversion ins AKS, die das SN24-Team nutzt. Der Shop-Admin wandert bereinigt ins SiSe-Portal.

Grundlage: Meesenburg.ES und PPR.DevTools, beide nur gelesen. Am Cluster habe ich nichts abgefragt und nichts geändert.

## 1. Was wir vorfinden

| Thema | Befund | Folge für uns |
|---|---|---|
| Cluster | Es gibt nur einen Cluster: `aks-meesenburg-prod` (westeurope). ES läuft im Namespace `mee-eserp`. Einen Test-Cluster gibt es nicht. | Die Testversion läuft im Produktiv-Cluster, in einem eigenen Namespace. |
| Speicher | Die Knoten sind beim Speicher voll (Bericht 20, Abschnitt 7: bis 99 %). | Ein eigener Knotenpool ist Voraussetzung. Sonst gefährdet der Shop ES. |
| Deploy-Weg | Es gibt keine Pipeline. `PPR.DevTools/scripts/release.sh` baut mit `az acr build` aus der **lokalen Arbeitskopie** und startet danach per `kubectl rollout restart` neu. | Alles, was im ES-Ordner liegt, geht beim nächsten ES-Release mit, auch Unfertiges (siehe Abschnitt 4). |
| ES-Repo | Es gibt nur `master`, letzter Commit April 2025. 928 Änderungen sind nicht committet, `Meesenburg.ES.Authorization` und `Navigation/` werden gar nicht versioniert. | Es gibt keinen sauberen Stand, auf den man zurückgehen könnte. Vor jeder Änderung brauchen wir einen Sicherungsstand. |
| Ingress | ingress-nginx mit öffentlichem LoadBalancer. TLS endet am Ingress. | Ohne Zusatzmaßnahmen ist alles im Cluster aus dem Internet erreichbar. |
| Portal | `portal.meesenburg-service.de`, öffentlich erreichbar, geschützt durch die Entra-Anmeldung. Es gibt kein robots.txt und kein noindex. | Der Admin im Portal ist so gut geschützt wie das Portal selbst. |
| Secrets | Das K8s-Secret `es-keyvault-app` greift auf Key Vault `kv-meesenburg-prod` zu. Alle Dienste nutzen dieselbe App-Identität. | Für den Shop eine eigene Identität mit Zugriff nur auf `SiseShop-*`-Secrets (Least Privilege). |
| Rechte | `Meesenburg.ES.Authorization` ist ein NuGet-Paket (1.0.15) aus dem Feed `Meesenburg.ES`. Rechte stehen in `Permissions.cs` mit Katalog-Eintrag. Seiten nutzen `[AuthorizeMee]`. Es gibt keine FallbackPolicy. | Shop-Rechte gehören ins Paket. Unser `Meesenburg.Authorization` ist schon gleich gebaut (Bericht 20, Abschnitt 10). |
| Portal-Aufbau | Blazor Server, MudBlazor 9.12. Das Menü kommt aus `Navigation/PortalTools.cs` (Bereiche `PortalArea`). Es gibt keinen Modul-Mechanismus: Der Router kennt nur die eigene Assembly. | Für einen Admin aus einem Paket sind kleine Eingriffe nötig (`AdditionalAssemblies`, `PagePermissions`). |
| Zertifikat | Das Wildcard-Zertifikat für `api.meesenburg.io` läuft am **19.11.2026** ab. Ein neues liegt laut ES-TODO seit 11.11. vor. | Betrifft nicht den Shop, aber dringend für ES. |

Unser Stand dazu: Die Dockerfiles für Api, Portal und Worker gibt es. Robots.txt, Sitemap und noindex für Suchseiten sind gebaut. Der Wartungsmodus ist da. Der Admin hat 25 Seiten und Bausteine mit rund 3.300 Zeilen. Wir nutzen MudBlazor 9.9, das Portal 9.12, also **heben wir unsere Version an, nicht die des Portals**.

## 2. Testversion im AKS: Wie wir sie absichern

**Frage: Lässt sich verhindern, dass man den Shop von außen erreicht?** Ja, auf drei Stufen. Ich empfehle Stufe 1, und wenn das nicht geht, Stufe 2. Stufe 3 gilt immer zusätzlich.

| Stufe | Wie | Wer kommt dran | Aufwand | Abhängig von |
|---|---|---|---|---|
| 1 Nur intern (empfohlen) | Zweiter Ingress mit **internem** LoadBalancer (private IP im AKS-Netz), Name nur im internen DNS | Nur aus dem Firmennetz und per VPN. Aus dem Internet gibt es keinen Weg | 1 PT + IT | IT: Ist das AKS-Netz vom Firmennetz und von Flensburg aus erreichbar (das AKS erreicht On-Prem-Mongo schon über VPN)? Interne DNS-Zone |
| 2 Öffentlich mit IP-Liste | Ingress auf dem bestehenden Controller mit `whitelist-source-range` auf die festen Ausgangs-IPs (Zentrale, Flensburg-Proxy) | Nur aus diesen Netzen. Alle anderen bekommen 403 | 0,5 PT | Feste Ausgangs-IPs von der IT. Wichtig: Der Controller muss die echte Client-IP sehen. Heute steht er auf `externalTrafficPolicy: Cluster`, dann sieht er nur Knoten-IPs. Das muss vorher geprüft werden, da es den gemeinsamen Controller betrifft |
| 3 Anmeldung vor dem ganzen Shop (immer) | Im Testbetrieb verlangt der Shop **auf jeder Seite** eine Entra-Anmeldung, bevor er etwas zeigt (Schalter `SiseShop:TestGate`). Die Anmeldung ist schon gebaut. Erlaubt sind nur die Gruppe `grp_mee_sise_shop_test` und die Entwickler | Nur angemeldete Mitarbeiter in der Gruppe | 1 PT | App-Registrierung des Shops (steht ohnehin aus) |

Dazu kommen immer diese Schutzmaßnahmen gegen Suchmaschinen und Bots:
- `robots.txt` im Testbetrieb: `Disallow: /` und keine Sitemap.
- Header `X-Robots-Tag: noindex, nofollow` auf **jeder** Antwort. Er greift auch für Bilder und PDFs.
- Hinter der Anmeldung aus Stufe 3 sieht ein Bot ohnehin nur die Entra-Anmeldeseite. Selbst wenn jemand die Adresse findet, gibt es nichts zu indizieren.
- Der Name der Test-Adresse kommt nirgends vor, der keine Anmeldung verlangt: keine Links, keine Mails nach außen.

**Weitere Schutzmaßnahmen im Testbetrieb:**
- **Wawi:** Die Schreibsperre bleibt an. Die Übertragung an die Wawi ist aus.
- **Stripe:** nur im Testmodus mit Test-Schlüsseln. Der Webhook bleibt im Testbetrieb aus, die Zahlung bestätigt die Seite nach der Rückleitung. Soll er doch laufen, gilt für genau den Pfad `/api/v1/payments/webhook` eine Ausnahme von Stufe 1/2, gesichert durch die Stripe-Signatur.
- **Mails:** nur an `@meesenburg.de` (gibt es schon), sonst Mailpit im Namespace.
- **Mongo:** eigene Datenbank `Dev-SiseShop` (Schalter E-P gibt es). Die ES-Datenbanken werden nicht berührt.

**Voraussetzungen, die nur IT bzw. ecomdata erfüllen können:**

| # | Was | Wer | Warum |
|---|---|---|---|
| V-1 | Eigener Knotenpool (z. B. 1 × D4ds_v5 oder 2 × D2ds_v5, mit Taint `workload=siseshop`) | IT | Die Knoten sind beim Speicher voll. Ohne eigenen Pool verdrängt der Shop ES |
| V-2 | **Feste Ausgangs-IP des AKS bei ecomdata freischalten** (REST 50443, SQL 50000) | IT + ecomdata | Die Wawi lässt heute nur node1 durch. Ohne Freigabe gibt es im Test keine Preise, keinen Bestand und keine Prüfungen. **Größter Engpass.** Notlösung: Tunnel-Pod über node1, das sehe ich aber nur als Brücke für ein paar Tage |
| V-3 | Entscheidung Stufe 1 oder 2. Bei Stufe 1: interner LoadBalancer und interne DNS-Zone. Bei Stufe 2: feste Ausgangs-IPs | IT | Siehe oben |
| V-4 | Entra-App-Registrierung „SiSe-Shop (Test)“ mit Redirect auf die Test-Adresse, Gruppe `grp_mee_sise_shop_test` mit dem SN24-Team | IT | Stufe 3 |
| V-5 | Eigene Key-Vault-Identität für den Shop, nur Lesen auf `SiseShop-*` | IT | Nicht die gemeinsame App von ES mitbenutzen |
| V-6 | DNS-Name, z. B. `shop-test.meesenburg-service.de` (bei Stufe 1 intern), TLS-Zertifikat | IT | |

**Was ich baue** (ohne einen Eingriff ins ES-Projekt):

| # | Was | PT |
|---|---|---|
| T-1 | Testbetrieb-Schalter: Anmeldung vor dem ganzen Shop, `X-Robots-Tag`, robots `Disallow`, Kennzeichnung „Testversion“ im Kopf, Testkonten-Schnellanmeldung bleibt aus | 1–1,5 |
| T-2 | K8s-Manifeste im eigenen Repo (`deploy/aks-test/`): Namespace `mee-siseshop-test`, Deployments Api/Portal/Worker, Services, Ingress (Stufe 1 oder 2), NetworkPolicy (nur Ingress → Portal → Api), Requests/Limits, Probes, nodeSelector/Toleration auf den Shop-Pool, Sticky Sessions für Blazor | 1,5–2 |
| T-3 | Release-Skript nach dem Muster von PPR.DevTools, aber als eigenes Ziel und **nur aus einem sauberen Git-Stand mit Tag** (kein Build aus der Arbeitskopie). Image-Tags mit Version statt `latest` | 1 |
| T-4 | Verteilter Cache für Login-Tickets und DataProtection-Schlüssel in Mongo (heute im Speicher, bei 2 Pods würden Anmeldungen verloren gehen) | 1 |
| T-5 | Erstbefüllung: Abgleich aus der Wawi, Inhalte, Weiterleitungen. Danach Rundgang mit dem SN24-Team | 1 |
| | **Summe** | **5,5–6,5 PT** |

Der Rollout selbst (Namespace anlegen, `kubectl apply`) ist eine Änderung am Produktiv-Cluster und läuft nur nach ausdrücklichem OK.

## 3. Shop-Admin ins SiSe-Portal: Wie wir weiterentwickeln, ohne ES zu gefährden

### 3.1 Bereinigen (vor dem Umzug)

Was nicht ins Portal geht:
- POC-Werkzeuge: Vergleich mit dem Stripe-Shop (`Compare`), Vergleich mit dem JTL-Plugin im Konfigurator, Trockenlauf-Ansichten mit Rohdaten (die bleiben als Entwickler-Seite).
- Entwickler-Anmeldung mit Test-Personen und „Testkonten anlegen“. Das gibt es im Portal über Entra.
- Eigene Profil-, Anmelde-, Zugriffs- und Layoutseiten, denn das Portal hat sie schon (`Profile`, `/unauthorized`, `MainLayout`).
- Unsere Berechtigungsverwaltung (`Permissions.razor`), denn die Rollenverwaltung im Portal übernimmt das.

Was mitgeht: Bestellungen, Kundenkonten, Versand, Inhalte, Menüs und Hinweise, Weiterleitungen, Entscheidungshelfer, Prüfungen und Abgleich, Systemstatus. Dabei werden die Texte auf „du“ umgestellt (Portal-Regel) und jede Seite gegen die Checkliste in `DESIGN_GUIDE.md` §12 geprüft.

### 3.2 Wie der Code ins Portal kommt

| Option | Wie | Pro | Contra |
|---|---|---|---|
| **A Paket (empfohlen)** | Der Admin wird eine Razor-Klassenbibliothek `Meesenburg.SiseShop.Admin` in **unserem** Repo, als NuGet-Paket im Feed `Meesenburg.ES` (wie `Meesenburg.ES.Authorization`). ES.Web bindet das Paket ein | Wir entwickeln und testen in unserem Repo, mit eigenem kleinem Host im Portal-Layout. ES ändert sich einmal und dann nur noch über die Paketversion. Ein Fehler im Admin lässt sich durch Zurückdrehen der Version beheben | Einmalig Eingriffe in ES.Web (Router, `PagePermissions`, Menü). Für jede Admin-Änderung braucht es ein ES-Release |
| B Code ins ES-Repo kopieren | Seiten liegen direkt in `Meesenburg.ES.Web/Components/Pages/Shop` | Am einfachsten | Jede Änderung findet in der ES-Arbeitskopie statt, die ohne Versionsstand deployt wird. Genau das Risiko, das wir vermeiden wollen |
| C Eigener Admin, nur verlinkt | Der Shop-Admin bleibt eigene App mit Portal-Design, im Portal gibt es eine Kachel | Kein Eingriff in ES | Nicht das, was gewünscht ist: zweite Anmeldung, zweite Rollenverwaltung |

**Empfehlung A.** Einmalige Änderungen in ES (klein, alle hinter einem Schalter):
1. `PackageReference Meesenburg.SiseShop.Admin` in `Meesenburg.ES.Web.csproj`.
2. `Routes.razor`: `AdditionalAssemblies` um die Admin-Assembly ergänzen. `PagePermissions.cs` so ändern, dass es auch diese Assembly durchsucht.
3. `PortalArea.Shop` und Einträge in `PortalTools.cs`. Diese Einträge liefert das Paket selbst (`SiseShopAdmin.Tools`), damit ES sie nicht pflegen muss.
4. `Program.cs`: `builder.AddSiseShopAdmin()` registriert den Broker zur Shop-API mit internem Schlüssel aus dem Key Vault. Ist der Schalter `SiseShop:Admin:Enabled` aus, wird nichts registriert und kein Menüpunkt angezeigt.
5. Shop-Rechte (`shop.orders.read` usw.) in `Meesenburg.ES.Authorization/Permissions.cs` mit Katalog-Eintrag, neues Paket 1.0.16. Laut ES-CLAUDE.md sind **keine neuen Entra-Rollen** vorgesehen. Wir nutzen also Portal-Rollen statt der geplanten Gruppen `grp_mee_sise_shop_*`, und die Rollen vergibt die Rollenverwaltung im Portal.

Routen im Portal: `/shop/...` statt `/admin/...`, denn der Guide sieht `…/admin` für Admin-Unterseiten einer Funktion vor. Das klären wir bei der Umsetzung im Abschnitt 13 des Guides als 💡-Vorschlag.

### 3.3 Schutz für das ES-Projekt

Weil ES aus der Arbeitskopie deployt wird, gilt:
1. **Vorher einen Sicherungsstand:** Du committest den heutigen ES-Stand (inklusive `Meesenburg.ES.Authorization` und `Navigation/`) und setzt einen Tag, z. B. `vor-siseshop`. Das ist deine Entscheidung im ES-Repo, ich mache es nicht.
2. Die ES-Eingriffe (Abschnitt 3.2, Punkte 1–5) bereite ich als **Patch-Datei in unserem Repo** vor (`deploy/es-integration/*.patch`) und spiele ihn nicht selbst ein. Du schaust ihn an und spielst ihn ein, wenn ein ES-Release ohnehin ansteht.
3. Der Schalter `SiseShop:Admin:Enabled` steht standardmäßig auf aus. Ein ES-Release mit Patch, aber ohne Konfiguration, verhält sich wie vorher.
4. Vor dem ersten Einschalten testen wir lokal: ES-AppHost mit Profil `Dev` plus Shop-API aus unserem AppHost. Dabei rufen wir alle bestehenden Portal-Seiten einmal auf (Rauchtest), prüfen Login, Rollen und die Startseite.
5. Das Admin-Paket hängt nur von MudBlazor 9.12 und `Meesenburg.ES.Authorization` ab, beides in den Versionen, die ES schon nutzt. Es bringt keine eigenen globalen Styles mit: CSS nur mit Präfix `.shop-admin-*`.

### 3.4 Aufwand

| # | Was | PT |
|---|---|---|
| P-1 | MudBlazor auf 9.12, Microsoft.Identity.Web und Aspire auf ES-Stand, Tests | 0,5 |
| P-2 | Bereinigen (3.1), Texte auf „du“, Routen `/shop/…` | 1,5–2 |
| P-3 | RCL `Meesenburg.SiseShop.Admin` herauslösen, eigener Test-Host im Portal-Layout | 2–3 |
| P-4 | Shop-Rechte in das gemeinsame Paket, Abgleich mit unserem `Meesenburg.Authorization` | 1 |
| P-5 | ES-Patch (3.2), lokaler Rauchtest ES + Shop | 1,5 |
| P-6 | Paket im Feed veröffentlichen, Doku für das Einspielen | 0,5 |
| | **Summe** | **7–8,5 PT** |

## 4. Reihenfolge

1. **Diese Woche, ohne Abhängigkeiten:** T-1, T-3, T-4, P-1, P-2. Fragen V-1 bis V-6 an die IT, V-2 zusätzlich an ecomdata.
2. **Wenn V-1, V-2, V-4 da sind:** T-2, Rollout nach OK, T-5 mit dem SN24-Team.
3. **Parallel:** P-3 bis P-6. Der ES-Sicherungsstand (3.3, Punkt 1) muss vor P-5 stehen.

Bis der Admin im Portal läuft, nutzt das SN24-Team im Test den bisherigen Shop-Admin hinter derselben Anmeldung (Stufe 3).

## 5. Entscheidungen (17.11.2026, M. Piper)

| # | Frage | Entscheidung |
|---|---|---|
| E-T1 | Zugang zur Testversion | **Nur Stufe 3:** Entra-Anmeldung vor dem ganzen Shop, kein eigener Ingress, keine IP-Liste. Die Sperre muss sich später einfach entfernen lassen. Im Shop arbeitet das Team mit unseren Testkonten. V-3 entfällt |
| E-T2 | Admin-Integration | Paket, solange es kompatibel bleibt. Alternative: direkt in das bestehende Paket, weil das SiSe-Portal ohnehin vom ES-Bezug gelöst und als eigenes Projekt geführt werden soll. Erst bei P-3 festlegen, die Schritte bis dahin sind für beide Wege gleich |
| E-T3 | Shop-Rechte | Portal-Rollen. Eventuell kommt später eine Entra-Rolle „Shop (Basis)“ dazu |
| E-T4 | Sicherungsstand im ES-Repo | Macht M. Piper. Das Zertifikat `api.meesenburg.io` ist erneuert |

App-Registrierung „SiSe-Shop (Test)“ legt M. Piper an. Die Einstellungen stehen in `deploy/aks-test/APP_REGISTRIERUNG.md`.

## 6. Umsetzungsstand

| # | Stand 17.11.2026 |
|---|---|
| T-1 | **Erledigt.** `Security/TestGate.cs`, Schalter `SiseShop:TestGate:Enabled`. Eigenes Cookie `siseshop.testzugang`, eigener Rückweg `/signin-oidc-testzugang`. Getestet lokal, mit dem Schalter im Produktionsmodus: Jede Adresse (Seiten, Bilder, Sitemap, Admin) leitet zu Microsoft um; `/alive` liefert 200; Blazor-POST ohne Anmeldung liefert 401; `robots.txt` liefert `Disallow: /`; `X-Robots-Tag` steht auf allen Antworten. Ohne Client-ID startet der Shop nicht. Mit Schalter aus ist alles wie vorher. Die Testkonten-Schnellanmeldung erscheint auch im Testbetrieb. Offen: echter Durchlauf, sobald die App-Registrierung steht |
| T-4 | **Erledigt.** `Services/PortalSharedState.cs`. DataProtection-Schlüssel liegen in `portal_dataprotection_keys`, Login-Tickets in `portal_login_tickets` (nur als SHA-256-Hash, TTL-Index, einmalig über FindOneAndDelete). Getestet: Anmeldung mit Testkonto; die Anmeldung übersteht einen Portal-Neustart. Hinweis: ES.Web nutzt dafür Redis. Wir bleiben bei Mongo, damit der Shop nichts von ES mitbenutzt |
| P-1 | **Erledigt.** MudBlazor 9.12.0, Microsoft.Identity.Web 4.17.0, OpenIdConnect 10.0.14 wie ES.Web. Dabei ist die Pkcs-Sicherheitswarnung im Portal weg. `PanelClass` heißt in 9.12 `TabPanelsClass`. 324 Tests grün, Shop und Admin im Browser geprüft |
| Test | Echter Entra-Durchlauf mit „SiSe-Shop (Test)“ lokal erfolgreich: Anmeldung, Shop, Testkonto-Anmeldung (17.11.) |
| T-2, T-3 | **Vorbereitet, nichts angewendet.** `deploy/aks-test/` mit README. Daten im Mongo-Cluster `Dev-Sise-Shop` (Entscheidung M. Piper: kein Mongo-Pod). Cluster gelesen: external-dns verwaltet `meesenburg-service.de`, cert-manager `letsencrypt-prod`, NetworkPolicies werden durchgesetzt (azure-npm), neuer Knotenpool `agentpool2` (E2as_v7), angefragter Speicher 21–29 % → V-1 nicht mehr zwingend. `.dockerignore` ist jetzt eine Freigabeliste, sonst wären Forensik und Analysen mit in den ACR-Build gegangen. Admin-Link im Shop einstellbar (`SiseShop:AdminUrl`, später das SiSe-Portal) |
| Mongo/KV | Entscheidung M. Piper (17.11.): Mongo-Cluster über das Key-Vault-Secret `MongoDBConnectionString` wie ES, Key-Vault-Abfragen nach dem ES-Muster (`AddKeyVaultSecrets`, jetzt auch in unseren ServiceDefaults). Zugang zum Tresor über eine Kopie von `es-keyvault-app`, später eine eigene Identität (V-5). Test- und Prod-Shop laufen auf `agentpool` (`agentpool2` ist für ES mit hohem Speicherbedarf); bei Engpässen kommen Knoten dazu |
| P-2 | Als Nächstes |
