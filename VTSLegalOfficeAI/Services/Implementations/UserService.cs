using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VTSLegalOfficeAI.Data;
using VTSLegalOfficeAI.Entities;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class UserService : IUserService
    {
        private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(48);

        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public UserService(ApplicationDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public async Task<User> CreateUserAsync(string username, string email, string password)
        {
            var usernameExists = await _context.Users.AnyAsync(u => u.Username == username);
            if (usernameExists)
                throw new Exception("Username is already taken.");

            var emailExists = await _context.Users.AnyAsync(u => u.Email == email);
            if (emailExists)
                throw new Exception("Email is already in use.");

            var isFirstUser = !await _context.Users.AnyAsync();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                IsAdmin = isFirstUser,
                EmailVerified = false,
                EmailVerificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                EmailVerificationTokenExpiresAt = DateTime.UtcNow.Add(VerificationTokenLifetime),
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

        public async Task<List<User>> GetAllAsync()
        {
            return await _context.Users
                .OrderBy(u => u.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.EmailVerificationToken == token &&
                u.EmailVerificationTokenExpiresAt != null &&
                u.EmailVerificationTokenExpiresAt > DateTime.UtcNow);

            if (user == null)
                return false;

            user.EmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiresAt = null;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
