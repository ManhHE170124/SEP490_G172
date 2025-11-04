using System.Security.Cryptography;
using System.Text;
using Keytietkiem.DTOs.Users;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.DTOs.Common;

//namespace Keytietkiem.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class UsersController : ControllerBase
//    {
//        private readonly KeytietkiemDbContext _db;
//        public UsersController(KeytietkiemDbContext db) => _db = db;

        // ===== Password hashing (PBKDF2 - 1 chiều) =====
        private static byte[] HashPassword(string password)
        {
            const int iterations = 100000;
            const int keySize = 32;

            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[16];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(keySize);

            var result = new byte[salt.Length + hash.Length];
            Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
            Buffer.BlockCopy(hash, 0, result, salt.Length, hash.Length);
            return result; // 48 bytes
        }

        private static bool VerifyPassword(string password, byte[]? storedHash)
        {
            if (storedHash == null || storedHash.Length != 48) return false;
            const int iterations = 100000;
            const int keySize = 32;

            var salt = new byte[16];
            Buffer.BlockCopy(storedHash, 0, salt, 0, 16);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(keySize);

            for (var i = 0; i < keySize; i++)
                if (storedHash[i + 16] != hash[i])
                    return false;
            return true;
        }

        private static IQueryable<User> ExcludeAdminUsers(IQueryable<User> q)
            => q.Where(u => !u.Roles.Any(r => r.Name.ToLower().Contains("admin")));

        // GET /api/users
        [HttpGet]
        public async Task<ActionResult<PagedResult<UserListItemDto>>> GetUsers(
            string? q, string? roleId, string? status,
            int page = 1, int pageSize = 10,
            string? sortBy = "CreatedAt", string? sortDir = "desc")
        {
            var users = _db.Users
                .AsNoTracking()
                .Include(u => u.Roles)
                .Include(u => u.Account)
                .AsQueryable();

//            users = ExcludeAdminUsers(users);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var key = q.Trim().ToLower();
                users = users.Where(u =>
                    (u.FullName ?? "").ToLower().Contains(key) ||
                    (u.Email ?? "").ToLower().Contains(key) ||
                    (u.Phone ?? "").Contains(q) ||
                    (u.Account != null && u.Account.Username.ToLower().Contains(key))
                );
            }

//            if (UserStatusHelper.IsValid(status))
//            {
//                var s = UserStatusHelper.Normalize(status!);
//                users = users.Where(u => u.Status == s);
//            }

//            if (!string.IsNullOrWhiteSpace(roleId))
//            {
//                users = users.Where(u => u.Roles.Any(r => r.RoleId == roleId));
//            }

            bool desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            users = (sortBy ?? "").ToLower() switch
            {
                "fullname" => desc ? users.OrderByDescending(u => u.FullName) : users.OrderBy(u => u.FullName),
                "email" => desc ? users.OrderByDescending(u => u.Email) : users.OrderBy(u => u.Email),
                "username" => desc ? users.OrderByDescending(u => u.Account!.Username) : users.OrderBy(u => u.Account!.Username),
                "status" => desc ? users.OrderByDescending(u => u.Status) : users.OrderBy(u => u.Status),
                "lastloginat" => desc ? users.OrderByDescending(u => u.Account!.LastLoginAt) : users.OrderBy(u => u.Account!.LastLoginAt),
                _ => desc ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt),
            };

//            var total = await users.CountAsync();

//            var items = await users
//                .Skip((page - 1) * pageSize)
//                .Take(pageSize)
//                .Select(u => new UserListItemDto
//                {
//                    UserId = u.UserId,
//                    FullName = u.FullName ?? "",
//                    Email = u.Email ?? "",
//                    RoleName = u.Roles.Select(r => r.Name).FirstOrDefault(),
//                    LastLoginAt = u.Account != null ? u.Account.LastLoginAt : null,
//                    Status = u.Status,
//                    CreatedAt = u.CreatedAt
//                })
//                .ToListAsync();

