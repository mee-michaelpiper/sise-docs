# Sicherheits- und Datenschutzprüfung (Code) – Meesenburg.ES

| | |
|---|---|
| Datum | 07.10.2026 |
| Gegenstand | Repository `Meesenburg.ES` (Stand HEAD `eed1920` + Arbeitskopie) |
| Art | Statische Code-Review im Auftrag des Eigentümers, rein lesend, ohne Netzwerkzugriffe, ohne Ausnutzung |
| Schwerpunkte | Authentifizierung/Autorisierung, Injection/Eingaben, Datenschutz (DSGVO), Härtung |
| Nicht im Fokus | Secrets, Konfiguration, Dockerfiles, Abhängigkeiten, Git-Historie (parallele Prüfung) – hier nur am Rand erwähnt |

Secrets werden in diesem Bericht nie im Klartext genannt, höchstens die ersten 4 Zeichen und „…“.

---

## 1. Zusammenfassung

**Gesamteinschätzung: mittleres bis hohes Risiko.** Ein anonymer Endpunkt in der esData-API ist kritisch. Dazu kommen einige strukturelle Schwächen bei Dienst-Identitäten und Datenhaltung. Das Portal selbst ist ordentlich abgesichert.

**Positiv**
- **Portal-Berechtigungen:** Alle fachlichen Seiten tragen `[AuthorizeMee(Permission = …)]`. Die Dienste prüfen die Berechtigung zusätzlich serverseitig (`PermissionAdminService`, `AccessRequestService`, `MonitoringService`, `FeatureRequestService`, MEQ-Prüfung im `CustomerRegistrationService`). Rollenänderungen werden auditiert (`PermissionAuditEntry`). Hohe Berechtigungen kann man nicht anfragen (`AccessRequestService.HighPermissions`).
- **ApiService:** Die Policies sind fein gegliedert (`JobsRead`/`JobsTrigger`/`JobsManage`/`EsErpCustomerSync`). `RecordSyncApi` und `CustomerSyncApi` bauen den Filter selbst und prüfen die Nummern streng (nur Ziffern, Länge, höchstens 50 IDs).
- **Fach-APIs (SAP, Mule, Graph, fast alle esData-Gruppen):** verlangen ein Entra-Token mit App-Rolle `Mee.API.All`. Issuer und Audience werden in `MeeApiAuthentication` explizit geprüft.
- **OpenAPI/Scalar:** nur in Development gemappt.
- **Fehlerbehandlung:** Die Fehlerseite (`Error.razor`) und die `ErrorBoundary` zeigen Exception-Texte nur mit `monitoring.log.read`. Die APIs antworten bei unerwarteten Fehlern generisch (ProblemDetails ohne Stacktrace).
- **Injection-Schutz:**
  - Mongo-Freitextsuche im Log mit `Regex.Escape` (`LogStore.cs:93,102`).
  - SOAP-XML wird über `XElement` gebaut (keine String-Konkatenation).
  - OpenPLZ bekommt nur die PLZ, URL-kodiert und mit fester Basis-URL – also kein SSRF.
  - Die News-Mail kodiert HTML konsequent (`NewsMailRenderer.E`). Es gibt keine `MarkupString`-Verwendung.
- **Datenminimierung:**
  - Die Personalnummer wird per FF1 (Format-Preserving Encryption) pseudonymisiert (`EncryptionService`).
  - Abwesenheiten gehen ohne Abwesenheitsgrund ans esERP: feste `urlaubsartNr = 1000` (`ESDataWorker/Models/EsApi/EmployeeAbsence.cs:8-18`).
  - Der Mailversand der Graph-API hat eine Empfänger-Domänen-Allowlist.
- **Aufbewahrung:** Logs haben einen TTL-Index von 90 Tagen, der Token-Cache wird in Mongo verschlüsselt (`Encrypt = true`), erledigte Sync-Probleme werden per TTL gelöscht.
- **Doppelte Ausführung:** Bei unsicheren HTTP-Methoden ist die Polly-Wiederholung abgeschaltet (Kundenanlage, Mail-Transfer). Die Kundenanlage nutzt eine Request-ID gegen Doppelanlage.

**Wichtigste Befunde**

1. **ES-C-01 (Kritisch):** Die Invoice-Gruppe der esData-API ist **ohne Authentifizierung** erreichbar. Wer sie erreicht, kann E-Rechnungsdaten (Name, Anschrift, E-Mail) lesen und Rechnungen in esERP als „erledigt“ markieren.
2. **ES-C-02 (Hoch):** Eine einzige grobe App-Rolle `Mee.API.All` gibt Zugriff auf alles: Bankdaten, SEPA-Mandate, HR-Daten, Mitarbeiter-Kennwörter in esERP, Job-Verwaltung. Das **Portal benutzt dazu die Client-Credentials des DataWorkers**. Die ApiService akzeptiert diese Tokens als „Dienst mit allen Rechten“.
3. **ES-C-03 (Hoch):** Die **Kennwort-Endpunkte** `/es/employee/password(/upsert)` reichen `kennwort`, `kennwortAlt`, `winBenutzer` und `aadObjectID` ungefiltert an esERP durch. Damit lassen sich esERP-Konten übernehmen oder SSO-Zuordnungen umbiegen.
4. **ES-C-04 (Hoch):** **Alle Dienste nutzen dieselbe Mongo-Verbindung.** Die DataProtection-Schlüssel des Portals liegen unverschlüsselt in derselben Datenbank wie das Berechtigungssystem. Wer irgendeinen Dienst kompromittiert, kann sich Rechte geben und Login-Cookies fälschen.
5. **ES-C-05 (Hoch, DSGVO):** Antwort- und Fehler-Bodies nachgelagerter Systeme landen roh in Exception-Texten und ProblemDetails, also im Mongo-Log (90 Tage) und in API-Antworten. Das betrifft auch personenbezogene Daten.

---

## 2. Befunde

