using KlodTattooWeb.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using KlodTattooWeb.Services; // Add this using directive
using KlodTattooWeb.Models; // Add this using directive

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Configure Email Settings and Register Email Sender
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

var app = builder.Build();

// Seed Admin User (only for development/initial setup)
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Create Admin Role if it doesn't exist
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    // Create Admin User if it doesn't exist
    var adminUser = await userManager.FindByEmailAsync("admin@klodtattoo.com");
    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = "admin@klodtattoo.com",
            Email = "admin@klodtattoo.com",
            EmailConfirmed = true // Assuming email is confirmed for initial admin
        };
        // WARNING: This is a default password for setup. Change it immediately in production!
        await userManager.CreateAsync(adminUser, "Admin@123");
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Required for wwwroot content

app.UseRouting();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages(); // Required for Identity UI

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