//            return Ok(new PagedResult<UserListItemDto>
//            {
//                Page = page,
//                PageSize = pageSize,
//                TotalItems = total,
//                Items = items
//            });
//        }

        // GET /api/users/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserDetailDto>> Get(Guid id)
        {
            var u = await _db.Users
                .Include(x => x.Roles)
                .Include(x => x.Account)
                .FirstOrDefaultAsync(x => x.UserId == id);

//            if (u == null) return NotFound();
//            if (u.Roles.Any(r => r.Name.ToLower().Contains("admin"))) return NotFound();

            return Ok(new UserDetailDto
            {
                UserId = u.UserId,
                FirstName = u.FirstName ?? "",
                LastName = u.LastName ?? "",
                FullName = u.FullName ?? $"{u.FirstName} {u.LastName}".Trim(),
                Email = u.Email ?? "",
                Phone = u.Phone,
                Address = u.Address,
                Status = u.Status,
                LastLoginAt = u.Account?.LastLoginAt,
                RoleId = u.Roles.Select(r => r.RoleId).FirstOrDefault(),
                HasAccount = u.Account != null,
                Username = u.Account?.Username ?? ""
                // KHÔNG trả mật khẩu nữa
            });
        }

        // POST /api/users
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] UserCreateDto dto)
        {
            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
                if (role != null && role.Name.Contains("admin", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Không được tạo người dùng với vai trò chứa 'admin'." });
            }

//            if (await _db.Users.AnyAsync(x => x.Email == dto.Email))
//                return Conflict(new { message = "Email đã tồn tại" });

//            var now = DateTime.UtcNow;

//            var user = new User
//            {
//                UserId = Guid.NewGuid(),
//                FirstName = dto.FirstName,
//                LastName = dto.LastName,
//                FullName = $"{dto.FirstName} {dto.LastName}".Trim(),
//                Email = dto.Email,
//                Phone = dto.Phone,
//                Address = dto.Address,
//                Status = UserStatusHelper.IsValid(dto.Status) ? UserStatusHelper.Normalize(dto.Status) : "Active",
//                EmailVerified = false,
//                CreatedAt = now,
//                UpdatedAt = now
//            };

//            if (!string.IsNullOrEmpty(dto.RoleId))
//            {
//                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
//                if (role != null) user.Roles.Add(role);
//            }

//            await _db.Users.AddAsync(user);

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                // Bảo đảm Username là duy nhất
                var username = string.IsNullOrWhiteSpace(dto.Username) ? dto.Email : dto.Username.Trim();
                var exists = await _db.Accounts.AnyAsync(a => a.Username == username);
                if (exists) return Conflict(new { message = "Username đã tồn tại." });

                await _db.Accounts.AddAsync(new Account
                {
                    AccountId = Guid.NewGuid(),
                    Username = username,
                    PasswordHash = HashPassword(dto.NewPassword),
                    UserId = user.UserId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

//            await _db.SaveChangesAsync();
//            return CreatedAtAction(nameof(Get), new { id = user.UserId }, new { user.UserId });
//        }

        // PUT /api/users/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UserUpdateDto dto)
        {
            if (id != dto.UserId) return BadRequest();

//            var u = await _db.Users.Include(x => x.Roles).Include(x => x.Account).FirstOrDefaultAsync(x => x.UserId == id);
//            if (u == null) return NotFound();
//            if (u.Roles.Any(r => r.Name.ToLower().Contains("admin"))) return NotFound();

//            if (!string.IsNullOrEmpty(dto.RoleId))
//            {
//                var r = await _db.Roles.FirstOrDefaultAsync(x => x.RoleId == dto.RoleId);
//                if (r != null && r.Name.Contains("admin", StringComparison.OrdinalIgnoreCase))
//                    return BadRequest(new { message = "Không được gán vai trò chứa 'admin'." });
//            }

//            u.FirstName = dto.FirstName;
//            u.LastName = dto.LastName;
//            u.FullName = $"{dto.FirstName} {dto.LastName}".Trim();
//            u.Email = dto.Email;
//            u.Phone = dto.Phone;
//            u.Address = dto.Address;
//            u.Status = UserStatusHelper.IsValid(dto.Status) ? UserStatusHelper.Normalize(dto.Status) : u.Status;
//            u.UpdatedAt = DateTime.UtcNow;

//            u.Roles.Clear();
//            if (!string.IsNullOrEmpty(dto.RoleId))
//            {
//                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
//                if (role != null) u.Roles.Add(role);
//            }

            // Username và password
            var newUsername = string.IsNullOrWhiteSpace(dto.Username) ? dto.Email : dto.Username.Trim();

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                var now = DateTime.UtcNow;

                if (u.Account == null)
                {
                    // Kiểm tra trùng username trước khi tạo account
                    var exists = await _db.Accounts.AnyAsync(a => a.Username == newUsername);
                    if (exists) return Conflict(new { message = "Username đã tồn tại." });

                    u.Account = new Account
                    {
                        AccountId = Guid.NewGuid(),
                        Username = newUsername,
                        PasswordHash = HashPassword(dto.NewPassword),
                        UserId = id,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _db.Accounts.Add(u.Account);
                }
                else
                {
                    // Nếu đổi username, check unique
                    if (!string.Equals(u.Account.Username, newUsername, StringComparison.Ordinal))
                    {
                        var exists = await _db.Accounts.AnyAsync(a => a.Username == newUsername && a.AccountId != u.Account.AccountId);
                        if (exists) return Conflict(new { message = "Username đã tồn tại." });
                        u.Account.Username = newUsername;
                    }

                    u.Account.PasswordHash = HashPassword(dto.NewPassword);
                    u.Account.UpdatedAt = now;
                }
            }
            else
            {
                // Không đổi password, nhưng có thể đổi username nếu có account
                if (u.Account != null && !string.Equals(u.Account.Username, newUsername, StringComparison.Ordinal))
                {
                    var exists = await _db.Accounts.AnyAsync(a => a.Username == newUsername && a.AccountId != u.Account.AccountId);
                    if (exists) return Conflict(new { message = "Username đã tồn tại." });
                    u.Account.Username = newUsername;
                    u.Account.UpdatedAt = DateTime.UtcNow;
                }
            }

//            await _db.SaveChangesAsync();
//            return NoContent();
//        }

        // DELETE /api/users/{id}  (giữ behavior toggle Active <-> Disabled như FE đang dùng)
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> ToggleActive([FromRoute] Guid id)
        {
            var u = await _db.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.UserId == id);
            if (u == null) return NotFound();
            if (u.Roles.Any(r => r.Name.ToLower().Contains("admin"))) return NotFound();

            u.Status = string.Equals(u.Status, "Active", StringComparison.OrdinalIgnoreCase) ? "Disabled" : "Active";
            u.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