| ID | Schwere | Fundstelle | Beschreibung | Auswirkung | Empfehlung |
|---|---|---|---|---|---|
| ES-C-01 | **Kritisch** | `Meesenburg.de.ES.API.ESData/Program.cs:156-158`; `Apis/InvoiceApi.cs:20-21` | Bei `NewVersionedApi("Invoice")` ist `RequireAuthorization` auskommentiert, eine Fallback-Policy gibt es nicht. `GET /es/invoice/outgoing/eInvoice/{invoiceId}` und `PUT /es/invoice/{type}/finish/{invoiceId}` sind anonym. | Anonymes Lesen von Rechnungsempfängern (Firma/Name, Straße, PLZ, Ort, E-Mail, Leitweg-/Peppol-ID) über fortlaufende IDs. Anonymes Abschließen von Ein- und Ausgangsrechnungen in esERP (Integrität der Buchhaltung). Die API ist im AppHost mit `WithExternalHttpEndpoints()` markiert. | Sofort `.RequireAuthorization(policy => policy.RequireRole("Mee.API.All"))` setzen. Zusätzlich eine **Fallback-Policy** `RequireAuthenticatedUser` in allen APIs (`AddAuthorizationBuilder().SetFallbackPolicy(...)`). Ingress-Logs auf Zugriffe prüfen. |
| ES-C-02 | **Hoch** | `Meesenburg.ES.Web/Program.cs:226-308`; `Web/Handler/EsApiAuthenticationHandler.cs:40-46`; `ApiService/Program.cs:49-55`; `ApiService/Auth/AuthorizationPolicies.cs:41-42`; `ServiceDefaults/MeeApiAuthentication.cs:16-47` | (a) Das Portal ruft esData-, SAP-, Mule- und Graph-API mit einem **App-Token** auf. Dafür nutzt es das Secret `DataWorker-EsApi-Secret`, also dieselbe Identität wie der Worker (ClientId `64cf…`). (b) Fach-APIs kennen nur die Rolle `Mee.API.All`, ohne Unterscheidung zwischen Lesen und Schreiben oder nach Fachgebiet. (c) Die ApiService akzeptiert zusätzlich die Audience der ES-API (`448e…`), und `IsService` = Rolle `Mee.API.All` bekommt **alle** Job-Rechte. (d) Es wird nicht geprüft, ob es wirklich ein App-Token ist (`idtyp`/fehlendes `scp`). | Confused Deputy: Die Berechtigung des Benutzers wird nur im Portal geprüft. Eine Schwachstelle im Portal (oder ein abfließendes Worker-Secret) bedeutet Vollzugriff auf alle Bankverbindungen (`/sap/customers/BankingDetails`), SEPA-Mandate, Mitarbeiterdaten und Abwesenheiten, die Kennwort-Endpunkte (ES-C-03), Kundenanlage in SAP-Prod und Job-Verwaltung. Im SAP- und esERP-Audit erscheint nur der Dienstbenutzer statt der Person. | Eigene App-Registrierung für das Portal mit **eng geschnittenen App-Rollen** (z. B. `Mee.Stock.Read`, `Mee.Customer.Create`, `Mee.Directory.Read`, `Mee.Mail.Send`). Besser: On-Behalf-Of mit Benutzer-Token und Prüfung der Berechtigung in der Fach-API. Die Fach-APIs sollten die Rollen je Endpunkt prüfen. Die ApiService sollte nur ihre eigene Audience akzeptieren und `IsService` zusätzlich an `idtyp == "app"` binden. Die App-Rolle `Mee.API.All` in Entra auf `allowedMemberTypes: Application` beschränken. |
| ES-C-03 | **Hoch** | `ESData/Apis/EmployeeApi.cs:20-21,41-52`; `Brokers/ERP/C4vApiBroker.Employee.cs:33-39`; `Models/c4vApi/PersonalPassword.cs:12-49`; `ESData/Apis/UserApi.cs:15` | Die API reicht beliebige `PersonalPassword`-Objekte durch (`kennwort`, `kennwortAlt`, `fehlVersuche`, `winDomaene`, `winBenutzer`, `aadObjectID`) und gibt die esERP-Antwort zurück. Fachlich nötig ist nur `aadObjectID` (`ESDataWorker/Services/UserService.cs:54-58`). Dazu kommt der Rest `POST /es/TEST/setNewPassword`. | Jeder Inhaber eines `Mee.API.All`-Tokens (siehe ES-C-02, auch das Portal) kann esERP-Kennwörter setzen, Fehlversuche zurücksetzen und die Entra- oder Windows-Zuordnung eines Mitarbeiters ändern. Das ermöglicht Kontoübernahme bzw. SSO-Anmeldung als andere Person. Kennwörter oder Hashes in der esERP-Antwort fließen in Antworten und bei Fehlern ins Log (ES-C-05). | Endpunkte entfernen oder durch einen engen Endpunkt „AAD-Objekt-ID setzen“ ersetzen: DTO nur mit `personalNr` und `aadObjectId`, Antwort ohne Kennwortfelder. `/es/TEST/*` entfernen. Prüfen, ob esERP Kennwörter im Klartext zurückgibt. |
| ES-C-04 | **Hoch** | `Web/Program.cs:85-99`; `Web/Brokers/Storages/MongoDataProtectionKeyRepository.cs:23-30`; `Authorization/PortalDatabase.cs:42-46`; `ServiceDefaults/Logging/MongoLogOptions.cs:17`; Secret-Name `MongoDBConnectionString` in Web, ApiService, Worker, SAP-API, Graph-API | (a) Alle Dienste verbinden sich mit **demselben Mongo-Secret** und damit denselben Rechten (Logs, Worker-Daten, Portal-DB mit Rollen, Zuweisungen, DataProtection-Keys, Token-Cache). (b) Die DataProtection-Schlüssel werden **ohne `ProtectKeysWith…`** gespeichert (Mongo oder Redis). (c) Die Token-Cache-Verschlüsselung hängt an genau diesen Schlüsseln. | Wird ein beliebiger Dienst kompromittiert (z. B. die SAP-API mit RFC-Bibliothek), kann er sich in `Prod-ES-Portal` eine Rolle mit `*` zuweisen, Auth-Cookies fälschen und MSAL-Refresh-Tokens entschlüsseln. Die Trennung der Dienste ist damit aufgehoben. | Mongo-Benutzer je Dienst mit minimalen Rollen (Datenbank/Collection, lesend wo möglich). Schlüsselring mit Key-Vault-Schlüssel schützen (`ProtectKeysWithAzureKeyVault`) oder Azure Blob + Key Vault nutzen. Den Schreibzugriff auf die Collections `Roles` und `Assignments` nur dem Portal geben. |
| ES-C-05 | **Hoch** (DSGVO) | `ServiceDefaults/HttpClientExtensions.cs:13-23,39-41,76-88,105-107,151,220,320-345`; `ServiceDefaults/ProblemExceptionHandler.cs:32-46`; `Logging/MongoLoggerProvider.cs:88-96`; `ESData/Handlers/C4vAuthenticationHandler.cs:107-109` | `HttpResponseException.Message` ist der **komplette Fehler-Body** der Gegenstelle. Bei JSON-Fehlern landet der **rohe Antwort-Body** in `ProblemDetails.Detail` („Raw content: …“). Beides wird mit Exception-Text (bis 32 000 Zeichen) ins Mongo-Log geschrieben und von `ProblemExceptionHandler` als ProblemDetails an den Aufrufer weitergegeben. Die Login-Fehlerantwort von esERP wird vollständig geloggt. | Personenbezogene Daten (Kunden, Bankdaten, Mitarbeiter, Abwesenheiten, ggf. Kennwortfelder aus ES-C-03) und Interna gelangen ins Log (90 Tage, lesbar mit `monitoring.log.read`) und in Antworten an andere Dienste und ans Portal (z. B. `EsSyncError = ex.Message`, `Web/Services/CustomerRegistrationService.cs:127`). | Raw-Content nicht in Message oder Detail übernehmen. Höchstens Statuscode, Korrelations-ID und ein gekürzter, **geschwärzter** Ausschnitt (Allowlist, z. B. nur das Feld `title`). ProblemDetails von Downstream nicht ungefiltert weiterreichen. Bei der Login-Antwort nur den Status loggen. |
| ES-C-06 | Mittel | `SAPData/Brokers/ERP/SAPBroker.Customer.cs:71-84,169`; `SAPBroker.Supplier.cs:53`; `SAPBroker.Article.cs:660`; `SAPData/Apis/CustomerApi.cs` (`string id`, `partnerRole`) | Die WHERE-Klauseln für `/SAPDS/RFC_READ_TABLE2` werden per String-Interpolation aus API-Parametern gebaut (`CUSTOMERID = '{id}'`, `PARVW = '{partnerRole}'`, `LIFNR`, `MATNR`). Es gibt keine Validierung und kein Escaping von `'`. Über `jobs.manage` (`POST /apisvc/jobs`, beliebiges `FilterJson`) gelangen freie IDs bis in diese Klauseln. | Filter lassen sich umgehen, z. B. die Einschränkung `ORGANISATIONID = '298'`. Dadurch werden Fremddaten gelesen und ggf. in esERP übertragen (Massenüberschreibung), oder es entstehen teure Abfragen (DoS auf SAP). | IDs streng validieren (nur Ziffern bzw. ein festes Zeichenset, maximale Länge, Auffüllen mit Nullen), `partnerRole` gegen eine Liste erlaubter Werte prüfen, `'` ablehnen. Das `FilterJson` in `JobApi.PostJob` ebenfalls schemabasiert validieren. |
| ES-C-07 | Mittel | `Web/Program.cs:338-375` | `/login` und `/refresh-login` übernehmen `returnUrl` ungeprüft als `AuthenticationProperties.RedirectUri`. Nach der Anmeldung leitet der OIDC-Handler auch auf externe URLs weiter. | Open Redirect auf der vertrauenswürdigen Portal-Domain nach echter Microsoft-Anmeldung. Das ist für Phishing gut nutzbar. | Nur lokale Pfade zulassen: `Url.IsLocalUrl`, also beginnt mit `/`, nicht mit `//` oder `/\` (wie schon in `Unauthorized.razor:66`). Sonst `/`. |
| ES-C-08 | Mittel | `Authorization/PermissionSeed.cs:36-39,93`; `docs/TODO.md` (Neukundenanlage) | Die Rolle „Grundzugang“ wird allen Angemeldeten zugewiesen und enthält `eserp.customer.add`. Jede Person im Tenant kann damit Kunden in **SAP-Prod (MEP)** anlegen und Sync-Jobs einreihen. Es gibt kein Rate-Limiting. | Unbeabsichtigte oder missbräuchliche Kundenanlagen, Stammdatenqualität, Last auf SAP und Worker. Eingegebene personenbezogene Daten von Privatkunden ohne fachliche Zuständigkeit. | Wie im TODO geplant `eserp.customer.add` nur der Rolle Vertrieb geben. Rate-Limiting (`AddRateLimiter`) für Anlage und Sync je Benutzer. Optional ein Vier-Augen-Prinzip für Privatkunden. |
| ES-C-09 | Mittel | `Web/Program.cs:312-335` (kein Header-Middleware); alle APIs | Keine Security-Header: kein CSP / `frame-ancestors`, kein `X-Content-Type-Options`, keine `Referrer-Policy`, keine `Permissions-Policy`. Die HSTS-Dauer ist Standard (30 Tage). Kein Rate-Limiting. | Clickjacking auf Blazor-Server-Aktionen (z. B. Rollen zuweisen, Kunde anlegen). Kein zweiter Schutzwall gegen XSS. | Middleware mit CSP (`default-src 'self'; frame-ancestors 'none'; …`, MudBlazor/Blazor-kompatibel testen), `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`. HSTS auf ≥ 1 Jahr. Alternativ alles am Ingress setzen. |
| ES-C-10 | Mittel (DSGVO) | `ESDataWorker/Services/UserService.cs:45-47,62-63`; `Web/Services/CustomerRegistrationService.cs:101-102`; `SAPData/Services/CustomerRegistrationService.cs:122`; `Web/Logging/UiErrorLogger.cs:32`; `Graph/Brokers/Imap/ImapProtocolLogger.cs:27-37` (wenn `ImapProtocolLog=true`); `Graph/Services/InvoiceMailService.cs:269`; `Meesenburg.SAP.Soap/Services/SapCustomerSoapGateway.cs:48` (Debug) | Im Mongo-Log (Information und höher, 90 Tage) landen Klartext-Namen und E-Mail-Adressen von Mitarbeitenden (inklusive Sammelliste aller nicht gefundenen Adressen), Kundennamen (bei Privatkunden also Personennamen), Benutzernamen und Mail-Betreffs. Der IMAP-Protokoll-Log würde Server-Zeilen mit `ENVELOPE` (Absender, Betreff) speichern. Das SOAP-XML mit allen Kundendaten wird auf Debug geloggt. | Personenbezogene Daten in einem technischen Log, das viele Personen lesen dürfen (`monitoring.log.read`, über `it-operations` und `portal-admin` vererbt). Das widerspricht der Datenminimierung (Art. 5 Abs. 1 lit. c, e DSGVO). | IDs (Personalnummer pseudonymisiert, Kundennummer) statt Namen und E-Mail loggen. E-Mail-Adressen maskieren (`m***@…`). `ImapProtocolLog` in Prod verbieten. SOAP-XML nie loggen. Log-Aufbewahrung je Kategorie abstufen (z. B. 30 Tage für Information). Leserechte am Log dokumentieren. |
| ES-C-11 | Mittel (DSGVO) | `Authorization/PermissionSeed.cs:21-27` (Audit ohne TTL); Collections `AccessRequests`, `FeatureRequests`, News-Profile (`NewsStore`), `Jobs`, `Jobs-Runs`, `Jobs-Log`, offene `SyncIssues` | TTL-Indizes gibt es nur für Logs, Token-Cache, erledigte Sync-Probleme und die Rechnungsmail-Sperren. Zugriffsanfragen (mit Begründung, Name, E-Mail, Rechte-Snapshot), Audit, Ideen, News-Einstellungen (E-Mail, Berechtigungen) und Job-Historien werden unbegrenzt aufbewahrt. | Kein Lösch- bzw. Aufbewahrungskonzept (Art. 5 Abs. 1 lit. e, Art. 17 DSGVO). Nach dem Austritt einer Person bleiben ihre Daten erhalten. | Aufbewahrungsfristen festlegen und als TTL umsetzen, z. B. Zugriffsanfragen und Audit 1–2 Jahre, Job-Runs 90 Tage. Einen Prozess beim Austritt definieren: Profile, Zuweisungen und Anfragen löschen oder anonymisieren. |
| ES-C-12 | Niedrig | `Web/Program.cs:351-375,377-382` | `/refresh-login` ist anonym per GET aufrufbar und erhöht bei jedem Aufruf den Änderungszähler in der Datenbank (`InvalidateEverywhereAsync`). Das leert die Rechte-Caches in allen Diensten. `/logout` ist ebenfalls GET. | Per CSRF oder Skript lässt sich Last auf Mongo und alle Dienste erzeugen (Cache-Thrashing). Erzwungenes Abmelden (Logout-CSRF). | `/refresh-login` nur für Angemeldete und mit Drosselung (z. B. höchstens 1×/Minute je Benutzer). Logout per POST mit Antiforgery-Token. |
| ES-C-13 | Niedrig | `ApiService/Program.cs:166-200`; `ApiService/appsettings.json` (Logging) | `/debug/whoami` ist in Prod aktiv. Jeder Inhaber eines gültigen Tokens bekommt alle Claims und bei Fehlern `exception.ToString()` zurück. Die Auth-Kategorien loggen auf Debug bzw. Trace. | Informationspreisgabe (Stacktraces, Policy-Interna), erhöhtes Log-Volumen. | Nur in Development mappen oder entfernen. Log-Level in Prod auf Warning. |
| ES-C-14 | Niedrig | `ESData/Apis/UserApi.cs:15-32`; `ESData/Program.cs:117-130,178-179`; `SAPData/Program.cs:110-122` | Test- und Template-Reste: `/es/TEST` würde das **esERP-Zugriffstoken** zurückgeben (zurzeit nur, weil `ITokenService` nicht registriert ist). `/weatherforecast` ist in der SAP-API anonym. Nach `app.Run()` steht toter Code. | Aktiviert jemand `ITokenService` wieder, liefert die API das esERP-Token an Aufrufer. Größere Angriffsfläche. | Entfernen. |
| ES-C-15 | Niedrig | `ServiceDefaults/Extensions.cs:105-122` | `/health` und `/alive` sind in allen Umgebungen anonym (das Template-`if (IsDevelopment)` ist auskommentiert). | Gering (nur Status), aber `/health` startet alle Checks und könnte zur Lasterzeugung genutzt werden. | Nur intern erreichbar machen (eigener Port oder Ingress-Regel) oder Host-Filter / `RequireHost` setzen. |
| ES-C-16 | Niedrig | `Web/Program.cs:105-114`; `Web/appsettings.json` (`AllowedHosts: *`); `Web/Components/Layout/MainLayout.razor.cs:46-51` | Forwarded Headers aus beliebigen Quellen (`KnownProxies` und `KnownIPNetworks` geleert, `XForwardedHost` aktiv). Die Logout-URL baut die App aus dem Host-Header und nutzt den Endpunkt `/common/` statt des Tenants. | Gefälschte Client-IP im Log, Host-Header-Manipulation (Rücksprungadresse; Entra prüft registrierte URIs). `common` ist funktional, aber unnötig breit. | Pod-Netz bzw. Ingress-CIDR als `KnownIPNetworks` eintragen. `AllowedHosts` auf die Portal-Domain setzen. Logout über `SignOutAsync(OpenIdConnect)` (macht schon `/logout`) bzw. Tenant-ID statt `common`. |
| ES-C-17 | Niedrig | `Web/Program.cs:133-165` | Persistentes Login-Cookie mit 7 Tagen Gleitzeit, Token-Cache 7 Tage. Cookie-Attribute sind nicht explizit gesetzt (Standards greifen). | Lange Sitzungen auf geteilten Geräten. Die Rechteauflösung ist aktuell (gut), das Entra-Token in der Sitzung aber nicht. | Fachlich 8–12 h abwägen. `Cookie.SecurePolicy = Always`, `SameSite = Lax`, `HttpOnly` explizit setzen. Entra Conditional Access (Anmeldefrequenz) nutzen. |
| ES-C-18 | Niedrig | `Graph/Brokers/Graph/GraphBroker.User.cs:26,38` | `$search` und `$filter` werden ohne Escaping gebaut (`onPremisesSamAccountName eq '{samAccountName}'`, Suchbegriff mit `"`). `EscapeODataString` gibt es, wird hier aber nicht genutzt. | OData-Injection, d. h. Filter erweitern oder Verzeichnis-Enumeration (nur mit `Mee.API.All`). | `EscapeODataString` verwenden, Suchbegriff auf erlaubte Zeichen begrenzen. |
| ES-C-19 | Niedrig | `Graph/Apis/MailApi.cs:39-49`; `GraphBroker.Mail.cs:81-92` | Die Empfänger-Domäne wird per `LastIndexOf('@')` ohne Adressparser ermittelt. Der HTML-Body ist frei wählbar. Die Graph-App sendet mit App-Berechtigung. | Bei abgeflossenen Credentials (ES-C-02) lassen sich interne HTML-Mails im Namen von `sise-portal@…` verschicken. Ohne Exchange Application Access Policy darf `Mail.Send` (App) theoretisch **jedes** Postfach nutzen. | `MailAddress` parsen und genau einen Empfänger erzwingen. In Exchange eine `ApplicationAccessPolicy` (bzw. RBAC for Applications) auf Absender- und Rechnungspostfächer beschränken. |
| ES-C-20 | Niedrig | `Web/Brokers/Apis/EsBroker.Article.cs:11`; `EsBroker.Sales.cs:12-15` | Pfadsegmente (Artikelnummer aus der Portal-URL `/eserp/stock/article/{ArticleNo}`) werden nicht URL-kodiert in die esData-URL eingesetzt. | Pfad-Manipulation gegen andere GET-Endpunkte der esData-API mit App-Token. Die Wirkung ist durch die Deserialisierung in `EsArticle` begrenzt. | `Uri.EscapeDataString` für alle Pfadsegmente, Artikelnummer validieren. |
| ES-C-21 | Niedrig | `Authorization/Permissions.cs:54`; `Web/Services/PermissionAdminService.cs:123-129`; `Authorization/PermissionResolver.cs:39,60-61`; `PermissionClaimsTransformation.cs:29-35` | `IsKnown("*")` ist wahr. Wer Berechtigungen verwaltet, kann also Rollen mit „alle Rechte“ bauen und sich selbst zuweisen (kein Vier-Augen-Prinzip). Die Developer-Gruppe bekommt immer `*`, auch bei Datenbankfehlern. | Rechteausweitung innerhalb der Admin-Rolle bis auf Developer-Niveau. Ein großer Personenkreis mit Vollzugriff. | `*` in der Rollenpflege nicht zulassen. Selbstzuweisung blockieren oder freigeben lassen. Mitglieder von `grp_mee_sise_developer` klein halten und regelmäßig rezertifizieren (Entra Access Reviews), Nutzung über das Audit überwachen. |
| ES-C-22 | Niedrig | `ESDataWorker/Services/AccountingService.cs:20-28,170-210` | Der Quellpfad der Rechnungskopie kommt ungeprüft aus esERP (`Document.FilePath`) und wird mit dem Dienstkonto von der SMB-Freigabe gelesen. Die Endung wird übernommen. Die SMB-Zugangsdaten stehen im Code (`tDwC…`, siehe Parallelprüfung). | Manipulierte Pfade in esERP könnten beliebige Dateien der Freigabe `Drives` in den SAP-Exportordner kopieren. | Quellpfad auf das erwartete Präfix bzw. Verzeichnis prüfen, Endungen per Allowlist (`.pdf`). Zugangsdaten in den Key Vault verschieben. |
| ES-C-23 | Niedrig | `ESDataWorker/…/JobTypeConcurrencyGate`; `docs/TODO.md` | Die Sperre „ein Job je Typ“ gilt nur pro Pod. Bei mehreren Replikas laufen gleiche Jobs parallel. | Doppelte oder gegenläufige Upserts nach esERP. | Mongo-Lease je JobType (wie beim Scheduler) oder Worker mit `replicas: 1` festschreiben. |
| ES-C-24 | Info | `SAPData/Program.cs:44`, `SAPData/appsettings.json`, `SAPData/Brokers/ERP/SAPBroker.cs:43` (`srv9…`), `Web/appsettings.json` (`hZE8…`), `AccountingService.cs:28` (`tDwC…`), ERPConnect-Lizenz (`F23W…`) | Nebenbei gefunden: Zugangsdaten und Secrets im Quelltext bzw. in der appsettings. | Siehe Parallelprüfung (Secrets/Git-Historie). | Rotieren, in den Key Vault verschieben. |

