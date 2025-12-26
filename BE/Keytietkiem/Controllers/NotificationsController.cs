// File: Controllers/NotificationsController.cs
using Keytietkiem.DTOs;
using Keytietkiem.Hubs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services.Background;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private const string SystemCreatorFilterValue = "__SYSTEM__";
        private const int RealtimeFanoutThreshold = 200;

        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IHubContext<NotificationHub> _notificationHub;
        private readonly INotificationDispatchQueue _dispatchQueue;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IHubContext<NotificationHub> notificationHub,
            INotificationDispatchQueue dispatchQueue,
            ILogger<NotificationsController> logger)
        {
            _dbFactory = dbFactory;
            _notificationHub = notificationHub;
            _dispatchQueue = dispatchQueue;
            _logger = logger;
        }

        // =========================
        // Helpers
        // =========================
        private Guid GetCurrentUserId()
        {
            var raw =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("UserId") ??
                User.FindFirstValue("sub");

            if (!Guid.TryParse(raw, out var userId))
                throw new UnauthorizedAccessException("Invalid user id claim.");

            return userId;
        }

        private static string NormalizeSortKey(string? sortBy)
        {
            var s = (sortBy ?? "CreatedAtUtc").Trim();
            if (s.Length == 0) return "createdatutc";
            return s.Replace(" ", "").ToLowerInvariant();
        }

        private static bool HasValue(string? s) => !string.IsNullOrWhiteSpace(s);

        private static bool IsAdminRoleValue(string? v)
        {
            if (!HasValue(v)) return false;
            var s = v!.Trim();
            return s.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                   s.Equals("ADMIN", StringComparison.OrdinalIgnoreCase);
        }

        private bool HasAdminClaim()
        {
            // Prefer claims if present
            var roleClaims = User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Concat(User.FindAll("role").Select(c => c.Value))
                .Concat(User.FindAll("roles").Select(c => c.Value));

            foreach (var v in roleClaims)
            {
                if (IsAdminRoleValue(v)) return true;
            }

            return false;
        }

        private async Task<bool> CurrentUserIsAdminAsync(KeytietkiemDbContext db, Guid userId, CancellationToken ct)
        {
            if (HasAdminClaim()) return true;

            // Fallback to DB (RoleId/Code/Name can vary between environments)
            return await db.Users.AsNoTracking()
                .Where(u => u.UserId == userId)
                .SelectMany(u => u.Roles)
                .AnyAsync(r =>
                    IsAdminRoleValue(r.RoleId) ||
                    IsAdminRoleValue(r.Code) ||
                    IsAdminRoleValue(r.Name), ct);
        }

        // =========================
        // ADMIN: LIST
        // GET /api/notifications
        // =========================
        [HttpGet]
        public async Task<ActionResult<NotificationListResponseDto>> GetAdminList(
            [FromQuery] NotificationAdminFilterDto filter,
            CancellationToken ct)
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct);

            var currentUserId = GetCurrentUserId();
            if (!await CurrentUserIsAdminAsync(db, currentUserId, ct))
                return Forbid();

            var pageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize <= 0 ? 20 : filter.PageSize;
            if (pageSize > 100) pageSize = 100;

            // total users count used for TotalTargetUsers when IsGlobal = true
            var totalActiveUsers = await db.Users.AsNoTracking()
                .CountAsync(u => u.Status == "Active", ct);

            var q =
                from n in db.Notifications.AsNoTracking()
                join u in db.Users.AsNoTracking() on n.CreatedByUserId equals u.UserId into uj
                from creator in uj.DefaultIfEmpty()
                select new
                {
                    N = n,
                    CreatedByEmail = creator != null ? creator.Email : null,
                    CreatedByFullName = creator != null ? creator.FullName : null
                };

            // filters
            if (filter.Severity.HasValue)
            {
                var sev = filter.Severity.Value;
                q = q.Where(x => x.N.Severity == sev);
            }

            if (filter.IsSystemGenerated.HasValue)
            {
                var isSys = filter.IsSystemGenerated.Value;
                q = q.Where(x => x.N.IsSystemGenerated == isSys);
            }

            if (filter.IsGlobal.HasValue)
            {
                var isGlobal = filter.IsGlobal.Value;
                q = q.Where(x => x.N.IsGlobal == isGlobal);
            }

            if (filter.CreatedFromUtc.HasValue)
            {
                var from = filter.CreatedFromUtc.Value;
                q = q.Where(x => x.N.CreatedAtUtc >= from);
            }

            if (filter.CreatedToUtc.HasValue)
            {
                var to = filter.CreatedToUtc.Value;
                q = q.Where(x => x.N.CreatedAtUtc <= to);
            }

            if (HasValue(filter.Type))
            {
                var type = filter.Type!.Trim();
                q = q.Where(x => x.N.Type != null && x.N.Type == type);
            }

            if (HasValue(filter.CreatedByEmail))
            {
                var email = filter.CreatedByEmail!.Trim();

                // FE dropdown can pass a special value to filter "system" creator
                if (email.Equals(SystemCreatorFilterValue, StringComparison.OrdinalIgnoreCase))
                {
                    q = q.Where(x => x.N.CreatedByUserId == null || x.CreatedByEmail == null);
                }
                else
                {
                    // Dropdown expects exact match (search box still covers partial search)
                    q = q.Where(x => x.CreatedByEmail != null && x.CreatedByEmail == email);
                }
            }


            if (HasValue(filter.Status))
            {
                var nowStatus = DateTime.UtcNow;
                var status = filter.Status!.Trim();
                var key = status.Replace(" ", "").ToLowerInvariant();

                // Active = not archived, not expired
                if (key == "active" || key == "danghieuluc" || key == "conhieuluc" || key == "valid")
                {
                    q = q.Where(x => x.N.ArchivedAtUtc == null
                                     && (x.N.ExpiresAtUtc == null || x.N.ExpiresAtUtc > nowStatus));
                }
                // Expired = not archived, expires <= now
                else if (key == "expired" || key == "hethanh" || key == "hethan" || key == "expiredat")
                {
                    q = q.Where(x => x.N.ArchivedAtUtc == null
                                     && x.N.ExpiresAtUtc != null
                                     && x.N.ExpiresAtUtc <= nowStatus);
                }
                // Archived
                else if (key == "archived" || key == "daluutru" || key == "luutru")
                {
                    q = q.Where(x => x.N.ArchivedAtUtc != null);
                }
            }

            // search: Title/Message/CreatedByEmail/Type/CorrelationId
            if (HasValue(filter.Search))
            {
                var s = filter.Search!.Trim();
                q = q.Where(x =>
                    x.N.Title.Contains(s) ||
                    x.N.Message.Contains(s) ||
                    (x.CreatedByEmail != null && x.CreatedByEmail.Contains(s)) ||
                    (x.CreatedByFullName != null && x.CreatedByFullName.Contains(s)) ||
                    (x.N.Type != null && x.N.Type.Contains(s)) ||
                    (x.N.CorrelationId != null && x.N.CorrelationId.Contains(s))
                );
            }

            // sort
            var sortKey = NormalizeSortKey(filter.SortBy);
            var desc = filter.SortDescending;

            // IMPORTANT: do not use dynamic/EF.Property<dynamic> => avoids CS1963
            q = sortKey switch
            {
                "title" => desc ? q.OrderByDescending(x => x.N.Title).ThenByDescending(x => x.N.Id)
                                : q.OrderBy(x => x.N.Title).ThenBy(x => x.N.Id),

                "severity" => desc ? q.OrderByDescending(x => x.N.Severity).ThenByDescending(x => x.N.Id)
                                   : q.OrderBy(x => x.N.Severity).ThenBy(x => x.N.Id),

                "type" => desc ? q.OrderByDescending(x => x.N.Type).ThenByDescending(x => x.N.Id)
                               : q.OrderBy(x => x.N.Type).ThenBy(x => x.N.Id),

                "createdbyemail" or "createdbyuseremail" => desc
                    ? q.OrderByDescending(x => x.CreatedByEmail).ThenByDescending(x => x.N.Id)
                    : q.OrderBy(x => x.CreatedByEmail).ThenBy(x => x.N.Id),

                "readcount" => desc
                    ? q.OrderByDescending(x => db.NotificationUsers.Count(nu => nu.NotificationId == x.N.Id && nu.IsRead))
                       .ThenByDescending(x => x.N.Id)
                    : q.OrderBy(x => db.NotificationUsers.Count(nu => nu.NotificationId == x.N.Id && nu.IsRead))
                       .ThenBy(x => x.N.Id),

                "totaltargetusers" => desc
                    ? q.OrderByDescending(x => x.N.IsGlobal ? totalActiveUsers : db.NotificationUsers.Count(nu => nu.NotificationId == x.N.Id))
                       .ThenByDescending(x => x.N.Id)
                    : q.OrderBy(x => x.N.IsGlobal ? totalActiveUsers : db.NotificationUsers.Count(nu => nu.NotificationId == x.N.Id))
                       .ThenBy(x => x.N.Id),

                "targetuserscount" => desc
                    ? q.OrderByDescending(x => db.NotificationUsers.Count(nu => nu.NotificationId == x.N.Id))
                       .ThenByDescending(x => x.N.Id)
                    : q.OrderBy(x => db.NotificationUsers.Count(nu => nu.NotificationId == x.N.Id))
                       .ThenBy(x => x.N.Id),

                "targetrolescount" => desc
                    ? q.OrderByDescending(x => db.NotificationTargetRoles.Count(ntr => ntr.NotificationId == x.N.Id))
                       .ThenByDescending(x => x.N.Id)
                    : q.OrderBy(x => db.NotificationTargetRoles.Count(ntr => ntr.NotificationId == x.N.Id))
                       .ThenBy(x => x.N.Id),

                "expiresatutc" => desc ? q.OrderByDescending(x => x.N.ExpiresAtUtc).ThenByDescending(x => x.N.Id)
                                       : q.OrderBy(x => x.N.ExpiresAtUtc).ThenBy(x => x.N.Id),

                "archivedatutc" => desc ? q.OrderByDescending(x => x.N.ArchivedAtUtc).ThenByDescending(x => x.N.Id)
                                        : q.OrderBy(x => x.N.ArchivedAtUtc).ThenBy(x => x.N.Id),

                // default createdAtUtc
                _ => desc ? q.OrderByDescending(x => x.N.CreatedAtUtc).ThenByDescending(x => x.N.Id)
                          : q.OrderBy(x => x.N.CreatedAtUtc).ThenBy(x => x.N.Id)
            };

            var totalCount = await q.CountAsync(ct);

            var rows = await q
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.N.Id,
                    x.N.Title,
                    x.N.Message,
                    x.N.Severity,
                    x.N.IsSystemGenerated,
                    x.N.IsGlobal,
                    x.N.CreatedAtUtc,
                    x.N.CreatedByUserId,
                    x.CreatedByEmail,
                    x.CreatedByFullName,
                    x.N.RelatedEntityType,
                    x.N.RelatedEntityId,
                    x.N.RelatedUrl,
                    x.N.Type,
                    x.N.CorrelationId,
                    x.N.ExpiresAtUtc,
                    x.N.ArchivedAtUtc
                })
                .ToListAsync(ct);

            var ids = rows.Select(r => r.Id).ToList();

            var agg = await db.NotificationUsers.AsNoTracking()
                .Where(nu => ids.Contains(nu.NotificationId))
                .GroupBy(nu => nu.NotificationId)
                .Select(g => new
                {
                    NotificationId = g.Key,
                    Total = g.Count(),
                    Read = g.Count(x => x.IsRead)
                })
                .ToListAsync(ct);

            var aggMap = agg.ToDictionary(x => x.NotificationId, x => x);

            var roleAgg = await db.NotificationTargetRoles.AsNoTracking()
                .Where(ntr => ids.Contains(ntr.NotificationId))
                .GroupBy(ntr => ntr.NotificationId)
                .Select(g => new
                {
                    NotificationId = g.Key,
                    Count = g.Count()
                })
                .ToListAsync(ct);

            var roleAggMap = roleAgg.ToDictionary(x => x.NotificationId, x => x.Count);

            var items = rows.Select(r =>
            {
                aggMap.TryGetValue(r.Id, out var a);

                var totalTargets = r.IsGlobal ? totalActiveUsers : (a?.Total ?? 0);
                var readCount = a?.Read ?? 0;
                var targetUsersCount = r.IsGlobal ? 0 : (a?.Total ?? 0);

                roleAggMap.TryGetValue(r.Id, out var targetRolesCount);

                return new NotificationListItemDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    Message = r.Message,
                    Severity = r.Severity,
                    IsSystemGenerated = r.IsSystemGenerated,
                    IsGlobal = r.IsGlobal,
                    CreatedAtUtc = r.CreatedAtUtc,
                    CreatedByUserId = r.CreatedByUserId,

                    // backward compatible + alias
                    CreatedByFullName = r.CreatedByFullName,
                    CreatedByUserEmail = r.CreatedByEmail,
                    CreatedByEmail = r.CreatedByEmail,

                    RelatedEntityType = r.RelatedEntityType,
                    RelatedEntityId = r.RelatedEntityId,
                    RelatedUrl = r.RelatedUrl,

                    TotalTargetUsers = totalTargets,
                    ReadCount = readCount,

                    TargetRolesCount = targetRolesCount,
                    TargetUsersCount = targetUsersCount,

                    Type = r.Type,
                    CorrelationId = r.CorrelationId,
                    ExpiresAtUtc = r.ExpiresAtUtc,
                    ArchivedAtUtc = r.ArchivedAtUtc
                };
            }).ToList();

            return Ok(new NotificationListResponseDto
            {
                TotalCount = totalCount,
                Items = items
            });
        }
        // =========================
        // ADMIN: DETAIL
        // GET /api/notifications/{id}
        // =========================
        [HttpGet("{id:int}")]
        public async Task<ActionResult<NotificationDetailDto>> GetAdminDetail([FromRoute] int id, CancellationToken ct)
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct);

            var currentUserId = GetCurrentUserId();
            if (!await CurrentUserIsAdminAsync(db, currentUserId, ct))
                return Forbid();

            var totalActiveUsers = await db.Users.AsNoTracking()
                .CountAsync(u => u.Status == "Active", ct);

            var row = await (
                from n in db.Notifications.AsNoTracking()
                join u in db.Users.AsNoTracking() on n.CreatedByUserId equals u.UserId into uj
                from creator in uj.DefaultIfEmpty()
                where n.Id == id
                select new
                {
                    N = n,
                    CreatedByEmail = creator != null ? creator.Email : null,
                    CreatedByFullName = creator != null ? creator.FullName : null
                }
            ).FirstOrDefaultAsync(ct);

            if (row == null) return NotFound();

            var readCount = await db.NotificationUsers.AsNoTracking()
                .CountAsync(nu => nu.NotificationId == id && nu.IsRead, ct);

            var totalTargets = row.N.IsGlobal
                ? totalActiveUsers
                : await db.NotificationUsers.AsNoTracking().CountAsync(nu => nu.NotificationId == id, ct);

            var unreadCount = Math.Max(0, totalTargets - readCount);

            // Target roles (RoleId is string in v16 schema)
            var targetRoles = await LoadTargetRolesAsync(db, id, ct);

            var dto = new NotificationDetailDto
            {
                Id = row.N.Id,
                Title = row.N.Title,
                Message = row.N.Message,
                Severity = row.N.Severity,
                IsSystemGenerated = row.N.IsSystemGenerated,
                IsGlobal = row.N.IsGlobal,
                CreatedAtUtc = row.N.CreatedAtUtc,
                CreatedByUserId = row.N.CreatedByUserId,

                CreatedByFullName = row.CreatedByFullName,
                CreatedByUserEmail = row.CreatedByEmail,
                CreatedByEmail = row.CreatedByEmail,

                RelatedEntityType = row.N.RelatedEntityType,
                RelatedEntityId = row.N.RelatedEntityId,
                RelatedUrl = row.N.RelatedUrl,

                TotalTargetUsers = totalTargets,
                ReadCount = readCount,
                UnreadCount = unreadCount,

                TargetRoles = targetRoles,
                // Recipients can be very large (especially Global).
                // FE should load recipients via the dedicated paging endpoint.
                Recipients = new List<NotificationRecipientDto>(),

                Type = row.N.Type,
                DedupKey = row.N.DedupKey,
                CorrelationId = row.N.CorrelationId,
                PayloadJson = row.N.PayloadJson,
                ExpiresAtUtc = row.N.ExpiresAtUtc,
                ArchivedAtUtc = row.N.ArchivedAtUtc
            };

            return Ok(dto);
        }

        // =========================
        // ADMIN: RECIPIENTS (PAGED)
        // GET /api/notifications/{id}/recipients?pageNumber=1&pageSize=20
        // =========================
        [HttpGet("{id:int}/recipients")]
        public async Task<ActionResult<NotificationRecipientsPagedResponseDto>> GetAdminRecipients(
            [FromRoute] int id,
            [FromQuery] NotificationRecipientsFilterDto filter,
            CancellationToken ct)
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct);

            var currentUserId = GetCurrentUserId();
            if (!await CurrentUserIsAdminAsync(db, currentUserId, ct))
                return Forbid();

            var n = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (n == null) return NotFound();

            var pageNumber = Math.Max(1, filter.PageNumber);
            var pageSize = Math.Clamp(filter.PageSize, 1, 200);
            var search = (filter.Search ?? string.Empty).Trim();
            var hasSearch = search.Length > 0;

            if (n.IsGlobal)
            {
                var q = db.Users.AsNoTracking()
                    .Where(u => u.Status == "Active");

                if (hasSearch)
                {
                    q = q.Where(u =>
                        (u.FullName != null && u.FullName.Contains(search)) ||
                        (u.Email != null && u.Email.Contains(search)));
                }

                // Left join NotificationUser to know read state (missing row => unread)
                var baseQuery =
                    from u in q
                    join nu in db.NotificationUsers.AsNoTracking().Where(x => x.NotificationId == id)
                        on u.UserId equals nu.UserId into nuj
                    from nu in nuj.DefaultIfEmpty()
                    select new
                    {
                        u.UserId,
                        u.FullName,
                        u.Email,
                        IsRead = (nu != null && nu.IsRead)
                    };

                if (filter.IsRead.HasValue)
                {
                    var wantRead = filter.IsRead.Value;
                    baseQuery = baseQuery.Where(x => x.IsRead == wantRead);
                }

                var totalCount = await baseQuery.CountAsync(ct);

                var pageRows = await baseQuery
                    .OrderBy(x => x.FullName)
                    .ThenBy(x => x.Email)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                var pageUserIds = pageRows.Select(x => x.UserId).ToList();
                var roleNamesMap = await GetRoleNamesByUserIdsAsync(db, pageUserIds, ct);

                var items = pageRows.Select(x => new NotificationRecipientDto
                {
                    UserId = x.UserId,
                    FullName = x.FullName,
                    Email = x.Email ?? string.Empty,
                    RoleNames = roleNamesMap.TryGetValue(x.UserId, out var rn) ? rn : string.Empty,
                    IsRead = x.IsRead
                }).ToList();

                return Ok(new NotificationRecipientsPagedResponseDto
                {
                    TotalCount = totalCount,
                    Items = items
                });
            }

            // Non-global: recipients are rows in NotificationUser.
            var q2 =
                from nu in db.NotificationUsers.AsNoTracking()
                join u in db.Users.AsNoTracking() on nu.UserId equals u.UserId
                where nu.NotificationId == id
                select new
                {
                    nu.UserId,
                    u.FullName,
                    u.Email,
                    nu.IsRead
                };

            if (hasSearch)
            {
                q2 = q2.Where(x =>
                    (x.FullName != null && x.FullName.Contains(search)) ||
                    (x.Email != null && x.Email.Contains(search)));
            }

            if (filter.IsRead.HasValue)
            {
                var wantRead = filter.IsRead.Value;
                q2 = q2.Where(x => x.IsRead == wantRead);
            }

            var total2 = await q2.CountAsync(ct);
            var page2 = await q2
                .OrderBy(x => x.FullName)
                .ThenBy(x => x.Email)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var ids2 = page2.Select(x => x.UserId).ToList();
            var roleMap2 = await GetRoleNamesByUserIdsAsync(db, ids2, ct);

            var items2 = page2.Select(x => new NotificationRecipientDto
            {
                UserId = x.UserId,
                FullName = x.FullName,
                Email = x.Email ?? string.Empty,
                RoleNames = roleMap2.TryGetValue(x.UserId, out var rn) ? rn : string.Empty,
                IsRead = x.IsRead
            }).ToList();

            return Ok(new NotificationRecipientsPagedResponseDto
            {
                TotalCount = total2,
                Items = items2
            });
        }

        // =========================
        // ADMIN: CREATE MANUAL
        // POST /api/notifications
        // =========================
        [HttpPost]
        public async Task<ActionResult<NotificationDetailDto>> CreateManual([FromBody] CreateNotificationDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            using var db = await _dbFactory.CreateDbContextAsync(ct);

            var userId = GetCurrentUserId();
            if (!await CurrentUserIsAdminAsync(db, userId, ct))
                return Forbid();

            // basic validation for manual non-global notifications
            if (!dto.IsGlobal)
            {
                var hasAnyRole = dto.TargetRoleIds != null && dto.TargetRoleIds.Any(x => HasValue(x));
                var hasAnyUser = dto.TargetUserIds != null && dto.TargetUserIds.Any();
                if (!hasAnyRole && !hasAnyUser)
                    return BadRequest(new { message = "Bạn phải chọn ít nhất 1 role hoặc 1 user (hoặc bật Global)." });
            }

            // Idempotency by DedupKey (optional)
            if (HasValue(dto.DedupKey))
            {
                var existed = await db.Notifications.AsNoTracking()
                    .FirstOrDefaultAsync(n => n.DedupKey == dto.DedupKey, ct);

                if (existed != null)
                {
                    // return detail of existed (safe)
                    return await GetAdminDetail(existed.Id, ct);
                }
            }

            var type = HasValue(dto.Type) ? dto.Type!.Trim() : "Manual";
            var correlationId = HasValue(dto.CorrelationId) ? dto.CorrelationId!.Trim() : Guid.NewGuid().ToString("N");

            var now = DateTime.UtcNow;

            var n = new Notification
            {
                Title = dto.Title.Trim(),
                Message = dto.Message.Trim(),
                Severity = dto.Severity,

                IsSystemGenerated = false,
                IsGlobal = dto.IsGlobal,

                CreatedAtUtc = now,
                CreatedByUserId = userId,

                RelatedEntityType = dto.RelatedEntityType,
                RelatedEntityId = dto.RelatedEntityId,
                RelatedUrl = dto.RelatedUrl,

                Type = type,
                CorrelationId = correlationId,
                DedupKey = dto.DedupKey,
                PayloadJson = dto.PayloadJson,
                ExpiresAtUtc = dto.ExpiresAtUtc,
                ArchivedAtUtc = null
            };

            // Targets (precompute first to avoid creating notifications without recipients)
            var targetUserIds = new HashSet<Guid>();
            var explicitUserIds = new HashSet<Guid>();
            var roleIds = new List<string>();

            // If global => do NOT bind users/roles (scope = all)
            if (!n.IsGlobal)
            {
                // 1) explicit users
                if (dto.TargetUserIds != null)
                {
                    foreach (var tu in dto.TargetUserIds)
                    {
                        targetUserIds.Add(tu);
                        explicitUserIds.Add(tu);
                    }
                }

                // 2) roles => expand to active users
                roleIds = dto.TargetRoleIds
                    ?.Where(r => HasValue(r))
                    .Select(r => r.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();

                if (roleIds.Count > 0)
                {
                    var userIdsFromRoles = await GetUserIdsByRoleIdsAsync(db, roleIds, ct);
                    foreach (var uid in userIdsFromRoles)
                        targetUserIds.Add(uid);
                }

                if (targetUserIds.Count == 0)
                    return BadRequest(new { message = "Không có user nào khớp role/user đã chọn." });
            }

            db.Notifications.Add(n);
            await db.SaveChangesAsync(ct);

            if (!n.IsGlobal)
            {
                // store target roles for admin detail UI (NotificationTargetRole PK: (NotificationId, RoleId))
                if (roleIds.Count > 0)
                {
                    var ntrs = roleIds.Select(rid => new NotificationTargetRole
                    {
                        NotificationId = n.Id,
                        RoleId = rid
                    }).ToList();
                    db.NotificationTargetRoles.AddRange(ntrs);
                }

                // create NotificationUser rows (so FE always has NotificationUserId)
                var nus = targetUserIds.Select(uid => new NotificationUser
                {
                    NotificationId = n.Id,
                    UserId = uid,
                    IsRead = false,
                    ReadAtUtc = null,
                    DismissedAtUtc = null
                }).ToList();

                db.NotificationUsers.AddRange(nus);
                await db.SaveChangesAsync(ct);

                // send realtime to each user group
                var totalRecipients = nus.Count;

                // If the fanout is small => send per-user directly (fast path).
                if (totalRecipients <= RealtimeFanoutThreshold)
                {
                    foreach (var nu in nus)
                    {
                        await _notificationHub.Clients.Group(NotificationHub.UserGroup(nu.UserId))
                            .SendAsync("ReceiveNotification", new
                            {
                                notificationUserId = nu.Id,
                                notificationId = n.Id,
                                title = n.Title,
                                message = n.Message,
                                severity = n.Severity,
                                createdAtUtc = n.CreatedAtUtc,
                                relatedUrl = n.RelatedUrl
                            }, ct);
                    }
                }
                else
                {
                    // P0: avoid looping SendAsync per-user when target is large.
                    // 1) Broadcast a lightweight hint to role groups (role claim = Role.Code).
                    if (roleIds.Count > 0)
                    {
                        var roleCodes = await db.Roles.AsNoTracking()
                            .Where(r => roleIds.Contains(r.RoleId))
                            .Select(r => (r.Code != null && r.Code.Trim() != "") ? r.Code : r.RoleId)
                            .Distinct()
                            .ToListAsync(ct);

                        foreach (var roleCode in roleCodes.Where(HasValue).Select(x => x.Trim()).Distinct())
                        {
                            await _dispatchQueue.QueueToGroupAsync(NotificationHub.RoleGroup(roleCode), new
                            {
                                notificationId = n.Id,
                                title = n.Title,
                                message = n.Message,
                                severity = n.Severity,
                                createdAtUtc = n.CreatedAtUtc,
                                relatedUrl = n.RelatedUrl,
                                isHint = true
                            }, "ReceiveNotification");
                        }
                    }

                    // 2) Explicit users: deliver to user groups.
                    // If explicit list is small => send full payload (includes NotificationUserId).
                    // If explicit list is large => send hint only (FE will refresh/pull data).
                    var explicitIds = explicitUserIds.ToList();
                    if (explicitIds.Count > 0)
                    {
                        if (explicitIds.Count <= RealtimeFanoutThreshold)
                        {
                            var nuIdByUserId = nus.ToDictionary(x => x.UserId, x => x.Id);
                            foreach (var uid in explicitIds)
                            {
                                if (!nuIdByUserId.TryGetValue(uid, out var nuId)) continue;
                                await _dispatchQueue.QueueToUserAsync(uid, new
                                {
                                    notificationUserId = nuId,
                                    notificationId = n.Id,
                                    title = n.Title,
                                    message = n.Message,
                                    severity = n.Severity,
                                    createdAtUtc = n.CreatedAtUtc,
                                    relatedUrl = n.RelatedUrl
                                }, "ReceiveNotification");
                            }
                        }
                        else
                        {
                            foreach (var uid in explicitIds)
                            {
                                await _dispatchQueue.QueueToUserAsync(uid, new
                                {
                                    notificationId = n.Id,
                                    title = n.Title,
                                    message = n.Message,
                                    severity = n.Severity,
                                    createdAtUtc = n.CreatedAtUtc,
                                    relatedUrl = n.RelatedUrl,
                                    isHint = true
                                }, "ReceiveNotification");
                            }
                        }
                    }
                }
            }
            else
            {
                // global broadcast (FE listens ReceiveGlobalNotification)
                await _notificationHub.Clients.Group(NotificationHub.GlobalGroup).SendAsync("ReceiveGlobalNotification", new
                {
                    notificationId = n.Id,
                    title = n.Title,
                    message = n.Message,
                    severity = n.Severity,
                    createdAtUtc = n.CreatedAtUtc,
                    relatedUrl = n.RelatedUrl
                }, ct);
            }

            return await GetAdminDetail(n.Id, ct);
        }

        // =========================
        // ADMIN: manual target options
        // GET /api/notifications/manual-target-options
        // =========================
        [HttpGet("manual-target-options")]
        public async Task<ActionResult<NotificationManualTargetOptionsDto>> GetManualTargetOptions(CancellationToken ct)
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct);

            var currentUserId = GetCurrentUserId();
            if (!await CurrentUserIsAdminAsync(db, currentUserId, ct))
                return Forbid();

            var roles = await LoadRoleOptionsAsync(db, ct);
            var users = await LoadUserOptionsAsync(db, ct);

            return Ok(new NotificationManualTargetOptionsDto
            {
                Roles = roles,
                Users = users
            });
        }

        // =========================
        // ADMIN: filter options for list
        // GET /api/notifications/admin-filter-options
        // =========================
        [HttpGet("admin-filter-options")]
        public async Task<ActionResult<NotificationAdminFilterOptionsDto>> GetAdminFilterOptions(CancellationToken ct)
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct);

            var currentUserId = GetCurrentUserId();
            if (!await CurrentUserIsAdminAsync(db, currentUserId, ct))
                return Forbid();

            var types = await db.Notifications.AsNoTracking()
                .Where(n => n.Type != null && n.Type.Trim() != "")
                .Select(n => n.Type!)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync(ct);

            var typeOptions = types.Select(t => new NotificationFilterOptionDto
            {
                Value = t,
                Label = t
            }).ToList();

            var hasSystemCreator = await db.Notifications.AsNoTracking()
                .AnyAsync(n => n.CreatedByUserId == null, ct);

            var creators = await (
                from n in db.Notifications.AsNoTracking()
                join u in db.Users.AsNoTracking() on n.CreatedByUserId equals u.UserId
                where n.CreatedByUserId != null
                select new { u.Email, u.FullName }
            )
            .Distinct()
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Email)
            .ToListAsync(ct);

            var creatorOptions = new List<NotificationFilterOptionDto>();
            if (hasSystemCreator)
            {
                creatorOptions.Add(new NotificationFilterOptionDto
                {
                    Value = SystemCreatorFilterValue,
                    Label = "Hệ thống"
                });
            }

            foreach (var c in creators)
            {
                if (!HasValue(c.Email)) continue;

                var label = HasValue(c.FullName)
                    ? c.FullName!.Trim()
                    : c.Email!;

                creatorOptions.Add(new NotificationFilterOptionDto
                {
                    Value = c.Email!,
                    Label = label
                });
            }

            return Ok(new NotificationAdminFilterOptionsDto
            {
                Types = typeOptions,
                Creators = creatorOptions
            });
        }

        // =========================
        // USER: unread count
        // GET /api/notifications/my/unread-count
        // =========================
        [HttpGet("my/unread-count")]
        public async Task<ActionResult<NotificationUnreadCountDto>> GetMyUnreadCount(CancellationToken ct)
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct);

            var userId = GetCurrentUserId();
            var now = DateTime.UtcNow;

            // unread targeted
            var unreadTargeted = await (
                from nu in db.NotificationUsers.AsNoTracking()
                join n in db.Notifications.AsNoTracking() on nu.NotificationId equals n.Id
                where nu.UserId == userId
                      && !nu.IsRead
                      && n.ArchivedAtUtc == null
                      && (n.ExpiresAtUtc == null || n.ExpiresAtUtc > now)
                select nu.Id
            ).CountAsync(ct);

            // unread global (no row or IsRead=false => unread; IsRead=true => read)
            var unreadGlobal = await db.Notifications.AsNoTracking()
                .Where(n => n.IsGlobal
                            && n.ArchivedAtUtc == null
                            && (n.ExpiresAtUtc == null || n.ExpiresAtUtc > now))
                .Where(n => !db.NotificationUsers.Any(nu => nu.UserId == userId && nu.NotificationId == n.Id && nu.IsRead))
                .CountAsync(ct);

            return Ok(new NotificationUnreadCountDto
            {
                UnreadCount = unreadTargeted + unreadGlobal
            });
        }

        // =========================
        // USER: list my notifications
        // GET /api/notifications/my
        // =========================
        [HttpGet("my")]
        public async Task<ActionResult<NotificationUserListResponseDto>> GetMyList(
            [FromQuery] NotificationUserFilterDto filter,
            CancellationToken ct)
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct);

            var userId = GetCurrentUserId();
            var now = DateTime.UtcNow;

            var pageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize <= 0 ? 20 : filter.PageSize;
            if (pageSize > 100) pageSize = 100;

            // 1) base notifications (global OR targeted to user)
            var q =
                from n in db.Notifications
                where n.ArchivedAtUtc == null
                      && (n.ExpiresAtUtc == null || n.ExpiresAtUtc > now)
                select n;

            if (filter.Severity.HasValue)
            {
                var sev = filter.Severity.Value;
                q = q.Where(n => n.Severity == sev);
            }

            if (filter.FromUtc.HasValue)
            {
                var from = filter.FromUtc.Value;
                q = q.Where(n => n.CreatedAtUtc >= from);
            }

            if (filter.ToUtc.HasValue)
            {
                var to = filter.ToUtc.Value;
                q = q.Where(n => n.CreatedAtUtc <= to);
            }

            if (HasValue(filter.Search))
            {
                var s = filter.Search!.Trim();
                q = q.Where(n => n.Title.Contains(s) || n.Message.Contains(s));
            }

            // restrict to visible to current user
            q = q.Where(n =>
                n.IsGlobal ||
                db.NotificationUsers.Any(nu => nu.UserId == userId && nu.NotificationId == n.Id)
            );

            // only unread
            if (filter.OnlyUnread)
            {
                q = q.Where(n =>
                    n.IsGlobal
                        ? !db.NotificationUsers.Any(nu => nu.UserId == userId && nu.NotificationId == n.Id && nu.IsRead)
                        : db.NotificationUsers.Any(nu => nu.UserId == userId && nu.NotificationId == n.Id && !nu.IsRead)
                );
            }

            // sort (simple for user list)
            var sortKey = NormalizeSortKey(filter.SortBy);
            var desc = filter.SortDescending;

            q = sortKey switch
            {
                "title" => desc ? q.OrderByDescending(n => n.Title).ThenByDescending(n => n.Id)
                                : q.OrderBy(n => n.Title).ThenBy(n => n.Id),

                "severity" => desc ? q.OrderByDescending(n => n.Severity).ThenByDescending(n => n.Id)
                                   : q.OrderBy(n => n.Severity).ThenBy(n => n.Id),

                _ => desc ? q.OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
                          : q.OrderBy(n => n.CreatedAtUtc).ThenBy(n => n.Id)
            };

            var totalCount = await q.CountAsync(ct);

            // 2) take page of notifications
            var page = await q.AsNoTracking()
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Severity,
                    n.IsSystemGenerated,
                    n.IsGlobal,
                    n.CreatedAtUtc,
                    n.RelatedEntityType,
                    n.RelatedEntityId,
                    n.RelatedUrl,
                    n.Type,
                    n.ExpiresAtUtc
                })
                .ToListAsync(ct);

            var notifIds = page.Select(x => x.Id).ToList();

            // 3) ensure NotificationUser exists for global notifications in page (so FE always has NotificationUserId)
            var existingNu = await db.NotificationUsers
                .Where(nu => nu.UserId == userId && notifIds.Contains(nu.NotificationId))
                .ToListAsync(ct);

            var existingMap = existingNu.ToDictionary(x => x.NotificationId, x => x);

            var missingGlobalIds = page
                .Where(x => x.IsGlobal && !existingMap.ContainsKey(x.Id))
                .Select(x => x.Id)
                .ToList();

            if (missingGlobalIds.Count > 0)
            {
                foreach (var nid in missingGlobalIds)
                {
                    db.NotificationUsers.Add(new NotificationUser
                    {
                        NotificationId = nid,
                        UserId = userId,
                        IsRead = false,
                        ReadAtUtc = null,
                        DismissedAtUtc = null
                    });
                }

                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex)
                {
                    // race: ignore duplicates
                    _logger.LogWarning(ex, "Upsert NotificationUser (global) raced. Safe to ignore.");
                }

                // reload to get ids
                existingNu = await db.NotificationUsers
                    .Where(nu => nu.UserId == userId && notifIds.Contains(nu.NotificationId))
                    .ToListAsync(ct);

                existingMap = existingNu.ToDictionary(x => x.NotificationId, x => x);
            }

            // 4) build response items
            var items = page.Select(x =>
            {
                existingMap.TryGetValue(x.Id, out var nu);

                return new NotificationUserListItemDto
                {
                    NotificationUserId = nu != null ? nu.Id : 0,
                    NotificationId = x.Id,

                    Title = x.Title,
                    Message = x.Message,
                    Severity = x.Severity,

                    IsRead = nu?.IsRead ?? false,
                    ReadAtUtc = nu?.ReadAtUtc,
                    CreatedAtUtc = x.CreatedAtUtc,

                    IsSystemGenerated = x.IsSystemGenerated,
                    IsGlobal = x.IsGlobal,

                    RelatedEntityType = x.RelatedEntityType,
                    RelatedEntityId = x.RelatedEntityId,
                    RelatedUrl = x.RelatedUrl,

                    Type = x.Type,
                    ExpiresAtUtc = x.ExpiresAtUtc
                };
            }).ToList();

            return Ok(new NotificationUserListResponseDto
            {
                TotalCount = totalCount,
                Items = items
            });
        }

        // =========================
        // USER: mark read
        // POST /api/notifications/my/{notificationUserId}/read
        // =========================
        [HttpPost("my/{notificationUserId:long}/read")]
        public async Task<ActionResult<NotificationUpsertResultDto>> MarkMyRead(
            [FromRoute] long notificationUserId,
            CancellationToken ct)
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct);

            var userId = GetCurrentUserId();
            var now = DateTime.UtcNow;

            var nu = await db.NotificationUsers
                .FirstOrDefaultAsync(x => x.Id == notificationUserId && x.UserId == userId, ct);

            if (nu == null) return NotFound();

            if (!nu.IsRead)
            {
                nu.IsRead = true;
                nu.ReadAtUtc = now;
                await db.SaveChangesAsync(ct);
            }

            return Ok(new NotificationUpsertResultDto
            {
                NotificationUserId = nu.Id,
                NotificationId = nu.NotificationId,
                IsRead = nu.IsRead,
                ReadAtUtc = nu.ReadAtUtc,
                DismissedAtUtc = nu.DismissedAtUtc
            });
        }

        // ============================================================
        // Raw SQL helpers (avoid depending on Role/UserRole EF models)
        // ============================================================

        private static async Task<List<NotificationTargetRoleDto>> LoadTargetRolesAsync(
            KeytietkiemDbContext db,
            int notificationId,
            CancellationToken ct)
        {
            // Use EF navigation (NotificationTargetRole -> Role) to avoid manual connection handling
            var rows = await db.NotificationTargetRoles.AsNoTracking()
                .Where(x => x.NotificationId == notificationId)
                .Select(x => new
                {
                    x.RoleId,
                    RoleName = x.Role != null ? x.Role.Name : null
                })
                .OrderBy(x => x.RoleName)
                .ThenBy(x => x.RoleId)
                .ToListAsync(ct);

            return rows.Select(x => new NotificationTargetRoleDto
            {
                RoleId = x.RoleId,
                RoleName = x.RoleName
            }).ToList();
        }

        private static async Task<List<NotificationRecipientDto>> LoadRecipientsForNonGlobalAsync(
            KeytietkiemDbContext db,
            int notificationId,
            CancellationToken ct)
        {
            // get notification users
            var rows = await (
                from nu in db.NotificationUsers.AsNoTracking()
                join u in db.Users.AsNoTracking() on nu.UserId equals u.UserId
                where nu.NotificationId == notificationId
                select new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    nu.IsRead
                }
            ).ToListAsync(ct);

            var userIds = rows.Select(x => x.UserId).Distinct().ToList();
            var roleMap = await GetRoleNamesByUserIdsAsync(db, userIds, ct);

            return rows.Select(x => new NotificationRecipientDto
            {
                UserId = x.UserId,
                FullName = x.FullName,
                Email = x.Email,
                RoleNames = roleMap.TryGetValue(x.UserId, out var rn) ? rn : "",
                IsRead = x.IsRead
            }).ToList();
        }

        private static async Task<List<NotificationRecipientDto>> LoadRecipientsForGlobalAsync(
            KeytietkiemDbContext db,
            int notificationId,
            CancellationToken ct)
        {
            // all active users
            var users = await db.Users.AsNoTracking()
                .Where(u => u.Status == "Active")
                .Select(u => new { u.UserId, u.FullName, u.Email })
                .OrderBy(u => u.Email)
                .ToListAsync(ct);

            // read status map
            var nuRows = await db.NotificationUsers.AsNoTracking()
                .Where(nu => nu.NotificationId == notificationId)
                .Select(nu => new { nu.UserId, nu.IsRead })
                .ToListAsync(ct);

            var readMap = nuRows.ToDictionary(x => x.UserId, x => x.IsRead);

            var userIds = users.Select(x => x.UserId).ToList();
            var roleMap = await GetRoleNamesByUserIdsAsync(db, userIds, ct);

            return users.Select(u => new NotificationRecipientDto
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                RoleNames = roleMap.TryGetValue(u.UserId, out var rn) ? rn : "",
                IsRead = readMap.TryGetValue(u.UserId, out var isRead) && isRead
            }).ToList();
        }

        private static async Task<List<NotificationTargetRoleOptionDto>> LoadRoleOptionsAsync(
            KeytietkiemDbContext db,
            CancellationToken ct)
        {
            // Use EF to avoid manual connection open/dispose
            return await db.Roles.AsNoTracking()
                .OrderBy(r => r.Name)
                .ThenBy(r => r.RoleId)
                .Select(r => new NotificationTargetRoleOptionDto
                {
                    RoleId = r.RoleId,
                    RoleName = r.Name
                })
                .ToListAsync(ct);
        }

        private static async Task<List<NotificationTargetUserOptionDto>> LoadUserOptionsAsync(
            KeytietkiemDbContext db,
            CancellationToken ct)
        {
            // small cap to keep UI light
            var users = await db.Users.AsNoTracking()
                .Where(u => u.Status == "Active")
                .OrderBy(u => u.Email)
                .Take(500)
                .Select(u => new NotificationTargetUserOptionDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email
                })
                .ToListAsync(ct);

            return users;
        }

        private static async Task<HashSet<Guid>> GetUserIdsByRoleIdsAsync(
            KeytietkiemDbContext db,
            IEnumerable<string> roleIds,
            CancellationToken ct)
        {
            var ids = roleIds
                .Where(r => HasValue(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ids.Count == 0) return new HashSet<Guid>();

            // Use many-to-many navigation User.Roles
            var userIds = await db.Users.AsNoTracking()
                .Where(u => u.Status == "Active" && u.Roles.Any(r => ids.Contains(r.RoleId)))
                .Select(u => u.UserId)
                .ToListAsync(ct);

            return userIds.ToHashSet();
        }

        private static async Task<Dictionary<Guid, string>> GetRoleNamesByUserIdsAsync(
            KeytietkiemDbContext db,
            List<Guid> userIds,
            CancellationToken ct)
        {
            var map = new Dictionary<Guid, string>();
            if (userIds == null || userIds.Count == 0) return map;

            // avoid SQL param limit blowups
            if (userIds.Count > 1800)
                userIds = userIds.Take(1800).ToList();

            // Flatten users -> roles using many-to-many navigation (avoids raw SQL)
            var pairs = await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .SelectMany(u => u.Roles.Select(r => new { u.UserId, RoleName = r.Name }))
                .ToListAsync(ct);

            foreach (var g in pairs
                .Where(x => !string.IsNullOrWhiteSpace(x.RoleName))
                .GroupBy(x => x.UserId))
            {
                map[g.Key] = string.Join(", ", g
                    .Select(x => x.RoleName!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x));
            }

            return map;
        }
    }
}
