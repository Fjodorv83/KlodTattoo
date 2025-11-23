using KlodTattooWeb.Data;
using KlodTattooWeb.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// SEED DEL PORTFOLIO
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate(); // Applica eventuali migrazioni

    // Percorso cartella wwwroot/images/portfolio
    var portfolioFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "portfolio");

    if (!context.PortfolioItems.Any())
    {
        var images = Directory.GetFiles(portfolioFolder)
                              .Where(f => f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".jpeg"))
                              .ToList();

        foreach (var imgPath in images)
        {
            var fileName = Path.GetFileName(imgPath);
            var relativePath = $"/images/portfolio/{fileName}";

            var item = new PortfolioItem
            {
                Title = Path.GetFileNameWithoutExtension(fileName), // tattoo1, tattoo2...
                Style = "Uncategorized",
                ImageUrl = relativePath,
                CreatedAt = DateTime.Now
            };

            context.PortfolioItems.Add(item);
        }

        context.SaveChanges();
        Console.WriteLine($"{images.Count} immagini inserite nel database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
