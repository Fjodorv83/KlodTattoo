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
// CONFIGURAZIONE PORTA PER RAILWAY
// ----------------------------------------------------------
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
Console.WriteLine($"🚀 Server configurato per ascoltare sulla porta: {port}");

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
           .EnableSensitiveDataLogging()
           .LogTo(Console.WriteLine, LogLevel.Information));

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
    var cultures = new[] { "de-DE", "it-IT" };
    options.DefaultRequestCulture = new RequestCulture("de-DE");
    options.SupportedCultures = cultures.Select(c => new CultureInfo(c)).ToList();
    options.SupportedUICultures = cultures.Select(c => new CultureInfo(c)).ToList();
});

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

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
    // NON usare HSTS in produzione con Railway - gestiscono loro HTTPS
    // app.UseHsts();
}

// IMPORTANTE: Disabilita HTTPS redirect su Railway
// Railway gestisce HTTPS tramite il loro proxy
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