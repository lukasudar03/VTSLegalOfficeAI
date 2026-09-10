using VTSLegalOfficeAI.Entities;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IJwtTokenService
    {
        (string Token, DateTime ExpiresAt) GenerateToken(User user);
    }
}
