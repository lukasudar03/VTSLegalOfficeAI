using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using VTSLegalOfficeAI.Options;
using VTSLegalOfficeAI.Services.Interfaces;
using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class SmtpEmailService : IEmailService
    {
        private readonly SmtpOptions _options;
        private readonly IRazorViewToStringRenderer _viewRenderer;

        public SmtpEmailService(IOptions<SmtpOptions> options, IRazorViewToStringRenderer viewRenderer)
        {
            _options = options.Value;
            _viewRenderer = viewRenderer;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string username, string verificationLink, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "Aktivacija naloga — VTS Legal Office AI";

            var htmlBody = await _viewRenderer.RenderViewToStringAsync(
                "/Views/Emails/VerificationEmail.cshtml",
                new VerificationEmailModel { Username = username, VerificationLink = verificationLink });

            var builder = new BodyBuilder
            {
                TextBody =
                    $"Zdravo {username},\n\n" +
                    "Kreiran je nalog za tebe u VTS Legal Office AI aplikaciji.\n" +
                    "Klikni na link ispod da aktiviraš nalog i možeš da se uloguješ:\n\n" +
                    $"{verificationLink}\n\n" +
                    "Ako nisi očekivao ovaj email, slobodno ga ignoriši.",
                HtmlBody = htmlBody,
            };

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient
            {
                // On some networks (notably common on macOS dev machines) the OCSP
                // revocation check for the server certificate can't complete, which
                // otherwise fails the TLS handshake even though the certificate itself
                // is valid. Gmail's SMTP endpoint is trusted, so skip that check.
                CheckCertificateRevocation = false,
            };
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
