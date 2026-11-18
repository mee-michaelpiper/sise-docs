# Portal-Suche (Strg+K / ⌘K / „/“)

Aufbau nach dem Raycast/Alfred-Prinzip: **ein Schema, viele Lieferanten, Suche im Speicher.**

```
Quellen (SAP, esERP, Portal-Daten)
   │  Worker-Job „Suchindex“ (JobType.SearchIndex, Bereich „System“) – ein Provider je Typ
   ▼
Mongo „Prod-ES-Search“ / „Dev-ES-Search“
   SearchDocuments  { _id "{type}:{key}", Type, Key, Title, Subtitle, Keywords, Tags{…}, ChangedAtUtc, IndexedAtUtc }
   SearchMeta       { _id type, Version, Hash, Count, BuiltAtUtc }   ← Version ändert sich nur bei neuem Inhalt
   SearchUsage      { _id "{user}|{docId}", Count, LastUtc }          ← Frecency je Person
   │  Portal prüft jede Minute SearchMeta, lädt nur geänderte Typen
   ▼
PortalSearchIndex (Web, im Speicher, FuzzyIndex je Typ) → QuickSearchService → QuickSearchDialog
```

- **Tippfehler/Umlaute:** `SearchText`/`FuzzyIndex` (ServiceDefaults/Search): Trigramme, ≥ 50 % je Wort, ä→ae usw.; Nummern über `Key`.
- **Frecency:** Score × (1 + 0,25 × min(4, Anzahl × 0,5^(Tage/14))).
- **Rechte:** zur Suchzeit über `Tags` (z. B. `clerk` = Mail des Sachbearbeiters bei Rechnungen) und den Bereich (`QuickSearchScope.Allowed`).
- **Ergänzung:** Was nicht im Index ist (Kunden anderer VKORG, Artikel ohne PRAT8), findet die direkte SAP-Suche (`/sap/search/customers|articles`), nur wenn der Index zu wenig liefert.
- **Lauf:** Zeitplan in der Jobverwaltung (empfohlen alle 30 Min.). Unverändert = Prüfsumme gleich → nichts geschrieben, Portale laden nicht neu. Leere Lieferung einer Quelle = Fehler, Index bleibt.
- **Überwachung:** Health-Check „Suchindex (Portal-Suche)“ im Portal (Dashboard → Systemstatus), älter als 90 Min. = Warnung.

## Neuen Suchtyp anbinden (bei jeder neuen Funktion mit eigenen Daten prüfen!)

1. **Typ** in `SearchDocumentTypes` (ServiceDefaults/Search/SearchStore.cs) ergänzen.
2. **Provider** im Worker (`ESDataWorker/Services/Search/SearchIndexProviders.cs`): `ISearchIndexProvider` mit `Type`, `Source`, `LoadAsync` (vollständiger Bestand). Title = was man sieht, Subtitle = zweite Zeile, Keywords = zusätzliche Suchwörter, Tags = Rechte/Status.
   Registrieren in `ESDataWorker/Program.cs` (`AddScoped<ISearchIndexProvider, …>`).
3. **Quelle:** braucht es einen Endpunkt (sapdata `/sap/search/export/…`, esERP-Liste gepaged mit „geändert seit“ – siehe docs/TODO-com4vision.md)?
4. **Portal:** Bereich in `QuickSearchService.Scopes` (Schlüsselwörter, Recht), Fall in `SearchScopedAsync` (`Ranked(type, …)` + Ziel-URL), Name in `JobUi.SearchTypeName`.
5. **Legt das Portal selbst etwas an** (Neukunde, Freigabe, Import …): direkt danach `PortalSearchIndex.AddAsync(type, "portal", doc)` – sofort im eigenen Pod, in den anderen binnen einer Minute (Einzelaufnahmen über `IndexedAtUtc`), Prüfsumme wird verworfen, der nächste volle Lauf gleicht ab. Fehler dabei werden nur geloggt. Heute: Neukunde anlegen (`CustomerRegistrationService`), Artikel fürs ES freigeben (`RecordCheckService`).
6. **Neuigkeiten:** Eintrag „Portal-Suche“ in den Release Notes.

Geplant: Angebote und Aufträge (esERP, sobald com4vision gepagte Listen liefert), Lieferanten.
