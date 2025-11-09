// File: Controllers/TicketsController.cs
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Tickets;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly KeytietkiemDbContext _db;
    public TicketsController(KeytietkiemDbContext db) => _db = db;

    private static string NormStatus(string? s)
    {
        var v = (s ?? "").Trim();
        if (string.Equals(v, "Open", StringComparison.OrdinalIgnoreCase)) return "New";
        return string.IsNullOrWhiteSpace(v) ? "New" : v;
    }
    private static string NormAssign(string? s) => string.IsNullOrWhiteSpace(s) ? "Unassigned" : s!;

    private static IQueryable<Ticket> BaseQuery(KeytietkiemDbContext db) => db.Tickets.AsNoTracking()
        .Include(t => t.User)
        .Include(t => t.Assignee);

    // ===== LIST =====
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> List(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? severity,
        [FromQuery] string? sla,
        [FromQuery(Name = "assignmentState")] string? assignmentState,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var assignedFilter = assignmentState;
        var query = BaseQuery(_db);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.Trim();
            query = query.Where(t =>
                (t.TicketCode ?? "").Contains(kw) ||
                (t.Subject ?? "").Contains(kw) ||
                (t.User.FullName ?? "").Contains(kw) ||
                (t.User.Email ?? "").Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "New")
                query = query.Where(t => (t.Status ?? "New") == "New" || t.Status == "Open");
            else
                query = query.Where(t => (t.Status ?? "New") == status);
        }

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(t => (t.Severity ?? "") == severity);

        if (!string.IsNullOrWhiteSpace(sla))
            query = query.Where(t => (t.SlaStatus ?? "") == sla);

        if (!string.IsNullOrWhiteSpace(assignedFilter))
            query = query.Where(t => (t.AssignmentState ?? "Unassigned") == assignedFilter);

        var total = await query.CountAsync();

        var raw = await query.OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var items = raw.Select(t => new TicketListItemDto
        {
            TicketId = t.TicketId,
            TicketCode = t.TicketCode ?? "",
            Subject = t.Subject ?? "",
            Status = NormStatus(t.Status),
            Severity = Enum.TryParse<TicketSeverity>(t.Severity ?? "", true, out var sev) ? sev : TicketSeverity.Medium,
            SlaStatus = Enum.TryParse<SlaState>(t.SlaStatus ?? "", true, out var s1) ? s1 : SlaState.OK,
            AssignmentState = Enum.TryParse<AssignmentState>(NormAssign(t.AssignmentState), true, out var asn) ? asn : AssignmentState.Unassigned,
            CustomerName = t.User.FullName ?? t.User.Email,
            CustomerEmail = t.User.Email,

            // NEW:
            AssigneeId = t.AssigneeId,
            AssigneeName = t.Assignee != null ? (t.Assignee.FullName ?? t.Assignee.Email) : null,
            AssigneeEmail = t.Assignee?.Email,

            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();

        return Ok(new PagedResult<TicketListItemDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            Items = items
        });
    }

    // ===== DETAIL =====
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDetailDto>> Detail(Guid id)
    {
        var t = await _db.Tickets
            .Include(x => x.User)
            .Include(x => x.Assignee)
            .FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        var replies = await _db.TicketReplies.AsNoTracking()
            .Include(r => r.Sender)
            .Where(r => r.TicketId == id)
            .OrderBy(r => r.SentAt)
            .Select(r => new TicketReplyDto
            {
                ReplyId = r.ReplyId,
                SenderId = r.SenderId,
                SenderName = r.Sender.FullName ?? r.Sender.Email,
                IsStaffReply = r.IsStaffReply,
                Message = r.Message,
                SentAt = r.SentAt
            })
            .ToListAsync();

        var dto = new TicketDetailDto
        {
            TicketId = t.TicketId,
            TicketCode = t.TicketCode ?? "",
            Subject = t.Subject ?? "",
            Status = NormStatus(t.Status),
            CustomerName = t.User.FullName ?? "",
            CustomerEmail = t.User.Email,
            CustomerPhone = t.User.Phone,
            Severity = Enum.TryParse<TicketSeverity>(t.Severity ?? "", true, out var sev) ? sev : TicketSeverity.Medium,
            SlaStatus = Enum.TryParse<SlaState>(t.SlaStatus ?? "", true, out var slaVal) ? slaVal : SlaState.OK,
            AssignmentState = Enum.TryParse<AssignmentState>(NormAssign(t.AssignmentState), true, out var asn) ? asn : AssignmentState.Unassigned,

            // NEW:
            AssigneeId = t.AssigneeId,
            AssigneeName = t.Assignee != null ? (t.Assignee.FullName ?? t.Assignee.Email) : null,
            AssigneeEmail = t.Assignee?.Email,

            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            Replies = replies
        };

        return Ok(dto);
    }

    private Guid? GetCurrentUserIdOrNull()
    {
        var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(str, out var id) ? id : null;
    }

    // Payload cho assign/transfer
    public class AssignTicketDto
    {
        public Guid AssigneeId { get; set; }
    }

    // ===== ASSIGN: New -> InProgress; Unassigned -> Assigned; set theo AssigneeId gửi lên =====
    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketDto dto)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        var st = NormStatus(t.Status);
        var asg = NormAssign(t.AssignmentState);

        if (st is "Closed" or "Completed")
            return BadRequest(new { message = "Ticket đã khoá." });

        // validate user
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.UserId == dto.AssigneeId);
        if (user == null) return BadRequest(new { message = "Không tìm thấy nhân viên được chọn." });

        // phải là Customer Care Staff và Active
        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase)
            || !user.Roles.Any(r => r.Name.Equals("Customer Care Staff", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active)." });

        if (asg == "Unassigned") t.AssignmentState = "Assigned";
        if (st == "New") t.Status = "InProgress";

        t.AssigneeId = dto.AssigneeId;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ===== TRANSFER TO TECH: phải đã gán; AssignmentState -> Technical; set AssigneeId mới (khác cũ) =====
    [HttpPost("{id:guid}/transfer-tech")]
    public async Task<IActionResult> TransferToTech(Guid id, [FromBody] AssignTicketDto dto)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        var st = NormStatus(t.Status);
        if (st is "Closed" or "Completed")
            return BadRequest(new { message = "Ticket đã khoá." });

        var asg = NormAssign(t.AssignmentState);
        if (asg == "Unassigned")
            return BadRequest(new { message = "Vui lòng gán trước khi chuyển hỗ trợ." });

        if (t.AssigneeId == dto.AssigneeId)
            return BadRequest(new { message = "Vui lòng chọn nhân viên khác với người đang phụ trách." });

        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.UserId == dto.AssigneeId);
        if (user == null) return BadRequest(new { message = "Không tìm thấy nhân viên được chọn." });
        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase)
            || !user.Roles.Any(r => r.Name.Equals("Customer Care Staff", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active)." });

        if (asg != "Technical") t.AssignmentState = "Technical";
        if (st == "New") t.Status = "InProgress";

        t.AssigneeId = dto.AssigneeId;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ===== COMPLETE =====
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        var st = NormStatus(t.Status);
        if (st is "Closed" or "Completed")
            return BadRequest(new { message = "Ticket đã khoá." });
        if (st != "InProgress")
            return BadRequest(new { message = "Chỉ hoàn thành khi trạng thái Đang xử lý." });

        t.Status = "Completed";
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ===== CLOSE =====
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        var st = NormStatus(t.Status);
        if (st is "Closed" or "Completed")
            return BadRequest(new { message = "Ticket đã khoá." });
        if (st != "New")
            return BadRequest(new { message = "Chỉ đóng khi trạng thái Mới." });

        t.Status = "Closed";
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ===== REPLIES (chat) =====
    [HttpPost("{id:guid}/replies")]
    public async Task<ActionResult<TicketReplyDto>> CreateReply(Guid id, [FromBody] CreateTicketReplyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto?.Message)) return BadRequest(new { message = "Nội dung phản hồi trống." });

        var t = await _db.Tickets.Include(x => x.User).FirstOrDefaultAsync(x => x.TicketId == id);
        if (t is null) return NotFound();

        var me = GetCurrentUserIdOrNull();
        if (!me.HasValue) return Unauthorized();

        var sender = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.UserId == me.Value);
        if (sender is null) return Unauthorized();

        var reply = new TicketReply
        {
            TicketId = t.TicketId,
            SenderId = sender.UserId,
            IsStaffReply = sender.Roles.Any(), // đơn giản: có role => staff
            Message = dto.Message.Trim(),
            SentAt = DateTime.UtcNow
        };

        _db.TicketReplies.Add(reply);
        await _db.SaveChangesAsync();

        var dtoOut = new TicketReplyDto
        {
            ReplyId = reply.ReplyId,
            SenderId = reply.SenderId,
            SenderName = sender.FullName ?? sender.Email ?? "Staff",
            IsStaffReply = reply.IsStaffReply,
            Message = reply.Message,
            SentAt = reply.SentAt
        };
        return Ok(dtoOut);
    }
}
