using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using VTSLegalOfficeAI.Options;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class SmtpEmailService : IEmailService
    {
        private readonly SmtpOptions _options;

        public SmtpEmailService(IOptions<SmtpOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string username, string verificationLink, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "Aktivacija naloga — VTS Legal Office AI";

            message.Body = new TextPart("plain")
            {
                Text =
                    $"Zdravo {username},\n\n" +
                    "Kreiran je nalog za tebe u VTS Legal Office AI aplikaciji.\n" +
                    "Klikni na link ispod da aktiviraš nalog i možeš da se uloguješ:\n\n" +
                    $"{verificationLink}\n\n" +
                    "Ako nisi očekivao ovaj email, slobodno ga ignoriši.",
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
