using KlodTattooWeb.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using KlodTattooWeb.Services;
using KlodTattooWeb.Models;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------
// Configurazione Porta per Railway
// ---------------------------
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// ---------------------------
// Database - Supporto SQLite (dev) e PostgreSQL (prod)
// ---------------------------
var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";

// Scegli la connection string in base al provider
var connectionString = databaseProvider == "PostgreSQL"
    ? builder.Configuration.GetConnectionString("PostgreSQL")
    : builder.Configuration.GetConnectionString("DefaultConnection");

// Supporto per DATABASE_URL di Railway (PostgreSQL)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    // Railway fornisce DATABASE_URL in formato: postgres://user:password@host:port/database
    // Convertiamo in formato connection string per Npgsql
    var databaseUri = new Uri(databaseUrl);
    var userInfo = databaseUri.UserInfo.Split(':');

    connectionString = $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseUri.LocalPath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
    databaseProvider = "PostgreSQL";
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider == "PostgreSQL")
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// ---------------------------
// Identity con RUOLI
// ---------------------------
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ---------------------------
// Razor + MVC
// ---------------------------
builder.Services.AddControllersWithViews()
    .AddViewLocalization();
builder.Services.AddRazorPages();

// ---------------------------
// Localization (IT/DE)
// ---------------------------
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "de-DE", "it-IT" };
    options.DefaultRequestCulture = new RequestCulture("de-DE");  // Tedesco default
    options.SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
    options.SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();

    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

// ---------------------------
// Cookie Policy
// ---------------------------
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    // This lambda determines whether user consent for non-essential cookies is needed for a given request.
    options.CheckConsentNeeded = context => true;
});

// ---------------------------
// Email Sender
// ---------------------------
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

var app = builder.Build();

// ---------------------------
// SEED: Ruoli e Admin/User + Migrazione Database
// ---------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AppDbContext>();

    // Applica migrazioni automaticamente in produzione
    try
    {
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Errore durante l'applicazione delle migrazioni del database");
    }

    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = { "Admin", "User" };

    // Crea ruoli se non esistono
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // ---------------------------
    // CREA ADMIN (usa variabili d'ambiente in produzione)
    // ---------------------------
    var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@klodtattoo.com";
    var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        await userManager.CreateAsync(adminUser, adminPassword);
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    // ---------------------------
    // CREA UTENTE STANDARD (opzionale)
    // ---------------------------
    var defaultEmail = "user@klodtattoo.com";
    var defaultUser = await userManager.FindByEmailAsync(defaultEmail);

    if (defaultUser == null)
    {
        defaultUser = new IdentityUser
        {
            UserName = defaultEmail,
            Email = defaultEmail,
            EmailConfirmed = true
        };

        await userManager.CreateAsync(defaultUser, "User@123");
        await userManager.AddToRoleAsync(defaultUser, "User");
    }

    // ---------------------------
    // SEED: Tattoo Styles
    // ---------------------------
    string[] tattooStyleNames = { "Realistic", "Fine line", "Black Art", "Lettering", "Small Tattoos", "Cartoons","Animals", "Tribals" };

    foreach (var styleName in tattooStyleNames)
    {
        if (!await dbContext.TattooStyles.AnyAsync(s => s.Name == styleName))
        {
            dbContext.TattooStyles.Add(new TattooStyle { Name = styleName });
        }
    }
    await dbContext.SaveChangesAsync();
}

// ---------------------------
// Middleware pipeline
// ---------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCookiePolicy();

// Localization middleware
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Area routing (must be before default route)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
