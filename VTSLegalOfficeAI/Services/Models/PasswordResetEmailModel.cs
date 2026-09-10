namespace VTSLegalOfficeAI.Services.Models
{
    public class PasswordResetEmailModel
    {
        public string Username { get; set; } = string.Empty;
        public string ResetLink { get; set; } = string.Empty;
    }
}
