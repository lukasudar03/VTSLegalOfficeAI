using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VTSLegalOfficeAI.DTOs.Auth;
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
        private readonly AdminOptions _adminOptions;

        public AuthController(IUserService userService, IJwtTokenService jwtTokenService, IOptions<AdminOptions> adminOptions)
        {
            _userService = userService;
            _jwtTokenService = jwtTokenService;
            _adminOptions = adminOptions.Value;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var user = await _userService.ValidateCredentialsAsync(request.Username, request.Password);
            if (user == null)
                return Unauthorized(new { Message = "Neispravno korisničko ime ili lozinka." });

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

            var user = await _userService.CreateUserAsync(request.Username, request.Password);

            return Ok(new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                IsAdmin = user.IsAdmin,
                CreatedAt = user.CreatedAt,
            });
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
                IsAdmin = u.IsAdmin,
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