---

## 3. Datenschutz (DSGVO)

### 3.1 Verarbeitungen und Datenkategorien

| Verarbeitung | Betroffene | Datenkategorien | Quelle → Ziel | Bemerkung |
|---|---|---|---|---|
| Kunden-/Partner-Stammdatenabgleich | Kunden (auch Privatpersonen), Ansprechpartner | Name, Anschrift, Telefon, E-Mail, USt-ID, Rechnungsempfänger, Werbeverbot | SAP (RFC) → SAP-API → Worker → esData-API → esERP | ES ist Zielsystem, SAP führend |
| Bankdaten / SEPA | Kunden | IBAN/Konto, Bank, SEPA-Mandate | SAP (`KNBK`, `TIBAN`, Mandate) → esERP | Sehr schützenswert (Betrugsrisiko). Endpunkte `/sap/customers/BankingDetails` (alle Kunden) und `/sap/customer/{id}/sepaMandate` nur mit `Mee.API.All` geschützt, siehe ES-C-02 |
| Neukundenanlage | Neukunden | wie oben + Zahlungsbedingungen, Rechnungsversand, Leitweg-ID | Portal → SAP-API → SAP SOAP (MEP/MEQ) → Sync-Job → esERP | Jede angemeldete Person darf anlegen (ES-C-08). Datenschutzerklärung der Meesenburg GmbH fehlt (laut `docs/TODO.md`), Art. 13 DSGVO |
| Adressprüfung | – | nur PLZ | SAP-API → OpenPLZ (openplzapi.org) | Datensparsam umgesetzt (Straße und Ort werden lokal verglichen). Lizenz der Daten offen |
| Mitarbeiter-Stammdaten | Beschäftigte | Name, E-Mail, Kostenstelle, Personalnummer (FF1-pseudonymisiert), Entra-Objekt-ID | SAP HCM → esERP; Graph (Entra) für die Objekt-ID | Beschäftigtendaten (§ 26 BDSG). Pseudonymisierung positiv |
| Abwesenheiten | Beschäftigte | Zeitraum, Anzahl Tage, SAP-Belegnr. | SAP HCM (`BAPI_EMPATTABS_GETLIST`) → esERP | **Positiv:** Abwesenheitsart (mögliche Gesundheitsdaten, Art. 9) wird nicht übertragen (`urlaubsartNr = 1000`). **Aber:** Die SAP-API liefert `TypeText` (Grund, z. B. Krankheit) an jeden `Mee.API.All`-Aufrufer, siehe `SAPData/Models/EmployeeAbsence.cs:14`. Feld in der API weglassen |
| **Mitarbeiter-Kennwörter (esERP)** | Beschäftigte | `kennwort`, `kennwortAlt`, Fehlversuche, Windows-Benutzer, AAD-ID | esData-API → esERP `/personal/kennwort(e)` | **Ergebnis der Prüfung:** Der Worker überträgt **keine** Kennwörter, nur `personalNr` + `aadObjectID` (`ESDataWorker/Services/UserService.cs:54-58`). Kennwörter werden im Code nicht gezielt geloggt. **Risiko:** Die API-Endpunkte reichen Kennwortfelder in beide Richtungen durch (ES-C-03). Bei Fehlern gelangen Antwort-Bodies ins Log (ES-C-05) |
| Rechnungen / E-Rechnung | Rechnungsempfänger | Name, Anschrift, E-Mail, Leitweg-/Peppol-ID, Belegpfade | esERP → esData-API; Worker → SMB-CSV (SAP-FI-Import) | Anonym abrufbar (ES-C-01) |
| Rechnungsmail-Transfer | Absender von Eingangsrechnungen | Mails inkl. Anhängen | Postfach `invoices@…` → `invoices_eserp@…` (Graph/IMAP) | Sperren mit TTL. Betreffs im Fehlerlog |
| Portal-Benutzerdaten | Beschäftigte | Name, E-Mail/UPN, Objekt-ID, Rollen, Zugriffsanfragen mit Begründung, Ideen, News-Einstellungen | Entra → Portal-DB (`Prod-ES-Portal`) | Keine Aufbewahrungsfristen (ES-C-11) |
| Verzeichnissuche | Beschäftigte | Name, UPN, Mail, Abteilung, Position, sAMAccountName | Graph → Portal (nur `portal.permissions.manage`) | Angemessen eingeschränkt |
| Technisches Log | alle obigen (indirekt) | Namen, E-Mails, Kundennamen, Fehler-Bodies | alle Dienste → Mongo `Prod-ES-Logs` (90 Tage TTL) | ES-C-05, ES-C-10 |
| News-Mail | Beschäftigte (Opt-in) | E-Mail, Name, Berechtigungen | Portal → Graph-API → Exchange | Domänen-Allowlist; HTML kodiert |
| Neukunden-Bestätigungsmail | Neukunden | – | nur Entwurf (`docs/entwurf-neukunden-bestaetigungsmail.md`), nicht umgesetzt | Vor Umsetzung Inhalt minimieren (keine Bankdaten), Rechtsgrundlage und Datenschutzhinweis klären |
| Kontoeröffnungen mit PDFs | – | – | **In diesem Repository nicht gefunden** (kein Upload/PDF-Handling außer Rechnungskopien und Mailanhängen) | Gegebenenfalls in einem anderen Repository prüfen |

