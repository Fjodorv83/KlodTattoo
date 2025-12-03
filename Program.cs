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

// ---------------------------------------------------------------------
// FIX per compatibilità PostgreSQL timestamp
// ---------------------------------------------------------------------
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ---------------------------------------------------------------------
// Configurazione Porta per Railway
// ---------------------------------------------------------------------
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// ---------------------------------------------------------------------
// 1. RILEVAMENTO DATABASE (Railway vs Locale)
// ---------------------------------------------------------------------
string? connectionString = null;
string databaseProvider = "Sqlite"; // Default

// Cerca la variabile d'ambiente (Railway usa DATABASE_URL)
var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("RAILWAY_DATABASE_URL");

if (!string.IsNullOrEmpty(dbUrl))
{
    // SIAMO SU RAILWAY
    try
    {
        // Normalizza schema (postgres:// -> postgresql://)
        if (dbUrl.StartsWith("postgres://"))
            dbUrl = dbUrl.Replace("postgres://", "postgresql://");

        var uri = new Uri(dbUrl);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo[0];
        var password = userInfo.Length > 1 ? userInfo[1] : "";

        // Costruisci la stringa di connessione corretta per Npgsql
        connectionString =
            $"Host={uri.Host};" +
            $"Port={uri.Port};" +
            $"Database={uri.AbsolutePath.TrimStart('/')};" +
            $"Username={username};" +
            $"Password={password};" +
            $"SSL Mode=Require;Trust Server Certificate=true";

        databaseProvider = "PostgreSQL";
        Console.WriteLine($"🐘 [Boot] Configurazione Railway rilevata. Host: {uri.Host}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ [Boot] Errore parsing DATABASE_URL: {ex.Message}");
    }
}
else
{
    // SIAMO IN LOCALE
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    // Auto-detect: se la stringa locale contiene "Host=", stiamo usando Postgres locale
    if (!string.IsNullOrEmpty(connectionString) &&
       (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("postgres", StringComparison.OrdinalIgnoreCase)))
    {
        databaseProvider = "PostgreSQL";
        Console.WriteLine("🐘 [Boot] Configurazione Locale (PostgreSQL)");
    }
    else
    {
        Console.WriteLine("🗄️ [Boot] Configurazione Locale (SQLite)");
    }
}

// ---------------------------------------------------------------------
// 2. CONFIGURAZIONE DbContext
// ---------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider == "PostgreSQL")
    {
        // Usa la connectionString calcolata sopra (che ora è corretta!)
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString ?? "Data Source=klodtattoo.db");
    }
});

// ---------------------------------------------------------------------
// Identity
// ---------------------------------------------------------------------
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// MVC + Razor
builder.Services.AddControllersWithViews().AddViewLocalization();
builder.Services.AddRazorPages();

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { "de-DE", "it-IT" };
    options.DefaultRequestCulture = new RequestCulture("de-DE");
    options.SupportedCultures = cultures.Select(c => new CultureInfo(c)).ToList();
    options.SupportedUICultures = cultures.Select(c => new CultureInfo(c)).ToList();
});

// Email
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

// ---------------------------------------------------------------------
// BUILD APP
// ---------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------
// Apply Migrations + Seed
// ---------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<AppDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("📦 Database migrato correttamente");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Errore durante la migration del database.");
    }

    // Ruoli
    string[] roles = { "Admin", "User" };
    foreach (var role in roles)
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

    // Admin
    var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@klodtattoo.com";
    var adminPass = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123";

    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(admin, adminPass);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }

    // Seed Tattoo Styles
    string[] tattooStyles = { "Realistic", "Fine line", "Black Art", "Lettering", "Small Tattoos", "Cartoons", "Animals" };
    foreach (var t in tattooStyles)
        if (!db.TattooStyles.Any(s => s.Name == t))
            db.TattooStyles.Add(new TattooStyle { Name = t });

    await db.SaveChangesAsync();
}

// ---------------------------------------------------------------------
// Middleware
// ---------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();