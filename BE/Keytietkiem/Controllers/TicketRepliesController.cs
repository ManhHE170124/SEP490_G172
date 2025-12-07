// File: Controllers/TicketRepliesController.cs
using Keytietkiem.DTOs.Tickets;
using Keytietkiem.Hubs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers;

[ApiController]
// Cố định prefix là "api/Tickets" để giữ nguyên route cũ:
// POST /api/Tickets/{id}/replies
[Route("api/Tickets")]
public class TicketRepliesController : ControllerBase
{
    private readonly KeytietkiemDbContext _db;
    private readonly IHubContext<TicketHub> _ticketHub;
    private readonly IAuditLogger _auditLogger;

    public TicketRepliesController(
        KeytietkiemDbContext db,
        IHubContext<TicketHub> ticketHub,
        IAuditLogger auditLogger)
    {
        _db = db;
        _ticketHub = ticketHub;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Tạo phản hồi (TicketReply) cho 1 ticket:
    /// - Chủ ticket
    /// - Nhân viên được gán
    /// - Admin
    /// </summary>
    /// <param name="id">TicketId</param>
    [HttpPost("{id:guid}/replies")]
    public async Task<ActionResult<TicketReplyDto>> CreateReply(Guid id, [FromBody] CreateTicketReplyDto dto)
    {
        var msg = (dto?.Message ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(msg))
            return BadRequest(new { message = "Nội dung phản hồi trống." });

        var t = await _db.Tickets
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TicketId == id);
        if (t is null)
            return NotFound();

        // Lấy id người đang đăng nhập từ Claim
        var meStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meStr, out var me))
            return Unauthorized();

        // Lấy đầy đủ thông tin + roles của người gửi
        var sender = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me);

        if (sender is null)
            return Unauthorized();

        // 🔒 Kiểm tra quyền gửi phản hồi:
        //  - Chủ ticket (UserId)
        //  - Nhân viên được gán (AssigneeId)
        //  - Admin (role Name / RoleId = "Admin")
        var isTicketOwner = t.UserId == sender.UserId;
        var isAssignee = t.AssigneeId.HasValue && t.AssigneeId.Value == sender.UserId;

        var isAdmin = sender.Roles.Any(r =>
        {
            var name = (r.Name ?? string.Empty).Trim().ToLowerInvariant();
            var rid = (r.RoleId ?? string.Empty).Trim().ToLowerInvariant();
            return name == "admin" || rid == "admin";
        });

        if (!isTicketOwner && !isAssignee && !isAdmin)
        {
            // Trả về 403 + message để FE hiển thị ở chỗ "Bạn cần đăng nhập...".
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Người dùng không có quyền hạn để phản hồi." });
        }

        var now = DateTime.UtcNow;

        // Staff = assignee hoặc admin (không phải chủ ticket)
        var isStaffReply = !isTicketOwner;

        var reply = new TicketReply
        {
            TicketId = t.TicketId,
            SenderId = sender.UserId,
            Message = msg,
            SentAt = now,
            IsStaffReply = isStaffReply
        };

        _db.TicketReplies.Add(reply);

        // SLA: nếu đây là phản hồi đầu tiên từ phía staff => set FirstRespondedAt
        if (isStaffReply && !t.FirstRespondedAt.HasValue)
        {
            t.FirstRespondedAt = now;
        }

        // Nếu ticket đang là New/Open và staff trả lời => chuyển sang InProgress
        var st = (t.Status ?? "New").Trim();
        if (isStaffReply &&
            (string.Equals(st, "New", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(st, "Open", StringComparison.OrdinalIgnoreCase)))
        {
            t.Status = "InProgress";
        }

        t.UpdatedAt = now;

        // Cập nhật lại SLA status
        TicketSlaHelper.UpdateSlaStatus(t, now);

        await _db.SaveChangesAsync();

        // Map sang DTO để trả về + bắn SignalR
        var dtoOut = new TicketReplyDto
        {
            ReplyId = reply.ReplyId,
            SenderId = reply.SenderId,
            SenderName = sender.FullName ?? sender.Email ?? string.Empty,
            IsStaffReply = reply.IsStaffReply,
            Message = reply.Message,
            SentAt = reply.SentAt
        };

        // 🔔 Broadcast realtime đến tất cả client đang xem ticket này (nhóm "ticket:{id}")
        await _ticketHub.Clients.Group($"ticket:{id}")
            .SendAsync("ReceiveReply", dtoOut);

        // 🔐 AUDIT LOG – CHỈ log khi staff/admin reply (không log khách để tránh spam)
        if (isStaffReply)
        {
            // Không log full message để tránh log nhạy cảm quá chi tiết, chỉ log preview
            var preview = msg.Length <= 200 ? msg : msg.Substring(0, 200);

            await _auditLogger.LogAsync(
                HttpContext,
                action: "StaffReply",
                entityType: "TicketReply",
                entityId: reply.ReplyId.ToString(),
                before: null,
                after: new
                {
                    reply.ReplyId,
                    reply.TicketId,
                    reply.SenderId,
                    reply.IsStaffReply,
                    MessagePreview = preview,
                    t.Status,
                    t.SlaStatus,
                    t.FirstRespondedAt,
                    t.UpdatedAt
                }
            );
        }

        // FE vẫn nhận response trực tiếp để xử lý lạc quan
        return Ok(dtoOut);
    }
}
