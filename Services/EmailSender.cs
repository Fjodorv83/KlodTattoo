using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using KlodTattooWeb.Models;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace KlodTattooWeb.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _emailSettings;

        public EmailSender(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            await ExecuteSendEmailAsync(email, subject, htmlMessage, null);
        }

        public async Task SendContactEmailAsync(string clientEmail, string clientName, string subject, string htmlMessage)
        {
            await ExecuteSendEmailAsync(_emailSettings.SenderEmail, subject, htmlMessage, clientEmail);
        }

        private async Task ExecuteSendEmailAsync(string toEmail, string subject, string htmlMessage, string? replyToEmail)
        {
            Console.WriteLine($"📧 [START] Inizio invio email a: {toEmail}");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            message.To.Add(new MailboxAddress("", toEmail));

            if (!string.IsNullOrEmpty(replyToEmail))
            {
                message.ReplyTo.Add(new MailboxAddress("", replyToEmail));
            }

            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlMessage };

            using var client = new SmtpClient();

            try
            {
                // 1. TRUCCO PER RAILWAY: Ignora errori certificati SSL del server
                // Questo risolve il 90% dei blocchi sui server Linux cloud
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                Console.WriteLine($"🔌 [CONNECT] Tentativo connessione a {_emailSettings.SmtpServer}:{_emailSettings.SmtpPort}...");

                // 2. TIMEOUT BREVE: Non aspettare 2 minuti, fallisci dopo 10 secondi così vediamo l'errore
                client.Timeout = 10000;

                // 3. Connessione FORZATA SSL (Usa SecureSocketOptions.SslOnConnect per la porta 465)
                await client.ConnectAsync(
                    _emailSettings.SmtpServer,
                    _emailSettings.SmtpPort,
                    SecureSocketOptions.SslOnConnect
                );

                Console.WriteLine("✅ [CONNECT] Connesso! Autenticazione in corso...");

                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);

                Console.WriteLine("✅ [AUTH] Autenticato! Invio messaggio...");

                await client.SendAsync(message);

                Console.WriteLine("🚀 [SUCCESS] Email inviata correttamente!");
            }
            catch (Exception ex)
            {
                // Stampa l'errore ESATTO nei log di Railway
                Console.WriteLine($"❌ [ERROR] ERRORE CRITICO EMAIL: {ex.Message}");
                Console.WriteLine($"❌ [STACK] {ex.StackTrace}");
                throw;
            }
            finally
            {
                if (client.IsConnected)
                    await client.DisconnectAsync(true);
            }
        }
    }
}