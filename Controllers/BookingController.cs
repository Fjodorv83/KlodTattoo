using KlodTattooWeb.Data;
using KlodTattooWeb.Models;
using Microsoft.AspNetCore.Mvc;
using KlodTattooWeb.Services; // Add this using directive
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services; // Add this using directive

namespace KlodTattooWeb.Controllers
{
    public class BookingController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly EmailSettings _emailSettings;

        public BookingController(AppDbContext context, IEmailSender emailSender, IOptions<EmailSettings> emailSettings)
        {
            _context = context;
            _emailSender = emailSender;
            _emailSettings = emailSettings.Value;
        }

        // GET: Booking/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Booking/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ClientName,Email,BodyPart,IdeaDescription,PreferredDate")] BookingRequest bookingRequest)
        {
            if (ModelState.IsValid)
            {
                _context.Add(bookingRequest);
                await _context.SaveChangesAsync();

                // Send email to client
                var clientSubject = "Conferma Ricezione Richiesta di Prenotazione KlodTattoo";
                var clientMessage = $"Ciao {bookingRequest.ClientName},<br/><br/>" +
                                    $"Abbiamo ricevuto la tua richiesta di prenotazione per un tatuaggio.<br/>" +
                                    $"Data preferita: {bookingRequest.PreferredDate.ToShortDateString()}<br/>" +
                                    $"Parte del corpo: {bookingRequest.BodyPart}<br/>" +
                                    $"Descrizione dell'idea: {bookingRequest.IdeaDescription}<br/><br/>" +
                                    $"Ti contatteremo presto per definire i dettagli.<br/><br/>" +
                                    $"Grazie,<br/>Il team KlodTattoo";
                await _emailSender.SendEmailAsync(bookingRequest.Email, clientSubject, clientMessage);

                // Send email to admin/studio
                var adminSubject = "NUOVA RICHIESTA DI PRENOTAZIONE KlodTattoo";
                var adminMessage = $"È stata inviata una nuova richiesta di prenotazione:<br/><br/>" +
                                   $"Nome Cliente: {bookingRequest.ClientName}<br/>" +
                                   $"Email Cliente: {bookingRequest.Email}<br/>" +
                                   $"Data Preferita: {bookingRequest.PreferredDate.ToShortDateString()}<br/>" +
                                   $"Parte del Corpo: {bookingRequest.BodyPart}<br/>" +
                                   $"Descrizione Idea: {bookingRequest.IdeaDescription}<br/><br/>" +
                                   $"Accedi all'area admin per gestire la richiesta.";
                await _emailSender.SendEmailAsync(_emailSettings.SenderEmail, adminSubject, adminMessage);


                return RedirectToAction(nameof(Success));
            }
            return View(bookingRequest);
        }

        public IActionResult Success()
        {
            return View();
        }
    }
}
