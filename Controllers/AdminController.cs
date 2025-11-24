using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KlodTattooWeb.Data;
using KlodTattooWeb.Models;

namespace KlodTattooWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public AdminController(AppDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // GET: Admin
        public async Task<IActionResult> Index()
        {
            return View(await _context.PortfolioItems.ToListAsync());
        }

        // GET: Admin/Bookings
        public async Task<IActionResult> Bookings()
        {
            var bookingRequests = await _context.BookingRequests.OrderByDescending(b => b.Id).ToListAsync();
            return View(bookingRequests);
        }


        // GET: Admin/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Style")] PortfolioItem portfolioItem, IFormFile imageFile)
        {
            // Rimuovi ImageUrl e Description dalla validazione del modello perché verranno gestiti manualmente.
            ModelState.Remove(nameof(portfolioItem.ImageUrl));
            ModelState.Remove(nameof(portfolioItem.Description));

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    // Generate unique file name
                    string wwwRootPath = _hostEnvironment.WebRootPath;
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                    string path = Path.Combine(wwwRootPath, "images", "portfolio", fileName);

                    // Save image to wwwroot/images/portfolio
                    using (var fileStream = new FileStream(path, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    portfolioItem.ImageUrl = "/images/portfolio/" + fileName;
                    portfolioItem.CreatedAt = DateTime.Now;

                    _context.Add(portfolioItem);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError("imageFile", "Please upload an image file.");
            }
            return View(portfolioItem);
        }

        // GET: Admin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var portfolioItem = await _context.PortfolioItems.FindAsync(id);
            if (portfolioItem == null)
            {
                return NotFound();
            }
            return View(portfolioItem);
        }

        // POST: Admin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Style,ImageUrl,CreatedAt")] PortfolioItem portfolioItem, IFormFile? imageFile)
        {
            if (id != portfolioItem.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        // Delete old image if it exists
                        if (!string.IsNullOrEmpty(portfolioItem.ImageUrl))
                        {
                            string oldImagePath = Path.Combine(_hostEnvironment.WebRootPath, portfolioItem.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }

                        // Save new image
                        string wwwRootPath = _hostEnvironment.WebRootPath;
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                        string path = Path.Combine(wwwRootPath, "images", "portfolio", fileName);

                        using (var fileStream = new FileStream(path, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        portfolioItem.ImageUrl = "/images/portfolio/" + fileName;
                    }

                    _context.Update(portfolioItem);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PortfolioItemExists(portfolioItem.Id))
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
            return View(portfolioItem);
        }

        // GET: Admin/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var portfolioItem = await _context.PortfolioItems
                .FirstOrDefaultAsync(m => m.Id == id);
            if (portfolioItem == null)
            {
                return NotFound();
            }

            return View(portfolioItem);
        }

        // POST: Admin/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var portfolioItem = await _context.PortfolioItems.FindAsync(id);
            if (portfolioItem != null)
            {
                // Delete image file from wwwroot/images/portfolio
                if (!string.IsNullOrEmpty(portfolioItem.ImageUrl))
                {
                    string imagePath = Path.Combine(_hostEnvironment.WebRootPath, portfolioItem.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                _context.PortfolioItems.Remove(portfolioItem);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PortfolioItemExists(int id)
        {
            return _context.PortfolioItems.Any(e => e.Id == id);
        }

        // GET: Admin/BookingDetails/5
        public async Task<IActionResult> BookingDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bookingRequest = await _context.BookingRequests
                .FirstOrDefaultAsync(m => m.Id == id);
            if (bookingRequest == null)
            {
                return NotFound();
            }

            return View(bookingRequest);
        }

        // GET: Admin/DeleteBooking/5
        public async Task<IActionResult> DeleteBooking(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bookingRequest = await _context.BookingRequests
                .FirstOrDefaultAsync(m => m.Id == id);
            if (bookingRequest == null)
            {
                return NotFound();
            }

            return View(bookingRequest);
        }

        // POST: Admin/DeleteBooking/5
        [HttpPost, ActionName("DeleteBooking")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBookingConfirmed(int id)
        {
            var bookingRequest = await _context.BookingRequests.FindAsync(id);
            if (bookingRequest != null)
            {
                _context.BookingRequests.Remove(bookingRequest);
                await _context.SaveChangesAsync();
            }
            
            return RedirectToAction(nameof(Bookings));
        }
    }
}
