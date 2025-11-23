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
        public async Task<IActionResult> Index()
        {
            // Recupera tutti i tatuaggi ordinati per data di creazione decrescente
            var items = await _context.PortfolioItems
                                      .AsNoTracking()
                                      .OrderByDescending(x => x.CreatedAt)
                                      .ToListAsync();

            return View(items);
        }

        // GET: Portfolio/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var item = await _context.PortfolioItems
                                     .AsNoTracking()
                                     .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null)
                return NotFound();

            return View(item);
        }

        // OPTIONAL: Filtra per stile
        public async Task<IActionResult> ByStyle(string style)
        {
            if (string.IsNullOrEmpty(style))
                return RedirectToAction(nameof(Index));

            var items = await _context.PortfolioItems
                                      .AsNoTracking()
                                      .Where(p => p.Style == style)
                                      .OrderByDescending(x => x.CreatedAt)
                                      .ToListAsync();

            ViewBag.SelectedStyle = style;
            return View("Index", items);
        }
    }
}
