using VTSLegalOfficeAI.Entities;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IUserService
    {
        Task<User> CreateUserAsync(string username, string email, string password);
        Task<User?> ValidateCredentialsAsync(string username, string password);
        Task<List<User>> GetAllAsync();
        Task<bool> VerifyEmailAsync(string token);
        Task DeleteUserAsync(Guid id);
    }
}
