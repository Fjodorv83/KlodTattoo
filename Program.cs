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
// LOG DIAGNOSTICO AVVIO
// ---------------------------------------------------------------------
var dbEnvVar = Environment.GetEnvironmentVariable("DATABASE_URL");
Console.WriteLine($"🔍 [BOOT] DATABASE_URL trovata? {(string.IsNullOrEmpty(dbEnvVar) ? "NO ❌" : "SI ✅")}");

// FIX per compatibilità PostgreSQL timestamp
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Configurazione Porta
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// ---------------------------------------------------------------------
// CONFIGURAZIONE DATABASE (SOLO POSTGRESQL)
// ---------------------------------------------------------------------
string connectionString;

if (!string.IsNullOrEmpty(dbEnvVar))
{
    // CASO 1: RAILWAY (Produzione)
    try
    {
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

        Console.WriteLine($"🐘 [BOOT] Configurazione Railway Attiva. Host: {uri.Host}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ [BOOT] Errore parsing URL Railway: {ex.Message}. Uso stringa locale.");
        connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }
}
else
{
    // CASO 2: LOCALE (Sviluppo)
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    Console.WriteLine("🐘 [BOOT] Configurazione Locale (PostgreSQL)");
}

// FORZIAMO SEMPRE POSTGRESQL
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// ---------------------------------------------------------------------
// SETUP SERVIZI
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

// Debug errori dettagliati
app.UseDeveloperExceptionPage();

// ---------------------------------------------------------------------
// MIGRATIONS
// ---------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        Console.WriteLine($"🔄 [MIGRATION] Tentativo migrazione su PostgreSQL...");
        await db.Database.MigrateAsync();
        Console.WriteLine("✅ [MIGRATION] Successo!");

        // Seed
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

        string[] tattooStyles = { "Realistic", "Fine line", "Black Art", "Lettering", "Small Tattoos", "Cartoons", "Animals" };
        foreach (var t in tattooStyles)
            if (!db.TattooStyles.Any(s => s.Name == t)) db.TattooStyles.Add(new TattooStyle { Name = t });

        await db.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ [MIGRATION ERROR] {ex.Message}");
    }
}

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