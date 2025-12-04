using KlodTattooWeb.Data;
using KlodTattooWeb.Models;
using KlodTattooWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

Console.WriteLine($"🔍 DATABASE_URL trovata? {(string.IsNullOrEmpty(dbUrl) ? "NO ❌" : "SI ✅")}");

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// ------------------------------------------------------------
// DATABASE
// ------------------------------------------------------------
string connStr;

if (!string.IsNullOrEmpty(dbUrl))
{
    try
    {
        var fixedUrl = dbUrl.Replace("postgres://", "postgresql://");
        var uri = new Uri(fixedUrl);
        var user = uri.UserInfo.Split(':')[0];
        var pass = uri.UserInfo.Split(':')[1];

        connStr =
            $"Host={uri.Host};" +
            $"Port={uri.Port};" +
            $"Database={uri.AbsolutePath.TrimStart('/')};" +
            $"Username={user};" +
            $"Password={pass};" +
            $"SSL Mode=Require;Trust Server Certificate=true";

        Console.WriteLine($"🐘 Railway DB attivo → Host: {uri.Host}");
    }
    catch
    {
        Console.WriteLine("⚠ Errore parsing DATABASE_URL, fallback a locale.");
        connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    }
}
else
{
    Console.WriteLine("🐘 DATABASE_URL assente → uso DefaultConnection (locale)");
    connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// ------------------------------------------------------------
// IDENTITY
// ------------------------------------------------------------
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();
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

// ------------------------------------------------------------
// MIGRAZIONI AUTOMATICHE
// ------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Console.WriteLine("🔄 Eseguo migrations...");
        await db.Database.MigrateAsync();
        Console.WriteLine("✅ Migrations OK");

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        // Seed Admin
        string adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@klodtattoo.com";
        string adminPass = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123";

        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));

        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            await userManager.CreateAsync(admin, adminPass);
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ MIGRATION ERROR: {ex.Message}");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapDefaultControllerRoute();

app.Run();
