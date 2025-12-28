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
    /// Service tạo notification "hệ thống":
    /// - Lưu DB Notification + NotificationTargetRole + NotificationUser
    /// - Push realtime qua SignalR (NotificationHub) bằng background queue
    ///
    /// Best-effort: bọc lỗi để không làm fail luồng chính.
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

        public Task<int> CreateForRoleCodesAsync(SystemNotificationCreateRequest request, CancellationToken cancellationToken = default)
            => CreateAsync(request, cancellationToken);

        public Task<int> CreateForUserIdsAsync(SystemNotificationCreateRequest request, CancellationToken cancellationToken = default)
            => CreateAsync(request, cancellationToken);

        public async Task<int> CreateAsync(SystemNotificationCreateRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) return 0;

            // Trim + validate tối thiểu (không throw)
            var title = (request.Title ?? string.Empty).Trim();
            var message = (request.Message ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("System notification missing Title/Message. Type={Type}", request.Type);
                return 0;
            }

            // Normalize targets
            var roleInputs = (request.TargetRoleCodes ?? new List<string>())
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

            if (roleInputs.Count == 0 && explicitUserIds.Count == 0)
            {
                // giống manual: không có target thì thôi (best-effort)
                return 0;
            }

            var dedupKey = string.IsNullOrWhiteSpace(request.DedupKey) ? null : request.DedupKey.Trim();
            var type = string.IsNullOrWhiteSpace(request.Type) ? "System" : request.Type.Trim();
            var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                ? Guid.NewGuid().ToString("N")
                : request.CorrelationId.Trim();

            var relatedEntityType = string.IsNullOrWhiteSpace(request.RelatedEntityType) ? null : request.RelatedEntityType.Trim();
            var relatedEntityId = string.IsNullOrWhiteSpace(request.RelatedEntityId) ? null : request.RelatedEntityId.Trim();

            var now = _clock.UtcNow;

            try
            {
                // Idempotency (giống manual)
                if (!string.IsNullOrWhiteSpace(dedupKey))
                {
                    var existed = await _context.Notifications.AsNoTracking()
                        .FirstOrDefaultAsync(n => n.DedupKey == dedupKey, cancellationToken);

                    if (existed != null)
                        return existed.Id;
                }

                // Resolve roles: accept Role.Code OR Role.RoleId
                List<Role> resolvedRoles = new();
                if (roleInputs.Count > 0)
                {
                    resolvedRoles = await _context.Roles.AsNoTracking()
                        .Where(r => roleInputs.Contains(r.Code) || roleInputs.Contains(r.RoleId))
                        .ToListAsync(cancellationToken);
                }

                var roleIds = resolvedRoles
                    .Select(r => r.RoleId)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                // Users from roles (User.Roles navigation)
                List<Guid> userIdsFromRoles = new();
                if (roleIds.Count > 0)
                {
                    userIdsFromRoles = await _context.Users.AsNoTracking()
                        .Where(u => u.Roles.Any(r => roleIds.Contains(r.RoleId)))
                        .Select(u => u.UserId)
                        .ToListAsync(cancellationToken);
                }

                // Validate explicit user ids exist
                List<Guid> existedExplicitUsers = new();
                if (explicitUserIds.Count > 0)
                {
                    existedExplicitUsers = await _context.Users.AsNoTracking()
                        .Where(u => explicitUserIds.Contains(u.UserId))
                        .Select(u => u.UserId)
                        .ToListAsync(cancellationToken);
                }

                var targetUserIds = userIdsFromRoles
                    .Concat(existedExplicitUsers)
                    .Distinct()
                    .ToList();

                // Create notification (DB)
                var notification = new Notification
                {
                    Title = title,
                    Message = message,
                    Severity = request.Severity,
                    IsSystemGenerated = true,
                    IsGlobal = false,

                    Type = type,
                    CorrelationId = correlationId,
                    DedupKey = dedupKey,
                    PayloadJson = request.PayloadJson,

                    // ✅ keep all relation fields
                    RelatedUrl = request.RelatedUrl,
                    RelatedEntityType = relatedEntityType,
                    RelatedEntityId = relatedEntityId,

                    ExpiresAtUtc = request.ExpiresAtUtc,

                    CreatedByUserId = request.CreatedByUserId,
                    CreatedAtUtc = now
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync(cancellationToken);

                // store target roles for admin detail UI
                if (roleIds.Count > 0)
                {
                    var ntrs = roleIds.Select(rid => new NotificationTargetRole
                    {
                        NotificationId = notification.Id,
                        RoleId = rid
                    }).ToList();

                    _context.NotificationTargetRoles.AddRange(ntrs);
                }

                // create NotificationUser rows (so FE always has NotificationUserId)
                List<NotificationUser> notificationUsers = new();
                if (targetUserIds.Count > 0)
                {
                    notificationUsers = targetUserIds.Select(uid => new NotificationUser
                    {
                        NotificationId = notification.Id,
                        UserId = uid,
                        IsRead = false,
                        ReadAtUtc = null,
                        CreatedAtUtc = now
                    }).ToList();

                    _context.NotificationUsers.AddRange(notificationUsers);
                }

                await _context.SaveChangesAsync(cancellationToken);

                // Realtime dispatch (best-effort)
                if (notificationUsers.Count > 0)
                {
                    foreach (var nu in notificationUsers)
                    {
                        var payload = new
                        {
                            notificationId = notification.Id,
                            notificationUserId = nu.Id,
                            title = notification.Title,
                            message = notification.Message,
                            severity = notification.Severity,
                            type = notification.Type,
                            createdAtUtc = notification.CreatedAtUtc,

                            // ✅ include related fields for FE click
                            relatedUrl = notification.RelatedUrl,
                            relatedEntityType = notification.RelatedEntityType,
                            relatedEntityId = notification.RelatedEntityId,

                            isSystemGenerated = notification.IsSystemGenerated,
                            isRead = false
                        };

                        // ✅ Queue signature has NO cancellationToken
                        await _dispatchQueue.QueueToGroupAsync(
                            NotificationHub.UserGroup(nu.UserId),
                            payload,
                            NotificationDispatchQueue.MethodReceiveNotification
                        );
                    }
                }

                return notification.Id;
            }
            catch (DbUpdateException ex)
            {
                // Nếu vướng unique DedupKey thì trả record cũ (giống manual)
                if (!string.IsNullOrWhiteSpace(dedupKey))
                {
                    try
                    {
                        var existed = await _context.Notifications.AsNoTracking()
                            .FirstOrDefaultAsync(n => n.DedupKey == dedupKey, cancellationToken);

                        if (existed != null)
                            return existed.Id;
                    }
                    catch { /* ignore */ }
                }

                _logger.LogWarning(ex, "Failed to create system notification. Type={Type}", request.Type);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create system notification. Type={Type}", request.Type);
                return 0;
            }
        }
    }
}
