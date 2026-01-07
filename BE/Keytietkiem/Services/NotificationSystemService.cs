// File: Services/NotificationSystemService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Hubs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services.Background;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Services
{
    /// <summary>
    /// Tạo + dispatch notification từ các nghiệp vụ khác.
    /// 
    /// Notes:
    /// - Role claim của hệ thống = Role Code, nhưng bảng NotificationTargetRole cần RoleId.
    /// - Dispatch realtime qua INotificationDispatchQueue để không fan-out SendAsync trực tiếp trong request.
    /// </summary>
    public class NotificationSystemService : INotificationSystemService
    {
        private readonly KeytietkiemDbContext _context;
        private readonly INotificationDispatchQueue _dispatchQueue;
        private readonly IClock _clock;
        private readonly ILogger<NotificationSystemService> _logger;

        public NotificationSystemService(
            KeytietkiemDbContext context,
            INotificationDispatchQueue dispatchQueue,
            IClock clock,
            ILogger<NotificationSystemService> logger)
        {
            _context = context;
            _dispatchQueue = dispatchQueue;
            _clock = clock;
            _logger = logger;
        }

        /// <summary>
        /// Tạo & dispatch system notification: có thể gửi theo role, theo user, hoặc cả 2.
        /// Best-effort: không throw làm hỏng luồng chính (trừ request null).
        /// </summary>
        public Task<int> CreateAsync(SystemNotificationCreateRequest request, CancellationToken cancellationToken = default)
        {
            // dùng 1 luồng chung để tránh duplicate code
            return CreateInternalAsync(request, cancellationToken);
        }

        public Task<int> CreateForRoleCodesAsync(SystemNotificationCreateRequest request, CancellationToken cancellationToken = default)
        {
            return CreateInternalAsync(request, cancellationToken);
        }

        public Task<int> CreateForUserIdsAsync(SystemNotificationCreateRequest request, CancellationToken cancellationToken = default)
        {
            return CreateInternalAsync(request, cancellationToken);
        }

        private async Task<int> CreateInternalAsync(SystemNotificationCreateRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var title = (request.Title ?? string.Empty).Trim();
            var message = (request.Message ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("System notification missing Title/Message. Type={Type}", request.Type);
                return 0;
            }

            // ---- Normalize inputs
            var roleCodes = (request.TargetRoleCodes ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

            var explicitUserIds = (request.TargetUserIds ?? new List<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .Take(20000)
                .ToList();

            // ---- Resolve roles: roleCode -> roleId
            List<string> roleIds = new();
            if (roleCodes.Count > 0)
            {
                roleIds = await _context.Roles
                    .AsNoTracking()
                    .Where(r => r.IsActive && roleCodes.Contains(r.Code))
                    .Select(r => r.RoleId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }

            // ---- Resolve users by roleIds
            List<Guid> userIdsFromRoles = new();
            if (roleIds.Count > 0)
            {
                userIdsFromRoles = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Status == "Active" && u.Roles.Any(r => roleIds.Contains(r.RoleId)))
                    .Select(u => u.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }

            // ---- Validate explicit users exist (and active)
            List<Guid> existedExplicitUsers = new();
            if (explicitUserIds.Count > 0)
            {
                existedExplicitUsers = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Status == "Active" && explicitUserIds.Contains(u.UserId))
                    .Select(u => u.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }

            var targetUserIds = userIdsFromRoles
                .Concat(existedExplicitUsers)
                .Distinct()
                .ToList();

            if (roleIds.Count == 0 && targetUserIds.Count == 0)
            {
                // Không có người nhận thì thôi
                return 0;
            }

            var now = _clock.UtcNow;

            // 1) Create notification
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Severity = request.Severity,
                IsSystemGenerated = true,
                IsGlobal = false,
                CreatedAtUtc = now,
                CreatedByUserId = request.CreatedByUserId,
                Type = string.IsNullOrWhiteSpace(request.Type) ? null : request.Type.Trim(),
                CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                    ? Guid.NewGuid().ToString("N")
                    : request.CorrelationId!.Trim(),
                RelatedEntityType = string.IsNullOrWhiteSpace(request.RelatedEntityType) ? null : request.RelatedEntityType.Trim(),
                RelatedEntityId = string.IsNullOrWhiteSpace(request.RelatedEntityId) ? null : request.RelatedEntityId.Trim(),
                RelatedUrl = string.IsNullOrWhiteSpace(request.RelatedUrl) ? null : request.RelatedUrl.Trim(),
                // ❌ Removed: DedupKey, PayloadJson, ExpiresAtUtc
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(cancellationToken);

            // 2) Target roles (để Admin xem "Phạm vi/Role" đúng)
            if (roleIds.Count > 0)
            {
                var targetRoleRows = roleIds
                    .Select(roleId => new NotificationTargetRole
                    {
                        NotificationId = notification.Id,
                        RoleId = roleId
                    })
                    .ToList();

                _context.NotificationTargetRoles.AddRange(targetRoleRows);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // 3) Create NotificationUser rows (to appear in /my list)
            // NOTE: Nếu targetUserIds rỗng nhưng roleIds có (và role không có user) => vẫn không có người nhận => return 0 ở trên rồi.
            var notificationUsersToInsert = targetUserIds
                .Select(userId => new NotificationUser
                {
                    NotificationId = notification.Id,
                    UserId = userId,
                    IsRead = false,
                    CreatedAtUtc = now
                })
                .ToList();

            if (notificationUsersToInsert.Count > 0)
            {
                _context.NotificationUsers.AddRange(notificationUsersToInsert);

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    // Unique index (NotificationId, UserId) prevents duplicates.
                    // In rare race conditions, ignore duplicates to avoid failing the whole business action.
                    _logger.LogWarning(ex, "NotificationUser insert may contain duplicates. NotificationId={NotificationId}", notification.Id);
                }
            }

            // 4) Dispatch realtime (enqueue)
            // ✅ Dispatch theo dữ liệu đã lưu thật trong DB (tránh trường hợp SaveChanges fail mà vẫn dispatch)
            var persistedUsers = await _context.NotificationUsers
                .AsNoTracking()
                .Where(nu => nu.NotificationId == notification.Id)
                .ToListAsync(cancellationToken);

            foreach (var nu in persistedUsers)
            {
                var payload = new
                {
                    notificationUserId = nu.Id,
                    notificationId = notification.Id,
                    title = notification.Title,
                    message = notification.Message,
                    severity = notification.Severity,
                    createdAtUtc = notification.CreatedAtUtc,
                    type = notification.Type,
                    isRead = false,
                    isSystemGenerated = true,
                    isGlobal = false,
                    relatedEntityType = notification.RelatedEntityType,
                    relatedEntityId = notification.RelatedEntityId,
                    relatedUrl = notification.RelatedUrl
                };

                await _dispatchQueue.QueueToGroupAsync(
                    NotificationHub.UserGroup(nu.UserId),
                    payload,
                    NotificationDispatchQueue.MethodReceiveNotification,
                    cancellationToken
                );
            }

            return notification.Id;
        }
    }
}
