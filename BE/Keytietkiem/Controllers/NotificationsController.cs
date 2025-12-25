// File: Controllers/NotificationsController.cs
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using Keytietkiem.DTOs;
using Keytietkiem.Hubs;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _db;
        private readonly ILogger<NotificationsController> _logger;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public NotificationsController(
            KeytietkiemDbContext db,
            ILogger<NotificationsController> logger,
            IHubContext<NotificationHub> notificationHub)
        {
            _db = db;
            _logger = logger;
            _notificationHub = notificationHub;
        }

        #region Helpers

        private Guid GetCurrentUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException("Cannot determine current user id from claims.");
            }

            return Guid.Parse(id);
        }

        private static string SafeTrim(string? s)
            => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim();

        /// <summary>
        /// Đếm user Active (dùng cho global đúng nghĩa).
        /// </summary>
        private Task<int> CountActiveUsersAsync()
        {
            return _db.Users
                .AsNoTracking()
                .CountAsync(u => u.Status == "Active");
        }

        /// <summary>
        /// Resolve userIds theo roleIds bằng query trực tiếp bảng dbo.UserRole (ADO),
        /// không phụ thuộc DbSet<UserRole> trong DbContext scaffold.
        /// </summary>
        private async Task<List<Guid>> ResolveUserIdsByRoleIdsAsync(List<string> roleIds)
        {
            if (roleIds == null || roleIds.Count == 0)
            {
                return new List<Guid>();
            }

            var normalized = roleIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalized.Count == 0)
            {
                return new List<Guid>();
            }

            var conn = _db.Database.GetDbConnection();
            var shouldClose = false;

            try
            {
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                    shouldClose = true;
                }

                using var cmd = conn.CreateCommand();

                var paramNames = new List<string>();
                for (var i = 0; i < normalized.Count; i++)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@r" + i;
                    p.Value = normalized[i];
                    cmd.Parameters.Add(p);
                    paramNames.Add(p.ParameterName);
                }

                // Join thêm [User] để filter Status == 'Active'
                cmd.CommandText = $@"
SELECT DISTINCT ur.UserId
FROM dbo.UserRole ur
JOIN dbo.[User] u ON u.UserId = ur.UserId
WHERE u.Status = 'Active'
  AND ur.RoleId IN ({string.Join(",", paramNames)})
