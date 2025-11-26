using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KlodTattooWeb.Data;
using KlodTattooWeb.Models;

namespace KlodTattooWeb.Areas.Admin.Controllers
{
    [Area("Admin")] // Specify the area
    [Authorize(Roles = "Admin")]
    public class TattooStylesController : Controller
    {
        private readonly AppDbContext _context;

        public TattooStylesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/TattooStyles
        public async Task<IActionResult> Index()
        {
            return View(await _context.TattooStyles.ToListAsync());
        }

        // GET: Admin/TattooStyles/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tattooStyle = await _context.TattooStyles
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tattooStyle == null)
            {
                return NotFound();
            }

            return View(tattooStyle);
        }

        // GET: Admin/TattooStyles/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/TattooStyles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name")] TattooStyle tattooStyle)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tattooStyle);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tattooStyle);
        }

        // GET: Admin/TattooStyles/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tattooStyle = await _context.TattooStyles.FindAsync(id);
            if (tattooStyle == null)
            {
                return NotFound();
            }
            return View(tattooStyle);
        }

        // POST: Admin/TattooStyles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] TattooStyle tattooStyle)
        {
            if (id != tattooStyle.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tattooStyle);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TattooStyleExists(tattooStyle.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(tattooStyle);
        }

        // GET: Admin/TattooStyles/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tattooStyle = await _context.TattooStyles
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tattooStyle == null)
            {
                return NotFound();
            }

            return View(tattooStyle);
        }

        // POST: Admin/TattooStyles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tattooStyle = await _context.TattooStyles.FindAsync(id);
            if (tattooStyle != null)
            {
                _context.TattooStyles.Remove(tattooStyle);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TattooStyleExists(int id)
        {
            return _context.TattooStyles.Any(e => e.Id == id);
        }
    }
}
