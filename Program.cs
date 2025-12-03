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
string? connectionString = null;

// PRIORITÀ 1: Controlla DATABASE_URL (Railway/produzione)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
                 ?? Environment.GetEnvironmentVariable("RAILWAY_DATABASE_URL");

if (!string.IsNullOrEmpty(databaseUrl))
{
    try
    {
        // Alcune piattaforme usano "postgres://" altre "postgresql://"
        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            databaseUrl = "postgresql://" + databaseUrl.Substring("postgres://".Length);
        }

        var databaseUri = new Uri(databaseUrl);

        var userInfo = databaseUri.UserInfo.Split(new[] { ':' }, 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

        var host = databaseUri.Host;
        var dbport = databaseUri.Port > 0 ? databaseUri.Port : 5432;
        var database = databaseUri.AbsolutePath.TrimStart('/');

        connectionString =
            $"Host={host};Port={dbport};Database={database};Username={username};Password={password};SSL Mode=Prefer;Trust Server Certificate=true";

        databaseProvider = "PostgreSQL";

        Console.WriteLine($"✅ Usando DATABASE_URL di Railway: host={host}, db={database}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Errore nel parsing di DATABASE_URL: {ex}");
    }
}

else
{
    // PRIORITÀ 2: Locale - usa appsettings.json
    if (databaseProvider == "PostgreSQL")
    {
        connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
        Console.WriteLine("✅ Usando PostgreSQL locale da appsettings.json");
    }
    else
    {
        connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine("✅ Usando SQLite locale da appsettings.json");
    }
}

// Verifica che ci sia una connection string
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("❌ Nessuna connection string configurata!");
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider == "PostgreSQL")
    {
        Console.WriteLine($"🐘 Configurando PostgreSQL");
        options.UseNpgsql(connectionString);
    }
    else
    {
        Console.WriteLine($"🗄️ Configurando SQLite");
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
// SEED: Ruoli e Admin/User + Verifica Database
// ---------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AppDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    // Verifica e crea il database se non esiste
    try
    {
        // DEBUG: Mostra quale connection string viene usata (CENSURA LA PASSWORD!)
        var connString = dbContext.Database.GetConnectionString();
        if (!string.IsNullOrEmpty(connString))
        {
            // Censura la password nel log
            var censored = System.Text.RegularExpressions.Regex.Replace(
                connString,
                @"Password=([^;]+)",
                "Password=***"
            );
            logger.LogInformation($"🔗 Connection string: {censored}");
        }

        // Verifica connessione con retry
        bool canConnect = false;
        int maxRetries = 5;
        int retryDelay = 2000; // 2 secondi

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                canConnect = await dbContext.Database.CanConnectAsync();
                if (canConnect)
                {
                    logger.LogInformation($"📡 Connessione database: OK ✅");
                    break;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"⚠️ Tentativo {i + 1}/{maxRetries} fallito: {ex.Message}");
                if (i < maxRetries - 1)
                {
                    logger.LogInformation($"⏳ Attendo {retryDelay}ms prima di ritentare...");
                    await Task.Delay(retryDelay);
                }
            }
        }

        if (!canConnect)
        {
            logger.LogError("❌ Impossibile connettersi al database dopo tutti i tentativi");
            logger.LogError("💡 Verifica che DATABASE_URL sia configurata correttamente su Railway");
            logger.LogError("💡 Controlla che il servizio PostgreSQL sia attivo e collegato");
            throw new Exception("Impossibile connettersi al database");
        }

        // Crea database e tabelle se non esistono (NON elimina dati esistenti)
        var created = await dbContext.Database.EnsureCreatedAsync();

        if (created)
        {
            logger.LogInformation("🆕 Database creato da zero");
        }
        else
        {
            logger.LogInformation("✅ Database già esistente, nessuna modifica effettuata");
        }

        // Conta record esistenti
        var userCount = await dbContext.Users.CountAsync();
        var bookingCount = await dbContext.BookingRequests.CountAsync();
        var portfolioCount = await dbContext.PortfolioItems.CountAsync();
        var styleCount = await dbContext.TattooStyles.CountAsync();

        logger.LogInformation("📊 Contenuto database:");
        logger.LogInformation($"   👥 Utenti: {userCount}");
        logger.LogInformation($"   📅 Prenotazioni: {bookingCount}");
        logger.LogInformation($"   🎨 Portfolio: {portfolioCount}");
        logger.LogInformation($"   ✨ Stili: {styleCount}");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ ERRORE CRITICO durante la verifica del database");
        logger.LogError($"Messaggio: {ex.Message}");
        if (ex.InnerException != null)
        {
            logger.LogError($"Dettaglio: {ex.InnerException.Message}");
        }
        throw; // Ferma l'applicazione se il database non funziona
    }

    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = { "Admin", "User" };

    // Crea ruoli se non esistono
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
            logger.LogInformation($"🔐 Ruolo '{role}' creato");
        }
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

        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            logger.LogInformation($"👤 Admin creato: {adminEmail}");
        }
        else
        {
            logger.LogWarning($"⚠️ Impossibile creare admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
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

        var result = await userManager.CreateAsync(defaultUser, "User@123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(defaultUser, "User");
            logger.LogInformation($"👤 User standard creato: {defaultEmail}");
        }
    }

    // ---------------------------
    // SEED: Tattoo Styles
    // ---------------------------
    try 
    {
        string[] tattooStyleNames = { "Realistic", "Fine line", "Black Art", "Lettering", "Small Tattoos", "Cartoons","Animals" };

        foreach (var styleName in tattooStyleNames)
        {
            if (!await dbContext.TattooStyles.AnyAsync(s => s.Name == styleName))
            {
                dbContext.TattooStyles.Add(new TattooStyle { Name = styleName });
            }
        }
        await dbContext.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Errore durante il seeding del database (TattooStyles). Assicurati che il database sia raggiungibile.");
    }
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