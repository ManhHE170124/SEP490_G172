/// <summary>
/// File: UsersController.cs
/// Layer: Web API (ASP.NET Core)
/// Purpose:
///   CRUD operations for users with the following guardrails:
///   - Any user having a role with name containing "admin" is hidden from FE (404/filtered).
///   - Admin-like roles cannot be assigned/created via these endpoints.
///   - CreatedAt/UpdatedAt are set to UTC timestamps at operation time.
/// Error Model:
///   - All errors standardized to { message: string } at middleware level.
/// Security Note:
///   - Password is AES-encrypted (reversible) to support "show/edit" in admin UI.
/// </summary>

using System.Security.Cryptography;
using System.Text;
using Keytietkiem.DTOs.Users;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.DTOs.Common;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly KeytietkiemDbContext _db;
        public UsersController(KeytietkiemDbContext db) => _db = db;

        private static readonly byte[] AesKey =
            SHA256.HashData(Encoding.UTF8.GetBytes("Keytietkiem::Users::Secret-v1"));

        /// <summary>Encrypt plain password using AES with random IV (IV prefixed to ciphertext).</summary>
        private static byte[] EncryptPassword(string plain)
        {
            using var aes = Aes.Create();
            aes.Key = AesKey;
            aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            var src = Encoding.UTF8.GetBytes(plain);
            var ct = enc.TransformFinalBlock(src, 0, src.Length);
            var output = new byte[aes.IV.Length + ct.Length];
            Buffer.BlockCopy(aes.IV, 0, output, 0, aes.IV.Length);
            Buffer.BlockCopy(ct, 0, output, aes.IV.Length, ct.Length);
            return output;
        }

        /// <summary>Decrypt AES-encrypted password (expects IV-prefixed buffer). Returns null on failure.</summary>
        private static string? DecryptPassword(byte[]? data)
        {
            if (data == null || data.Length < 17) return null;
            using var aes = Aes.Create();
            aes.Key = AesKey;
            var iv = new byte[16];
            Buffer.BlockCopy(data, 0, iv, 0, 16);
            aes.IV = iv;
            using var dec = aes.CreateDecryptor();
            var ct = new byte[data.Length - 16];
            Buffer.BlockCopy(data, 16, ct, 0, ct.Length);
            try
            {
                var pt = dec.TransformFinalBlock(ct, 0, ct.Length);
                return Encoding.UTF8.GetString(pt);
            }
            catch { return null; }
        }

        /// <summary>Filter helper: exclude users who have any role whose name contains "admin" (case-insensitive).</summary>
        private static IQueryable<User> ExcludeAdminUsers(IQueryable<User> q)
            => q.Where(u => !u.Roles.Any(r => r.Name.ToLower().Contains("admin")));

        /// <summary>
        /// List users with filters, pagination and sorting.
        /// Excludes users in admin-like roles.
        /// </summary>
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

            users = ExcludeAdminUsers(users);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var key = q.Trim().ToLower();
                users = users.Where(u =>
                    (u.FullName ?? "").ToLower().Contains(key) ||
                    (u.Email ?? "").ToLower().Contains(key) ||
                    (u.Phone ?? "").Contains(q));
            }

            if (UserStatusHelper.IsValid(status))
            {
                var s = UserStatusHelper.Normalize(status!);
                users = users.Where(u => u.Status == s);
            }

            if (!string.IsNullOrWhiteSpace(roleId))
            {
                users = users.Where(u => u.Roles.Any(r => r.RoleId == roleId));
            }

            bool desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            users = (sortBy ?? "").ToLower() switch
            {
                "fullname" => desc ? users.OrderByDescending(u => u.FullName) : users.OrderBy(u => u.FullName),
                "email" => desc ? users.OrderByDescending(u => u.Email) : users.OrderBy(u => u.Email),
                "status" => desc ? users.OrderByDescending(u => u.Status) : users.OrderBy(u => u.Status),
                "lastloginat" => desc ? users.OrderByDescending(u => u.Account!.LastLoginAt) : users.OrderBy(u => u.Account!.LastLoginAt),
                _ => desc ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt),
            };

            var total = await users.CountAsync();

            var items = await users
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListItemDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName ?? "",
                    Email = u.Email ?? "",
                    RoleName = u.Roles.Select(r => r.Name).FirstOrDefault(),
                    LastLoginAt = u.Account != null ? u.Account.LastLoginAt : null,
                    Status = u.Status,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Ok(new PagedResult<UserListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                Items = items
            });
        }

        /// <summary>
        /// Get user details by id. Returns 404 if the user belongs to admin-like role.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserDetailDto>> Get(Guid id)
        {
            var u = await _db.Users
                .Include(x => x.Roles)
                .Include(x => x.Account)
                .FirstOrDefaultAsync(x => x.UserId == id);

            if (u == null) return NotFound();
            if (u.Roles.Any(r => r.Name.ToLower().Contains("admin"))) return NotFound();

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
                PasswordPlain = DecryptPassword(u.Account?.PasswordHash)
            });
        }

        /// <summary>
        /// Create a new user. Rejects admin-like roles. Sets CreatedAt/UpdatedAt to UTC now.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] UserCreateDto dto)
        {
            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
                if (role != null && role.Name.Contains("admin", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Không được tạo người dùng với vai trò chứa 'admin'." });
            }

            if (await _db.Users.AnyAsync(x => x.Email == dto.Email))
                return Conflict(new { message = "Email đã tồn tại" });

            var now = DateTime.UtcNow;

            var user = new User
            {
                UserId = Guid.NewGuid(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                FullName = $"{dto.FirstName} {dto.LastName}".Trim(),
                Email = dto.Email,
                Phone = dto.Phone,
                Address = dto.Address,
                Status = UserStatusHelper.IsValid(dto.Status) ? UserStatusHelper.Normalize(dto.Status) : "Active",
                EmailVerified = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
                if (role != null) user.Roles.Add(role);
            }

            await _db.Users.AddAsync(user);

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                await _db.Accounts.AddAsync(new Account
                {
                    AccountId = Guid.NewGuid(),
                    Username = dto.Email,
                    PasswordHash = EncryptPassword(dto.NewPassword),
                    UserId = user.UserId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = user.UserId }, new { user.UserId });
        }

        /// <summary>
        /// Update an existing user. Rejects admin-like roles. Sets UpdatedAt to UTC now.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UserUpdateDto dto)
        {
            if (id != dto.UserId) return BadRequest();

            var u = await _db.Users.Include(x => x.Roles).Include(x => x.Account).FirstOrDefaultAsync(x => x.UserId == id);
            if (u == null) return NotFound();
            if (u.Roles.Any(r => r.Name.ToLower().Contains("admin"))) return NotFound();

            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var r = await _db.Roles.FirstOrDefaultAsync(x => x.RoleId == dto.RoleId);
                if (r != null && r.Name.Contains("admin", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Không được gán vai trò chứa 'admin'." });
            }

            u.FirstName = dto.FirstName;
            u.LastName = dto.LastName;
            u.FullName = $"{dto.FirstName} {dto.LastName}".Trim();
            u.Email = dto.Email;
            u.Phone = dto.Phone;
            u.Address = dto.Address;
            u.Status = UserStatusHelper.IsValid(dto.Status) ? UserStatusHelper.Normalize(dto.Status) : u.Status;
            u.UpdatedAt = DateTime.UtcNow;

            u.Roles.Clear();
            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
                if (role != null) u.Roles.Add(role);
            }

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                var now = DateTime.UtcNow;
                if (u.Account == null)
                {
                    u.Account = new Account
                    {
                        AccountId = Guid.NewGuid(),
                        Username = dto.Email,
                        PasswordHash = EncryptPassword(dto.NewPassword),
                        UserId = id,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _db.Accounts.Add(u.Account);
                }
                else
                {
                    u.Account.Username = dto.Email;
                    u.Account.PasswordHash = EncryptPassword(dto.NewPassword);
                    u.Account.UpdatedAt = now;
                }
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Toggle Active/Disabled status of a user. Returns 404 for admin-like users.
        /// Sets UpdatedAt to UTC now.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> ToggleDisable([FromRoute] Guid id)
        {
            var u = await _db.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.UserId == id);
            if (u == null) return NotFound();
            if (u.Roles.Any(r => r.Name.ToLower().Contains("admin"))) return NotFound();

            u.Status = u.Status == UserStatus.Active.ToString()
                ? UserStatus.Disabled.ToString()
                : UserStatus.Active.ToString();

            u.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
