using KlodTattooWeb.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KlodTattooWeb.Data;

public class AppDbContext : IdentityDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<PortfolioItem> PortfolioItems { get; set; }
    public DbSet<BookingRequest> BookingRequests { get; set; }
    public DbSet<TattooStyle> TattooStyles { get; set; }
}