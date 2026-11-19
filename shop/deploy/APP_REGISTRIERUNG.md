# App-Registrierung „SiSe-Shop (Test)“ (Bericht 22, V-4)

Stand 17.11.2026. Für den Testzugang vor dem .NET-Shop (`SiseShop:TestGate`) und die Mitarbeiter-Anmeldung im Shop-Admin, solange dieser noch im Shop liegt.

## In Entra anlegen

1. **App-Registrierungen → Neue Registrierung**
   - Name: `SiSe-Shop (Test)`
   - Kontotypen: **Nur Konten in diesem Organisationsverzeichnis** (ein Mandant)
   - Umleitungs-URI: Plattform **Web**, `https://<test-adresse>/signin-oidc-testzugang`
2. **Authentifizierung**
   - Weitere Umleitungs-URIs (Web):
     - `https://<test-adresse>/signin-oidc` (Mitarbeiter-Anmeldung im Shop-Admin)
     - `http://localhost:5099/signin-oidc-testzugang` (lokaler Test, kann nach der Abnahme raus)
     - `https://localhost:7240/signin-oidc` (lokale Admin-Anmeldung – seit 19.11.2026 nötig, weil es keine Test-Personen mehr gibt)
   - **Implizite Gewährung und Hybridflows: „ID-Token“ anhaken.** Der Shop holt nur ein ID-Token (`response_type=id_token`, form_post). Ohne Haken bricht die Anmeldung mit AADSTS700054 ab.
   - Kein Client-Geheimnis nötig.
3. **API-Berechtigungen:** nur `openid` und `profile` (Microsoft Graph `User.Read` als Standard kann bleiben). Administratorzustimmung erteilen, damit niemand gefragt wird.
4. **Unternehmensanwendung** (gleicher Name) → Eigenschaften:
   - **Zuweisung erforderlich: Ja.** Das ist die eigentliche Sperre: Nur zugewiesene Personen bekommen überhaupt ein Token.
   - Für Benutzer sichtbar: Nein.
5. **Benutzer und Gruppen:** die Gruppe `grp_mee_sise_shop_test` (SN24-Team) und `grp_mee_sise_developer` zuweisen.

`<test-adresse>` ist der DNS-Name aus V-6, z. B. `shop-test.meesenburg-service.de`. Weitere URIs lassen sich später ergänzen.

## Was ich brauche

Nur **Mandanten-ID** und **Anwendungs-ID (Client-ID)**. Beides ist kein Geheimnis und kommt in die Konfiguration:

```
SiseShop__TestGate__Enabled=true
SiseShop__TestGate__AzureAd__Instance=https://login.microsoftonline.com/
SiseShop__TestGate__AzureAd__TenantId=<Mandanten-ID>
SiseShop__TestGate__AzureAd__ClientId=<Client-ID>
# optional zusätzlich zur Zuweisung: nur diese Gruppen (Objekt-IDs) bzw. App-Rollen
# SiseShop__TestGate__AllowedGroups__0=<Objekt-ID grp_mee_sise_shop_test>
```

`AllowedGroups` bleibt leer, wenn „Zuweisung erforderlich“ an ist. Sonst müsste die App auch Gruppen-Claims ausgeben (Tokenkonfiguration → Gruppenanspruch), und das wird bei vielen Gruppen unübersichtlich.

## Verhalten

- Ohne Anmeldung leitet jede Adresse zu Microsoft um, auch Bilder, Sitemap und Feeds. Ausnahmen sind nur `/health`, `/alive` (Probes) und der Rückweg `/signin-oidc-testzugang`.
- Jede Antwort trägt `X-Robots-Tag: noindex, nofollow, noarchive`. `robots.txt` liefert `Disallow: /`.
- Ist der Schalter an, aber keine Client-ID gesetzt, startet der Shop nicht (lieber aus als offen).
- Im Shop gibt es auf „Anmelden“ die Testkonten-Schnellanmeldung. Die Shop-API braucht dafür `Customers__TestAccountsEnabled=true` und die Testkonten aus `appsettings.Development.json`.

## Später entfernen

- **Abschalten:** `SiseShop__TestGate__Enabled=false`.
- **Ausbauen:**
  - `src/Meesenburg.Shop.Portal/Security/TestGate.cs` löschen;
  - in `Program.cs` die Zeilen `builder.AddTestGate();` und `app.UseTestGate();` löschen;
  - in `ShopLogin.razor` die Bedingung `Gate.Enabled` entfernen.
- Danach die App-Registrierung löschen oder für den Live-Betrieb umbenennen.
