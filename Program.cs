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

// ----------------------------------------------------------
// LOGGING CONFIGURATION (IMPORTANTE PER DEBUG!)
// ----------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// ----------------------------------------------------------
// DATABASE CONFIG (LOCALE o RAILWAY)
// ----------------------------------------------------------
var dbEnvVar = Environment.GetEnvironmentVariable("DATABASE_URL");
var connectionString = ConnectionHelper.GetConnectionString(builder.Configuration);

if (!string.IsNullOrEmpty(dbEnvVar))
{
    try
    {
        var validUrl = dbEnvVar.StartsWith("postgres://")
            ? dbEnvVar.Replace("postgres://", "postgresql://")
            : dbEnvVar;

        var uri = new Uri(validUrl);
        var userInfo = uri.UserInfo.Split(':');

        connectionString =
            $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};" +
            $"Username={userInfo[0]};Password={(userInfo.Length > 1 ? userInfo[1] : "")};" +
            $"SSL Mode=Require;Trust Server Certificate=true";

        Console.WriteLine($"🐘 Railway DB: {uri.Host}:{uri.Port}");
    }
    catch (Exception ex)
    {
        connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
        Console.WriteLine($"⚠️ Errore parsing DATABASE_URL: {ex.Message}");
    }
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
    Console.WriteLine("🐘 PostgreSQL Locale");
}

// Fix timestamp PostgreSQL
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ----------------------------------------------------------
// SERVICES
// ----------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .EnableSensitiveDataLogging() // Per debug
           .LogTo(Console.WriteLine, LogLevel.Information));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true; // CAMBIATO: richiede uppercase
    options.Password.RequireNonAlphanumeric = true; // CAMBIATO: richiede caratteri speciali
    options.Password.RequiredLength = 8; // CAMBIATO: almeno 8 caratteri
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Data Protection
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

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
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

builder.Services.AddControllersWithViews().AddViewLocalization();
builder.Services.AddRazorPages();

// ----------------------------------------------------------
// BUILD APP
// ----------------------------------------------------------
var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseAuthentication();
app.UseAuthorization();

// ----------------------------------------------------------
// MIGRATIONS + SEEDING DETTAGLIATO CON LOGGING
// ----------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("========================================");
        logger.LogInformation("🚀 INIZIO PROCESSO DI SEEDING");
        logger.LogInformation("========================================");

        var db = services.GetRequiredService<AppDbContext>();

        logger.LogInformation("🔄 Applicazione migrazioni database...");

        // Controlla se ci sono pending migrations
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogWarning("⚠️ Ci sono migrazioni in sospeso:");
            foreach (var m in pendingMigrations)
                logger.LogWarning($" - {m}");

            logger.LogInformation("✅ Applicazione migrazioni in sospeso...");
            await db.Database.MigrateAsync();
            logger.LogInformation("✅ Migrazioni completate con successo");
        }
        else
        {
            logger.LogInformation("✅ Nessuna migrazione pendente, DB aggiornato");
        }

        await Task.Delay(500); // Assicurati che il DB sia pronto

        // ---------------- RUOLI ----------------
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roles = { "Admin", "User" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (result.Succeeded)
                    logger.LogInformation($"✅ Ruolo '{role}' creato");
                else
                    logger.LogError($"❌ Errore creazione ruolo '{role}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        // ---------------- ADMIN ----------------
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@klodtattoo.com";
        var adminPass = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123!Strong";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);

        if (existingAdmin == null)
        {
            var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var result = await userManager.CreateAsync(admin, adminPass);

            if (result.Succeeded)
            {
                logger.LogInformation($"✅ Admin creato: {adminEmail}");
                await userManager.AddToRoleAsync(admin, "Admin");
                logger.LogInformation("✅ Ruolo 'Admin' assegnato");
            }
            else
            {
                logger.LogError($"❌ Errore creazione Admin '{adminEmail}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            logger.LogInformation($"✅ Admin già esistente: {adminEmail}");
            if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
            {
                await userManager.AddToRoleAsync(existingAdmin, "Admin");
                logger.LogInformation("✅ Ruolo 'Admin' aggiunto");
            }
        }

        // ---------------- TATTOO STYLES ----------------
        string[] tattooStyles = { "Realistic", "Fine line", "Black Art", "Lettering", "Small Tattoos", "Cartoons", "Animals" };
        var existingStyles = await db.TattooStyles.Select(t => t.Name).ToListAsync();

        int addedCount = 0;
        foreach (var style in tattooStyles)
        {
            if (!existingStyles.Contains(style))
            {
                db.TattooStyles.Add(new TattooStyle { Name = style });
                addedCount++;
            }
        }
        if (addedCount > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation($"✅ Aggiunti {addedCount} nuovi stili tatuaggio");
        }
        else
        {
            logger.LogInformation("✅ Tutti gli stili tatuaggio già presenti");
        }

        logger.LogInformation("========================================");
        logger.LogInformation("✅ SEEDING COMPLETATO CON SUCCESSO");
        logger.LogInformation("========================================");
    }
    catch (Exception ex)
    {
        logger.LogError("========================================");
        logger.LogError("❌ ERRORE DURANTE MIGRAZIONE/SEEDING");
        logger.LogError($"Tipo: {ex.GetType().Name}");
        logger.LogError($"Messaggio: {ex.Message}");
        logger.LogError($"StackTrace:\n{ex.StackTrace}");

        if (ex.InnerException != null)
        {
            logger.LogError("--- Inner Exception ---");
            logger.LogError($"Tipo: {ex.InnerException.GetType().Name}");
            logger.LogError($"Messaggio: {ex.InnerException.Message}");
        }

        logger.LogError("⚠️ L'app continuerà ad avviarsi, ma il seeding potrebbe essere incompleto");
    }
}

// ----------------------------------------------------------
// ROUTES
// ----------------------------------------------------------
app.MapRazorPages();
app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();