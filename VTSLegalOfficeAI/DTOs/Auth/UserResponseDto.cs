namespace VTSLegalOfficeAI.DTOs.Auth
{
    public class UserResponseDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool EmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
