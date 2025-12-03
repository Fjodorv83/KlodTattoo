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
// 🔍 DIAGNOSTICA AVVIO (Copia dai log se il deploy fallisce)
// ---------------------------------------------------------------------
Console.WriteLine("--------------------------------------------------");
Console.WriteLine("🚀 AVVIO APPLICAZIONE - DIAGNOSTICA VARIABILI");
var envVar = Environment.GetEnvironmentVariable("DATABASE_URL");
var railwayVar = Environment.GetEnvironmentVariable("RAILWAY_DATABASE_URL");

Console.WriteLine($"1. DATABASE_URL presente? {(string.IsNullOrEmpty(envVar) ? "NO ❌" : "SI ✅")}");
Console.WriteLine($"2. RAILWAY_DATABASE_URL presente? {(string.IsNullOrEmpty(railwayVar) ? "NO ❌" : "SI ✅")}");

if (!string.IsNullOrEmpty(envVar))
    Console.WriteLine($"   -> Valore inizia con: {envVar.Substring(0, Math.Min(envVar.Length, 15))}...");
// ---------------------------------------------------------------------

// FIX per compatibilità PostgreSQL timestamp
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Configurazione Porta
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// ---------------------------------------------------------------------
// 1. RILEVAMENTO DATABASE
// ---------------------------------------------------------------------
string? connectionString = null;
string databaseProvider = "Sqlite"; // Default

// A) PROVIAMO A LEGGERE DALLE VARIABILI D'AMBIENTE
var dbUrl = envVar ?? railwayVar; // Usiamo quelle lette sopra

if (!string.IsNullOrEmpty(dbUrl))
{
    try
    {
        // Normalizza schema
        if (dbUrl.StartsWith("postgres://"))
            dbUrl = dbUrl.Replace("postgres://", "postgresql://");

        var uri = new Uri(dbUrl);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo[0];
        var password = userInfo.Length > 1 ? userInfo[1] : "";

        connectionString =
            $"Host={uri.Host};" +
            $"Port={uri.Port};" +
            $"Database={uri.AbsolutePath.TrimStart('/')};" +
            $"Username={username};" +
            $"Password={password};" +
            $"SSL Mode=Require;Trust Server Certificate=true";

        databaseProvider = "PostgreSQL";
        Console.WriteLine($"✅ Configurazione Railway OK. Host: {uri.Host}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Errore parsing DATABASE_URL: {ex.Message}");
    }
}

// B) SE FALLISCE, USIAMO APPSETTINGS (LOCALE)
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("⚠️ Nessuna variabile ambiente valida trovata. Tentativo lettura appsettings.json...");
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    // Auto-detect PostgreSQL locale
    if (!string.IsNullOrEmpty(connectionString) &&
       (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("postgres", StringComparison.OrdinalIgnoreCase)))
    {
        databaseProvider = "PostgreSQL";
        Console.WriteLine($"🐘 Configurazione Locale PostgreSQL rilevata: {connectionString}");
    }
    else
    {
        databaseProvider = "Sqlite";
        Console.WriteLine("🗄️ Configurazione Locale SQLite (Fallback)");
    }
}

// ---------------------------------------------------------------------
// 2. CONFIGURAZIONE DbContext
// ---------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider == "PostgreSQL")
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString ?? "Data Source=klodtattoo.db");
    }
});

// Identity
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

var app = builder.Build();

// ---------------------------------------------------------------------
// MIGRATIONS
// ---------------------------------------------------------------------
using (var scope = app.Services.CreateScope())

        // ... (Seed user e tattoo styles omessi per brevità, se funzionano le migrazioni funzionerà anche questo)
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@klodtattoo.com";
        var adminPass = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            await userManager.CreateAsync(admin, adminPass);
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        string[] tattooStyles = { "Realistic", "Fine line", "Black Art", "Lettering", "Small Tattoos", "Cartoons", "Animals" };
        foreach (var t in tattooStyles)
            if (!db.TattooStyles.Any(s => s.Name == t))
                db.TattooStyles.Add(new TattooStyle { Name = t });

        await db.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ ERRORE CRITICO DATABASE");
        // Non rilanciamo l'eccezione per permettere di leggere i log
    }
}

// 🔥 FORZIAMO LA PAGINA DI ERRORE DETTAGLIATA (DEBUG)
app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();