### 3.2 Datenweitergabe an Dritte / externe Dienste
- **OpenPLZ (openplzapi.org):** nur die PLZ. Unkritisch.
- **Microsoft (Entra, Graph, Exchange Online, Azure Service Bus, Key Vault):** Auftragsverarbeitung über den Microsoft-Vertrag (DPA) und Cloud-Region prüfen.
- **MuleSoft / Akeneo (über die Mule-API):** Artikel- und Bestandsdaten, keine personenbezogenen Daten erkennbar.
- **esERP-Hersteller (com4/c4v, `esapi.meesenburg.de`):** Kunden-, Bank-, Mitarbeiter- und Abwesenheitsdaten. Prüfen, ob ein AV-Vertrag nach Art. 28 vorliegt und wo gehostet wird.

### 3.3 Wesentliche Datenschutz-Risiken und Maßnahmen
1. **Unbefugter Zugriff auf Rechnungsempfängerdaten** (ES-C-01). Sofort schließen. Prüfen, ob ein meldepflichtiger Vorfall nach Art. 33 vorliegt: Ingress- und Access-Logs auswerten, ob der Endpunkt von außen erreichbar war und abgerufen wurde.
2. **Eine Rolle für alles** (ES-C-02). Bank-, HR- und Kennwortdaten sind nicht nach Need-to-know getrennt (Art. 25, 32).
3. **Personenbezogene Daten im technischen Log** (ES-C-05, ES-C-10). Minimieren, maskieren, kürzere Fristen.
4. **Fehlendes Löschkonzept** für die Portal- und Job-Collections (ES-C-11).
5. **Transparenz:** Datenschutzerklärung für Neukunden fehlt bzw. ist falsch verlinkt. Einträge im Verzeichnis von Verarbeitungstätigkeiten (Art. 30) für den Abgleich SAP→esERP, die Abwesenheiten und das Portal anlegen bzw. ergänzen.
6. **Beschäftigtendaten:** Abgleich von Abwesenheiten und Entra-Zuordnung mit Betriebsrat und DSB abstimmen. Das Abwesenheitsgrund-Feld aus der SAP-API entfernen.

