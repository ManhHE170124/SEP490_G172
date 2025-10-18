using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.Models;
using Keytietkiem.DTOs;
using Keytietkiem.Models;

namespace Keytietkiem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly KeytietkiemContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(KeytietkiemContext context, ILogger<UsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            var users = await _context.Users
                .Include(u => u.Account)
                .Select(u => new UserDto
                {
                    UserId = u.UserId,
                    AccountId = u.AccountId,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    AvatarUrl = u.AvatarUrl,
                    Notes = u.Notes,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt,
                    Username = u.Account.Username,
                    Email = u.Account.Email,
                    Status = u.Account.Status,
                    EmailVerified = u.Account.EmailVerified,
                    TwoFaEnabled = u.Account.TwoFaenabled, // tên trường trong model scaffold: TwoFaenabled
                    LastLoginAt = u.Account.LastLoginAt
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/users/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserDto>> GetUser(Guid id)
        {
            var u = await _context.Users.Include(x => x.Account).FirstOrDefaultAsync(x => x.UserId == id);
            if (u == null) return NotFound();

            var dto = new UserDto
            {
                UserId = u.UserId,
                AccountId = u.AccountId,
                FullName = u.FullName,
                PhoneNumber = u.PhoneNumber,
                AvatarUrl = u.AvatarUrl,
                Notes = u.Notes,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                Username = u.Account.Username,
                Email = u.Account.Email,
                Status = u.Account.Status,
                EmailVerified = u.Account.EmailVerified,
                TwoFaEnabled = u.Account.TwoFaenabled,
                LastLoginAt = u.Account.LastLoginAt
            };
            return Ok(dto);
        }
    }
}
