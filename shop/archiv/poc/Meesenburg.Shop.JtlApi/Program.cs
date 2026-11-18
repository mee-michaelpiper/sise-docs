using Azure.Identity;
using Meesenburg.Shop.JtlApi.Apis;
using Meesenburg.Shop.JtlApi.Brokers.Jtl;
using Meesenburg.Shop.JtlApi.Options;
using Meesenburg.Shop.JtlApi.Services;
using Meesenburg.Shop.JtlApi.Services.Contracts;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Secrets: optional aus dem Key Vault (Secret-Namen mit "--", z. B. "Jtl--ApiToken"), lokal per az login / DefaultAzureCredential
if (builder.Configuration["KeyVault:Uri"] is { Length: > 0 } kvUri)
    builder.Configuration.AddAzureKeyVault(new Uri(kvUri), new DefaultAzureCredential());

builder.Services.Configure<JtlOptions>(builder.Configuration.GetSection(JtlOptions.SectionName));
builder.Services.Configure<CustomApiOptions>(builder.Configuration.GetSection(CustomApiOptions.SectionName));
builder.Services.Configure<SnapshotOptions>(builder.Configuration.GetSection(SnapshotOptions.SectionName));
builder.Services.Configure<JtlSearchOptions>(builder.Configuration.GetSection(JtlSearchOptions.SectionName));

var useSampleData = builder.Configuration.GetValue<bool>("Jtl:UseSampleData");
builder.Services.AddSingleton(new SampleDataInfo(useSampleData));

if (useSampleData)
{
    builder.Services.AddSingleton<IJtlWawiBroker, SampleJtlWawiBroker>();
}
else
{
    // Nur GET-Abfragen: Wiederholungen sind unkritisch (lesend). Timeout je Versuch aus der Konfiguration.
    builder.Services.AddHttpClient<IJtlWawiBroker, JtlWawiBroker>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<JtlOptions>>().Value;
            if (!o.IsConfigured)
                throw new InvalidOperationException("JTL-Zugang fehlt: Jtl:BaseUrl und Jtl:ApiToken setzen (User-Secrets, Umgebung oder Key Vault) – oder Jtl:UseSampleData=true.");
            client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/" + o.BasePath.Trim('/') + "/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("X-AppId", o.AppId);
            client.DefaultRequestHeaders.Add("X-AppVersion", o.AppVersion);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", o.ApiToken);
        })
        .AddStandardResilienceHandler(o =>
        {
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Jtl:TimeoutSeconds", 30));
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Jtl:TimeoutSeconds", 30) * 3);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(builder.Configuration.GetValue("Jtl:TimeoutSeconds", 30) * 2);
        });
}

builder.Services.AddHttpClient<ICustomApiBroker, CustomApiBroker>((sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<CustomApiOptions>>().Value;
    client.BaseAddress = new Uri((o.IsConfigured ? o.BaseUrl : "http://localhost").TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    if (o.IsConfigured) client.DefaultRequestHeaders.Add("x-token", o.ApiToken);
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITaxService, TaxService>();
builder.Services.AddTransient<IArticleService, ArticleService>();
builder.Services.AddSingleton<ISnapshotStore, FileSnapshotStore>();
builder.Services.AddTransient<IContractCheckService, ContractCheckService>();
builder.Services.AddTransient<IConnectionService, ConnectionService>();

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<JtlExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.Servers = []);
}

app.MapHealthChecks("/health");
app.MapArticleApiV1();
app.MapContractApiV1();
app.MapStatusApiV1();
app.MapCustomApiV1();

app.Run();
