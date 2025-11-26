using KlodTattooWeb.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using KlodTattooWeb.Services;
using KlodTattooWeb.Models;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// ---------------------------
// Database
// ---------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

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
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ---------------------------
// Email Sender
// ---------------------------
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

var app = builder.Build();

// ---------------------------
// SEED: Ruoli e Admin/User
// ---------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

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
    // CREA ADMIN
    // ---------------------------
    var adminEmail = "admin@klodtattoo.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        await userManager.CreateAsync(adminUser, "Admin@123"); // Cambia in produzione
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
    var dbContext = services.GetRequiredService<AppDbContext>();
    string[] tattooStyleNames = { "Realistic", "Fine line", "black art", "Lettering", "small" };

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
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
