using System.Net;
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

            var builder = new BodyBuilder
            {
                TextBody =
                    $"Zdravo {username},\n\n" +
                    "Kreiran je nalog za tebe u VTS Legal Office AI aplikaciji.\n" +
                    "Klikni na link ispod da aktiviraš nalog i možeš da se uloguješ:\n\n" +
                    $"{verificationLink}\n\n" +
                    "Ako nisi očekivao ovaj email, slobodno ga ignoriši.",
                HtmlBody = BuildVerificationEmailHtml(username, verificationLink),
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

        private static string BuildVerificationEmailHtml(string username, string verificationLink)
        {
            var safeUsername = WebUtility.HtmlEncode(username);
            var safeLink = WebUtility.HtmlEncode(verificationLink);

            return $$"""
                <!DOCTYPE html>
                <html lang="sr">
                  <body style="margin:0; padding:32px 16px; background:#f4f3ec; font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                      <tr>
                        <td align="center">
                          <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="max-width:480px; width:100%; background:#ffffff; border:1px solid #e5e4e7; border-radius:12px; overflow:hidden;">
                            <tr>
                              <td style="padding:32px 32px 8px; text-align:center;">
                                <div style="font-size:20px; font-weight:600; color:#08060d;">VTS Legal Office AI</div>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:16px 32px 0;">
                                <p style="margin:0 0 16px; font-size:15px; line-height:1.5; color:#08060d;">Zdravo {{safeUsername}},</p>
                                <p style="margin:0 0 24px; font-size:14px; line-height:1.6; color:#6b6375;">
                                  Kreiran je nalog za tebe u <strong>VTS Legal Office AI</strong> aplikaciji. Klikni na dugme ispod da aktiviraš nalog i počneš da ga koristiš.
                                </p>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:0 32px 32px; text-align:center;">
                                <a href="{{safeLink}}" style="display:inline-block; padding:12px 28px; background:#aa3bff; color:#ffffff; font-size:15px; font-weight:600; text-decoration:none; border-radius:8px;">
                                  Aktiviraj nalog
                                </a>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:20px 32px 32px; border-top:1px solid #e5e4e7;">
                                <p style="margin:0; font-size:12px; line-height:1.6; color:#6b6375;">
                                  Ako dugme ne radi, kopiraj i nalepi ovaj link u browser:<br>
                                  <a href="{{safeLink}}" style="color:#aa3bff; word-break:break-all;">{{safeLink}}</a>
                                </p>
                                <p style="margin:16px 0 0; font-size:12px; color:#9c96a0;">
                                  Ako nisi očekivao ovaj email, slobodno ga ignoriši.
                                </p>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                    </table>
                  </body>
                </html>
                """;
        }
    }
}
