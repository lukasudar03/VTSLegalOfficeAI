using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VTSLegalOfficeAI.Data;
using VTSLegalOfficeAI.Entities;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public UserService(ApplicationDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public async Task<User> CreateUserAsync(string username, string password)
        {
            var usernameExists = await _context.Users.AnyAsync(u => u.Username == username);
            if (usernameExists)
                throw new Exception("Username is already taken.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                CreatedAt = DateTime.UtcNow,
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<User?> ValidateCredentialsAsync(string username, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
                return null;

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

            return result == PasswordVerificationResult.Failed ? null : user;
        }
    }
}
