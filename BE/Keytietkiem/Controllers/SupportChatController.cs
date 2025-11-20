// File: Controllers/SupportChatController.cs
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Keytietkiem.DTOs.SupportChat;
using Keytietkiem.Hubs;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/support-chats")]
public class SupportChatController : ControllerBase
{
    private readonly KeytietkiemDbContext _db;
    private readonly IHubContext<SupportChatHub> _hub;

    private const string StatusWaiting = "Waiting";
    private const string StatusActive = "Active";
    private const string StatusClosed = "Closed";

    // Tên role giống TicketsController (Customer Care Staff)
    private const string StaffRoleName = "customer care staff";
    private const string AdminRoleName = "admin";

    public SupportChatController(KeytietkiemDbContext db, IHubContext<SupportChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // ===== Helpers =====

    private Guid? GetCurrentUserIdOrNull()
    {
        var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(str, out var id) ? id : null;
    }

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
            var name = (r.Name ?? string.Empty).Trim().ToLowerInvariant();
            return name == StaffRoleName || name == AdminRoleName;
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
        if (!IsStaffLike(user))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ nhân viên hỗ trợ mới xem được queue unassigned." });

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
    /// </summary>
    [HttpPost("open-or-get")]
    public async Task<ActionResult<SupportChatSessionItemDto>> OpenOrGet([FromBody] OpenSupportChatDto? body)
    {
        var me = GetCurrentUserIdOrNull();
        if (me is null) return Unauthorized();

        var now = DateTime.UtcNow;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (user is null) return Unauthorized();

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

        var dto = MapToSessionItem(session);

        // Nếu là phiên mới => broadcast cho queue staff
        if (isNew)
        {
            await _hub.Clients.Group(QueueGroup)
                .SendAsync("SupportSessionCreated", dto);
        }
        else if (!string.IsNullOrWhiteSpace(initialMessage))
        {
            // Nếu chỉ thêm tin nhắn mới từ customer => broadcast cập nhật queue
            await _hub.Clients.Group(QueueGroup)
                .SendAsync("SupportSessionUpdated", dto);
        }

        return Ok(dto);
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
        if (!IsStaffLike(user))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ nhân viên hỗ trợ mới được claim phiên chat." });

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
            return Ok(dtoExisting);
        }

        // Thực hiện claim
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

        // Broadcast tới group của phiên chat
        await _hub.Clients.Group(BuildSessionGroup(session.ChatSessionId))
            .SendAsync("ReceiveSupportMessage", dto);

        // Cập nhật queue cho staff
        var sessionDto = MapToSessionItem(session);
        await _hub.Clients.Group(QueueGroup)
            .SendAsync("SupportSessionUpdated", sessionDto);

        return Ok(dto);
    }

    // ===== 6. GET MESSAGES =====
    // GET /api/support-chats/{sessionId}/messages
    /// <summary>
    /// Lấy toàn bộ lịch sử tin nhắn của 1 phiên chat.
    /// Customer: chỉ xem được các phiên của mình.
    /// Staff/Admin: chỉ xem được các phiên được gán cho mình.
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

        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstOrDefaultAsync(s => s.ChatSessionId == sessionId);

        if (session is null) return NotFound();

        var isCustomer = session.CustomerId == me.Value;
        var isStaff = session.AssignedStaffId == me.Value && IsStaffLike(user);

        if (!isCustomer && !isStaff)
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

    // ===== 7. CLOSE SESSION =====
    // POST /api/support-chats/{sessionId}/close
    /// <summary>
    /// Đóng phiên chat.
    /// - Customer: chỉ được đóng phiên của mình.
    /// - Staff/Admin: chỉ đóng được phiên được gán cho mình.
    /// Idempotent: nếu đã đóng thì trả về 204.
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

        var session = await _db.SupportChatSessions
            .Include(s => s.Customer)
            .Include(s => s.AssignedStaff)
            .FirstOrDefaultAsync(s => s.ChatSessionId == sessionId);

        if (session is null) return NotFound();

        var isCustomer = session.CustomerId == me.Value;
        var isStaff = session.AssignedStaffId == me.Value && IsStaffLike(user);

        if (!isCustomer && !isStaff)
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

        return NoContent();
    }
}
