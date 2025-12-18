using KlodTattoo.Data.Helper;
using KlodTattooWeb.Data;
using KlodTattooWeb.Models;
using KlodTattooWeb.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);


var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    Console.WriteLine($"🚀 Server configurato per ascoltare sulla porta: {port}");
}

// ----------------------------------------------------------
// LOGGING CONFIGURATION
// ----------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// ----------------------------------------------------------
// DATABASE CONFIG (IONOS MSSQL)
// ----------------------------------------------------------
var dbProvider = builder.Configuration["ConnectionStrings:DatabaseProvider"] ?? "Mssql";
string connectionString = "";

// Config File (appsettings.json)
if (dbProvider.Equals("Mssql", StringComparison.OrdinalIgnoreCase))
{
    connectionString = builder.Configuration.GetConnectionString("MssqlConnection") ?? "";
    Console.WriteLine("🗄️ MSSQL Database (IONOS)");
}
else if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    connectionString = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=klodtattoo.db";
    Console.WriteLine("📂 SQLite Database");
}
else // Fallback/Other
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
    Console.WriteLine("🐘 Database (Custom/Legacy)");
}



// Fix timestamp PostgreSQL (solo se servisse, innocuo altrove)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ----------------------------------------------------------
// SERVICES
// ----------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider.Equals("Mssql", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
    }
    else if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseNpgsql(connectionString);
    }

    options.EnableSensitiveDataLogging()
           .LogTo(Console.WriteLine, LogLevel.Information);
});

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { "de-DE", "it-IT", "en" };
    options.DefaultRequestCulture = new RequestCulture("de-DE");
    options.SupportedCultures = cultures.Select(c => new CultureInfo(c)).ToList();
    options.SupportedUICultures = cultures.Select(c => new CultureInfo(c)).ToList();
});

// --- CONFIGURAZIONE EMAIL AGGIORNATA ---
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// 2. Per Identity (Interfaccia generica)
builder.Services.AddTransient<IEmailSender, EmailSender>();

// 2. Per il BookingController (Classe concreta - FONDAMENTALE PER IL REPLY-TO)
builder.Services.AddTransient<EmailSender>();
// ---------------------------------------
builder.Services.AddControllersWithViews().AddViewLocalization();
builder.Services.AddRazorPages();

var app = builder.Build();

// ----------------------------------------------------------
// MIDDLEWARE CONFIGURATION
// ----------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    
}

// Compressione Risposte (Performance)
app.UseResponseCompression();

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseAuthentication();
app.UseAuthorization();

// ----------------------------------------------------------
// DATABASE SEEDING - USA IL TUO SEEDER DEDICATO
// ----------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        // Usa il tuo DatabaseSeeder che ha già il reset password!
        await DatabaseSeeder.SeedAsync(services, logger);
    }
    catch (Exception ex)
    {
        logger.LogError($"❌ ERRORE CRITICO NEL SEEDING: {ex}");
        // In produzione, potrebbe essere meglio lanciare l'eccezione
        // per far fallire il deploy se il seeding è critico
        throw;
    }
}

app.MapRazorPages();
app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

Console.WriteLine($"✅ Applicazione pronta e in ascolto sulla porta {port}");
app.Run();