---

## 4. Details zu den Befunden „Kritisch“ und „Hoch“

### ES-C-01 – Anonyme Invoice-Endpunkte der esData-API (Kritisch)
- **Code:** `Meesenburg.de.ES.API.ESData/Program.cs:156-158` – `app.NewVersionedApi("Invoice") // .RequireAuthorization(...) .MapInvoiceApiV1();`. Alle anderen Gruppen haben `RequireAuthorization`. Eine Fallback-Policy gibt es nicht, also gilt für diese Routen keine Authentifizierungspflicht.
- **Betroffen:** `GET /es/invoice/outgoing/eInvoice/{invoiceId}` (Rückgabe `EInvoice`: Name1–3, Straße, PLZ, Ort, E-Mail, Leitweg- und Peppol-ID; die IDs sind fortlaufende Ganzzahlen) und `PUT /es/invoice/{type}/finish/{invoiceId}` (setzt Rechnungen in esERP auf „erledigt“, Typ `i`/`o`).
- **Erreichbarkeit:** Der Dienst ist im AppHost mit `WithExternalHttpEndpoints()` markiert. Ob ein Ingress die API öffentlich macht, ist in diesem Repository nicht sichtbar. Selbst wenn nicht, kann sie jeder Pod oder jedes Netzsegment im Cluster aufrufen.
- **Maßnahmen:** (1) `RequireAuthorization` mit `Mee.API.All` bzw. einer engeren Rolle setzen. (2) In allen APIs eine Fallback-Policy `RequireAuthenticatedUser`, damit vergessene Gruppen standardmäßig geschützt sind. (3) Einen automatischen Test, der alle Endpunkte enumeriert (`EndpointDataSource`) und anonyme Endpunkte außer Health meldet. (4) Zugriffe rückwirkend prüfen (siehe 3.3).

