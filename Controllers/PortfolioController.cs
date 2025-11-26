using Microsoft.AspNetCore.Mvc;
using KlodTattooWeb.Data;
using Microsoft.EntityFrameworkCore;
using KlodTattooWeb.Models;

namespace KlodTattooWeb.Controllers
{
    public class PortfolioController : Controller
    {
        private readonly AppDbContext _context;

        public PortfolioController(AppDbContext context)
        {
            _context = context;
        }

                // GET: Portfolio
                public async Task<IActionResult> Index(string styleName)
                {
                    // Recupera tutti i tatuaggi ordinati per data di creazione decrescente
                    var query = _context.PortfolioItems
                                              .AsNoTracking()
                                              .Include(p => p.TattooStyle)
                                              .OrderByDescending(x => x.CreatedAt);
        
                    if (!string.IsNullOrEmpty(styleName))
                    {
                        query = (IOrderedQueryable<PortfolioItem>)query.Where(p => p.TattooStyle != null && p.TattooStyle.Name == styleName);
                        ViewBag.SelectedStyle = styleName;
                    }
        
                    var items = await query.ToListAsync();
        
                    // Pass all available styles to the view for filter buttons
                    ViewBag.TattooStyles = await _context.TattooStyles.Select(s => s.Name).ToListAsync();
        
                    return View(items);
                }
        
                // GET: Portfolio/Details/5
                public async Task<IActionResult> Details(int? id)
                {
                    if (id == null)
                        return NotFound();
        
                    var item = await _context.PortfolioItems
                                             .AsNoTracking()
                                             .Include(p => p.TattooStyle)
                                             .FirstOrDefaultAsync(m => m.Id == id);
        
                    if (item == null)
                        return NotFound();
        
                    return View(item);
                }    }
}
