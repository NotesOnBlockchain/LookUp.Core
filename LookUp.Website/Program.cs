using LookUp.Website.Components;
using LookUp.Helpers;
using LookUp.Config;
using LookUp.Logger;

var builder = WebApplication.CreateBuilder(args);

string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("LookUp", "Website"));

Logger.Initialize(Path.Combine(dataDir, "Logs.txt"));

string configFilePath = Path.Combine(dataDir, "Config.json");

WebsiteConfig config = WebsiteConfig.LoadFile(configFilePath);
builder.Services.AddSingleton(services => config);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped(sp => new HttpClient());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