### ES-C-02 – Grobe App-Rolle und geteilte Dienstidentität (Hoch)
- **Ist-Zustand:**
  - Das Portal (`Web/Program.cs:226-308`) hängt an die HttpClients für esData, SAP, Mule und Graph den `EsApiAuthenticationHandler`. Der holt das Secret `DataWorker-EsApi-Secret` aus dem Key Vault und fordert per Client-Credentials ein App-Token für `api://448e…/.default` an (`Web/Handler/EsApiAuthenticationHandler.cs:40-46`).
  - Die Fach-APIs prüfen nur `RequireRole("Mee.API.All")`.
  - Die ApiService akzeptiert zusätzlich die Audience `448e…` (`ApiService/Program.cs:49-55`) und behandelt jede Rolle `Mee.API.All` als Dienst mit allen Job-Rechten (`AuthorizationPolicies.IsService`).
- **Folge:** Die Berechtigungsprüfung je Person (Bestandsabfrage, Kundenanlage, Personensuche) findet nur im Portal statt. Fach-APIs und SAP sehen keinen Benutzer. Die Sicherheit aller Bank-, HR- und Kennwort-Endpunkte hängt also an der Integrität des Portal-Prozesses und eines geteilten Secrets.
- **Maßnahmen (Reihenfolge):**
  1. Eigene App-Registrierung bzw. eigenes Secret (oder Workload Identity) für das Portal, getrennt vom Worker.
  2. Fein geschnittene App-Rollen in der ES-API-Registrierung und Prüfung je Endpunkt-Gruppe, z. B. das Portal nur `Stock.Read`, `Customer.Create`, `Directory.Read`, `Mail.Send`.
  3. Die ApiService akzeptiert nur ihre eigene Audience. `IsService` verlangt `idtyp == app` (bzw. `azp` aus einer Allowlist).
  4. Mittelfristig On-Behalf-Of, damit SAP und esERP die handelnde Person kennen (Nachvollziehbarkeit).

