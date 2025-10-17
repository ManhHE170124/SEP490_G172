using Keytietkiem.Models;
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



        // Đăng ký
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



            return Ok(new { message = "Đăng ký thành công." });
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

            // ✅ Tạm gán role = Admin nếu email có chứa “admin”
            string roleName = acc.Email.Contains("admin") ? "Admin" : "User";


            return Ok(new
            {
                message = "Sai email hoặc mật khẩu." 
        });
        }
    }

    // ===============================
    // Request Models
    // ===============================
    public class RegisterRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? DisplayName { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
