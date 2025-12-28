// File: Controllers/NotificationsController.cs
using Keytietkiem.DTOs;
using Keytietkiem.Hubs;
using Keytietkiem.Models;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
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

        /// <summary>
        /// Áp dụng sort cho danh sách Notification (Admin).
        /// </summary>
        private static IQueryable<Notification> ApplyAdminSort(
            IQueryable<Notification> query,
            NotificationAdminFilterDto filter)
        {
            var desc = filter.SortDescending;
            var raw = filter.SortBy;

            // Mặc định sort theo CreatedAt DESC nếu không truyền
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
                    return desc
                        ? query.OrderByDescending(n => n.Title)
                        : query.OrderBy(n => n.Title);

                case "severity":
                    return desc
                        ? query.OrderByDescending(n => n.Severity)
                        : query.OrderBy(n => n.Severity);

                case "system":
                case "issystemgenerated":
                    return desc
                        ? query.OrderByDescending(n => n.IsSystemGenerated)
                        : query.OrderBy(n => n.IsSystemGenerated);

                case "global":
                case "isglobal":
                    return desc
                        ? query.OrderByDescending(n => n.IsGlobal)
                        : query.OrderBy(n => n.IsGlobal);

                case "targets":
                case "totaltargetusers":
                    // Sort theo tổng số user target của thông báo
                    return desc
                        ? query.OrderByDescending(n => n.NotificationUsers.Count)
                        : query.OrderBy(n => n.NotificationUsers.Count);

                case "read":
                case "readcount":
                    // Sort theo số user đã đọc
                    return desc
                        ? query.OrderByDescending(n =>
                            n.NotificationUsers.Count(x => x.IsRead))
                        : query.OrderBy(n =>
                            n.NotificationUsers.Count(x => x.IsRead));

                case "createdat":
                case "createdatutc":
                default:
                    return desc
                        ? query.OrderByDescending(n => n.CreatedAtUtc)
                        : query.OrderBy(n => n.CreatedAtUtc);
            }
        }

        private static IQueryable<NotificationUser> ApplyUserSort(
            IQueryable<NotificationUser> query,
            NotificationUserFilterDto filter)
        {
            var desc = filter.SortDescending;
            var sortBy = (filter.SortBy ?? "CreatedAtUtc").ToLowerInvariant();

            return sortBy switch
            {
                "severity" => desc
                    ? query.OrderByDescending(nu => nu.Notification.Severity)
                    : query.OrderBy(nu => nu.Notification.Severity),

                "isread" => desc
                    ? query.OrderByDescending(nu => nu.IsRead)
                    : query.OrderBy(nu => nu.IsRead),

                _ => desc
                    ? query.OrderByDescending(nu => nu.CreatedAtUtc)
                    : query.OrderBy(nu => nu.CreatedAtUtc),
            };
        }

        #endregion

        /// <summary>
        /// Dữ liệu dropdown phục vụ tạo thông báo thủ công:
        ///  - Roles: danh sách role (RoleId + RoleName).
        ///  - Users: danh sách user (UserId + FullName + Email).
        /// </summary>
        [HttpGet("manual-target-options")]
        [ProducesResponseType(typeof(NotificationManualTargetOptionsDto), 200)]
        public async Task<ActionResult<NotificationManualTargetOptionsDto>> GetManualTargetOptions()
        {
            // Roles
            var roles = await _db.Roles
                .AsNoTracking()
                .OrderBy(r => r.RoleId)
                .Select(r => new NotificationTargetRoleOptionDto
                {
                    RoleId = r.RoleId,
                    RoleName = r.Name
                })
                .ToListAsync();

            // Users – tuỳ schema mà chỉnh lại FullName, IsActive...
            var usersQuery = _db.Users.AsNoTracking();

            var users = await usersQuery
                .OrderBy(u => u.FullName)   // nếu không có FullName thì OrderBy(u => u.Email)
                .ThenBy(u => u.Email)
                .Select(u => new NotificationTargetUserOptionDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email
                })
                .ToListAsync();

            var result = new NotificationManualTargetOptionsDto
            {
                Roles = roles,
                Users = users
            };

            return Ok(result);
        }
        // Thêm vào trong #region Helpers của NotificationsController
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

        /// <summary>
        /// Danh sách thông báo (Admin) với filter + phân trang + sort + search.
        /// </summary>
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN)]
        [ProducesResponseType(typeof(NotificationListResponseDto), 200)]
        public async Task<ActionResult<NotificationListResponseDto>> GetNotifications(
            [FromQuery] NotificationAdminFilterDto filter)
        {
            var query = _db.Notifications
                .AsNoTracking()
                .Include(n => n.CreatedByUser)
                .Include(n => n.NotificationUsers)
                .AsQueryable();

            // Filter
            if (filter.Severity.HasValue)
            {
                query = query.Where(n => n.Severity == filter.Severity.Value);
            }

            if (filter.IsSystemGenerated.HasValue)
            {
                query = query.Where(n => n.IsSystemGenerated == filter.IsSystemGenerated.Value);
            }

            if (filter.IsGlobal.HasValue)
            {
                query = query.Where(n => n.IsGlobal == filter.IsGlobal.Value);
            }

            if (filter.CreatedFromUtc.HasValue)
            {
                query = query.Where(n => n.CreatedAtUtc >= filter.CreatedFromUtc.Value);
            }

            if (filter.CreatedToUtc.HasValue)
            {
                query = query.Where(n => n.CreatedAtUtc <= filter.CreatedToUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(n =>
                    n.Title.Contains(search) ||
                    n.Message.Contains(search));
            }

            var totalCount = await query.CountAsync();

            // Sort tất cả các cột hiển thị
            query = ApplyAdminSort(query, filter);

            // Chuẩn hóa paging giống CategoriesController
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
                    TotalTargetUsers = n.NotificationUsers.Count,
                    ReadCount = n.NotificationUsers.Count(x => x.IsRead)
                })
                .ToListAsync();

            var response = new NotificationListResponseDto
            {
                TotalCount = totalCount,
                Items = items
            };

            return Ok(response);
        }

        /// <summary>
        /// Chi tiết thông báo (Admin).
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
                .Include(n => n.NotificationUsers)
                .Include(n => n.NotificationTargetRoles)
                    .ThenInclude(tr => tr.Role)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (notification == null)
            {
                return NotFound();
            }

            var totalTargetUsers = notification.NotificationUsers.Count;
            var readCount = notification.NotificationUsers.Count(x => x.IsRead);

            // Lấy danh sách userId trong thông báo này
            var userIds = notification.NotificationUsers
                .Select(nu => nu.UserId)
                .Distinct()
                .ToList();

            // Lấy thông tin user (FullName, Email)
            var users = await _db.Users
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email
                })
                .ToListAsync();

            var usersDict = users.ToDictionary(x => x.UserId, x => x);

            // Map ra danh sách Recipients cho bảng chi tiết
            var recipients = notification.NotificationUsers
                .Select(nu =>
                {
                    usersDict.TryGetValue(nu.UserId, out var userInfo);

                    return new NotificationRecipientDto
                    {
                        UserId = nu.UserId,
                        FullName = userInfo?.FullName,
                        Email = userInfo?.Email ?? string.Empty,

                        // TODO: Nếu sau này có bảng UserRoles / navigation User.UserRoles
                        // thì map RoleNames ở đây, ví dụ:
                        // RoleNames = roleNamesDict.TryGetValue(nu.UserId, out var rn) ? rn : string.Empty,
                        RoleNames = string.Empty,

                        IsRead = nu.IsRead
                    };
                })
                .OrderBy(r => r.FullName ?? r.Email)
                .ToList();

            var dto = new NotificationDetailDto
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
                UnreadCount = totalTargetUsers - readCount,
                TargetRoles = notification.NotificationTargetRoles
                    .Select(tr => new NotificationTargetRoleDto
                    {
                        RoleId = tr.RoleId,
                        RoleName = tr.Role?.Name
                    })
                    .ToList(),
                Recipients = recipients
            };

            return Ok(dto);
        }

        /// <summary>
        /// Lịch sử thông báo của user hiện tại (dựa vào NotificationUser).
        /// </summary>
        [HttpGet("my")]
        [Authorize]
        [ProducesResponseType(typeof(NotificationUserListResponseDto), 200)]
        public async Task<ActionResult<NotificationUserListResponseDto>> GetMyNotifications(
            [FromQuery] NotificationUserFilterDto filter)
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

            var query = _db.NotificationUsers
                .AsNoTracking()
                .Where(nu => nu.UserId == userId)
                .Include(nu => nu.Notification)
                .AsQueryable();

            if (filter.OnlyUnread)
            {
                query = query.Where(nu => !nu.IsRead);
            }

            if (filter.Severity.HasValue)
            {
                query = query.Where(nu => nu.Notification.Severity == filter.Severity.Value);
            }

            if (filter.FromUtc.HasValue)
            {
                query = query.Where(nu => nu.CreatedAtUtc >= filter.FromUtc.Value);
            }

            if (filter.ToUtc.HasValue)
            {
                query = query.Where(nu => nu.CreatedAtUtc <= filter.ToUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(nu =>
                    nu.Notification.Title.Contains(search) ||
                    nu.Notification.Message.Contains(search));
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
                    CreatedAtUtc = nu.CreatedAtUtc,
                    ReadAtUtc = nu.ReadAtUtc,
                    IsSystemGenerated = nu.Notification.IsSystemGenerated,
                    IsGlobal = nu.Notification.IsGlobal,
                    RelatedEntityType = nu.Notification.RelatedEntityType,
                    RelatedEntityId = nu.Notification.RelatedEntityId,
                    RelatedUrl = nu.Notification.RelatedUrl
                })
                .ToListAsync();

            var response = new NotificationUserListResponseDto
            {
                TotalCount = totalCount,
                Items = items
            };

            return Ok(response);
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
        /// Tạo thông báo thủ công và gán cho danh sách user cụ thể.
        /// </summary>
        [HttpPost]
        [RequireRole(RoleCodes.ADMIN)]
        [ProducesResponseType(typeof(object), 201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult> CreateManualNotification(
            [FromBody] CreateNotificationDto dto)
        {
            // TC1: ModelState invalid -> trả 400 với ObjectResult
            if (!ModelState.IsValid)
            {
                return CreateValidationProblemResult();
            }

            // TC2: thiếu TargetUserIds -> 400 + ModelState có error
            if (dto.TargetUserIds == null || dto.TargetUserIds.Count == 0)
            {
                ModelState.AddModelError(nameof(dto.TargetUserIds),
                    "At least one target user is required.");
                return CreateValidationProblemResult();
            }

            Guid creatorId;
            try
            {
                creatorId = GetCurrentUserId();
            }
            catch
            {
                // TC3: không xác định được current user -> 401
                return Unauthorized();
            }

            // Lọc các user tồn tại thật trong hệ thống
            var targetUserIds = dto.TargetUserIds.Distinct().ToList();

            var existingUserIds = await _db.Users
                .Where(u => targetUserIds.Contains(u.UserId))
                .Select(u => u.UserId)
                .ToListAsync();

            // TC4: không có user nào tồn tại -> 400 + ModelState error
            if (existingUserIds.Count == 0)
            {
                ModelState.AddModelError(nameof(dto.TargetUserIds),
                    "No valid users found for the given TargetUserIds.");
                return CreateValidationProblemResult();
            }

            var utcNow = DateTime.UtcNow;

            var notification = new Notification
            {
                Title = dto.Title?.Trim(),
                Message = dto.Message?.Trim(),
                Severity = dto.Severity,
                IsSystemGenerated = false,
                IsGlobal = dto.IsGlobal,
                CreatedAtUtc = utcNow,
                CreatedByUserId = creatorId,
                RelatedEntityType = string.IsNullOrWhiteSpace(dto.RelatedEntityType)
                    ? null
                    : dto.RelatedEntityType.Trim(),
                RelatedEntityId = string.IsNullOrWhiteSpace(dto.RelatedEntityId)
                    ? null
                    : dto.RelatedEntityId.Trim(),
                RelatedUrl = string.IsNullOrWhiteSpace(dto.RelatedUrl)
                    ? null
                    : dto.RelatedUrl.Trim()
            };

            // Mapping các role mục tiêu (nếu có, chỉ thêm các role tồn tại)
            if (dto.TargetRoleIds != null && dto.TargetRoleIds.Count > 0)
            {
                var roleIds = dto.TargetRoleIds.Distinct().ToList();

                var existingRoles = await _db.Roles
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

            // Tạo NotificationUser cho từng user
            foreach (var userId in existingUserIds)
            {
                notification.NotificationUsers.Add(new NotificationUser
                {
                    UserId = userId,
                    IsRead = false,
                    CreatedAtUtc = utcNow,
                    ReadAtUtc = null
                });
            }

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            // Push realtime qua SignalR (best-effort, lỗi thì chỉ log)
            try
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
                        CreatedAtUtc = nu.CreatedAtUtc,
                        ReadAtUtc = nu.ReadAtUtc,
                        IsSystemGenerated = notification.IsSystemGenerated,
                        IsGlobal = notification.IsGlobal,
                        RelatedEntityType = notification.RelatedEntityType,
                        RelatedEntityId = notification.RelatedEntityId,
                        RelatedUrl = notification.RelatedUrl
                    };

                    await _notificationHub
                        .Clients
                        .Group(NotificationHub.UserGroup(nu.UserId))
                        .SendAsync("ReceiveNotification", dtoUser);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push realtime notifications");
            }

            // Trả về 201 + id của thông báo vừa tạo
            return CreatedAtAction(
                nameof(GetNotificationDetail),
                new { id = notification.Id },
                new { notification.Id });
        }

    }
}
