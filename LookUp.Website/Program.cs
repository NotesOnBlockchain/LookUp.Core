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
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
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

app.UseForwardedHeaders();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
