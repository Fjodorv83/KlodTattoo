using KlodTattooWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml;

namespace KlodTattooWeb.Controllers
{
    public class SitemapController : Controller
    {
        private readonly AppDbContext _context;

        public SitemapController(AppDbContext context)
        {
            _context = context;
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> Index()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var portfolioItems = await _context.PortfolioItems.ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            // Pagine Statiche
            AddUrl(sb, baseUrl, "", "1.0");
            AddUrl(sb, baseUrl, "/Portfolio", "0.9");
            AddUrl(sb, baseUrl, "/Services", "0.9");
            AddUrl(sb, baseUrl, "/Info", "0.8");
            AddUrl(sb, baseUrl, "/Contacts", "0.8");
            AddUrl(sb, baseUrl, "/Booking/Create", "0.9");

            // Pagine Dinamiche (Portfolio Details)
            foreach (var item in portfolioItems)
            {
                AddUrl(sb, baseUrl, $"/Portfolio/Details/{item.Id}", "0.7", item.CreatedAt);
            }

            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml", Encoding.UTF8);
        }

        private void AddUrl(StringBuilder sb, string baseUrl, string path, string priority, DateTime? lastMod = null)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{baseUrl}{path}</loc>");
            if (lastMod.HasValue)
            {
                sb.AppendLine($"    <lastmod>{lastMod.Value:yyyy-MM-dd}</lastmod>");
            }
            sb.AppendLine($"    <priority>{priority}</priority>");
            sb.AppendLine("  </url>");
        }
    }
}
