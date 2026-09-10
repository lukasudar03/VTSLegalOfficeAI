using VTSLegalOfficeAI.Entities;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IUserService
    {
        Task<User> CreateUserAsync(string username, string password);
        Task<User?> ValidateCredentialsAsync(string username, string password);
        Task<List<User>> GetAllAsync();
    }
}
