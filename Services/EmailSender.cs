using MailKit.Net.Smtp;
using MailKit;
using Microsoft.Extensions.Options;
using MimeKit;
using KlodTattooWeb.Models; // Assuming EmailSettings is a part of Models or a separate config model

namespace KlodTattooWeb.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _emailSettings;

        public EmailSender(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            var emailMessage = new MimeMessage();

            emailMessage.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            emailMessage.To.Add(new MailboxAddress("", email));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart("html") { Text = message };

            using (var client = new SmtpClient())
            {
                try
                {
                    await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                    await client.SendAsync(emailMessage);
                }
                catch (Exception ex)
                {
                    // Log the exception
                    Console.WriteLine($"Error sending email: {ex.Message}");
                    throw; // Re-throw to indicate failure
                }
                finally
                {
                    await client.DisconnectAsync(true);
                }
            }
        }
    }

    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string message);
    }
}
