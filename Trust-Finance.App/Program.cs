using ApexCharts;
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TrustFinance.App.Components;
using TrustFinance.App.Services;
using TrustFinance.App.Services.Market;
using TrustFinance.Data;

// Every screen is Brazilian Portuguese: R$, dd/MM/yyyy, comma decimals. Fixed here
// rather than negotiated from the browser so the app looks the same everywhere.
var culture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

// The database is a single SQLite file in the user's profile. A connection string in
// configuration overrides it, which is how tests and unusual installs point elsewhere.
var databasePath = builder.Configuration.GetConnectionString("Default")
    ?? DatabaseLocation.ConnectionString(DatabaseLocation.DefaultPath());

// A factory rather than a scoped DbContext: in Blazor Server a scope lives as long as
// the circuit, so one shared context would accumulate tracked entities and could be
// hit by two event handlers at once. Each service call opens its own short-lived one.
builder.Services.AddDbContextFactory<TrustFinanceDbContext>(options =>
    options.UseSqlite(databasePath));

builder.Services.AddScoped<UserAccountService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<RecurrenceService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<CategoryRuleService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ThemeState>();
builder.Services.AddScoped<UiState>();
builder.Services.AddSingleton(TimeProvider.System);

// Investing, planning and the notification tray.
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<InvestmentService>();
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<CorporateActionService>();
builder.Services.AddSingleton<BcbSeries>();
builder.Services.AddScoped<FixedIncomeService>();
builder.Services.AddScoped<WatchlistService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<MarketData>();
builder.Services.AddMemoryCache();

// Quotes are cached per user, so this client is only reached on a real miss. The short
// timeout is deliberate: a slow provider must not hold a page open, it must degrade.
builder.Services.AddHttpClient(nameof(BrapiMarketData), client =>
{
    client.BaseAddress = new Uri(BrapiMarketData.BaseAddress);
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TrustFinance/1.0");
});

// The central bank's open-data API needs no token; a short timeout keeps a slow day
// from holding the portfolio page, which falls back to a flat estimate.
builder.Services.AddHttpClient(BcbSeries.ClientName, client =>
{
    client.BaseAddress = new Uri(BcbSeries.BaseAddress);
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TrustFinance/1.0");
});

// Authentication is still being decided, so it can be stood down with a flag. The cookie
// scheme below is registered either way and is the default whenever the flag is off.
var bypass = new AuthBypass { Enabled = builder.Configuration.GetValue<bool>("Auth:Bypass") };
builder.Services.AddSingleton(bypass);

var authentication = builder.Services
    .AddAuthentication(bypass.Enabled ? AuthBypass.SchemeName : CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "tf.auth";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

if (bypass.Enabled)
    authentication.AddScheme<AuthenticationSchemeOptions, BypassAuthenticationHandler>(AuthBypass.SchemeName, null);

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddApexCharts();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// The schema is applied on every start: a fresh install gets its tables, an upgrade
// gets its new columns, and nobody has to know what a migration is.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TrustFinanceDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database: {DataSource}", db.Database.GetDbConnection().DataSource);

    if (bypass.Enabled)
    {
        await bypass.EnsureUserAsync(db);
        app.Logger.LogWarning(
            "Auth:Bypass is ON — every request runs as {Email} and the sign-in screens are skipped.",
            AuthBypass.LocalEmail);

        // The public demo: an empty ledger is filled with a plausible year so the app
        // opens on something to look at. Only ever into an empty one, so a real install
        // that happens to run with the flag on is never written over.
        if (builder.Configuration.GetValue<bool>("Demo:Enabled"))
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            await DemoData.SeedAsync(db, bypass.UserId, today, app.Logger);
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/erro", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/nao-encontrado", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

// Signing out is a cookie operation, and cookies can only be written on a real HTTP
// response, not from inside an interactive circuit — hence an endpoint, not a component.
app.MapPost("/sair", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = app.Urls.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
              ?? app.Urls.FirstOrDefault();
    if (url is null) return;

    // Kestrel reports the wildcard it bound to, which is not something a browser can open.
    url = url.Replace("0.0.0.0", "localhost").Replace("[::]", "localhost").Replace("127.0.0.1", "localhost");
    app.Logger.LogInformation("Trust Finance is running at {Url}", url);

    // Someone who double-clicked the executable is waiting for a window to appear — but
    // a container has no desktop to open one on, and the attempt only costs a warning.
    // Set Browser:Launch to false wherever nobody is sitting in front of the process.
    var launchBrowser = app.Configuration.GetValue("Browser:Launch", !app.Environment.IsDevelopment());
    if (launchBrowser)
        BrowserLauncher.TryOpen(url, app.Logger);
});

app.Run();

// Top-level statements compile to an internal Program, which WebApplicationFactory
// cannot reach. Making it public is the documented way to let the integration tests
// boot this exact file rather than a copy of its wiring.
public partial class Program;