";

                var result = new List<Guid>();
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        result.Add(reader.GetGuid(0));
                    }
                }

                return result;
            }
            finally
            {
                if (shouldClose && conn.State == ConnectionState.Open)
                {
                    await conn.CloseAsync();
                }
            }
        }

        /// <summary>
        /// Query role names cho 1 list userId bằng JOIN dbo.UserRole + dbo.Role (ADO).
        /// Output: userId -> "Admin, Customer Care Staff"
        /// </summary>
        private async Task<Dictionary<Guid, string>> GetRoleNamesByUserIdsAsync(List<Guid> userIds)
        {
            var ids = userIds?
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList() ?? new List<Guid>();

            if (ids.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            var conn = _db.Database.GetDbConnection();
            var shouldClose = false;

            try
            {
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                    shouldClose = true;
                }

                using var cmd = conn.CreateCommand();

                var paramNames = new List<string>();
                for (var i = 0; i < ids.Count; i++)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@u" + i;
                    p.Value = ids[i];
                    cmd.Parameters.Add(p);
                    paramNames.Add(p.ParameterName);
                }

                cmd.CommandText = $@"
SELECT ur.UserId, r.[Name]
FROM dbo.UserRole ur
JOIN dbo.[Role] r ON r.RoleId = ur.RoleId
WHERE ur.UserId IN ({string.Join(",", paramNames)})
";

                var pairs = new List<(Guid UserId, string RoleName)>();
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (reader.IsDBNull(0)) continue;

                    var uid = reader.GetGuid(0);
                    var roleName = reader.IsDBNull(1) ? "" : reader.GetString(1);

                    if (!string.IsNullOrWhiteSpace(roleName))
                    {
                        pairs.Add((uid, roleName));
                    }
                }

                return pairs
                    .GroupBy(x => x.UserId)
                    .ToDictionary(
                        g => g.Key,
                        g => string.Join(", ", g.Select(x => x.RoleName).Distinct().OrderBy(x => x))
                    );
            }
            finally
            {
                if (shouldClose && conn.State == ConnectionState.Open)
                {
                    await conn.CloseAsync();
                }
            }
        }

        /// <summary>
        /// Resolve recipients = union(TargetUserIds + users theo TargetRoleIds).
        /// </summary>
        private async Task<List<Guid>> ResolveTargetUserIdsAsync(
            List<Guid>? targetUserIds,
            List<string>? targetRoleIds)
        {
            var result = new HashSet<Guid>();

            if (targetUserIds != null && targetUserIds.Count > 0)
            {
                foreach (var id in targetUserIds.Distinct())
                {
                    if (id != Guid.Empty) result.Add(id);
                }
            }

            if (targetRoleIds != null && targetRoleIds.Count > 0)
            {
                var roleUserIds = await ResolveUserIdsByRoleIdsAsync(targetRoleIds);
                foreach (var id in roleUserIds)
                {
                    if (id != Guid.Empty) result.Add(id);
                }
            }

            return result.ToList();
        }

        /// <summary>
        /// Lazy materialize global notifications cho user hiện tại:
        /// user tạo sau vẫn thấy thông báo IsGlobal=true.
        /// </summary>
        private async Task EnsureGlobalNotificationsMaterializedAsync(Guid userId)
        {
            var utcNow = DateTime.UtcNow;
            var fromUtc = utcNow.AddDays(-180);

            var missingGlobalIds = await _db.Notifications
                .AsNoTracking()
                .Where(n => n.IsGlobal && n.CreatedAtUtc >= fromUtc)
                .Where(n => n.ArchivedAtUtc == null)
                .Where(n => n.ExpiresAtUtc == null || n.ExpiresAtUtc > utcNow)
                .Where(n => !_db.NotificationUsers.Any(nu => nu.UserId == userId && nu.NotificationId == n.Id))
                .OrderByDescending(n => n.CreatedAtUtc)
                .Select(n => n.Id)
                .Take(500)
                .ToListAsync();

            if (missingGlobalIds.Count == 0)
            {
                return;
            }

            foreach (var notifId in missingGlobalIds)
            {
                _db.NotificationUsers.Add(new NotificationUser
                {
                    NotificationId = notifId,
                    UserId = userId,
                    IsRead = false,
                    ReadAtUtc = null,
                    DismissedAtUtc = null,
                    CreatedAtUtc = utcNow
                });
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Áp dụng sort cho danh sách Notification (Admin).
        /// Có xử lý global đúng nghĩa (TotalTargetUsers của global = activeUserCount).
        /// </summary>
        private static IQueryable<Notification> ApplyAdminSort(
            IQueryable<Notification> query,
            NotificationAdminFilterDto filter,
            int activeUserCountForGlobal)
        {
            var desc = filter.SortDescending;
            var raw = filter.SortBy;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return desc
                    ? query.OrderByDescending(n => n.CreatedAtUtc)
                    : query.OrderBy(n => n.CreatedAtUtc);
            }

            var sortBy = raw.Trim().ToLowerInvariant();

            switch (sortBy)
            {
                case "title":
                    return desc ? query.OrderByDescending(n => n.Title) : query.OrderBy(n => n.Title);

                case "severity":
                    return desc ? query.OrderByDescending(n => n.Severity) : query.OrderBy(n => n.Severity);

                case "system":
                case "issystemgenerated":
                    return desc ? query.OrderByDescending(n => n.IsSystemGenerated) : query.OrderBy(n => n.IsSystemGenerated);

                case "global":
                case "isglobal":
                    return desc ? query.OrderByDescending(n => n.IsGlobal) : query.OrderBy(n => n.IsGlobal);

                case "targets":
                case "totaltargetusers":
                    return desc
                        ? query.OrderByDescending(n => n.IsGlobal ? activeUserCountForGlobal : n.NotificationUsers.Count)
                        : query.OrderBy(n => n.IsGlobal ? activeUserCountForGlobal : n.NotificationUsers.Count);

                case "read":
                case "readcount":
                    return desc
                        ? query.OrderByDescending(n => n.NotificationUsers.Count(x => x.IsRead))
                        : query.OrderBy(n => n.NotificationUsers.Count(x => x.IsRead));

                case "createdat":
                case "createdatutc":
                default:
                    return desc ? query.OrderByDescending(n => n.CreatedAtUtc) : query.OrderBy(n => n.CreatedAtUtc);
            }
        }

        private static IQueryable<NotificationUser> ApplyUserSort(
            IQueryable<NotificationUser> query,
            NotificationUserFilterDto filter)
        {
            var desc = filter.SortDescending;
            var sortBy = (filter.SortBy ?? "CreatedAtUtc").ToLowerInvariant();

            // IMPORTANT: sort theo Notification.CreatedAtUtc để global không bị “nhảy” theo thời điểm materialize
            return sortBy switch
            {
                "severity" => desc
                    ? query.OrderByDescending(nu => nu.Notification.Severity).ThenByDescending(nu => nu.Notification.CreatedAtUtc)
                    : query.OrderBy(nu => nu.Notification.Severity).ThenByDescending(nu => nu.Notification.CreatedAtUtc),

                "isread" => desc
                    ? query.OrderByDescending(nu => nu.IsRead).ThenByDescending(nu => nu.Notification.CreatedAtUtc)
                    : query.OrderBy(nu => nu.IsRead).ThenByDescending(nu => nu.Notification.CreatedAtUtc),

                _ => desc
                    ? query.OrderByDescending(nu => nu.Notification.CreatedAtUtc)
                    : query.OrderBy(nu => nu.Notification.CreatedAtUtc),
            };
        }

        private ObjectResult CreateValidationProblemResult()
        {
            var problemDetails = new ValidationProblemDetails(ModelState)
            {
                Status = StatusCodes.Status400BadRequest
            };

            return new ObjectResult(problemDetails)
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        #endregion

        /// <summary>
        /// Dữ liệu dropdown phục vụ tạo thông báo thủ công:
        ///  - Roles: danh sách role (RoleId + RoleName).
        ///  - Users: danh sách user (UserId + FullName + Email).
        /// </summary>
        [HttpGet("manual-target-options")]
        [RequireRole(RoleCodes.ADMIN)]
        [ProducesResponseType(typeof(NotificationManualTargetOptionsDto), 200)]
        public async Task<ActionResult<NotificationManualTargetOptionsDto>> GetManualTargetOptions()
        {
            var roles = await _db.Roles
                .AsNoTracking()
                .OrderBy(r => r.RoleId)
                .Select(r => new NotificationTargetRoleOptionDto
                {
                    RoleId = r.RoleId,
                    RoleName = r.Name
                })
                .ToListAsync();

            // NOTE: hiện trả full list. Về lâu dài nên search/paging.
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => u.Status == "Active")
                .OrderBy(u => u.FullName)
                .ThenBy(u => u.Email)
                .Select(u => new NotificationTargetUserOptionDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email
                })
                .ToListAsync();

            return Ok(new NotificationManualTargetOptionsDto
            {
                Roles = roles,
                Users = users
            });
        }

        /// <summary>
        /// Danh sách thông báo (Admin) với filter + phân trang + sort + search.
        /// Global đúng nghĩa: TotalTargetUsers = tổng user Active, không phụ thuộc NotificationUser.
        /// </summary>
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN)]
        [ProducesResponseType(typeof(NotificationListResponseDto), 200)]
        public async Task<ActionResult<NotificationListResponseDto>> GetNotifications([FromQuery] NotificationAdminFilterDto filter)
        {
            var query = _db.Notifications
                .AsNoTracking()
                .Include(n => n.CreatedByUser)
                .AsQueryable();

            if (filter.Severity.HasValue)
                query = query.Where(n => n.Severity == filter.Severity.Value);

            if (filter.IsSystemGenerated.HasValue)
                query = query.Where(n => n.IsSystemGenerated == filter.IsSystemGenerated.Value);

            if (filter.IsGlobal.HasValue)
                query = query.Where(n => n.IsGlobal == filter.IsGlobal.Value);

            if (filter.CreatedFromUtc.HasValue)
                query = query.Where(n => n.CreatedAtUtc >= filter.CreatedFromUtc.Value);

            if (filter.CreatedToUtc.HasValue)
                query = query.Where(n => n.CreatedAtUtc <= filter.CreatedToUtc.Value);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(n => n.Title.Contains(search) || n.Message.Contains(search));
            }

            var totalCount = await query.CountAsync();

            // Chỉ cần tính active user count khi có khả năng trả global
            var activeUserCount = 0;
            if (!filter.IsGlobal.HasValue || filter.IsGlobal.Value)
            {
                activeUserCount = await CountActiveUsersAsync();
            }

            query = ApplyAdminSort(query, filter, activeUserCount);

            var pageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize <= 0 ? 1 : filter.PageSize;
            if (pageSize > 200) pageSize = 200;

            var skip = (pageNumber - 1) * pageSize;

            var items = await query
                .Skip(skip)
                .Take(pageSize)
                .Select(n => new NotificationListItemDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    Severity = n.Severity,
                    IsSystemGenerated = n.IsSystemGenerated,
                    IsGlobal = n.IsGlobal,
                    CreatedAtUtc = n.CreatedAtUtc,
                    CreatedByUserId = n.CreatedByUserId,
                    CreatedByUserEmail = n.CreatedByUser != null ? n.CreatedByUser.Email : null,
                    RelatedEntityType = n.RelatedEntityType,
                    RelatedEntityId = n.RelatedEntityId,
                    RelatedUrl = n.RelatedUrl,

                    // ✅ Global đúng nghĩa:
                    TotalTargetUsers = n.IsGlobal ? activeUserCount : n.NotificationUsers.Count,
                    ReadCount = n.NotificationUsers.Count(x => x.IsRead),

                    // Option A
                    Type = n.Type,
                    CorrelationId = n.CorrelationId,
                    ExpiresAtUtc = n.ExpiresAtUtc,
                    ArchivedAtUtc = n.ArchivedAtUtc
                })
                .ToListAsync();

            return Ok(new NotificationListResponseDto
            {
                TotalCount = totalCount,
                Items = items
            });
        }

        /// <summary>
        /// Chi tiết thông báo (Admin).
        /// Global đúng nghĩa: TotalTargetUsers = tổng user Active.
        /// Recipients: chỉ liệt kê những user đã có NotificationUser row (đã materialize).
        /// </summary>
        [HttpGet("{id:int}")]
        [RequireRole(RoleCodes.ADMIN)]
        [ProducesResponseType(typeof(NotificationDetailDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<NotificationDetailDto>> GetNotificationDetail(int id)
        {
            var notification = await _db.Notifications
                .AsNoTracking()
                .Include(n => n.CreatedByUser)
                .Include(n => n.NotificationTargetRoles)
                    .ThenInclude(tr => tr.Role)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (notification == null)
            {
                return NotFound();
            }

            var activeUserCount = notification.IsGlobal ? await CountActiveUsersAsync() : 0;

            var readCount = await _db.NotificationUsers
                .AsNoTracking()
                .CountAsync(nu => nu.NotificationId == id && nu.IsRead);

            var totalTargetUsers = notification.IsGlobal
                ? activeUserCount
                : await _db.NotificationUsers.AsNoTracking().CountAsync(nu => nu.NotificationId == id);

            // Recipients: với global thì chỉ show top 200 người đã phát sinh row (đỡ nặng UI)
            var recipientRowsQuery = _db.NotificationUsers
                .AsNoTracking()
                .Where(nu => nu.NotificationId == id);

            if (notification.IsGlobal)
            {
                recipientRowsQuery = recipientRowsQuery
                    .OrderByDescending(nu => nu.IsRead)
                    .ThenByDescending(nu => nu.ReadAtUtc)
                    .ThenByDescending(nu => nu.CreatedAtUtc)
                    .Take(200);
            }

            var recipientRows = await recipientRowsQuery.ToListAsync();

            var userIds = recipientRows
                .Select(nu => nu.UserId)
                .Distinct()
                .ToList();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName, u.Email })
                .ToListAsync();

            var usersDict = users.ToDictionary(x => x.UserId, x => x);
            var roleNamesDict = await GetRoleNamesByUserIdsAsync(userIds);

            var recipients = recipientRows
                .Select(nu =>
                {
                    usersDict.TryGetValue(nu.UserId, out var userInfo);
                    roleNamesDict.TryGetValue(nu.UserId, out var roleNames);

                    return new NotificationRecipientDto
                    {
                        UserId = nu.UserId,
                        FullName = userInfo?.FullName,
                        Email = userInfo?.Email ?? string.Empty,
                        RoleNames = roleNames ?? string.Empty,
                        IsRead = nu.IsRead
                    };
                })
                .OrderBy(r => r.FullName ?? r.Email)
                .ToList();

            return Ok(new NotificationDetailDto
            {
                Id = notification.Id,
                Title = notification.Title,
                Message = notification.Message,
                Severity = notification.Severity,
                IsSystemGenerated = notification.IsSystemGenerated,
                IsGlobal = notification.IsGlobal,
                CreatedAtUtc = notification.CreatedAtUtc,
                CreatedByUserId = notification.CreatedByUserId,
                CreatedByUserEmail = notification.CreatedByUser?.Email,
                RelatedEntityType = notification.RelatedEntityType,
                RelatedEntityId = notification.RelatedEntityId,
                RelatedUrl = notification.RelatedUrl,

                TotalTargetUsers = totalTargetUsers,
                ReadCount = readCount,
                UnreadCount = Math.Max(0, totalTargetUsers - readCount),

                TargetRoles = notification.NotificationTargetRoles
                    .Select(tr => new NotificationTargetRoleDto
                    {
                        RoleId = tr.RoleId,
                        RoleName = tr.Role?.Name
                    })
                    .ToList(),

                Recipients = recipients,

                // Option A
                Type = notification.Type,
                DedupKey = notification.DedupKey,
                CorrelationId = notification.CorrelationId,
                PayloadJson = notification.PayloadJson,
                ExpiresAtUtc = notification.ExpiresAtUtc,
                ArchivedAtUtc = notification.ArchivedAtUtc
            });
        }

        /// <summary>
        /// Lịch sử thông báo của user hiện tại (dựa vào NotificationUser).
        /// IMPORTANT: CreatedAtUtc trả về = Notification.CreatedAtUtc để global không bị sai thời gian.
        /// </summary>
        [HttpGet("my")]
        [Authorize]
        [ProducesResponseType(typeof(NotificationUserListResponseDto), 200)]
        public async Task<ActionResult<NotificationUserListResponseDto>> GetMyNotifications([FromQuery] NotificationUserFilterDto filter)
        {
            Guid userId;
            try
            {
                userId = GetCurrentUserId();
            }
            catch
            {
                return Unauthorized();
            }

            try
            {
                await EnsureGlobalNotificationsMaterializedAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to materialize global notifications for user {UserId}", userId);
            }

            var utcNow = DateTime.UtcNow;

            var query = _db.NotificationUsers
                .AsNoTracking()
                .Where(nu => nu.UserId == userId)
                .Include(nu => nu.Notification)
                .Where(nu => nu.Notification.ArchivedAtUtc == null)
                .Where(nu => nu.Notification.ExpiresAtUtc == null || nu.Notification.ExpiresAtUtc > utcNow)
                .AsQueryable();

            if (filter.OnlyUnread)
                query = query.Where(nu => !nu.IsRead);

            if (filter.Severity.HasValue)
                query = query.Where(nu => nu.Notification.Severity == filter.Severity.Value);

            if (filter.FromUtc.HasValue)
                query = query.Where(nu => nu.Notification.CreatedAtUtc >= filter.FromUtc.Value);

            if (filter.ToUtc.HasValue)
                query = query.Where(nu => nu.Notification.CreatedAtUtc <= filter.ToUtc.Value);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(nu => nu.Notification.Title.Contains(search) || nu.Notification.Message.Contains(search));
            }

            var totalCount = await query.CountAsync();
            query = ApplyUserSort(query, filter);

            var pageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize <= 0 ? 1 : filter.PageSize;
            if (pageSize > 200) pageSize = 200;

            var skip = (pageNumber - 1) * pageSize;

            var items = await query
                .Skip(skip)
                .Take(pageSize)
                .Select(nu => new NotificationUserListItemDto
                {
                    NotificationUserId = nu.Id,
                    NotificationId = nu.NotificationId,
                    Title = nu.Notification.Title,
                    Message = nu.Notification.Message,
                    Severity = nu.Notification.Severity,
                    IsRead = nu.IsRead,

                    // ✅ dùng Notification.CreatedAtUtc
                    CreatedAtUtc = nu.Notification.CreatedAtUtc,
                    ReadAtUtc = nu.ReadAtUtc,

                    IsSystemGenerated = nu.Notification.IsSystemGenerated,
                    IsGlobal = nu.Notification.IsGlobal,

                    RelatedEntityType = nu.Notification.RelatedEntityType,
                    RelatedEntityId = nu.Notification.RelatedEntityId,
                    RelatedUrl = nu.Notification.RelatedUrl,

                    // Option A
                    Type = nu.Notification.Type,
                    ExpiresAtUtc = nu.Notification.ExpiresAtUtc
                })
                .ToListAsync();

            return Ok(new NotificationUserListResponseDto
            {
                TotalCount = totalCount,
                Items = items
            });
        }

        /// <summary>
        /// Unread count (user hiện tại).
        /// </summary>
        [HttpGet("my/unread-count")]
        [Authorize]
        [ProducesResponseType(typeof(NotificationUnreadCountDto), 200)]
        public async Task<ActionResult<NotificationUnreadCountDto>> GetMyUnreadCount()
        {
            Guid userId;
            try
            {
                userId = GetCurrentUserId();
            }
            catch
            {
                return Unauthorized();
            }

            try
            {
                await EnsureGlobalNotificationsMaterializedAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to materialize global notifications for unread-count user {UserId}", userId);
            }

            var utcNow = DateTime.UtcNow;

            var unreadCount = await _db.NotificationUsers
                .AsNoTracking()
                .Where(nu => nu.UserId == userId && !nu.IsRead)
                .Where(nu => nu.Notification.ArchivedAtUtc == null)
                .Where(nu => nu.Notification.ExpiresAtUtc == null || nu.Notification.ExpiresAtUtc > utcNow)
                .CountAsync();

            return Ok(new NotificationUnreadCountDto { UnreadCount = unreadCount });
        }

        /// <summary>
        /// Đánh dấu 1 thông báo của user hiện tại là đã đọc.
        /// </summary>
        [HttpPost("my/{notificationUserId:long}/read")]
        [Authorize]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<ActionResult> MarkMyNotificationAsRead(long notificationUserId)
        {
            Guid userId;
            try
            {
                userId = GetCurrentUserId();
            }
            catch
            {
                return Unauthorized();
            }

            var notifUser = await _db.NotificationUsers
                .FirstOrDefaultAsync(nu => nu.Id == notificationUserId && nu.UserId == userId);

            if (notifUser == null)
            {
                return NotFound();
            }

            if (!notifUser.IsRead)
            {
                notifUser.IsRead = true;
                notifUser.ReadAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return NoContent();
        }

        /// <summary>
        /// Tạo thông báo thủ công.
        /// - IsGlobal=true: không bắt buộc TargetUserIds/TargetRoleIds (global đúng nghĩa)
        /// - IsGlobal=false: recipients = union(TargetUserIds + users theo TargetRoleIds)
        /// </summary>
        [HttpPost]
        [RequireRole(RoleCodes.ADMIN)]
        [ProducesResponseType(typeof(object), 201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult> CreateManualNotification([FromBody] CreateNotificationDto dto)
        {
            if (!ModelState.IsValid)
            {
                return CreateValidationProblemResult();
            }

            if (!dto.IsGlobal)
            {
                var hasUsers = dto.TargetUserIds != null && dto.TargetUserIds.Count > 0;
                var hasRoles = dto.TargetRoleIds != null && dto.TargetRoleIds.Count > 0;

                if (!hasUsers && !hasRoles)
                {
                    ModelState.AddModelError(nameof(dto.TargetUserIds),
                        "At least one target user or target role is required.");
                    return CreateValidationProblemResult();
                }
            }

            Guid creatorId;
            try
            {
                creatorId = GetCurrentUserId();
            }
            catch
            {
                return Unauthorized();
            }

            var recipients = new List<Guid>();

            if (!dto.IsGlobal)
            {
                recipients = await ResolveTargetUserIdsAsync(dto.TargetUserIds, dto.TargetRoleIds);

                recipients = await _db.Users
                    .AsNoTracking()
                    .Where(u => recipients.Contains(u.UserId) && u.Status == "Active")
                    .Select(u => u.UserId)
                    .Distinct()
                    .ToListAsync();

                if (recipients.Count == 0)
                {
                    ModelState.AddModelError(nameof(dto.TargetUserIds),
                        "No valid users found for the given TargetUserIds/TargetRoleIds.");
                    return CreateValidationProblemResult();
                }
            }

            var utcNow = DateTime.UtcNow;

            var notification = new Notification
            {
                Title = SafeTrim(dto.Title),
                Message = SafeTrim(dto.Message),
                Severity = dto.Severity,
                IsSystemGenerated = false,
                IsGlobal = dto.IsGlobal,
                CreatedAtUtc = utcNow,
                CreatedByUserId = creatorId,
                RelatedEntityType = string.IsNullOrWhiteSpace(dto.RelatedEntityType) ? null : dto.RelatedEntityType.Trim(),
                RelatedEntityId = string.IsNullOrWhiteSpace(dto.RelatedEntityId) ? null : dto.RelatedEntityId.Trim(),
                RelatedUrl = string.IsNullOrWhiteSpace(dto.RelatedUrl) ? null : dto.RelatedUrl.Trim(),

                // ✅ Option A: set tối thiểu để trace (không bắt FE phải truyền)
                Type = "Manual",
                CorrelationId = Guid.NewGuid().ToString("N"),
            };

            if (dto.TargetRoleIds != null && dto.TargetRoleIds.Count > 0)
            {
                var roleIds = dto.TargetRoleIds
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct()
                    .ToList();

                var existingRoles = await _db.Roles
                    .AsNoTracking()
                    .Where(r => roleIds.Contains(r.RoleId))
                    .Select(r => r.RoleId)
                    .ToListAsync();

                foreach (var roleId in existingRoles)
                {
                    notification.NotificationTargetRoles.Add(new NotificationTargetRole
                    {
                        RoleId = roleId
                    });
                }
            }

            if (!notification.IsGlobal)
            {
                foreach (var userId in recipients)
                {
                    notification.NotificationUsers.Add(new NotificationUser
                    {
                        UserId = userId,
                        IsRead = false,
                        CreatedAtUtc = utcNow,
                        ReadAtUtc = null,
                        DismissedAtUtc = null
                    });
                }
            }

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            // Push realtime (best-effort)
            try
            {
                if (notification.IsGlobal)
                {
                    var dtoGlobal = new
                    {
                        notificationId = notification.Id,
                        title = notification.Title,
                        message = notification.Message,
                        severity = notification.Severity,
                        createdAtUtc = notification.CreatedAtUtc,
                        isGlobal = true,
                        type = notification.Type,
                        correlationId = notification.CorrelationId,
                        relatedUrl = notification.RelatedUrl
                    };

                    await _notificationHub.Clients.All.SendAsync("ReceiveGlobalNotification", dtoGlobal);
                }
                else
                {
                    foreach (var nu in notification.NotificationUsers)
                    {
                        var dtoUser = new NotificationUserListItemDto
                        {
                            NotificationUserId = nu.Id,
                            NotificationId = nu.NotificationId,
                            Title = notification.Title,
                            Message = notification.Message,
                            Severity = notification.Severity,
                            IsRead = nu.IsRead,
                            CreatedAtUtc = notification.CreatedAtUtc,
                            ReadAtUtc = nu.ReadAtUtc,
                            IsSystemGenerated = notification.IsSystemGenerated,
                            IsGlobal = notification.IsGlobal,
                            RelatedEntityType = notification.RelatedEntityType,
                            RelatedEntityId = notification.RelatedEntityId,
                            RelatedUrl = notification.RelatedUrl,
                            Type = notification.Type,
                            ExpiresAtUtc = notification.ExpiresAtUtc
                        };

                        await _notificationHub
                            .Clients
                            .Group(NotificationHub.UserGroup(nu.UserId))
                            .SendAsync("ReceiveNotification", dtoUser);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push realtime notifications");
            }

            return CreatedAtAction(
                nameof(GetNotificationDetail),
                new { id = notification.Id },
                new { notification.Id });
        }
    }
}
