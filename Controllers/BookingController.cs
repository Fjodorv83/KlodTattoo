using KlodTattooWeb.Data;
using KlodTattooWeb.Models;
using Microsoft.AspNetCore.Mvc;
using KlodTattooWeb.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace KlodTattooWeb.Controllers
{
    public class BookingController : Controller
    {
        private readonly AppDbContext _context;
        // Usiamo la classe concreta per accedere al metodo SendContactEmailAsync
        private readonly EmailSender _emailSender;
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<BookingController> _logger;

        public BookingController(AppDbContext context, EmailSender emailSender, IOptions<EmailSettings> emailSettings, ILogger<BookingController> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _emailSettings = emailSettings.Value;
            _logger = logger;
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

                try
                {
                    // ---------------------------------------------------
                    // 1. Email al CLIENTE (Conferma)
                    // ---------------------------------------------------
                    var clientSubject = "Conferma Ricezione Richiesta di Prenotazione KlodTattoo";
                    var clientMessage = $"Ciao {bookingRequest.ClientName},<br/><br/>" +
                                        $"Abbiamo ricevuto la tua richiesta di prenotazione per un tatuaggio.<br/>" +
                                        $"Data preferita: {bookingRequest.PreferredDate.ToShortDateString()}<br/>" +
                                        $"Parte del corpo: {bookingRequest.BodyPart}<br/>" +
                                        $"Descrizione dell'idea: {bookingRequest.IdeaDescription}<br/><br/>" +
                                        $"Ti contatteremo presto per definire i dettagli.<br/><br/>" +
                                        $"Grazie,<br/>Il team KlodTattoo";

                    // Usa il metodo standard (base)
                    await _emailSender.SendEmailAsync(bookingRequest.Email, clientSubject, clientMessage);


                    // ---------------------------------------------------
                    // 2. Email all'ADMIN (Tu)
                    // ---------------------------------------------------
                    var adminSubject = "NUOVA RICHIESTA DI PRENOTAZIONE KlodTattoo";
                    var adminMessage = $"È stata inviata una nuova richiesta di prenotazione:<br/><br/>" +
                                       $"Nome Cliente: {bookingRequest.ClientName}<br/>" +
                                       $"Email Cliente: {bookingRequest.Email}<br/>" +
                                       $"Data Preferita: {bookingRequest.PreferredDate.ToShortDateString()}<br/>" +
                                       $"Parte del Corpo: {bookingRequest.BodyPart}<br/>" +
                                       $"Descrizione Idea: {bookingRequest.IdeaDescription}<br/><br/>" +
                                       $"Accedi all'area admin per gestire la richiesta.";

                    // Usa il metodo NUOVO che imposta il Reply-To verso il cliente
                    await _emailSender.SendContactEmailAsync(
                        bookingRequest.Email,      // Email del cliente (per rispondere a lui)
                        bookingRequest.ClientName, // Nome del cliente
                        adminSubject,
                        adminMessage
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Si è verificato un errore durante l'invio dell'email di conferma della prenotazione.");
                }

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