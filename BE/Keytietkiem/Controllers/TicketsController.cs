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

    // Helpers: parse enum sau khi đã materialize
    private static TicketSeverity ParseSeverity(string? s) =>
        Enum.TryParse<TicketSeverity>(s ?? "", true, out var v) ? v : TicketSeverity.Medium;

    private static SlaState ParseSla(string? s) =>
        Enum.TryParse<SlaState>(s ?? "", true, out var v) ? v : SlaState.OK;

    private static AssignmentState ParseAssignState(string? s) =>
        Enum.TryParse<AssignmentState>(NormAssign(s), true, out var v) ? v : AssignmentState.Unassigned;

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
            Severity = ParseSeverity(t.Severity),
            SlaStatus = ParseSla(t.SlaStatus),
            AssignmentState = ParseAssignState(t.AssignmentState),
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

        // NEW: Ticket liên quan – 2-phase (KHÔNG dùng out var trong Select của IQueryable)
        var relatedRaw = await _db.Tickets.AsNoTracking()
            .Where(x => x.UserId == t.UserId && x.TicketId != t.TicketId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.TicketId,
                x.TicketCode,
                x.Subject,
                x.Status,
                x.Severity,
                x.SlaStatus,
                x.CreatedAt
            })
            .Take(10)
            .ToListAsync();

        var related = relatedRaw.Select(x => new RelatedTicketDto
        {
            TicketId = x.TicketId,
            TicketCode = x.TicketCode ?? "",
            Subject = x.Subject ?? "",
            Status = NormStatus(x.Status),
            Severity = ParseSeverity(x.Severity),
            SlaStatus = ParseSla(x.SlaStatus),
            CreatedAt = x.CreatedAt
        }).ToList();

        var dto = new TicketDetailDto
        {
            TicketId = t.TicketId,
            TicketCode = t.TicketCode ?? "",
            Subject = t.Subject ?? "",
            Status = NormStatus(t.Status),
            CustomerName = t.User.FullName ?? "",
            CustomerEmail = t.User.Email,
            CustomerPhone = t.User.Phone,
            Severity = ParseSeverity(t.Severity),
            SlaStatus = ParseSla(t.SlaStatus),
            AssignmentState = ParseAssignState(t.AssignmentState),

            // NEW: nhân viên phụ trách
            AssigneeId = t.AssigneeId,
            AssigneeName = t.Assignee != null ? (t.Assignee.FullName ?? t.Assignee.Email) : null,
            AssigneeEmail = t.Assignee?.Email,

            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,

            Replies = replies,
            RelatedTickets = related
            // LatestOrder = ... (nếu bạn muốn thêm sau)
        };

        return Ok(dto);
    }

    private Guid? GetCurrentUserIdOrNull()
    {
        var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(str, out var id) ? id : null;
    }

    public class AssignTicketDto { public Guid AssigneeId { get; set; } }

    // ===== ASSIGN =====
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
}
