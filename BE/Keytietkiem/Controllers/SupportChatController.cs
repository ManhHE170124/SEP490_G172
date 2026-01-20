// File: Controllers/SupportChatController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Support;
using Keytietkiem.Hubs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // ✅ NEW
using Microsoft.Extensions.Configuration;
namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/support-chats")]
[Authorize] // ✅ NEW: bắt buộc đăng nhập cho toàn bộ SupportChat APIs
public class SupportChatController : ControllerBase
{
    private readonly KeytietkiemDbContext _db;
    private readonly IHubContext<SupportChatHub> _hub;
    private readonly IAuditLogger _auditLogger;
    private readonly INotificationSystemService _notificationSystemService;
    private readonly IConfiguration _config;
    private readonly IClock _clock;

    private const string StatusWaiting = "Waiting";
    private const string StatusActive = "Active";
    private const string StatusClosed = "Closed";

    public SupportChatController(
        KeytietkiemDbContext db,
        IHubContext<SupportChatHub> hub,
        IAuditLogger auditLogger,
        INotificationSystemService notificationSystemService,
        IConfiguration config,
        IClock clock)
    {
        _db = db;
        _hub = hub;
        _auditLogger = auditLogger;
        _notificationSystemService = notificationSystemService;
        _config = config;
        _clock = clock;
    }

    // ===== Helpers =====

    private Guid? GetCurrentUserIdOrNull()
    {
        var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(str, out var id) ? id : (Guid?)null;
    }

    /// <summary>
    /// Chuẩn hoá PriorityLevel về khoảng [1..3].
    /// </summary>
    private static int NormalizePriority(int? level)
    {
        var p = level.GetValueOrDefault(1);
        if (p < 1) p = 1;
        if (p > 3) p = 3;
        return p;
    }

    private static string BuildSessionGroup(Guid sessionId) => $"support:{sessionId:D}";
    private const string QueueGroup = "support:queue";

