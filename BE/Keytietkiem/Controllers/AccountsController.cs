using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KeytietkiemApi.Utils;

namespace KeytietkiemApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        public AccountsController(KeytietkiemDbContext context) => _context = context;

        // GET: api/accounts  (kèm email của user)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAccounts()
        {
            var data = await _context.Accounts
                .Include(a => a.User)
                .Select(a => new {
                    a.AccountId,
                    a.Username,
                    a.UserId,
                    Email = a.User.Email,
                    a.CreatedAt,
                    a.LastLoginAt,
                    a.FailedLoginCount,
                    a.LockedUntil
                })
                .ToListAsync();

            return Ok(data);
        }

        // GET: api/accounts/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<object>> GetAccount(Guid id)
        {
            var acc = await _context.Accounts
                .Include(a => a.User)
                .Where(a => a.AccountId == id)
                .Select(a => new {
                    a.AccountId,
                    a.Username,
                    a.UserId,
                    Email = a.User.Email,
                    a.CreatedAt,
                    a.LastLoginAt,
                    a.FailedLoginCount,
                    a.LockedUntil
                })
                .FirstOrDefaultAsync();

            if (acc == null) return NotFound();
            return Ok(acc);
        }

        public class CreateAccountRequest
        {
            public Guid UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        // POST: api/accounts
        [HttpPost]
        public async Task<ActionResult<object>> CreateAccount([FromBody] CreateAccountRequest req)
        {
            // User phải tồn tại và chưa có account (UNIQUE UserId trong Accounts)
            var user = await _context.Users.FindAsync(req.UserId);
            if (user == null) return BadRequest(new { message = "UserId không tồn tại." });

            var hasAccount = await _context.Accounts.AnyAsync(a => a.UserId == req.UserId);
            if (hasAccount) return BadRequest(new { message = "User này đã có tài khoản." });

            var acc = new Account
            {
                AccountId = Guid.NewGuid(),
                UserId = req.UserId,
                Username = req.Username,
                PasswordHash = PasswordHasher.HashPassword(req.Password),
                CreatedAt = DateTime.UtcNow,
                FailedLoginCount = 0
            };

            _context.Accounts.Add(acc);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAccount), new { id = acc.AccountId }, new
            {
                acc.AccountId,
                acc.Username,
                acc.UserId,
                Email = user.Email,
                acc.CreatedAt
            });
        }

        public class UpdateAccountRequest
        {
            public string? Username { get; set; }
            public string? NewPassword { get; set; }
        }

        // PUT: api/accounts/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateAccountRequest req)
        {
            var acc = await _context.Accounts.FindAsync(id);
            if (acc == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(req.Username))
                acc.Username = req.Username;

            if (!string.IsNullOrWhiteSpace(req.NewPassword))
                acc.PasswordHash = PasswordHasher.HashPassword(req.NewPassword);

            acc.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/accounts/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteAccount(Guid id)
        {
            var acc = await _context.Accounts.FindAsync(id);
            if (acc == null) return NotFound();

            _context.Accounts.Remove(acc);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
