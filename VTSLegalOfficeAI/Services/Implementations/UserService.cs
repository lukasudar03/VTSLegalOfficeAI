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
        private const int MinPasswordLength = 8;

        private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(48);
        private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

        private static void ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
                throw new Exception($"Lozinka mora imati najmanje {MinPasswordLength} karaktera.");
        }

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
                throw new Exception("Korisničko ime je već zauzeto.");

            var emailExists = await _context.Users.AnyAsync(u => u.Email == email);
            if (emailExists)
                throw new Exception("Email adresa je već u upotrebi.");

            ValidatePasswordStrength(password);

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

        public async Task<User> UpdateUserAsync(Guid id, string username, string email)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                throw new Exception("Korisnik nije pronađen.");

            var usernameTaken = await _context.Users.AnyAsync(u => u.Username == username && u.Id != id);
            if (usernameTaken)
                throw new Exception("Korisničko ime je već zauzeto.");

            var emailTaken = await _context.Users.AnyAsync(u => u.Email == email && u.Id != id);
            if (emailTaken)
                throw new Exception("Email adresa je već u upotrebi.");

            user.Username = username;
            user.Email = email;

            await _context.SaveChangesAsync();
            return user;
        }

        public async Task DeleteUserAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return;

            var filePaths = await _context.Documents
                .Where(d => d.UserId == id)
                .Select(d => d.FilePath)
                .ToListAsync();

            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }

        public async Task<User?> RequestPasswordResetAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return null;

            user.PasswordResetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.Add(PasswordResetTokenLifetime);

            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.PasswordResetToken == token &&
                u.PasswordResetTokenExpiresAt != null &&
                u.PasswordResetTokenExpiresAt > DateTime.UtcNow);

            if (user == null)
                return false;

            ValidatePasswordStrength(newPassword);

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresAt = null;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
            if (result == PasswordVerificationResult.Failed)
                return false;

            ValidatePasswordStrength(newPassword);

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
