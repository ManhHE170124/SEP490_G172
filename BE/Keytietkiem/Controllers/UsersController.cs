using System.Security.Cryptography;
using System.Text;
using Keytietkiem.DTOs.Users;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Enums;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly KeytietkiemDbContext _db;
        public UsersController(KeytietkiemDbContext db) => _db = db;

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
            return result; // 48 bytes (16 salt + 32 hash)
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
            => q.Where(u => !u.Roles.Any(r => r.Code.ToLower().Contains("admin")));

        /// <summary>
        /// Lấy message lỗi đầu tiên từ ModelState (DataAnnotations trên DTO),
        /// dùng cho 400 BadRequest khi dữ liệu không hợp lệ (vượt length, sai format…)
        /// </summary>
        private string GetFirstModelError()
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault();
            return firstError?.ErrorMessage
                   ?? "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại các trường thông tin.";
        }

        /// <summary>
        /// Helper: Admin gán / đổi / huỷ gói hỗ trợ cho user (không áp dụng cho user tạm thời).
        /// targetSupportPlanId:
        ///   - null hoặc <= 0  : huỷ mọi subscription đang Active (chỉ còn mức độ ưu tiên gốc / chỉnh tay).
        ///   - > 0             : chuyển sang gói mới (Cancel gói cũ nếu có, tạo subscription mới 1 tháng,
        ///                       update SupportPriorityLevel theo PriorityLevel của gói).
        /// </summary>
        private async Task<(bool ok, string? error)> ApplySupportPlanChangeByAdmin(
            User user,
            int? targetSupportPlanId)
        {
            var nowUtc = DateTime.UtcNow;

            // Lấy subs đang active (đảm bảo chỉ có 0 hoặc 1 nhưng để chắc ăn vẫn hủy all bên dưới)
            var activeSub = await _db.UserSupportPlanSubscriptions
                .Include(s => s.SupportPlan)
                .Where(s =>
                    s.UserId == user.UserId &&
                    s.Status == "Active" &&
                    (!s.ExpiresAt.HasValue || s.ExpiresAt > nowUtc))
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            // Nếu target null hoặc <= 0 => huỷ mọi gói hiện tại, không tạo gói mới
            if (!targetSupportPlanId.HasValue || targetSupportPlanId.Value <= 0)
            {
                var allActiveSubsToCancel = await _db.UserSupportPlanSubscriptions
                    .Where(s => s.UserId == user.UserId && s.Status == "Active")
                    .ToListAsync();

                foreach (var sub in allActiveSubsToCancel)
                {
                    sub.Status = "Cancelled";
                    if (!sub.ExpiresAt.HasValue || sub.ExpiresAt > nowUtc)
                    {
                        sub.ExpiresAt = nowUtc;
                    }
                }

                // Khi huỷ gói, SupportPriorityLevel của user sẽ quay về mức gốc (ở đây: giữ nguyên
                // giá trị hiện được set trực tiếp bằng tay qua form admin).
                // Nếu bạn muốn có "priority gốc" riêng, cần thêm cột khác, còn hiện tại để nguyên.
                return (true, null);
            }

            var planId = targetSupportPlanId.Value;

            // Nếu gói mới trùng với gói hiện đang active thì bỏ qua (không đổi)
            if (activeSub != null && activeSub.SupportPlanId == planId)
            {
                return (true, null);
            }

            var plan = await _db.SupportPlans
                .FirstOrDefaultAsync(p => p.SupportPlanId == planId && p.IsActive);

            if (plan == null)
            {
                return (false, "Gói hỗ trợ không tồn tại hoặc đã bị khóa.");
            }

            // ===== Đảm bảo chỉ có 1 subscription Active tại một thời điểm =====
            // Huỷ hết các subscription đang Active cho user (nếu còn)
            var allActiveSubs = await _db.UserSupportPlanSubscriptions
                .Where(s => s.UserId == user.UserId && s.Status == "Active")
                .ToListAsync();

            foreach (var sub in allActiveSubs)
            {
                sub.Status = "Cancelled";
                if (!sub.ExpiresAt.HasValue || sub.ExpiresAt > nowUtc)
                {
                    sub.ExpiresAt = nowUtc;
                }
            }

            // Tạo subscription thủ công cho gói mới (thời hạn 1 tháng)
            var manualSub = new UserSupportPlanSubscription
            {
                SubscriptionId = Guid.NewGuid(),
                UserId = user.UserId,
                SupportPlanId = plan.SupportPlanId,
                Status = "Active",
                StartedAt = nowUtc,
                ExpiresAt = nowUtc.AddMonths(1),
                PaymentId = null,
                Note = "Assigned/updated by admin từ UsersController."
            };
            _db.UserSupportPlanSubscriptions.Add(manualSub);

            // ==== Cập nhật priority của user theo gói ====
            // Giả sử SupportPlan.PriorityLevel là int, map thẳng vào user.
            user.SupportPriorityLevel = plan.PriorityLevel;

            return (true, null);
        }

        // GET /api/users
        [HttpGet]
        public async Task<ActionResult<PagedResult<UserListItemDto>>> GetUsers(
            string? q,
            string? roleId,
            string? status,
            bool isTemp = false,
            int? supportPriorityLevel = null,
            int page = 1,
            int pageSize = 10,
            string? sortBy = "CreatedAt",
            string? sortDir = "desc")
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

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
                    (u.Phone ?? "").Contains(q) ||
                    (u.Account != null && u.Account.Username.ToLower().Contains(key))
                );
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

            // Lọc theo user tạm thời (IsTemp). Mặc định isTemp = false => chỉ hiển thị user thật.
            users = isTemp
                ? users.Where(u => u.IsTemp)
                : users.Where(u => !u.IsTemp);

            // Lọc theo mức độ ưu tiên (SupportPriorityLevel) nếu có
            if (supportPriorityLevel.HasValue)
            {
                var lv = supportPriorityLevel.Value;
                users = users.Where(u => u.SupportPriorityLevel == lv);
            }

            bool desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            users = (sortBy ?? "").ToLower() switch
            {
                "fullname" => desc
                    ? users.OrderByDescending(u => u.FullName)
                    : users.OrderBy(u => u.FullName),

                "email" => desc
                    ? users.OrderByDescending(u => u.Email)
                    : users.OrderBy(u => u.Email),

                "username" => desc
                    ? users.OrderByDescending(u => u.Account != null ? u.Account.Username : "")
                    : users.OrderBy(u => u.Account != null ? u.Account.Username : ""),

                "status" => desc
                    ? users.OrderByDescending(u => u.Status)
                    : users.OrderBy(u => u.Status),

                "lastloginat" => desc
                    ? users.OrderByDescending(u => u.Account != null ? u.Account.LastLoginAt : null)
                    : users.OrderBy(u => u.Account != null ? u.Account.LastLoginAt : null),

                _ => desc
                    ? users.OrderByDescending(u => u.CreatedAt)
                    : users.OrderBy(u => u.CreatedAt),
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
                    CreatedAt = u.CreatedAt,

                    // ==== Mức độ ưu tiên & cờ user tạm thời ====
                    SupportPriorityLevel = u.SupportPriorityLevel,
                    IsTemp = u.IsTemp
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

        // GET /api/users/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserDetailDto>> Get(Guid id)
        {
            var u = await _db.Users
                .Include(x => x.Roles)
                .Include(x => x.Account)
                .Include(x => x.UserSupportPlanSubscriptions)
                    .ThenInclude(s => s.SupportPlan)
                .FirstOrDefaultAsync(x => x.UserId == id);

            if (u == null) return NotFound();
            if (u.Roles.Any(r => r.Code.ToLower().Contains("admin"))) return NotFound();
            if (u.IsTemp) return NotFound(); // Không cho xem chi tiết user tạm thời

            var now = DateTime.UtcNow;
            var activeSub = u.UserSupportPlanSubscriptions
                .Where(s => s.Status == "Active" && (!s.ExpiresAt.HasValue || s.ExpiresAt > now))
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefault();

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
                Username = u.Account?.Username ?? "",

                // ==== Mức độ ưu tiên & user tạm thời ====
                SupportPriorityLevel = u.SupportPriorityLevel,
                IsTemp = u.IsTemp,

                // ==== Thông tin gói hỗ trợ đang active (nếu có) ====
                ActiveSupportPlanId = activeSub?.SupportPlanId,
                ActiveSupportPlanName = activeSub?.SupportPlan?.Name,
                ActiveSupportPlanStartedAt = activeSub?.StartedAt,
                ActiveSupportPlanExpiresAt = activeSub?.ExpiresAt,
                ActiveSupportPlanStatus = activeSub?.Status,

                // ==== Tổng tiền đã tiêu (TotalProductSpend) ====
                TotalProductSpend = u.TotalProductSpend
            });
        }

        // POST /api/users
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] UserCreateDto dto)
        {
            // Chặn sớm dữ liệu sai format / vượt độ dài DB (DataAnnotations trong DTO)
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = GetFirstModelError() });
            }

            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
                if (role != null && role.Code.Contains("admin", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Không được tạo người dùng với vai trò chứa 'admin'." });
            }

            // Email UNIQUE
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
                UpdatedAt = now,

                // ==== Mức độ ưu tiên gốc & user tạm thời ====
                SupportPriorityLevel = dto.SupportPriorityLevel,
                IsTemp = false // Admin tạo mới luôn là user thật
            };

            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
                if (role != null) user.Roles.Add(role);
            }

            await _db.Users.AddAsync(user);

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

            // Nếu admin chọn gói hỗ trợ khi tạo user -> tạo subscription thủ công
            if (dto.ActiveSupportPlanId.HasValue && dto.ActiveSupportPlanId.Value > 0)
            {
                var (okPlan, planError) =
                    await ApplySupportPlanChangeByAdmin(user, dto.ActiveSupportPlanId.Value);
                if (!okPlan)
                {
                    return BadRequest(new { message = planError });
                }
            }

            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = user.UserId }, new { user.UserId });
        }

        // PUT /api/users/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UserUpdateDto dto)
        {
            if (id != dto.UserId) return BadRequest();

            // Validate theo DTO (length, email, phone, status…)
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = GetFirstModelError() });
            }

            var u = await _db.Users
                .Include(x => x.Roles)
                .Include(x => x.Account)
                .FirstOrDefaultAsync(x => x.UserId == id);

            if (u == null) return NotFound();
            if (u.Roles.Any(r => r.Code.ToLower().Contains("admin"))) return NotFound();
            if (u.IsTemp) return NotFound(); // Không cho sửa user tạm thời

            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var r = await _db.Roles.FirstOrDefaultAsync(x => x.RoleId == dto.RoleId);
                if (r != null && r.Code.Contains("admin", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Không được gán vai trò chứa 'admin'." });
            }

            // Check email UNIQUE khi sửa
            if (!string.Equals(u.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
            {
                var emailExists = await _db.Users.AnyAsync(x => x.Email == dto.Email && x.UserId != id);
                if (emailExists)
                    return Conflict(new { message = "Email đã tồn tại" });
            }

            u.FirstName = dto.FirstName;
            u.LastName = dto.LastName;
            u.FullName = $"{dto.FirstName} {dto.LastName}".Trim();
            u.Email = dto.Email;
            u.Phone = dto.Phone;
            u.Address = dto.Address;
            u.Status = UserStatusHelper.IsValid(dto.Status) ? UserStatusHelper.Normalize(dto.Status) : u.Status;

            // ==== Cập nhật mức độ ưu tiên gốc (SupportPriorityLevel) (trường hợp không gói hoặc set tay) ====
            u.SupportPriorityLevel = dto.SupportPriorityLevel;

            u.UpdatedAt = DateTime.UtcNow;

            u.Roles.Clear();
            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
                if (role != null) u.Roles.Add(role);
            }

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

            // Xử lý gói hỗ trợ nếu admin có truyền vào
            if (dto.ActiveSupportPlanId.HasValue)
            {
                int? targetPlanId = dto.ActiveSupportPlanId.Value <= 0
                    ? (int?)null
                    : dto.ActiveSupportPlanId.Value;

                var (okPlan, planError) =
                    await ApplySupportPlanChangeByAdmin(u, targetPlanId);

                if (!okPlan)
                {
                    return BadRequest(new { message = planError });
                }
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // DELETE /api/users/{id}  (giữ behavior toggle Active <-> Disabled như FE đang dùng)
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> ToggleActive([FromRoute] Guid id)
        {
            var u = await _db.Users
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.UserId == id);

            if (u == null) return NotFound();
            if (u.Roles.Any(r => r.Code.ToLower().Contains("admin"))) return NotFound();
            if (u.IsTemp) return NotFound(); // Không cho toggle user tạm thời

            u.Status = string.Equals(u.Status, "Active", StringComparison.OrdinalIgnoreCase)
                ? "Disabled"
                : "Active";

            u.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
