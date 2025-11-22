// File: Controllers/TicketsController.cs
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Tickets;
using Keytietkiem.Hubs;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly KeytietkiemDbContext _db;
    private readonly IHubContext<TicketHub> _ticketHub;

    public TicketsController(KeytietkiemDbContext db, IHubContext<TicketHub> ticketHub)
    {
        _db = db;
        _ticketHub = ticketHub;
    }

    // ============ Helpers ============
    private static string NormStatus(string? s)
    {
        var v = (s ?? "").Trim();
        if (string.Equals(v, "Open", StringComparison.OrdinalIgnoreCase)) return "New";
        return string.IsNullOrWhiteSpace(v) ? "New" : v;
    }

    private static string NormAssign(string? s) => string.IsNullOrWhiteSpace(s) ? "Unassigned" : s!;

    private static TicketSeverity ParseSeverity(string? s)
        => Enum.TryParse<TicketSeverity>(s ?? "", true, out var v) ? v : TicketSeverity.Medium;

    private static SlaState ParseSla(string? s)
        => Enum.TryParse<SlaState>(s ?? "", true, out var v) ? v : SlaState.OK;

    private static AssignmentState ParseAssignState(string? s)
        => Enum.TryParse<AssignmentState>(NormAssign(s), true, out var v) ? v : AssignmentState.Unassigned;

    private static IQueryable<Ticket> BaseQuery(KeytietkiemDbContext db) => db.Tickets.AsNoTracking()
        .Include(t => t.User)
        .Include(t => t.Assignee);

    /// <summary>
    /// Sinh TicketCode mới dạng TCK-0001 dựa trên TicketCode lớn nhất hiện có.
    /// Dùng string order vì phần số luôn cố định 4 chữ số.
    /// </summary>
    private async Task<string> GenerateNextTicketCodeAsync()
    {
        const string prefix = "TCK-";

        var lastCode = await _db.Tickets.AsNoTracking()
            .Where(t => t.TicketCode.StartsWith(prefix))
            .OrderByDescending(t => t.TicketCode)
            .Select(t => t.TicketCode)
            .FirstOrDefaultAsync();

        var lastNumber = 0;

        if (!string.IsNullOrEmpty(lastCode) && lastCode.Length > prefix.Length)
        {
            var numericPart = lastCode.Substring(prefix.Length);
            if (int.TryParse(numericPart, out var n) && n >= 0)
            {
                lastNumber = n;
            }
        }

        var nextNumber = lastNumber + 1;
        return $"{prefix}{nextNumber:D4}";
    }

    // ============ LIST ============
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> List(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? severity,
        [FromQuery] string? sla,
        [FromQuery(Name = "assignmentState")] string? assignmentState,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

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

        if (!string.IsNullOrWhiteSpace(assignmentState))
            query = query.Where(t => (t.AssignmentState ?? "Unassigned") == assignmentState);

        var total = await query.CountAsync();

        // Sắp xếp theo TicketCode (giảm dần)
        var raw = await query
            .OrderByDescending(t => t.TicketCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

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

    // ============ DETAIL ============
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
            Description = t.Description,
            Status = NormStatus(t.Status),
            Severity = ParseSeverity(t.Severity),
            PriorityLevel = t.PriorityLevel,
            SlaStatus = ParseSla(t.SlaStatus),
            AssignmentState = ParseAssignState(t.AssignmentState),

            FirstResponseDueAt = t.FirstResponseDueAt,
            FirstRespondedAt = t.FirstRespondedAt,
            ResolutionDueAt = t.ResolutionDueAt,
            ResolvedAt = t.ResolvedAt,

            CustomerName = t.User.FullName ?? "",
            CustomerEmail = t.User.Email,
            CustomerPhone = t.User.Phone,

            AssigneeId = t.AssigneeId,
            AssigneeName = t.Assignee != null ? (t.Assignee.FullName ?? t.Assignee.Email) : null,
            AssigneeEmail = t.Assignee?.Email,

            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,

            Replies = replies,
            RelatedTickets = related
        };

        return Ok(dto);
    }

    // ============ CUSTOMER CREATE ============
    /// <summary>
    /// Customer tạo ticket mới từ màn hình customer-ticket.
    /// - Chỉ cho phép user đang đăng nhập có role "Customer".
    /// - Severity mặc định = Medium (không cho customer chọn).
    /// - PriorityLevel lấy từ SupportPriorityLevel của user.
    /// - Tự sinh TicketCode dạng "TCK-0001" dựa trên mã lớn nhất hiện có.
    /// - Tự áp dụng SLA (SlaRuleId, FirstResponseDueAt, ResolutionDueAt, SlaStatus).
    /// </summary>
    /// <remarks>
    /// POST /api/Tickets/create
    /// Body: { "templateCode": "...", "description": "..." }
    /// </remarks>
    [HttpPost("create")]
    public async Task<ActionResult<CustomerTicketCreatedDto>> CreateCustomerTicket([FromBody] CustomerCreateTicketDto dto)
    {
        var templateCode = (dto?.TemplateCode ?? string.Empty).Trim();
        var descriptionRaw = dto?.Description ?? string.Empty;
        var description = descriptionRaw.Trim();

        if (string.IsNullOrWhiteSpace(templateCode))
        {
            return BadRequest(new { message = "Vui lòng chọn loại vấn đề (tiêu đề ticket)." });
        }

        if (templateCode.Length > 50)
        {
            return BadRequest(new { message = "Mã template không hợp lệ." });
        }

        if (description.Length > 1000)
        {
            return BadRequest(new { message = "Mô tả ticket tối đa 1000 ký tự." });
        }

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

        // Chỉ cho phép khách hàng tạo ticket (lọc theo Role.Code chứa "customer")
        var isCustomer = sender.Roles.Any(r =>
        {
            var code = (r.Code ?? string.Empty).Trim().ToLowerInvariant();
            return code.Contains("customer");
        });

        if (!isCustomer)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ khách hàng mới được phép tạo ticket." });
        }

        // Lấy template tiêu đề + severity tương ứng
        var template = await _db.TicketSubjectTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateCode == templateCode && t.IsActive);

        if (template == null)
        {
            return BadRequest(new
            {
                message = "Loại vấn đề bạn chọn không hợp lệ hoặc đã bị vô hiệu hóa. Vui lòng tải lại trang và thử lại."
            });
        }

        var now = DateTime.UtcNow;

        // Sinh TicketCode mới kiểu TCK-0001, TCK-0002...
        var ticketCode = await GenerateNextTicketCodeAsync();

        var ticket = new Ticket
        {
            TicketId = Guid.NewGuid(),
            UserId = sender.UserId,
            Subject = template.Title,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            Status = "New",
            AssigneeId = null,
            TicketCode = ticketCode,
            // SLA fields
            Severity = null,        // sẽ được gán chính xác trong ApplyOnCreate
            SlaStatus = SlaState.OK.ToString(),
            AssignmentState = "Unassigned",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Áp dụng logic SLA chung:
        // - Severity lấy theo template (Low/Medium/High/Critical)
        // - PriorityLevel = sender.SupportPriorityLevel
        TicketSlaHelper.ApplyOnCreate(
            _db,
            ticket,
            template.Severity,
            sender.SupportPriorityLevel,
            now
        );

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        var result = new CustomerTicketCreatedDto
        {
            TicketId = ticket.TicketId,
            TicketCode = ticket.TicketCode,
            Subject = ticket.Subject,
            Description = ticket.Description,
            Status = ticket.Status,
            Severity = ParseSeverity(ticket.Severity),
            SlaStatus = ParseSla(ticket.SlaStatus),
            CreatedAt = ticket.CreatedAt
        };

        return Ok(result);
    }

    // ============ SUBJECT TEMPLATES (Customer create) =============
    [HttpGet("subject-templates")]
    public async Task<ActionResult<List<TicketSubjectTemplateDto>>> GetSubjectTemplates([FromQuery] bool activeOnly = true)
    {
        var query = _db.TicketSubjectTemplates.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(t => t.IsActive);
        }

        var list = await query
            .OrderBy(t => t.Category ?? "General")
            .ThenBy(t => t.Title)
            .Select(t => new TicketSubjectTemplateDto
            {
                TemplateCode = t.TemplateCode,
                Title = t.Title,
                Severity = t.Severity,
                Category = t.Category,
                IsActive = t.IsActive
            })
            .ToListAsync();

        return Ok(list);
    }



    // ============ ASSIGN / TRANSFER / COMPLETE / CLOSE ============
    public class AssignTicketDto { public Guid AssigneeId { get; set; } }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketDto dto)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        var st = NormStatus(t.Status);
        var asg = NormAssign(t.AssignmentState);

        if (st is "Closed" or "Completed")
            return BadRequest(new { message = "Ticket đã khoá." });

        // Validate staff: Active + Role.Code chứa "care"
        var userOk = await _db.Users
            .Include(u => u.Roles)
            .AnyAsync(u =>
                u.UserId == dto.AssigneeId &&
                ((u.Status ?? "Active") == "Active") &&
                u.Roles.Any(r => (r.Code ?? string.Empty).ToLower().Contains("care")));
        if (!userOk) return BadRequest(new { message = "Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active)." });

        if (asg == "Unassigned") t.AssignmentState = "Assigned";
        if (st == "New") t.Status = "InProgress";

        t.AssigneeId = dto.AssigneeId;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

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

        // Validate staff: Active + Role.Code chứa "care"
        var userOk = await _db.Users
            .Include(u => u.Roles)
            .AnyAsync(u =>
                u.UserId == dto.AssigneeId &&
                ((u.Status ?? "Active") == "Active") &&
                u.Roles.Any(r => (r.Code ?? string.Empty).ToLower().Contains("care")));
        if (!userOk) return BadRequest(new { message = "Nhân viên không hợp lệ (yêu cầu Customer Care Staff & Active)." });

        if (asg != "Technical") t.AssignmentState = "Technical";
        if (st == "New") t.Status = "InProgress";

        t.AssigneeId = dto.AssigneeId;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

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

        var now = DateTime.UtcNow;

        t.Status = "Completed";

        // SLA: đánh dấu thời điểm giải quyết nếu chưa có
        if (!t.ResolvedAt.HasValue)
            t.ResolvedAt = now;

        t.UpdatedAt = now;

        // Cập nhật SlaStatus (OK / Warning / Overdue)
        TicketSlaHelper.UpdateSlaStatus(t, now);

        await _db.SaveChangesAsync();
        return NoContent();
    }

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

        var now = DateTime.UtcNow;

        t.Status = "Closed";

        // SLA: đóng ticket cũng xem như đã giải quyết
        if (!t.ResolvedAt.HasValue)
            t.ResolvedAt = now;

        t.UpdatedAt = now;

        TicketSlaHelper.UpdateSlaStatus(t, now);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ============ NEW: staff lookups (Assign / Transfer) ============
    public class StaffMiniDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
    }

    // Chỉ lấy nhân viên Active + role Code chứa "care"
    private IQueryable<User> StaffBaseQuery()
    {
        var users = _db.Users.AsNoTracking()
            .Include(u => u.Roles)
            .Where(u =>
                ((u.Status ?? "Active") == "Active") &&
                u.Roles.Any(r => (r.Code ?? string.Empty).ToLower().Contains("care")));
        return users;
    }

    // GET /api/tickets/assignees?q=&page=&pageSize=
    [HttpGet("assignees")]
    public async Task<ActionResult<List<StaffMiniDto>>> GetAssignableStaff(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var users = StaffBaseQuery();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var key = q.Trim();
            users = users.Where(u =>
                EF.Functions.Like(u.FullName ?? "", $"%{key}%") ||
                EF.Functions.Like(u.Email ?? "", $"%{key}%"));
        }

        var items = await users
            .OrderBy(u => u.FullName ?? u.Email)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new StaffMiniDto
            {
                UserId = u.UserId,
                FullName = u.FullName ?? u.Email ?? "",
                Email = u.Email ?? ""
            })
            .ToListAsync();

        return Ok(items);
    }

    // GET /api/tickets/assignees/transfer?excludeUserId=&q=&page=&pageSize=
    [HttpGet("assignees/transfer")]
    public async Task<ActionResult<List<StaffMiniDto>>> GetTransferAssignees(
        [FromQuery] Guid? excludeUserId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var users = StaffBaseQuery();

        if (excludeUserId.HasValue)
        {
            users = users.Where(u => u.UserId != excludeUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var key = q.Trim();
            users = users.Where(u =>
                EF.Functions.Like(u.FullName ?? "", $"%{key}%") ||
                EF.Functions.Like(u.Email ?? "", $"%{key}%"));
        }

        var items = await users
            .OrderBy(u => u.FullName ?? u.Email)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new StaffMiniDto
            {
                UserId = u.UserId,
                FullName = u.FullName ?? u.Email ?? "",
                Email = u.Email ?? ""
            })
            .ToListAsync();

        return Ok(items);
    }
}
