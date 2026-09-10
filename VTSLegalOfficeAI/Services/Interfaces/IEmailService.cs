namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string username, string verificationLink, CancellationToken cancellationToken = default);
        Task SendPasswordResetEmailAsync(string toEmail, string username, string resetLink, CancellationToken cancellationToken = default);
    }
}
