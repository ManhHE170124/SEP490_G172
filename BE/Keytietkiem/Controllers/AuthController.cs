using Keytietkiem.Models;
using KeytietkiemApi.Dtos.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace KeytietkiemApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly KeytietkiemContext _context;
        private readonly IConfiguration _config;

        public AuthController(KeytietkiemContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }
        //  Generate JWT Token
        private string GenerateJwtToken(Account acc, string role, out DateTime expiresAt)
        {
            var keyString = _config["Jwt:Key"] ?? "super_secret_key_123456789012345678901234567890";
            var keyBytes = Encoding.UTF8.GetBytes(keyString);

            if (keyBytes.Length < 32)
                throw new ArgumentOutOfRangeException(nameof(keyBytes), "JWT key phải dài tối thiểu 32 bytes (256 bit).");

            var key = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            expiresAt = DateTime.UtcNow.AddDays(7);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, acc.AccountId.ToString()),
                new Claim(ClaimTypes.Email, acc.Email),
                new Claim(ClaimTypes.Role, role)
            };

            var token = new JwtSecurityToken(
                issuer: "KeytietkiemApi",
                audience: "KeytietkiemClient",
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        //  Đăng ký
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (await _context.Accounts.AnyAsync(a => a.Email == req.Email))
                return BadRequest(new { message = "Email đã tồn tại." });

            using var hmac = new HMACSHA512();
            var acc = new Account
            {
                AccountId = Guid.NewGuid(),
                Username = req.Email.Split('@')[0],
                Email = req.Email,
                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(req.Password)),
                TwoFasecret = hmac.Key,
                Status = "Active",
                EmailVerified = false,
                TwoFaenabled = false,
                CreatedAt = DateTime.UtcNow,
                FailedLoginCount = 0
            };

            var user = new User
            {
                UserId = Guid.NewGuid(),
                AccountId = acc.AccountId,
                FullName = string.IsNullOrWhiteSpace(req.DisplayName)
                    ? acc.Username
                    : req.DisplayName,
                CreatedAt = DateTime.UtcNow
            };

            _context.Accounts.Add(acc);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(acc, "User", out var expiresAt);

            var response = new RegisterResponse
            {
                Token = token,
                Email = acc.Email,
                Role = "User",
                CreatedAt = acc.CreatedAt
            };

            return Ok(response);
        }

        //  Đăng nhập
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var acc = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == req.Email);
            if (acc == null)
                return BadRequest(new { message = "Tài khoản không tồn tại." });

            if (acc.LockedUntil.HasValue && acc.LockedUntil.Value > DateTime.UtcNow)
                return BadRequest(new { message = "Tài khoản đang bị khóa tạm thời." });

            using var hmac = new HMACSHA512(acc.TwoFasecret ?? new byte[0]);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(req.Password));

            if (!computedHash.SequenceEqual(acc.PasswordHash ?? Array.Empty<byte>()))
            {
                acc.FailedLoginCount++;
                if (acc.FailedLoginCount >= 5)
                {
                    acc.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                    await _context.SaveChangesAsync();
                    return BadRequest(new { message = "Sai mật khẩu quá nhiều, tài khoản bị khóa 15 phút." });
                }

                await _context.SaveChangesAsync();
                return BadRequest(new { message = "Sai email hoặc mật khẩu." });
            }

            acc.FailedLoginCount = 0;
            acc.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            string roleName = acc.Email.Contains("admin") ? "Admin" : "User";
            var token = GenerateJwtToken(acc, roleName, out var expiresAt);

            var response = new AuthResponse
            {
                Token = token,
                Email = acc.Email,
                Role = roleName,
                ExpiresAt = expiresAt
            };

            return Ok(response);
        }

        //  Quên mật khẩu
        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword([FromBody] ForgotPasswordRequest req)
        {
            if (string.IsNullOrEmpty(req.Email))
                return BadRequest(new { message = "Email không hợp lệ." });
            var acc = _context.Accounts.FirstOrDefault(a => a.Email == req.Email);
            if (acc == null) return BadRequest(new { message = "Email chưa được đăng ký." });

            return Ok(new { message = $"Link đặt lại mật khẩu đã được gửi tới {req.Email} (demo)." });
        }
    }
}