    private static string BuildPreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        content = content.Trim();
        return content.Length <= 255 ? content : content.Substring(0, 255);
    }

    private static SupportChatSessionItemDto MapToSessionItem(SupportChatSession s)
    {
        return new SupportChatSessionItemDto
        {
            ChatSessionId = s.ChatSessionId,
            CustomerId = s.CustomerId,
            CustomerName = s.Customer?.FullName ?? s.Customer?.Email ?? string.Empty,
            CustomerEmail = s.Customer?.Email ?? string.Empty,
            AssignedStaffId = s.AssignedStaffId,
            AssignedStaffName = s.AssignedStaff != null
                ? (s.AssignedStaff.FullName ?? s.AssignedStaff.Email ?? string.Empty)
                : null,
            AssignedStaffEmail = s.AssignedStaff?.Email,
            Status = s.Status,
            PriorityLevel = s.PriorityLevel,
            StartedAt = s.StartedAt,
            LastMessageAt = s.LastMessageAt,
            LastMessagePreview = s.LastMessagePreview
        };
    }

    private static bool IsStaffLike(User u)
    {
        var roles = u.Roles ?? Array.Empty<Role>();
        return roles.Any(r =>
        {
            var code = (r.Code ?? string.Empty).Trim().ToLowerInvariant();
            return code.Contains("care") || code.Contains("admin");
        });
    }

    private static bool IsAdminLike(User u)
    {
        var roles = u.Roles ?? Array.Empty<Role>();
        return roles.Any(r =>
        {
            var name = (r.Name ?? string.Empty).Trim().ToLowerInvariant();
            var rid = (r.RoleId ?? string.Empty).Trim().ToLowerInvariant();
            var code = (r.Code ?? string.Empty).Trim().ToLowerInvariant();
            return name == "admin" || rid == "admin" || code.Contains("admin");
        });
    }

    // ===== 1. MY SESSIONS =====
    // GET /api/support-chats/my-sessions
    /// <summary>
    /// Trả về danh sách phiên chat của user hiện tại.
    /// - Nếu là customer => các phiên chat mà user là CustomerId.
    /// - Nếu là Customer Care Staff/Admin => các phiên chat đang được gán cho nhân viên đó.
    /// </summary>
    [HttpGet("my-sessions")]
    public async Task<ActionResult> GetMySessions([FromQuery] bool includeClosed = false)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);

        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        var isStaff = IsStaffLike(user);

        var query = _db.SupportChatSessions
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .AsQueryable();

        if (!includeClosed)
        {
            query = query.Where(s => s.Status != StatusClosed);
        }

        if (isStaff)
        {
            query = query.Where(s => s.AssignedStaffId == me.Value);
        }
        else
        {
            query = query.Where(s => s.CustomerId == me.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.LastMessageAt ?? s.StartedAt)
            .Take(50)
            .ToListAsync();

        var result = sessions.Select(MapToSessionItem).ToList();
        return Ok(result);
    }

    // ===== 2. UNASSIGNED QUEUE =====
    // GET /api/support-chats/unassigned
    /// <summary>
    /// Danh sách các phiên chat đang Waiting + chưa gán nhân viên.
    /// Chỉ cho Customer Care Staff/Admin.
    /// </summary>
    [HttpGet("unassigned")]
    public async Task<ActionResult> GetUnassigned(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);

        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsStaffLike(user))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ nhân viên hỗ trợ mới xem được queue unassigned." });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.SupportChatSessions
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .Where(s => s.Status == StatusWaiting && s.AssignedStaffId == null);

        // Ưu tiên: PriorityLevel cao hơn trước, sau đó LastMessageAt (cũ hơn trước)
        query = query
            .OrderByDescending(s => s.PriorityLevel)
            .ThenBy(s => s.LastMessageAt ?? s.StartedAt);

        var sessions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = sessions.Select(MapToSessionItem).ToList();
        return Ok(result);
    }

    // ===== 3. OPEN OR GET =====
    // POST /api/support-chats/open-or-get
    /// <summary>
    /// Customer mở widget chat:
    /// - Nếu đã có phiên chat chưa đóng => trả về phiên đó.
    /// - Nếu chưa có => tạo mới (Status = Waiting, Priority theo User.SupportPriorityLevel).
    /// Có thể gửi kèm tin nhắn đầu tiên (InitialMessage).
    /// Đồng thời trả thêm flag IsNew / HasPreviousClosedSession để FE hiển thị UI phù hợp.
    /// </summary>
    [HttpPost("open-or-get")]
    public async Task<ActionResult<OpenSupportChatResultDto>> OpenOrGet([FromBody] OpenSupportChatDto? body)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var now = DateTime.UtcNow;

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        // Tìm phiên chat đã Closed gần nhất (nếu có) của user này
        var lastClosedSession = await _db.SupportChatSessions
            .Where(s => s.CustomerId == me.Value && s.Status == StatusClosed)
            .OrderByDescending(s => s.ClosedAt ?? s.StartedAt)
            .FirstOrDefaultAsync();

        // Tìm phiên chat đang mở (Waiting/Active) gần nhất
        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .Where(s => s.CustomerId == me.Value && s.Status != StatusClosed)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        var isNew = false;

        if (session == null)
        {
            session = new SupportChatSession
            {
                ChatSessionId = Guid.NewGuid(),
                CustomerId = me.Value,
                AssignedStaffId = null,
                Status = StatusWaiting,
                PriorityLevel = NormalizePriority(user.SupportPriorityLevel),
                StartedAt = now,
                LastMessageAt = null,
                LastMessagePreview = null
            };

            _db.SupportChatSessions.Add(session);
            isNew = true;
        }

        var initialMessage = body?.InitialMessage?.Trim();
        if (!string.IsNullOrWhiteSpace(initialMessage))
        {
            var msg = new SupportChatMessage
            {
                ChatSessionId = session.ChatSessionId,
                SenderId = me.Value,
                IsFromStaff = false,
                Content = initialMessage,
                SentAt = now
            };

            _db.SupportChatMessages.Add(msg);

            session.LastMessageAt = now;
            session.LastMessagePreview = BuildPreview(initialMessage);
        }

        await _db.SaveChangesAsync();

        // Lấy lại session kèm navigation để map DTO
        session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstAsync(s => s.ChatSessionId == session.ChatSessionId);

        var baseDto = MapToSessionItem(session);

        var result = new OpenSupportChatResultDto
        {
            ChatSessionId = baseDto.ChatSessionId,
            CustomerId = baseDto.CustomerId,
            CustomerName = baseDto.CustomerName,
            CustomerEmail = baseDto.CustomerEmail,
            AssignedStaffId = baseDto.AssignedStaffId,
            AssignedStaffName = baseDto.AssignedStaffName,
            AssignedStaffEmail = baseDto.AssignedStaffEmail,
            Status = baseDto.Status,
            PriorityLevel = baseDto.PriorityLevel,
            StartedAt = baseDto.StartedAt,
            LastMessageAt = baseDto.LastMessageAt,
            LastMessagePreview = baseDto.LastMessagePreview,

            IsNew = isNew,
            HasPreviousClosedSession = isNew && lastClosedSession != null,
            LastClosedSessionId = isNew ? lastClosedSession?.ChatSessionId : null,
            LastClosedAt = isNew ? (lastClosedSession?.ClosedAt ?? lastClosedSession?.StartedAt) : null
        };

        // Nếu là phiên mới => broadcast cho queue staff
        if (isNew)
        {
            await _hub.Clients.Group(QueueGroup)
                .SendAsync("SupportSessionCreated", result);
        }
        else if (!string.IsNullOrWhiteSpace(initialMessage))
        {
            // Nếu chỉ thêm tin nhắn mới từ customer => broadcast cập nhật queue
            await _hub.Clients.Group(QueueGroup)
                .SendAsync("SupportSessionUpdated", result);
        }

        return Ok(result);
    }

    // ===== 4. CLAIM SESSION =====
    // POST /api/support-chats/{sessionId}/claim
    /// <summary>
    /// Nhân viên claim 1 phiên chat chưa gán (Manual-assign).
    /// Chỉ customer care staff/admin.
    /// </summary>
    [HttpPost("{sessionId:guid}/claim")]
    public async Task<ActionResult<SupportChatSessionItemDto>> Claim(Guid sessionId)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);

        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsStaffLike(user))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ nhân viên hỗ trợ mới được claim phiên chat." });
        }

        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstOrDefaultAsync(s => s.ChatSessionId == sessionId);

        if (session is null) return NotFound();

        if (session.Status == StatusClosed)
            return BadRequest(new { message = "Phiên chat đã đóng." });

        // Đã gán cho người khác
        if (session.AssignedStaffId.HasValue && session.AssignedStaffId != me.Value)
        {
            return Conflict(new { message = "Phiên chat đã được gán cho nhân viên khác." });
        }

        // Idempotent: nếu đã gán cho chính nhân viên này thì trả về luôn
        if (session.AssignedStaffId == me.Value)
        {
            var dtoExisting = MapToSessionItem(session);

            await _auditLogger.LogAsync(
                HttpContext,
                action: "ClaimSupportChatSession",
                entityType: "SupportChatSession",
                entityId: session.ChatSessionId.ToString(),
                before: new
                {
                    ChatSessionId = session.ChatSessionId
                },
                after: new
                {
                    ChatSessionId = session.ChatSessionId,
                    AssignedStaffId = session.AssignedStaffId,
                    Status = session.Status
                }
            );

            return Ok(dtoExisting);
        }

        session.AssignedStaffId = me.Value;
        session.Status = StatusActive;
        session.AssignedStaff = user;

        if (!session.LastMessageAt.HasValue)
        {
            session.LastMessageAt = session.StartedAt;
        }

        await _db.SaveChangesAsync();

        var dto = MapToSessionItem(session);

        // Broadcast tới queue + group của phiên chat
        await _hub.Clients.Group(QueueGroup)
            .SendAsync("SupportSessionUpdated", dto);

        await _hub.Clients.Group(BuildSessionGroup(session.ChatSessionId))
            .SendAsync("SupportSessionUpdated", dto);

        await _auditLogger.LogAsync(
            HttpContext,

            action: "ClaimSupportChatSession",
            entityType: "SupportChatSession",
            entityId: session.ChatSessionId.ToString(),
            before: null,
            after: new
            {
                ChatSessionId = session.ChatSessionId,
                AssignedStaffId = session.AssignedStaffId,
                Status = session.Status
            }
        );

        return Ok(dto);
    }

    // ===== 5. POST MESSAGE =====
    // POST /api/support-chats/{sessionId}/messages
    /// <summary>
    /// Gửi tin nhắn trong 1 phiên chat hỗ trợ.
    /// - Customer: phải là chủ phiên (CustomerId).
    /// - Staff/Admin: phải là nhân viên được gán vào phiên đó.
    /// </summary>
    [HttpPost("{sessionId:guid}/messages")]
    public async Task<ActionResult<SupportChatMessageDto>> PostMessage(
        Guid sessionId,
        [FromBody] CreateSupportChatMessageDto body)
    {
        var content = (body?.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { message = "Nội dung tin nhắn trống." });

        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstOrDefaultAsync(s => s.ChatSessionId == sessionId);

        if (session is null) return NotFound();

        if (session.Status == StatusClosed)
            return BadRequest(new { message = "Phiên chat đã đóng." });

        var isCustomer = session.CustomerId == me.Value;
        var isStaff = session.AssignedStaffId == me.Value && IsStaffLike(user);

        if (!isCustomer && !isStaff)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Người dùng không có quyền gửi tin trong phiên chat này." });
        }

        var now = DateTime.UtcNow;

        var msg = new SupportChatMessage
        {
            ChatSessionId = session.ChatSessionId,
            SenderId = me.Value,
            IsFromStaff = !isCustomer,
            Content = content,
            SentAt = now
        };

        _db.SupportChatMessages.Add(msg);

        session.LastMessageAt = now;
        session.LastMessagePreview = BuildPreview(content);

        // Nếu nhân viên gửi tin đầu tiên vào phiên Waiting => chuyển thành Active
        if (!isCustomer && session.Status == StatusWaiting)
        {
            session.Status = StatusActive;
        }

        await _db.SaveChangesAsync();

        var dto = new SupportChatMessageDto
        {
            MessageId = msg.MessageId,
            ChatSessionId = msg.ChatSessionId,
            SenderId = msg.SenderId,
            SenderName = user.FullName ?? user.Email ?? string.Empty,
            IsFromStaff = msg.IsFromStaff,
            Content = msg.Content,
            SentAt = msg.SentAt
        };

        // Broadcast tới group của phiên chat (FE customer + staff cùng nhận)
        await _hub.Clients.Group(BuildSessionGroup(session.ChatSessionId))
            .SendAsync("ReceiveSupportMessage", dto);

        // Cập nhật queue cho staff
        var sessionDto = MapToSessionItem(session);
        await _hub.Clients.Group(QueueGroup)
            .SendAsync("SupportSessionUpdated", sessionDto);

        // Không audit log cho PostMessage để tránh spam log

        return Ok(dto);
    }

    // ===== 6. GET MESSAGES =====
    // GET /api/support-chats/{sessionId}/messages
    /// <summary>
    /// Lấy toàn bộ lịch sử tin nhắn của 1 phiên chat.
    /// Customer: chỉ xem được các phiên của mình.
    /// Staff: xem được phiên mình phụ trách + phiên queue + các phiên trước của cùng customer.
    /// Admin: xem được mọi phiên.
    /// </summary>
    [HttpGet("{sessionId:guid}/messages")]
    public async Task<ActionResult> GetMessages(Guid sessionId)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstOrDefaultAsync(s => s.ChatSessionId == sessionId);

        if (session is null) return NotFound();

        var isCustomer = session.CustomerId == me.Value;
        var isAssignedStaff = session.AssignedStaffId == me.Value && IsStaffLike(user);
        var isStaffLikeUser = IsStaffLike(user);
        var isAdmin = IsAdminLike(user);

        // 1) Staff xem tin nhắn của phiên đang ở queue (Waiting + chưa assign)
        var canViewQueueSession = isStaffLikeUser
                                  && session.Status == StatusWaiting
                                  && session.AssignedStaffId == null;

        // 2) Staff đang xử lý khách này được xem các phiên khác (previous sessions)
        bool canViewPreviousSessions = false;
        if (isStaffLikeUser && !isCustomer && !isAssignedStaff && !isAdmin)
        {
            // Có ít nhất 1 phiên Active của cùng customer đang gán cho staff hiện tại
            // hoặc customer đang ở queue (Waiting + unassigned) để staff xem lịch sử trước khi claim.
            canViewPreviousSessions = await _db.SupportChatSessions.AnyAsync(s =>
                s.CustomerId == session.CustomerId &&
                (
                    (s.AssignedStaffId == me.Value && s.Status == StatusActive) ||
                    (s.Status == StatusWaiting && s.AssignedStaffId == null)
                ) &&
                s.ChatSessionId != session.ChatSessionId);
        }

        // Admin luôn pass qua isAdmin
        if (!(isCustomer || isAssignedStaff || isAdmin || canViewQueueSession || canViewPreviousSessions))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Người dùng không có quyền truy cập phiên chat này." });
        }

        var messages = await _db.SupportChatMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var result = messages.Select(m =>
        {
            var sender = m.Sender;
            return new SupportChatMessageDto
            {
                MessageId = m.MessageId,
                ChatSessionId = m.ChatSessionId,
                SenderId = m.SenderId,
                SenderName = sender.FullName ?? sender.Email ?? string.Empty,
                IsFromStaff = m.IsFromStaff,
                Content = m.Content,
                SentAt = m.SentAt
            };
        }).ToList();

        return Ok(result);
    }

    // ===== 7. UNASSIGN SESSION =====
    // POST /api/support-chats/{sessionId}/unassign
    /// <summary>
    /// Nhân viên trả lại phiên chat đang phụ trách về hàng chờ (Waiting + không có AssignedStaffId).
    /// Nhân viên phụ trách hoặc Admin được unassign.
    /// </summary>
    [HttpPost("{sessionId:guid}/unassign")]
    public async Task<ActionResult<SupportChatSessionItemDto>> Unassign(Guid sessionId)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsStaffLike(user))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ nhân viên hỗ trợ mới được trả lại phiên chat." });
        }

        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstOrDefaultAsync(s => s.ChatSessionId == sessionId);

        if (session is null) return NotFound();

        if (session.Status == StatusClosed)
        {
            return BadRequest(new { message = "Phiên chat đã đóng, không thể trả lại hàng chờ." });
        }

        var isAdmin = IsAdminLike(user);

        if (session.AssignedStaffId != me.Value && !isAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bạn không phải nhân viên đang phụ trách phiên chat này." });
        }

        // Đưa về queue: bỏ gán nhân viên, set Status = Waiting
        session.AssignedStaffId = null;
        session.AssignedStaff = null;
        session.Status = StatusWaiting;

        await _db.SaveChangesAsync();

        var dto = MapToSessionItem(session);

        // Broadcast tới group của phiên chat (customer + staff) để header/status được cập nhật
        await _hub.Clients.Group(BuildSessionGroup(session.ChatSessionId))
            .SendAsync("SupportSessionUpdated", dto);

        // Broadcast cho queue staff để tất cả màn staff thấy session quay lại hàng chờ
        await _hub.Clients.Group(QueueGroup)
            .SendAsync("SupportSessionUpdated", dto);

        await _auditLogger.LogAsync(
            HttpContext,
            action: "UnassignSupportChatSession",
            entityType: "SupportChatSession",
            entityId: session.ChatSessionId.ToString(),
            before: null,
            after: new
            {
                ChatSessionId = session.ChatSessionId,
                AssignedStaffId = session.AssignedStaffId,
                Status = session.Status
            }
        );

        return Ok(dto);
    }

    // ===== 8. CLOSE SESSION =====
    // POST /api/support-chats/{sessionId}/close
    /// <summary>
    /// Đóng 1 phiên chat.
    /// Customer: chỉ được đóng phiên chat của mình.
    /// Staff: chỉ được đóng phiên chat được gán cho mình.
    /// Admin: được đóng bất kỳ phiên chat nào.
    /// </summary>
    [HttpPost("{sessionId:guid}/close")]
    public async Task<IActionResult> Close(Guid sessionId)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstOrDefaultAsync(s => s.ChatSessionId == sessionId);

        if (session is null) return NotFound();

        var isCustomer = session.CustomerId == me.Value;
        var isAssignedStaff = session.AssignedStaffId == me.Value && IsStaffLike(user);
        var isAdmin = IsAdminLike(user);

        if (!isCustomer && !isAssignedStaff && !isAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Người dùng không có quyền đóng phiên chat này." });
        }

        if (session.Status == StatusClosed)
        {
            return NoContent();
        }

        session.Status = StatusClosed;
        session.ClosedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var dto = MapToSessionItem(session);

        await _hub.Clients.Group(BuildSessionGroup(session.ChatSessionId))
            .SendAsync("SupportSessionClosed", dto);

        await _hub.Clients.Group(QueueGroup)
            .SendAsync("SupportSessionUpdated", dto);

        await _auditLogger.LogAsync(
            HttpContext,
            action: "CloseSupportChatSession",
            entityType: "SupportChatSession",
            entityId: session.ChatSessionId.ToString(),
            before: null,
            after: new
            {
                ChatSessionId = session.ChatSessionId,
                Status = session.Status,
                ClosedAt = session.ClosedAt
            }
        );

        return NoContent();
    }

    // ===== 9. ADMIN: CURRENT ASSIGNED SESSIONS (ĐÃ NHẬN) =====
    // GET /api/support-chats/admin/assigned-sessions
    /// <summary>
    /// Danh sách tất cả các phiên chat đã được bất kỳ nhân viên nào nhận.
    /// Chỉ dành cho Admin.
    /// </summary>
    [HttpGet("admin/assigned-sessions")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<ActionResult<List<SupportChatSessionItemDto>>> AdminGetAssignedSessions(
        [FromQuery] bool includeClosed = false)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsAdminLike(user))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ admin mới xem được danh sách phiên đã nhận của toàn hệ thống." });
        }

        var query = _db.SupportChatSessions
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .Where(s => s.AssignedStaffId != null);

        if (!includeClosed)
        {
            query = query.Where(s => s.Status != StatusClosed);
        }

        var sessions = await query
            .OrderByDescending(s => s.LastMessageAt ?? s.StartedAt)
            .Take(200)
            .ToListAsync();

        var result = sessions.Select(MapToSessionItem).ToList();
        return Ok(result);
    }

    // GET /api/support-chats/customer/{customerId}/sessions
    [HttpGet("customer/{customerId:guid}/sessions")]
    public async Task<ActionResult<List<SupportChatSessionItemDto>>> GetCustomerSessionsForStaff(
        Guid customerId,
        [FromQuery] bool includeClosed = true,
        [FromQuery] Guid? excludeSessionId = null)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsStaffLike(user))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ nhân viên hỗ trợ mới xem được các phiên chat của khách hàng." });
        }

        // ✅ FIX: chặn nhét customerId để xem lịch sử của khách khác (IDOR)
        // - Admin: xem được tất cả
        // - Staff: chỉ xem khi có "anchor" hợp lệ (đang xem 1 session của customer này)
        //   hoặc đang có ít nhất 1 phiên Active của customer này được gán cho mình.
        var isAdmin = IsAdminLike(user);

        if (!isAdmin)
        {
            var allow = false;

            if (excludeSessionId.HasValue)
            {
                var anchor = await _db.SupportChatSessions.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ChatSessionId == excludeSessionId.Value);

                if (anchor != null && anchor.CustomerId == customerId)
                {
                    // staff đang phụ trách anchor
                    if (anchor.AssignedStaffId.HasValue && anchor.AssignedStaffId.Value == me.Value)
                        allow = true;

                    // hoặc anchor đang ở hàng chờ (queue)
                    if (anchor.Status == StatusWaiting && anchor.AssignedStaffId == null)
                        allow = true;
                }
            }

            if (!allow)
            {
                allow = await _db.SupportChatSessions.AsNoTracking().AnyAsync(s =>
                    s.CustomerId == customerId &&
                    s.AssignedStaffId == me.Value &&
                    s.Status == StatusActive);
            }

            if (!allow)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Bạn không có quyền truy cập chức năng này." });
            }
        }

        var query = _db.SupportChatSessions
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .Where(s => s.CustomerId == customerId);

        if (!includeClosed)
        {
            query = query.Where(s => s.Status != StatusClosed);
        }

        if (excludeSessionId.HasValue)
        {
            query = query.Where(s => s.ChatSessionId != excludeSessionId.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.LastMessageAt ?? s.StartedAt)
            .Take(50)
            .ToListAsync();

        var result = sessions.Select(MapToSessionItem).ToList();
        return Ok(result);
    }

    // GET /api/support-chats/admin/sessions
    [HttpGet("admin/sessions")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<ActionResult<PagedResult<SupportChatAdminSessionListItemDto>>> AdminSearchSessions(
        [FromQuery] SupportChatAdminSessionFilterDto filter)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        // Chỉ admin (code role chứa "admin")
        if (!(user.Roles ?? Array.Empty<Role>())
                .Any(r => (r.Code ?? string.Empty).ToLower().Contains("admin")))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ admin mới truy cập được lịch sử chat tổng." });
        }

        var query = _db.SupportChatSessions
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .AsQueryable();

        if (filter.From.HasValue)
        {
            query = query.Where(s => s.StartedAt >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(s => s.StartedAt <= filter.To.Value);
        }

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(s => s.CustomerId == filter.CustomerId.Value);
        }

        if (filter.StaffId.HasValue)
        {
            query = query.Where(s => s.AssignedStaffId == filter.StaffId.Value);
        }

        if (filter.PriorityLevel.HasValue)
        {
            query = query.Where(s => s.PriorityLevel == filter.PriorityLevel.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(s => s.Status == filter.Status);
        }

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var totalCount = await query.CountAsync();

        var sessions = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Lấy message count theo group
        var sessionIds = sessions.Select(s => s.ChatSessionId).ToList();

        var messageCounts = await _db.SupportChatMessages
            .Where(m => sessionIds.Contains(m.ChatSessionId))
            .GroupBy(m => m.ChatSessionId)
            .Select(g => new { ChatSessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ChatSessionId, x => x.Count);

        var items = sessions.Select(s =>
        {
            var baseDto = MapToSessionItem(s);
            return new SupportChatAdminSessionListItemDto
            {
                ChatSessionId = baseDto.ChatSessionId,
                CustomerId = baseDto.CustomerId,
                CustomerName = baseDto.CustomerName,
                CustomerEmail = baseDto.CustomerEmail,
                AssignedStaffId = baseDto.AssignedStaffId,
                AssignedStaffName = baseDto.AssignedStaffName,
                AssignedStaffEmail = baseDto.AssignedStaffEmail,
                Status = baseDto.Status,
                PriorityLevel = baseDto.PriorityLevel,
                StartedAt = baseDto.StartedAt,
                LastMessageAt = baseDto.LastMessageAt,
                LastMessagePreview = baseDto.LastMessagePreview,
                MessageCount = messageCounts.TryGetValue(s.ChatSessionId, out var c) ? c : 0
            };
        }).ToList();

        var result = new PagedResult<SupportChatAdminSessionListItemDto>(
            items,
            page,
            pageSize,
            totalCount
        );

        return Ok(result);
    }

    // ===== 5b. ADMIN SEND MESSAGE WITHOUT CLAIM / STATUS CHANGE =====
    // POST /api/support-chats/admin/{sessionId}/messages
    /// <summary>
    /// Admin gửi tin nhắn vào bất kỳ phiên chat nào (Waiting hoặc Active)
    /// mà không thay đổi AssignedStaffId, Status, v.v.
    /// </summary>
    [HttpPost("admin/{sessionId:guid}/messages")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<ActionResult<SupportChatMessageDto>> AdminPostMessage(
        Guid sessionId,
        [FromBody] CreateSupportChatMessageDto body)
    {
        var content = (body?.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { message = "Nội dung tin nhắn trống." });

        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (user is null) return Unauthorized();

        if ((user.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsAdminLike(user))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ admin mới được gửi tin theo chế độ admin." });
        }

        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstOrDefaultAsync(s => s.ChatSessionId == sessionId);

        if (session is null) return NotFound();

        if (session.Status == StatusClosed)
            return BadRequest(new { message = "Phiên chat đã đóng." });

        var now = DateTime.UtcNow;

        var msg = new SupportChatMessage
        {
            ChatSessionId = session.ChatSessionId,
            SenderId = me.Value,
            IsFromStaff = true, // Admin luôn là phía staff
            Content = content,
            SentAt = now
        };

        _db.SupportChatMessages.Add(msg);

        session.LastMessageAt = now;
        session.LastMessagePreview = BuildPreview(content);
        // Không đổi AssignedStaffId, Status – giữ nguyên đúng yêu cầu

        await _db.SaveChangesAsync();

        var dto = new SupportChatMessageDto
        {
            MessageId = msg.MessageId,
            ChatSessionId = msg.ChatSessionId,
            SenderId = msg.SenderId,
            SenderName = user.FullName ?? user.Email ?? string.Empty,
            IsFromStaff = msg.IsFromStaff,
            Content = msg.Content,
            SentAt = msg.SentAt
        };

        // Broadcast tới group của phiên để cả customer + staff nhận được
        await _hub.Clients.Group(BuildSessionGroup(session.ChatSessionId))
            .SendAsync("ReceiveSupportMessage", dto);

        // Đồng bộ queue / danh sách phiên cho staff/admin
        var sessionDto = MapToSessionItem(session);
        await _hub.Clients.Group(QueueGroup)
            .SendAsync("SupportSessionUpdated", sessionDto);

        // Không audit log cho AdminPostMessage để tránh spam log

        return Ok(dto);
    }

    // ===== 10. ADMIN ASSIGN / TRANSFER STAFF =====

    public class SupportChatAssignStaffDto
    {
        public Guid AssigneeId { get; set; }
    }

    /// <summary>
    /// Admin gán nhân viên cho phiên chat (thường dùng cho cột "Chờ nhận" trên màn admin).
    /// Yêu cầu:
    /// - Phiên chưa đóng.
    /// - Phiên hiện chưa có AssignedStaffId.
    /// - AssigneeId là nhân viên CSKH Active (Role.Code chứa "care").
    /// </summary>
    /// <remarks>
    /// POST /api/support-chats/admin/{sessionId}/assign
    /// Body: { "assigneeId": "..." }
    /// </remarks>
    [HttpPost("admin/{sessionId:guid}/assign")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<ActionResult<SupportChatSessionItemDto>> AdminAssignStaff(
        Guid sessionId,
        [FromBody] SupportChatAssignStaffDto dtoBody)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var currentUser = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (currentUser is null) return Unauthorized();

        if ((currentUser.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsAdminLike(currentUser))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ admin mới được gán nhân viên cho phiên chat." });
        }

        if (dtoBody == null || dtoBody.AssigneeId == Guid.Empty)
        {
            return BadRequest(new { message = "Vui lòng chọn nhân viên cần gán." });
        }

        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstOrDefaultAsync(s => s.ChatSessionId == sessionId);

        if (session is null) return NotFound();

        if (session.Status == StatusClosed)
        {
            return BadRequest(new { message = "Phiên chat đã đóng, không thể gán nhân viên." });
        }

        if (session.AssignedStaffId.HasValue)
        {
            return BadRequest(new { message = "Phiên chat đã có nhân viên, hãy dùng chức năng chuyển nhân viên." });
        }

        // Validate staff: Active + Role.Code chứa "care"
        var staff = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u =>
                u.UserId == dtoBody.AssigneeId &&
                ((u.Status ?? "Active") == "Active") &&
                u.Roles.Any(r => (r.Code ?? string.Empty).ToLower().Contains("care")));

        if (staff is null)
        {
            return BadRequest(new { message = "Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active)." });
        }



        session.AssignedStaffId = staff.UserId;
        session.AssignedStaff = staff;
        // Giữ nguyên Status (thường vẫn là Waiting),
        // khi nhân viên trả lời tin đầu tiên thì PostMessage sẽ set sang Active.

        await _db.SaveChangesAsync();

        var dto = MapToSessionItem(session);

        // Broadcast cập nhật cho cả group phiên + queue
        await _hub.Clients.Group(BuildSessionGroup(session.ChatSessionId))
            .SendAsync("SupportSessionUpdated", dto);

        await _hub.Clients.Group(QueueGroup)
            .SendAsync("SupportSessionUpdated", dto);

        await _auditLogger.LogAsync(
            HttpContext,
            action: "AdminAssignStaff",
            entityType: "SupportChatSession",
            entityId: session.ChatSessionId.ToString(),
            before: null,
            after: new
            {
                ChatSessionId = session.ChatSessionId,
                AssignedStaffId = session.AssignedStaffId,
                Status = session.Status
            }
        );

        // ✅ System notification: notify assignee that admin assigned the chat session (best-effort)
        try
        {
            var actorName = currentUser.FullName ?? "(unknown)";
            var actorEmail = currentUser.Email ?? "(unknown)";
            var customerName = session.Customer?.FullName ?? session.Customer?.Email ?? "Khách hàng";
            var customerEmail = session.Customer?.Email ?? string.Empty;

            var origin = PublicUrlHelper.GetPublicOrigin(HttpContext, _config);
            var relatedUrl = $"{origin}/staff/support-chats?sessionId={session.ChatSessionId:D}";

            await _notificationSystemService.CreateForUserIdsAsync(new SystemNotificationCreateRequest
            {
                Title = "Bạn được gán phiên chat hỗ trợ",
                Message = $"Admin {actorName} đã gán bạn phụ trách phiên chat của {customerName} ({customerEmail}).",
                Severity = 0, // Info
                CreatedByUserId = currentUser.UserId,
                CreatedByEmail = actorEmail,
                Type = "SupportChat.AdminAssigned",
                RelatedEntityType = "SupportChatSession",
                RelatedEntityId = session.ChatSessionId.ToString(),
                RelatedUrl = relatedUrl,
                TargetUserIds = new List<Guid> { staff.UserId }
            });
        }
        catch { }


        return Ok(dto);
    }

    /// <summary>
    /// Admin chuyển phiên chat sang nhân viên khác.
    /// Yêu cầu:
    /// - Phiên chưa đóng.
    /// - Phiên đang có AssignedStaffId.
    /// - AssigneeId mới khác với AssignedStaffId hiện tại.
    /// - Nhân viên đích là CSKH Active (Role.Code chứa "care").
    /// </summary>
    /// <remarks>
    /// POST /api/support-chats/admin/{sessionId}/transfer-staff
    /// Body: { "assigneeId": "..." }
    /// </remarks>
    [HttpPost("admin/{sessionId:guid}/transfer-staff")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<ActionResult<SupportChatSessionItemDto>> AdminTransferStaff(
        Guid sessionId,
        [FromBody] SupportChatAssignStaffDto dtoBody)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var currentUser = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (currentUser is null) return Unauthorized();

        if ((currentUser.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsAdminLike(currentUser))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ admin mới được chuyển nhân viên phụ trách phiên chat." });
        }

        if (dtoBody == null || dtoBody.AssigneeId == Guid.Empty)
        {
            return BadRequest(new { message = "Vui lòng chọn nhân viên cần chuyển tới." });
        }

        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstOrDefaultAsync(s => s.ChatSessionId == sessionId);

        if (session is null) return NotFound();

        if (session.Status == StatusClosed)
        {
            return BadRequest(new { message = "Phiên chat đã đóng, không thể chuyển nhân viên." });
        }

        if (!session.AssignedStaffId.HasValue)
        {
            return BadRequest(new { message = "Phiên chat chưa có nhân viên, hãy dùng chức năng gán nhân viên." });
        }

        if (session.AssignedStaffId == dtoBody.AssigneeId)
        {
            return BadRequest(new { message = "Vui lòng chọn nhân viên khác với người đang phụ trách." });
        }

        // Validate staff: Active + Role.Code chứa "care"
        var staff = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u =>
                u.UserId == dtoBody.AssigneeId &&
                ((u.Status ?? "Active") == "Active") &&
                u.Roles.Any(r => (r.Code ?? string.Empty).ToLower().Contains("care")));

        if (staff is null)
        {
            return BadRequest(new { message = "Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active)." });
        }

        // Capture previous assignee (for notification)
        var oldStaffId = session.AssignedStaffId;

        session.AssignedStaffId = staff.UserId;
        session.AssignedStaff = staff;
        // Không đổi Status (vẫn Waiting / Active tuỳ trạng thái hiện tại)

        await _db.SaveChangesAsync();

        var dto = MapToSessionItem(session);

        // Broadcast cập nhật cho cả group phiên + queue
        await _hub.Clients.Group(BuildSessionGroup(session.ChatSessionId))
            .SendAsync("SupportSessionUpdated", dto);

        await _hub.Clients.Group(QueueGroup)
            .SendAsync("SupportSessionUpdated", dto);

        await _auditLogger.LogAsync(
            HttpContext,
            action: "AdminTransferStaff",
            entityType: "SupportChatSession",
            entityId: session.ChatSessionId.ToString(),
            before: null,
            after: new
            {
                ChatSessionId = session.ChatSessionId,
                AssignedStaffId = session.AssignedStaffId,
                Status = session.Status
            }
        );

        // ✅ System notification: notify new assignee + previous assignee about transfer (best-effort)
        try
        {
            var actorName = currentUser.FullName ?? "(unknown)";
            var actorEmail = currentUser.Email ?? "(unknown)";
            var customerName = session.Customer?.FullName ?? session.Customer?.Email ?? "Khách hàng";
            var customerEmail = session.Customer?.Email ?? string.Empty;

            var origin = PublicUrlHelper.GetPublicOrigin(HttpContext, _config);
            var relatedUrl = $"{origin}/staff/support-chats?sessionId={session.ChatSessionId:D}";

            // Notify new staff
            await _notificationSystemService.CreateForUserIdsAsync(new SystemNotificationCreateRequest
            {
                Title = "Bạn được chuyển phiên chat hỗ trợ",
                Message = $"Admin {actorName} đã chuyển phiên chat của {customerName} ({customerEmail}) cho bạn.",
                Severity = 0, // Info
                CreatedByUserId = currentUser.UserId,
                CreatedByEmail = actorEmail,
                Type = "SupportChat.AdminTransferredToYou",
                RelatedEntityType = "SupportChatSession",
                RelatedEntityId = session.ChatSessionId.ToString(),
                RelatedUrl = relatedUrl,
                TargetUserIds = new List<Guid> { staff.UserId }
            });

            // Notify previous staff (if any)
            if (oldStaffId.HasValue)
            {
                var newStaffName = staff.FullName ?? staff.Email ?? string.Empty;
                await _notificationSystemService.CreateForUserIdsAsync(new SystemNotificationCreateRequest
                {
                    Title = "Phiên chat của bạn đã được chuyển",
                    Message = $"Admin {actorName} đã chuyển phiên chat của {customerName} ({customerEmail}) sang cho {newStaffName}.",
                    Severity = 1, // Warning
                    CreatedByUserId = currentUser.UserId,
                    CreatedByEmail = actorEmail,
                    Type = "SupportChat.AdminTransferredAway",
                    RelatedEntityType = "SupportChatSession",
                    RelatedEntityId = session.ChatSessionId.ToString(),
                    RelatedUrl = relatedUrl,
                    TargetUserIds = new List<Guid> { oldStaffId.Value }
                });
            }
        }
        catch { }


        return Ok(dto);
    }
}
