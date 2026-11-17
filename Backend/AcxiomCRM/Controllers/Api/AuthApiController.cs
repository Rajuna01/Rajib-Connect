
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;

        public AuthApiController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
        }

        // ==========================================
        // POST: api/auth/login
        // ==========================================

        [AllowAnonymous]
        [EnableRateLimiting("LoginPolicy")]
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginApiRequest request)
        {
            // Find user by username or email.

            var user = await _userManager.FindByNameAsync(
                request.UserName);

            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(
                    request.UserName);
            }

            // Check account existence and active status.

            if (user == null || !user.IsActive)
            {
                return Unauthorized(new
                {
                    message = "Invalid username/email or password."
                });
            }

            // Validate password and apply Identity lockout.

            var signInResult =
                await _signInManager.CheckPasswordSignInAsync(
                    user,
                    request.Password,
                    lockoutOnFailure: true);

            if (!signInResult.Succeeded)
            {
                return Unauthorized(new
                {
                    message = "Invalid username/email or password."
                });
            }

            // Retrieve current user roles.

            var roles = await _userManager.GetRolesAsync(user);

            // Read JWT configuration.

            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey) ||
                string.IsNullOrWhiteSpace(jwtIssuer) ||
                string.IsNullOrWhiteSpace(jwtAudience))
            {
                return StatusCode(500, new
                {
                    message = "JWT configuration is incomplete."
                });
            }

            var accessTokenMinutes =
                _configuration.GetValue<int>(
                    "Jwt:AccessTokenMinutes", 30);

            if (accessTokenMinutes <= 0)
            {
                return StatusCode(500, new
                {
                    message = "JWT token lifetime configuration is invalid."
                });
            }

            // ==========================================
            // CREATE JWT CLAIMS
            // ==========================================

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id),

                new Claim(
                    ClaimTypes.Name,
                    user.UserName ?? string.Empty)
            };

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                claims.Add(
                    new Claim(ClaimTypes.Email, user.Email));
            }

            foreach (var role in roles)
            {
                claims.Add(
                    new Claim(ClaimTypes.Role, role));
            }

            // ==========================================
            // CREATE SIGNING CREDENTIALS
            // ==========================================

            var key = new SymmetricSecurityKey(
                Convert.FromBase64String(jwtKey));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            // ==========================================
            // GENERATE JWT TOKEN
            // ==========================================

            var issuedAtUtc = DateTime.UtcNow;

            var expiresAtUtc =
                issuedAtUtc.AddMinutes(accessTokenMinutes);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                notBefore: issuedAtUtc,
                expires: expiresAtUtc,
                signingCredentials: credentials);

            var accessToken =
                new JwtSecurityTokenHandler()
                    .WriteToken(token);

            // ==========================================
            // RETURN LOGIN RESPONSE
            // ==========================================

            return Ok(new
            {
                accessToken = accessToken,
                tokenType = "Bearer",
                expiresAtUtc = expiresAtUtc,

                user = new
                {
                    userId = user.Id,
                    userName = user.UserName,
                    email = user.Email,
                    roles = roles
                }
            });
        }
    }
}
