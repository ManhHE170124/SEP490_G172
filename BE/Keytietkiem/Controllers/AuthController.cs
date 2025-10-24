using Keytietkiem.Models;
using KeytietkiemApi.Dtos.Auth;
using KeytietkiemApi.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace KeytietkiemApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(KeytietkiemDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        private string GenerateJwtToken(User user, string role, out DateTime expiresAt)
        {
            var keyString = _config["Jwt:Key"] ?? "super_secret_key_123456789012345678901234567890";
            var keyBytes = Encoding.UTF8.GetBytes(keyString);
            if (keyBytes.Length < 32) throw new ArgumentOutOfRangeException(nameof(keyBytes), "JWT key phải >= 32 bytes.");

            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
            expiresAt = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:ExpiresInDays"] ?? "7"));

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName ?? ""),
                new Claim(ClaimTypes.Role, role)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "KeytietkiemApi",
                audience: _config["Jwt:Audience"] ?? "KeytietkiemClient",
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // POST: api/auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Email và mật khẩu là bắt buộc." });

            if (await _context.Users.AnyAsync(u => u.Email == req.Email))
                return BadRequest(new { message = "Email đã tồn tại." });

            var user = new User
            {
                UserId = Guid.NewGuid(),
                FirstName = null,
                LastName = null,
                FullName = string.IsNullOrWhiteSpace(req.DisplayName) ? req.Email.Split('@')[0] : req.DisplayName,
                Email = req.Email,
                Status = "Active",
                EmailVerified = false,
                CreatedAt = DateTime.UtcNow
            };

            var account = new Account
            {
                AccountId = Guid.NewGuid(),
                Username = req.Email.Split('@')[0],
                PasswordHash = PasswordHasher.HashPassword(req.Password),
                CreatedAt = DateTime.UtcNow,
                FailedLoginCount = 0,
                LockedUntil = null,
                UserId = user.UserId
            };

            var roleUser = await _context.Roles.FindAsync("User");
            if (roleUser == null)
            {
                roleUser = new Role
                {
                    RoleId = "User",
                    Name = "User",
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Roles.Add(roleUser);
            }

            user.Roles ??= new List<Role>();
            user.Roles.Add(roleUser);

            _context.Users.Add(user);
            _context.Accounts.Add(account);

            await _context.SaveChangesAsync();


            var token = GenerateJwtToken(user, "User", out var expiresAt);

            var resp = new RegisterResponse
            {
                Token = token,
                Email = user.Email,
                Role = "User",
                CreatedAt = user.CreatedAt
            };
            return Ok(resp);
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Email và mật khẩu là bắt buộc." });

            var account = await _context.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.User.Email == req.Email);

            if (account == null)
                return BadRequest(new { message = "Tài khoản không tồn tại." });

            var user = account.User;
            if (user.Status == "Disabled")
                return BadRequest(new { message = "Tài khoản đã bị vô hiệu hóa." });

            if (account.LockedUntil.HasValue && account.LockedUntil.Value > DateTime.UtcNow)
                return BadRequest(new { message = "Tài khoản đang bị khóa tạm thời." });

            var ok = PasswordHasher.VerifyPassword(account.PasswordHash, req.Password);
            if (!ok)
            {
                account.FailedLoginCount++;
                if (account.FailedLoginCount >= 5)
                {
                    account.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                }
                await _context.SaveChangesAsync();
                return BadRequest(new { message = "Sai email hoặc mật khẩu." });
            }

            account.FailedLoginCount = 0;
            account.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Lấy role đầu tiên
            var role = await _context.Users
                .Where(u => u.UserId == user.UserId)
                .SelectMany(u => u.Roles)
                .Select(r => r.RoleId)
                .FirstOrDefaultAsync() ?? "User";

            var token = GenerateJwtToken(user, role, out var expiresAt);

            var response = new AuthResponse
            {
                Token = token,
                Email = user.Email,
                Role = role,
                ExpiresAt = expiresAt
            };
            return Ok(response);
        }

        // POST: api/auth/forgot-password  (demo: chỉ check email tồn tại)
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(new { message = "Email không hợp lệ." });

            var exists = await _context.Users.AnyAsync(u => u.Email == req.Email);
            if (!exists) return BadRequest(new { message = "Email chưa được đăng ký." });

            return Ok(new { message = $"Link đặt lại mật khẩu đã được gửi tới {req.Email} (demo)." });
        }
    }
}
