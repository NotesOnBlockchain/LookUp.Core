using LookUp.Config;
using LookUp.Helpers;
using LookUp.Logger;
using LookUp.Website.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("LookUp", "Website"));

Logger.Initialize(dataDir);

string configFilePath = Path.Combine(dataDir, "Config.json");

WebsiteConfig config;
try
{
    config = WebsiteConfig.LoadFile(configFilePath);
}
catch (Exception ex)
{
    Logger.LogCritical(ex);
    throw;
}

builder.Services.AddSingleton(services => config);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "DataProtection")));

builder.Services.AddHttpContextAccessor();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// First in the pipeline, so everything after it sees the visitor's scheme and IP instead of the Docker gateway's.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // No UseHsts(): Nginx already sends Strict-Transport-Security, and a second header from here would conflict with it.
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.MapGet("/activefilters", () =>
{
    return Results.Json(
        HexChecker.DescribeRules(),
        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            // Keeps characters such as < > & ' readable in the rule text; the response is JSON, never embedded in HTML.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
});

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