### ES-C-03 – Kennwort-Durchreichung an esERP (Hoch)
- **Code:** `EmployeeApi.cs:20-21` mappt `POST /es/employee/password` und `/password/upsert` direkt auf `C4vApiBroker.InsertEmployeePasswordAsync` / `UpsertEmployeePasswordAsync` (`/personal/kennwort`, `/personal/kennwoerter/upsert`). Das DTO `PersonalPassword` enthält `kennwort`, `kennwortAlt`, `fehlVersuche`, `winDomaene`, `winBenutzer`, `aadObjectID`. Die esERP-Antwort wird 1:1 zurückgegeben.
- **Nutzung heute:** Nur der Worker setzt `personalNr` + `aadObjectID`. Es fließen also keine Kennwörter. Die Fähigkeit bleibt trotzdem für jeden `Mee.API.All`-Aufrufer offen.
- **Risiko:** Kennwort-Reset bzw. Kontoübernahme in esERP, Umbiegen der Entra-Zuordnung (SSO als andere Person), Zurücksetzen der Sperre nach Fehlversuchen. Kennwortfelder in Antworten oder Fehlerlogs (ES-C-05).
- **Maßnahmen:** Endpunkt durch `PUT /es/employee/{personalNr}/aad-object-id` ersetzen (DTO nur mit Objekt-ID, Antwort ohne Kennwortfelder). Kennwort-Endpunkte und `/es/TEST/*` entfernen. Beim Hersteller klären, ob `/personal/kennwoerter` Kennwörter oder Hashes zurückliefert, und gegebenenfalls das Feld in der Antwort verwerfen.

### ES-C-04 – Geteilte Mongo-Identität und ungeschützte DataProtection-Schlüssel (Hoch)
- **Code:**
  - Web, ApiService, Worker, SAP-API und Graph-API lesen dasselbe Key-Vault-Secret `MongoDBConnectionString` (`PortalDatabase.cs:42-46`, `MongoLogOptions.cs:17`, `KeyVaultMongoConnectionStringProvider`).
  - Das Portal speichert den DataProtection-Schlüsselring als Klartext-XML (`MongoDataProtectionKeyRepository.cs:23-30`) bzw. in Redis (`Program.cs:88-93`), ohne `ProtectKeysWith…`.
  - Rollen und Zuweisungen des Berechtigungssystems liegen in derselben Instanz.
