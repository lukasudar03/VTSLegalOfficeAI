using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VTSLegalOfficeAI.DTOs.Auth;
using VTSLegalOfficeAI.Entities;
using VTSLegalOfficeAI.Options;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IEmailService _emailService;
        private readonly AdminOptions _adminOptions;
        private readonly FrontendOptions _frontendOptions;

        public AuthController(
            IUserService userService,
            IJwtTokenService jwtTokenService,
            IEmailService emailService,
            IOptions<AdminOptions> adminOptions,
            IOptions<FrontendOptions> frontendOptions)
        {
            _userService = userService;
            _jwtTokenService = jwtTokenService;
            _emailService = emailService;
            _adminOptions = adminOptions.Value;
            _frontendOptions = frontendOptions.Value;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var user = await _userService.ValidateCredentialsAsync(request.Username, request.Password);
            if (user == null)
                return Unauthorized(new { Message = "Neispravno korisničko ime ili lozinka." });

            if (!user.EmailVerified)
                return Unauthorized(new { Message = "Nalog nije verifikovan. Proveri email i klikni na link za aktivaciju." });

            var (token, expiresAt) = _jwtTokenService.GenerateToken(user);

            return Ok(new LoginResponseDto
            {
                Token = token,
                ExpiresAt = expiresAt,
                Username = user.Username,
                IsAdmin = user.IsAdmin,
            });
        }

        // Creating a user requires either the shared bootstrap admin key (used the very
        // first time, before any admin account exists) or a valid JWT for an existing
        // admin user. This keeps account creation out of public reach without a
        // chicken-and-egg problem for the first admin account.
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto request, [FromHeader(Name = "X-Admin-Key")] string? adminKey)
        {
            var isAuthenticatedAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");

            if (!isAuthenticatedAdmin && !IsAdminKeyValid(adminKey))
                return Unauthorized(new { Message = "Nemaš dozvolu da kreiraš korisnike." });

            User user;
            try
            {
                user = await _userService.CreateUserAsync(request.Username, request.Email, request.Password);
            }
            catch (Exception ex)
            {
                return Conflict(new { Message = ex.Message });
            }

            try
            {
                var verificationLink = $"{_frontendOptions.BaseUrl}/verify-email?token={user.EmailVerificationToken}";
                await _emailService.SendVerificationEmailAsync(user.Email, user.Username, verificationLink);
            }
            catch
            {
                // Don't leave an unverifiable account behind if the email never went out.
                await _userService.DeleteUserAsync(user.Id);
                return StatusCode(502, new { Message = "Korisnik nije kreiran jer slanje email-a nije uspelo. Proveri SMTP podešavanja i pokušaj ponovo." });
            }

            return Ok(new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                IsAdmin = user.IsAdmin,
                EmailVerified = user.EmailVerified,
                CreatedAt = user.CreatedAt,
            });
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request)
        {
            var verified = await _userService.VerifyEmailAsync(request.Token);
            if (!verified)
                return BadRequest(new { Message = "Link za verifikaciju je nevažeći ili je istekao." });

            return Ok(new { Message = "Email je uspešno verifikovan. Sada možeš da se uloguješ." });
        }

        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userService.GetAllAsync();

            return Ok(users.Select(u => new UserResponseDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                IsAdmin = u.IsAdmin,
                EmailVerified = u.EmailVerified,
                CreatedAt = u.CreatedAt,
            }));
        }

        private bool IsAdminKeyValid(string? providedKey)
        {
            if (string.IsNullOrEmpty(_adminOptions.AdminKey) || string.IsNullOrEmpty(providedKey))
                return false;

            var expected = Encoding.UTF8.GetBytes(_adminOptions.AdminKey);
            var provided = Encoding.UTF8.GetBytes(providedKey);

            return expected.Length == provided.Length && CryptographicOperations.FixedTimeEquals(expected, provided);
        }
    }
}
