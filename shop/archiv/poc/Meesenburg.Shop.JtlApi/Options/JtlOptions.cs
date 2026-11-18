namespace Meesenburg.Shop.JtlApi.Options;

/// Zugang zur offiziellen JTL-Wawi-REST-API (eazybusiness).
/// Werte kommen aus User-Secrets, Umgebungsvariablen (Jtl__ApiToken) oder dem Key Vault (Secret "Jtl--ApiToken") – nie aus appsettings.json.
public sealed class JtlOptions
{
    public const string SectionName = "Jtl";

    /// z. B. http://c10330.servers.ecomdata.de:50443
    public string BaseUrl { get; set; } = "";

    public string BasePath { get; set; } = "/api/eazybusiness";

    /// Vollständiger Header-Wert, z. B. "Wawi 1234…"
    public string ApiToken { get; set; } = "";

    public string AppId { get; set; } = "";

    public string AppVersion { get; set; } = "1.0.0";

    /// Firma für die Steuersatz-Abfrage. Das Alt-Backend nutzt fest 6 (global_data_logic.py).
    public int CompanyId { get; set; } = 6;

    public int TimeoutSeconds { get; set; } = 30;

    /// Shop-Kanal "JTL-Shop: sicherheit.meesenburg.com". Ohne diesen Filter bricht /v1/items mit HTTP 500 ab,
    /// sobald ein Artikel ohne Kategorie auf der Seite liegt (Bericht 13, J-01). Leer = ohne Filter (alle Artikel).
    public string SalesChannelId { get; set; } = "2-2-5";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiToken);
}

/// Zusatz-API des Alt-Dienstleisters auf dem JTL-Server (Konfigurationsgruppen, Kategorien, Versand …).
public sealed class CustomApiOptions
{
    public const string SectionName = "CustomApi";

    public string BaseUrl { get; set; } = "";

    public string ApiToken { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiToken);
}

public sealed class SnapshotOptions
{
    public const string SectionName = "Snapshots";

    /// Ablage der Schnittstellen-Stände (JSON-Dateien). Im AKS später ein Volume oder Blob Storage.
    public string Directory { get; set; } = "data/snapshots";
}