- **Folge:** Jede Codeausführung in einem beliebigen Dienst reicht für eine vollständige Rechteausweitung im Portal: Rolle mit `*` zuweisen, Cookies mit dem Schlüsselring fälschen, MSAL-Refresh-Tokens aus `TokenCache` entschlüsseln.
- **Maßnahmen:** Mongo-Benutzer je Dienst nach Least Privilege (die SAP-API braucht z. B. nur `Customer-SalesOffices` lesend und Logs schreibend). `ProtectKeysWithAzureKeyVault(...)` aktivieren. Einen separaten Datenbankbenutzer für `DataProtectionKeys` und `TokenCache`, den nur das Portal kennt. Zugriffe auf die Portal-Collections auditieren.

### ES-C-05 – Downstream-Bodies in Exceptions, Logs und Antworten (Hoch, DSGVO)
- **Code:**
  - `HttpClientExtensions.cs`: Bei Fehlerstatus `new HttpResponseException(status, errorContent, …)`, d. h. Message = vollständiger Body.
  - Bei Deserialisierungsfehlern `Detail = "… Raw content: {rawContent}"`, d. h. der vollständige **Erfolgs**-Body (z. B. Kundenlisten oder Mitarbeiterdaten).
  - `ProblemExceptionHandler.cs:32-46` loggt die Exception inklusive `ToString()` (bis 32 000 Zeichen, Mongo) und schreibt die ProblemDetails an den Aufrufer.
  - `C4vAuthenticationHandler.cs:107-109` loggt die Antwort des esERP-Logins vollständig.
- **Folge:** Personenbezogene Daten und Interna liegen 90 Tage im Log (Leserecht `monitoring.log.read`). Kaskadierende Weitergabe an andere Dienste und das Portal.
- **Maßnahmen:** Raw-Content nur auf Debug und gekürzt bzw. geschwärzt loggen, nie in Message oder Detail übernehmen. Der Aufrufer bekommt eine generische Meldung mit `traceId`. Zentralen Redaction-Mechanismus (`Microsoft.Extensions.Compliance.Redaction`) für Felder wie `iban`, `email`, `kennwort*` einführen. Vorhandene Log-Einträge mit „Raw content“ suchen und bereinigen.

---

## 5. Empfohlene Reihenfolge

1. **Sofort:**
   - ES-C-01 schließen und Zugriffe prüfen.
   - Kennwort- und TEST-Endpunkte der esData-API entfernen (ES-C-03, ES-C-14).
   - Open Redirect beheben (ES-C-07).
   - Secrets rotieren (Parallelprüfung, ES-C-24).
2. **Kurzfristig (Wochen):**
   - Eigene Portal-Identität und engere App-Rollen, Audience der ApiService einschränken (ES-C-02).
   - Raw-Bodies aus Logs und Antworten entfernen (ES-C-05).
   - WHERE-Parameter der SAP-API validieren (ES-C-06).
   - Security-Header (ES-C-09).
   - `eserp.customer.add` einschränken und Rate-Limiting (ES-C-08).
3. **Mittelfristig:**
   - Mongo-Least-Privilege und Key-Vault-geschützter DataProtection-Schlüsselring (ES-C-04).
   - Lösch- und Aufbewahrungskonzept mit TTL (ES-C-11).
   - Log-Minimierung (ES-C-10).
   - Fallback-Policies und automatischer Test auf anonyme Endpunkte.
   - VVT/AVV und Datenschutzerklärung für Neukunden.

---

## Nachtrag: Gegenprüfung und Klarstellungen (07.10.2026)

| Befund | Klarstellung |
|---|---|
| ES-C-01 | **Behoben und deployt** (laut Projektleitung, 07.10.2026). |
| ES-C-02 `Mee.API.All` | Bekannt. Verschachtelte Berechtigungen waren geplant, Entra-App-Rollen unterstützen das nicht. → TODO: feinere App-Rollen bzw. Scopes je Fach-API. |
| ES-C-03 `/es/employee/password` | Im Betrieb wird **nur die AAD-Objekt-ID** übertragen (nötig für die Entra-Anmeldung am esERP). Kennwort- und Windows-Benutzer-Felder werden nie befüllt. Restrisiko: Der Endpunkt würde sie annehmen → Felder serverseitig verwerfen oder DTO auf die Objekt-ID reduzieren. Schweregrad faktisch **Mittel**. |
| DSGVO Abwesenheitsgrund | Kommt kodiert mit, wird nicht verarbeitet, liegt nur kurz im Arbeitsspeicher. **Von HR und Datenschutzbeauftragter abgenommen.** Kein offener Befund. |
| DSGVO Log | Im Log stehen Namen und E-Mails **von Mitarbeitern** (unbedenklich, 90 Tage TTL). Ausnahme: Die Neukundenanlage loggt den **Kundennamen** (`Meesenburg.ES.Web/Services/CustomerRegistrationService.cs:101`, `Meesenburg.ES.API.SAPData/Services/CustomerRegistrationService.cs:122`). Bei Privatkunden ist das eine natürliche Person → geringes Risiko; besser RequestId bzw. Kundennummer statt Name loggen. |
| DSGVO Neukundenanlage „Datenschutzerklärung fehlt“ | **Befund falsch.** Die Maske enthält Hinweise auf AGB und Datenschutz mit Pflicht-Bestätigung durch den Mitarbeiter (vgl. DESIGN_GUIDE 7.2a). Die Anlage erfolgt nur im Kundenauftrag; Information und Einwilligung des Kunden liegen vorher vor. |
| SQL in `RFC_READ_TABLE2` | Eingaben kommen heute nicht ungeprüft von extern. Risiko entsteht nur, wenn ein Aufrufer mit `Mee.API.All` (oder ein offengelegtes App-Secret) manipulierte IDs schickt. Dann ließe sich z. B. die Einschränkung `ORGANISATIONID = '298'` in `SAPBroker.Customer.cs:169` aushebeln. Schweregrad **Mittel/Niedrig**; Abhilfe: IDs vor der Verwendung auf Format prüfen (z. B. nur Ziffern, max. 10 Zeichen). |

Siehe auch den Nachtrag im Bericht `security_review_2026-10-07_secrets_infra.md` (was tatsächlich in Git liegt).
