using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.DTOs.Users;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly KeytietkiemDbContext _db;
        private readonly IAuditLogger _auditLogger;

        private bool IsValidPhoneNumber(string? phone)
        {
            if (string.IsNullOrEmpty(phone)) return true;
            return System.Text.RegularExpressions.Regex.IsMatch(phone, @"^0(3|5|7|8|9)[0-9]{8}$");
        }

        public UsersController(KeytietkiemDbContext db, IAuditLogger auditLogger)
        {
            _db = db;
            _auditLogger = auditLogger;
        }

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
            => q.Where(u => !u.Roles.Any(r => ((r.Code ?? "").ToLower()).Contains("admin")));

        /// <summary>
        /// Lấy message lỗi đầu tiên từ ModelState (DataAnnotations trên DTO)
        /// </summary>
        private string GetFirstModelError()
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault();
            return firstError?.ErrorMessage
                   ?? "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại các trường thông tin.";
        }

        private static bool IsActiveCustomer(User user)
        {
            return string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase)
                   && user.Roles.Any(r => !string.IsNullOrEmpty(r.Code)
                                         && r.Code.Equals("customer", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Tính loyalty base theo bảng SupportPriorityLoyaltyRule (DB mới)
        /// </summary>
        private async Task<int> CalculateLoyaltyBaseLevelAsync(User user)
        {
            // Nếu chưa có rule thì base = 0
            var spend = user.TotalProductSpend;

            var lv = await _db.Set<SupportPriorityLoyaltyRule>()
                .AsNoTracking()
                .Where(r => r.IsActive && r.MinTotalSpend <= spend)
                .OrderByDescending(r => r.MinTotalSpend)
                .Select(r => (int?)r.PriorityLevel)
                .FirstOrDefaultAsync();

            return lv ?? 0;
        }

        /// <summary>
        /// Lấy PriorityLevel của gói support đang active (nếu có) (DB mới)
        /// </summary>
        private async Task<int> GetActivePlanPriorityLevelAsync(Guid userId)
        {
            var nowUtc = DateTime.UtcNow;

            // JOIN subscription + plan để không phụ thuộc navigation name
            var planLv = await (
                from s in _db.Set<UserSupportPlanSubscription>().AsNoTracking()
                join p in _db.Set<SupportPlan>().AsNoTracking()
                    on s.SupportPlanId equals p.SupportPlanId
                where s.UserId == userId
                      && s.Status == "Active"
                      && (!s.ExpiresAt.HasValue || s.ExpiresAt > nowUtc)
                orderby s.StartedAt descending
                select (int?)p.PriorityLevel
            ).FirstOrDefaultAsync();

            return planLv ?? 0;
        }

        /// <summary>
        /// Admin gán / đổi / huỷ gói hỗ trợ cho user (không áp dụng user tạm thời).
        /// targetSupportPlanId:
        ///   - null hoặc <= 0  : huỷ mọi subscription đang Active
        ///   - > 0             : chuyển sang gói mới (Cancel gói cũ nếu có, tạo subscription mới 1 tháng)
        /// Rule: Không cho phép gán gói có PriorityLevel thấp hơn loyalty base (Active Customer).
        /// </summary>
        private async Task<(bool ok, string? error)> ApplySupportPlanChangeByAdmin(
            User user,
            int? targetSupportPlanId)
        {
            var nowUtc = DateTime.UtcNow;

            if (user.IsTemp)
                return (false, "Không thể thay đổi gói hỗ trợ cho người dùng tạm thời (IsTemp = true).");

            var isActiveCustomer = IsActiveCustomer(user);
            var loyaltyBaseLevel = isActiveCustomer ? await CalculateLoyaltyBaseLevelAsync(user) : user.SupportPriorityLevel;

            // sub đang active (nếu có)
            var activeSub = await _db.Set<UserSupportPlanSubscription>()
                .AsNoTracking()
                .Where(s =>
                    s.UserId == user.UserId &&
                    s.Status == "Active" &&
                    (!s.ExpiresAt.HasValue || s.ExpiresAt > nowUtc))
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            // ===== Huỷ gói =====
            if (!targetSupportPlanId.HasValue || targetSupportPlanId.Value <= 0)
            {
                var allActiveSubsToCancel = await _db.Set<UserSupportPlanSubscription>()
                    .Where(s => s.UserId == user.UserId && s.Status == "Active")
                    .ToListAsync();

                foreach (var sub in allActiveSubsToCancel)
                {
                    sub.Status = "Cancelled";
                    if (!sub.ExpiresAt.HasValue || sub.ExpiresAt > nowUtc)
                        sub.ExpiresAt = nowUtc;
                }

                // Khi huỷ gói: nếu là Active Customer thì đưa về loyalty base
                if (isActiveCustomer)
                {
                    user.SupportPriorityLevel = loyaltyBaseLevel;
                    user.UpdatedAt = nowUtc;
                }

                return (true, null);
            }

            var planId = targetSupportPlanId.Value;

            if (activeSub != null && activeSub.SupportPlanId == planId)
            {
                return (false, "Gói hỗ trợ được chọn đang là gói hiện tại của người dùng, không có thay đổi nào được áp dụng.");
            }

            var plan = await _db.Set<SupportPlan>()
                .FirstOrDefaultAsync(p => p.SupportPlanId == planId && p.IsActive);

            if (plan == null)
                return (false, "Gói hỗ trợ không tồn tại hoặc đã bị khóa.");

            // Rule: không cho gán plan < loyalty base (Active Customer)
            if (isActiveCustomer && plan.PriorityLevel < loyaltyBaseLevel)
            {
                return (false,
                    $"Không thể gán gói hỗ trợ có PriorityLevel = {plan.PriorityLevel} " +
                    $"thấp hơn mức loyalty base hiện tại của người dùng (loyalty base = {loyaltyBaseLevel}).");
            }

            // ===== Đảm bảo chỉ có 1 subscription Active =====
            var allActiveSubs = await _db.Set<UserSupportPlanSubscription>()
                .Where(s => s.UserId == user.UserId && s.Status == "Active")
                .ToListAsync();

            foreach (var sub in allActiveSubs)
            {
                sub.Status = "Cancelled";
                if (!sub.ExpiresAt.HasValue || sub.ExpiresAt > nowUtc)
                    sub.ExpiresAt = nowUtc;
            }

            // DB mới: UserSupportPlanSubscription KHÔNG có PaymentId
            var manualSub = new UserSupportPlanSubscription
            {
                SubscriptionId = Guid.NewGuid(),
                UserId = user.UserId,
                SupportPlanId = plan.SupportPlanId,
                Status = "Active",
                StartedAt = nowUtc,
                ExpiresAt = nowUtc.AddMonths(1),
                Note = "Assigned/updated by admin từ UsersController."
            };
            _db.Set<UserSupportPlanSubscription>().Add(manualSub);

            user.SupportPriorityLevel = plan.PriorityLevel;
            user.UpdatedAt = nowUtc;

            return (true, null);
        }

        // GET /api/users
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN,RoleCodes.CUSTOMER_CARE)]
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

            var users = _db.Set<User>()
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

            users = isTemp ? users.Where(u => u.IsTemp) : users.Where(u => !u.IsTemp);

            if (supportPriorityLevel.HasValue)
            {
                var lv = supportPriorityLevel.Value;
                users = users.Where(u => u.SupportPriorityLevel == lv);
            }

            bool desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            users = (sortBy ?? "").ToLower() switch
            {
                "fullname" => desc ? users.OrderByDescending(u => u.FullName) : users.OrderBy(u => u.FullName),
                "email" => desc ? users.OrderByDescending(u => u.Email) : users.OrderBy(u => u.Email),
                "username" => desc
                    ? users.OrderByDescending(u => u.Account != null ? u.Account.Username : "")
                    : users.OrderBy(u => u.Account != null ? u.Account.Username : ""),
                "status" => desc ? users.OrderByDescending(u => u.Status) : users.OrderBy(u => u.Status),
                "lastloginat" => desc
                    ? users.OrderByDescending(u => u.Account != null ? u.Account.LastLoginAt : null)
                    : users.OrderBy(u => u.Account != null ? u.Account.LastLoginAt : null),
                _ => desc ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt),
            };

            var total = await users.CountAsync();

            var pageQuery = users.Skip((page - 1) * pageSize).Take(pageSize);

            // (Giữ logic refresh priority theo trang hiện tại) - tính trực tiếp theo DB mới
            var pageUsersMeta = await pageQuery
                .Select(u => new
                {
                    u.UserId,
                    u.Status,
                    RoleCodes = u.Roles.Select(r => r.Code)
                })
                .ToListAsync();

            foreach (var meta in pageUsersMeta)
            {
                var isActiveCustomerForPage =
                    string.Equals(meta.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                    meta.RoleCodes.Any(code => !string.IsNullOrEmpty(code) &&
                                               code.Equals("customer", StringComparison.OrdinalIgnoreCase));

                if (!isActiveCustomerForPage) continue;

                // recalc final = max(loyaltyBase, activePlan)
                var trackedUser = await _db.Set<User>()
                    .Include(u => u.Roles)
                    .FirstOrDefaultAsync(u => u.UserId == meta.UserId);

                if (trackedUser == null) continue;

                var loyaltyBase = await CalculateLoyaltyBaseLevelAsync(trackedUser);
                var planLv = await GetActivePlanPriorityLevelAsync(trackedUser.UserId);
                var finalLv = Math.Max(loyaltyBase, planLv);

                if (trackedUser.SupportPriorityLevel != finalLv)
                {
                    trackedUser.SupportPriorityLevel = finalLv;
                    trackedUser.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }

            var items = await pageQuery
                .Select(u => new UserListItemDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName ?? "",
                    Email = u.Email ?? "",
                    RoleName = u.Roles.Select(r => r.Name).FirstOrDefault(),
                    LastLoginAt = u.Account != null ? u.Account.LastLoginAt : null,
                    Status = u.Status,
                    CreatedAt = u.CreatedAt,
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
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<UserDetailDto>> Get(Guid id)
        {
            var u = await _db.Set<User>()
                .Include(x => x.Roles)
                .Include(x => x.Account)
                .FirstOrDefaultAsync(x => x.UserId == id);

            if (u == null)
                return NotFound(new { message = "Không tìm thấy người dùng với Id đã cung cấp." });

            if (u.Roles.Any(r => ((r.Code ?? "").ToLower()).Contains("admin")))
                return BadRequest(new { message = "Không được xem chi tiết hoặc thao tác trên tài khoản có vai trò admin." });

            if (u.IsTemp)
                return BadRequest(new { message = "Không thể xem chi tiết người dùng tạm thời (IsTemp = true)." });

            var now = DateTime.UtcNow;

            // active subscription (query riêng theo DB mới)
            var activeSub = await (
                from s in _db.Set<UserSupportPlanSubscription>().AsNoTracking()
                join p in _db.Set<SupportPlan>().AsNoTracking()
                    on s.SupportPlanId equals p.SupportPlanId
                where s.UserId == u.UserId
                      && s.Status == "Active"
                      && (!s.ExpiresAt.HasValue || s.ExpiresAt > now)
                orderby s.StartedAt descending
                select new
                {
                    s.SupportPlanId,
                    PlanName = p.Name,
                    s.StartedAt,
                    s.ExpiresAt,
                    s.Status
                }
            ).FirstOrDefaultAsync();

            // refresh final priority cho Active Customer
            if (IsActiveCustomer(u))
            {
                var loyaltyBase = await CalculateLoyaltyBaseLevelAsync(u);
                var planLv = await GetActivePlanPriorityLevelAsync(u.UserId);
                var finalLv = Math.Max(loyaltyBase, planLv);

                if (u.SupportPriorityLevel != finalLv)
                {
                    u.SupportPriorityLevel = finalLv;
                    u.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }

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

                SupportPriorityLevel = u.SupportPriorityLevel,
                IsTemp = u.IsTemp,

                ActiveSupportPlanId = activeSub?.SupportPlanId,
                ActiveSupportPlanName = activeSub?.PlanName,
                ActiveSupportPlanStartedAt = activeSub?.StartedAt,
                ActiveSupportPlanExpiresAt = activeSub?.ExpiresAt,
                ActiveSupportPlanStatus = activeSub?.Status,

                TotalProductSpend = u.TotalProductSpend
            });
        }

        // POST /api/users
        [HttpPost]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult> Create([FromBody] UserCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = GetFirstModelError() });

            if (!string.IsNullOrEmpty(dto.Phone) && !IsValidPhoneNumber(dto.Phone))
            {
                 return BadRequest(new { message = "Số điện thoại không hợp lệ (phải là số VN 10 chữ số, bắt đầu bằng 03, 05, 07, 08, 09)" });
            }

            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await _db.Set<Role>().FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
                if (role != null && (role.Code ?? "").Contains("admin", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Không được tạo người dùng với vai trò chứa 'admin'." });
            }

            if (await _db.Set<User>().AnyAsync(x => x.Email == dto.Email))
                return Conflict(new { message = "Email đã tồn tại, vui lòng dùng email khác." });

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

                SupportPriorityLevel = dto.SupportPriorityLevel,
                TotalProductSpend = 0m, // DB mới: NOT NULL
                IsTemp = false
            };

            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await _db.Set<Role>().FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
                if (role != null) user.Roles.Add(role);
            }

            _db.Set<User>().Add(user);

            bool hasAccount = false;
            string? createdUsername = null;

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                var username = string.IsNullOrWhiteSpace(dto.Username) ? dto.Email : dto.Username.Trim();
                var exists = await _db.Set<Account>().AnyAsync(a => a.Username == username);
                if (exists) return Conflict(new { message = "Username đã tồn tại, vui lòng dùng username khác." });

                var acc = new Account
                {
                    AccountId = Guid.NewGuid(),
                    Username = username,
                    PasswordHash = HashPassword(dto.NewPassword),
                    UserId = user.UserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Set<Account>().Add(acc);

                hasAccount = true;
                createdUsername = username;
            }

            if (dto.ActiveSupportPlanId.HasValue && dto.ActiveSupportPlanId.Value > 0)
            {
                var (okPlan, planError) =
                    await ApplySupportPlanChangeByAdmin(user, dto.ActiveSupportPlanId.Value);

                if (!okPlan)
                    return BadRequest(new { message = planError });
            }

            await _db.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "CreateUser",
                entityType: "User",
                entityId: user.UserId.ToString(),
                before: null,
                after: new
                {
                    user.UserId,
                    user.FirstName,
                    user.LastName,
                    user.FullName,
                    user.Email,
                    user.Phone,
                    user.Address,
                    user.Status,
                    user.SupportPriorityLevel,
                    user.TotalProductSpend,
                    user.IsTemp,
                    RoleIds = user.Roles.Select(r => r.RoleId).ToList(),
                    HasAccount = hasAccount,
                    Username = createdUsername
                }
            );

            return CreatedAtAction(nameof(Get), new { id = user.UserId }, new { user.UserId });
        }

        // PUT /api/users/{id}
        [HttpPut("{id:guid}")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UserUpdateDto dto)
        {
            if (id != dto.UserId)
                return BadRequest(new { message = "UserId trong URL không khớp với UserId trong dữ liệu gửi lên." });

            if (!ModelState.IsValid)
                return BadRequest(new { message = GetFirstModelError() });

            if (!string.IsNullOrEmpty(dto.Phone) && !IsValidPhoneNumber(dto.Phone))
            {
                 return BadRequest(new { message = "Số điện thoại không hợp lệ (phải là số VN 10 chữ số, bắt đầu bằng 03, 05, 07, 08, 09)" });
            }

            var u = await _db.Users
                .Include(x => x.Roles)
                .Include(x => x.Account)
                .FirstOrDefaultAsync(x => x.UserId == id);

            if (u == null)
                return NotFound(new { message = "Không tìm thấy người dùng để cập nhật." });

            if (u.Roles.Any(r => ((r.Code ?? "").ToLower()).Contains("admin")))
                return BadRequest(new { message = "Không được phép sửa thông tin tài khoản admin." });

            if (u.IsTemp)
                return BadRequest(new { message = "Không thể sửa thông tin người dùng tạm thời (IsTemp = true)." });

            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var r = await _db.Set<Role>().FirstOrDefaultAsync(x => x.RoleId == dto.RoleId);
                if (r != null && (r.Code ?? "").Contains("admin", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Không được gán vai trò chứa 'admin' cho người dùng." });
            }

            if (!string.Equals(u.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
            {
                var emailExists = await _db.Set<User>().AnyAsync(x => x.Email == dto.Email && x.UserId != id);
                if (emailExists)
                    return Conflict(new { message = "Email đã tồn tại, vui lòng dùng email khác." });
            }

            var before = new
            {
                u.UserId,
                u.FirstName,
                u.LastName,
                u.FullName,
                u.Email,
                u.Phone,
                u.Address,
                u.Status,
                u.SupportPriorityLevel,
                u.TotalProductSpend,
                u.IsTemp,
                RoleIds = u.Roles.Select(r => r.RoleId).ToList(),
                HasAccount = u.Account != null,
                Username = u.Account?.Username
            };

            u.FirstName = dto.FirstName;
            u.LastName = dto.LastName;
            u.FullName = $"{dto.FirstName} {dto.LastName}".Trim();
            u.Email = dto.Email;
            u.Phone = dto.Phone;
            u.Address = dto.Address;
            u.Status = UserStatusHelper.IsValid(dto.Status) ? UserStatusHelper.Normalize(dto.Status) : u.Status;

            u.SupportPriorityLevel = dto.SupportPriorityLevel;
            u.UpdatedAt = DateTime.UtcNow;

            u.Roles.Clear();
            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await _db.Set<Role>().FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
                if (role != null) u.Roles.Add(role);
            }

            var newUsername = string.IsNullOrWhiteSpace(dto.Username) ? dto.Email : dto.Username.Trim();

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                var now = DateTime.UtcNow;

                if (u.Account == null)
                {
                    var exists = await _db.Set<Account>().AnyAsync(a => a.Username == newUsername);
                    if (exists) return Conflict(new { message = "Username đã tồn tại, vui lòng dùng username khác." });

                    u.Account = new Account
                    {
                        AccountId = Guid.NewGuid(),
                        Username = newUsername,
                        PasswordHash = HashPassword(dto.NewPassword),
                        UserId = id,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _db.Set<Account>().Add(u.Account);
                }
                else
                {
                    if (!string.Equals(u.Account.Username, newUsername, StringComparison.Ordinal))
                    {
                        var exists = await _db.Set<Account>()
                            .AnyAsync(a => a.Username == newUsername && a.AccountId != u.Account.AccountId);

                        if (exists) return Conflict(new { message = "Username đã tồn tại, vui lòng dùng username khác." });

                        u.Account.Username = newUsername;
                    }

                    u.Account.PasswordHash = HashPassword(dto.NewPassword);
                    u.Account.UpdatedAt = now;
                }
            }
            else
            {
                if (u.Account != null && !string.Equals(u.Account.Username, newUsername, StringComparison.Ordinal))
                {
                    var exists = await _db.Set<Account>()
                        .AnyAsync(a => a.Username == newUsername && a.AccountId != u.Account.AccountId);

                    if (exists) return Conflict(new { message = "Username đã tồn tại, vui lòng dùng username khác." });

                    u.Account.Username = newUsername;
                    u.Account.UpdatedAt = DateTime.UtcNow;
                }
            }

            if (dto.ActiveSupportPlanId.HasValue)
            {
                int? targetPlanId = dto.ActiveSupportPlanId.Value <= 0 ? (int?)null : dto.ActiveSupportPlanId.Value;

                var (okPlan, planError) = await ApplySupportPlanChangeByAdmin(u, targetPlanId);
                if (!okPlan)
                    return BadRequest(new { message = planError });
            }

            await _db.SaveChangesAsync();

            var after = new
            {
                u.UserId,
                u.FirstName,
                u.LastName,
                u.FullName,
                u.Email,
                u.Phone,
                u.Address,
                u.Status,
                u.SupportPriorityLevel,
                u.TotalProductSpend,
                u.IsTemp,
                RoleIds = u.Roles.Select(r => r.RoleId).ToList(),
                HasAccount = u.Account != null,
                Username = u.Account?.Username
            };

            await _auditLogger.LogAsync(
                HttpContext,
                action: "UpdateUser",
                entityType: "User",
                entityId: u.UserId.ToString(),
                before: before,
                after: after
            );

            return NoContent();
        }

        // DELETE /api/users/{id}  (toggle Active <-> Disabled)
        [HttpDelete("{id:guid}")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> ToggleActive([FromRoute] Guid id)
        {
            var u = await _db.Set<User>()
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.UserId == id);

            if (u == null)
                return NotFound(new { message = "Không tìm thấy người dùng để thay đổi trạng thái." });

            if (u.Roles.Any(r => ((r.Code ?? "").ToLower()).Contains("admin")))
                return BadRequest(new { message = "Không được phép khoá/mở khoá tài khoản có vai trò admin." });

            if (u.IsTemp)
                return BadRequest(new { message = "Không thể khoá/mở khoá người dùng tạm thời (IsTemp = true)." });

            var before = new
            {
                u.UserId,
                u.Email,
                Status = u.Status
            };

            u.Status = string.Equals(u.Status, "Active", StringComparison.OrdinalIgnoreCase)
                ? "Disabled"
                : "Active";

            u.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var after = new
            {
                u.UserId,
                u.Email,
                Status = u.Status
            };

            await _auditLogger.LogAsync(
                HttpContext,
                action: "ToggleActive",
                entityType: "User",
                entityId: u.UserId.ToString(),
                before: before,
                after: after
            );

            return NoContent();
        }
    }
}
