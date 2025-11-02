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

    // Normalize: dữ liệu cũ "Open" quy về "New"; rỗng -> "New"
    private static string NormStatus(string? s)
    {
        var v = (s ?? "").Trim();
        if (string.Equals(v, "Open", StringComparison.OrdinalIgnoreCase)) return "New";
        return string.IsNullOrWhiteSpace(v) ? "New" : v;
    }

    // Normalize assignment: rỗng -> "Unassigned"
    private static string NormAssign(string? s) => string.IsNullOrWhiteSpace(s) ? "Unassigned" : s!;

    private static IQueryable<Ticket> BaseQuery(KeytietkiemDbContext db) => db.Tickets.AsNoTracking()
        .Include(t => t.User)
        .Include(t => t.Assignee);

    // GET /api/tickets?q=&status=&severity=&sla=&assigned= | assignmentState=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> List(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? severity,
        [FromQuery] string? sla,
        [FromQuery] string? assigned,
        [FromQuery(Name = "assignmentState")] string? assignmentState,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var assignedFilter = string.IsNullOrWhiteSpace(assigned) ? assignmentState : assigned;
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
            AssigneeName = t.Assignee != null ? (t.Assignee.FullName ?? t.Assignee.Email) : null,
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

    // GET /api/tickets/{id}
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
            AssigneeName = t.Assignee != null ? (t.Assignee.FullName ?? t.Assignee.Email) : null,
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

    // POST /api/tickets/{id}/assign
    // "Gán": New -> InProgress, Unassigned -> Assigned; set AssigneeId nếu có user đăng nhập
    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        var st = NormStatus(t.Status);
        var asg = NormAssign(t.AssignmentState);

        if (st is "Closed" or "Completed")
            return BadRequest(new { message = "Ticket đã khoá." });

        if (asg == "Unassigned") t.AssignmentState = "Assigned";
        if (st == "New") t.Status = "InProgress";

        var me = GetCurrentUserIdOrNull();
        if (me.HasValue) t.AssigneeId = me.Value;

        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/tickets/{id}/transfer-tech
    // "Chuyển hỗ trợ": phải đã gán; AssignmentState -> "Technical"; New -> InProgress
    [HttpPost("{id:guid}/transfer-tech")]
    public async Task<IActionResult> TransferToTech(Guid id)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        var st = NormStatus(t.Status);
        if (st is "Closed" or "Completed")
            return BadRequest(new { message = "Ticket đã khoá." });

        var asg = NormAssign(t.AssignmentState);
        if (asg == "Unassigned")
            return BadRequest(new { message = "Vui lòng gán trước khi chuyển hỗ trợ." });

        if (asg != "Technical") t.AssignmentState = "Technical";
        if (st == "New") t.Status = "InProgress";

        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/tickets/{id}/complete
    // "Hoàn thành": chỉ cho InProgress -> Completed
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

    // POST /api/tickets/{id}/close
    // "Đóng": chỉ cho New -> Closed
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
}
