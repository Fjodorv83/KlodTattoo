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
// 🔍 LOG DIAGNOSTICO AVVIO
// ---------------------------------------------------------------------
var dbEnvVar = Environment.GetEnvironmentVariable("DATABASE_URL");
Console.WriteLine($"🔍 [BOOT] DATABASE_URL trovata? {(string.IsNullOrEmpty(dbEnvVar) ? "NO ❌" : "SI ✅")}");
if (!string.IsNullOrEmpty(dbEnvVar))
{
    // Maschero la password per sicurezza nei log
    var safeLog = System.Text.RegularExpressions.Regex.Replace(dbEnvVar, @":[^/]+@", ":***@");
    Console.WriteLine($"🔍 [BOOT] Valore: {safeLog}");
}

// FIX per compatibilità PostgreSQL timestamp
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Configurazione Porta
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// ---------------------------------------------------------------------
// CONFIGURAZIONE DATABASE
// ---------------------------------------------------------------------
string connectionString;
string databaseProvider;

if (!string.IsNullOrEmpty(dbEnvVar))
{
    // CASO 1: RAILWAY (Produzione)
    try
    {
        // Normalizza lo schema (postgres:// -> postgresql://)
        var validUrl = dbEnvVar.StartsWith("postgres://")
            ? dbEnvVar.Replace("postgres://", "postgresql://")
            : dbEnvVar;

        var uri = new Uri(validUrl);
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
        Console.WriteLine($"🐘 [BOOT] Configurazione Railway Attiva. Host: {uri.Host}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ [BOOT] Errore parsing URL Railway: {ex.Message}. Passo al fallback.");
        connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        databaseProvider = "Sqlite";
    }
}
else
{
    // CASO 2: LOCALE (Sviluppo)
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    databaseProvider = "Sqlite";
    Console.WriteLine("🗄️ [BOOT] Configurazione Locale (SQLite)");
}

// AddDbContext
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider == "PostgreSQL")
        options.UseNpgsql(connectionString);
    else
        options.UseSqlite(connectionString);
});

// ---------------------------------------------------------------------
// SETUP SERVIZI (Identity, MVC, ecc.)
// ---------------------------------------------------------------------
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews().AddViewLocalization();
builder.Services.AddRazorPages();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { "de-DE", "it-IT" };
    options.DefaultRequestCulture = new RequestCulture("de-DE");
    options.SupportedCultures = cultures.Select(c => new CultureInfo(c)).ToList();
    options.SupportedUICultures = cultures.Select(c => new CultureInfo(c)).ToList();
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

var app = builder.Build();

// 🔥 DEBUG: Mostra errori dettagliati anche in produzione per ora
app.UseDeveloperExceptionPage();

// ---------------------------------------------------------------------
// MIGRAZIONI AUTOMATICHE
// ---------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        Console.WriteLine($"🔄 [MIGRATION] Tentativo connessione a: {databaseProvider}");
        await db.Database.MigrateAsync();
        Console.WriteLine("✅ [MIGRATION] Successo!");

        // Seed Ruoli e Utente Admin
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole(role));

        var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@klodtattoo.com";
        var adminPass = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            await userManager.CreateAsync(admin, adminPass);
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // Seed Stili
        string[] tattooStyles = { "Realistic", "Fine line", "Black Art", "Lettering", "Small Tattoos", "Cartoons", "Animals" };
        foreach (var t in tattooStyles)
            if (!db.TattooStyles.Any(s => s.Name == t)) db.TattooStyles.Add(new TattooStyle { Name = t });

        await db.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ [MIGRATION ERROR] {ex.Message}");
        // Non blocchiamo l'app, ma vedremo l'errore nei log o a video
    }
}

// Middleware
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