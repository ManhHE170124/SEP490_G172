// File: Controllers/TicketsController.cs
using Keytietkiem.Constants;
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Tickets;
using Keytietkiem.Hubs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // ✅ NEW: bắt buộc đăng nhập cho toàn bộ Tickets APIs
public class TicketsController : ControllerBase
{
    private readonly KeytietkiemDbContext _db;
    private readonly IHubContext<TicketHub> _ticketHub;
    private readonly IAuditLogger _auditLogger;
    private readonly INotificationSystemService _notificationSystemService;
    private readonly IConfiguration _config;
    private readonly IClock _clock;
    public TicketsController(
        KeytietkiemDbContext db,
        IHubContext<TicketHub> ticketHub,
        IAuditLogger auditLogger,
        INotificationSystemService notificationSystemService,
        IConfiguration config)
    {
        _db = db;
        _ticketHub = ticketHub;
        _auditLogger = auditLogger;
        _notificationSystemService = notificationSystemService;
        _config = config;
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

    // ✅ NEW: helpers role tối thiểu để kiểm tra quyền theo yêu cầu
    private static bool IsCustomer(User u)
    {
        // FIX: tránh bắt nhầm role kiểu "customer-care-staff" là customer
        if (u.Roles == null || u.Roles.Count == 0) return false;

        var hasCustomer = u.Roles.Any(r =>
        {
            var code = (r.Code ?? string.Empty).Trim().ToLowerInvariant();
            return code.Contains("customer");
        });

        // Nếu đồng thời là staff/admin thì không coi là customer
        if (hasCustomer && IsStaffOrAdmin(u)) return false;

        return hasCustomer;
    }

    private static bool IsCareStaff(User u)
    {
        return u.Roles != null && u.Roles.Any(r =>
        {
            var code = (r.Code ?? string.Empty).Trim().ToLowerInvariant();
            return code.Contains("care");
        });
    }

    private static bool IsAdmin(User u)
    {
        return u.Roles != null && u.Roles.Any(r =>
        {
            var name = (r.Name ?? string.Empty).Trim().ToLowerInvariant();
            var rid = (r.RoleId ?? string.Empty).Trim().ToLowerInvariant();
            var code = (r.Code ?? string.Empty).Trim().ToLowerInvariant();
            return name == "admin" || rid == "admin" || code.Contains("admin");
        });
    }

    private static bool IsStaffOrAdmin(User u) => IsAdmin(u) || IsCareStaff(u);

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
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<ActionResult<PagedResult<TicketListItemWithSlaDto>>> List(
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

        // search
        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.Trim();
            query = query.Where(t =>
                (t.TicketCode ?? "").Contains(kw) ||
                (t.Subject ?? "").Contains(kw) ||
                (t.User.FullName ?? "").Contains(kw) ||
                (t.User.Email ?? "").Contains(kw));
        }

        // filter trạng thái
        if (!string.IsNullOrWhiteSpace(status))
        {
            status = status.Trim();
            if (status == "New")
            {
                query = query.Where(t => (t.Status ?? "New") == "New" || t.Status == "Open");
            }
            else
            {
                query = query.Where(t => (t.Status ?? "New") == status);
            }
        }

        // filter mức độ
        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(t => (t.Severity ?? "") == severity);
        }

        // filter SLA
        if (!string.IsNullOrWhiteSpace(sla))
        {
            query = query.Where(t => (t.SlaStatus ?? "") == sla);
        }

        // filter trạng thái gán
        if (!string.IsNullOrWhiteSpace(assignmentState))
        {
            if (string.Equals(assignmentState, "Mine", StringComparison.OrdinalIgnoreCase))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdStr, out var userId))
                {
                    return Unauthorized(new { message = "Bạn cần đăng nhập để xem ticket của mình." });
                }

                query = query.Where(t => t.AssigneeId == userId);
            }
            else
            {
                query = query.Where(t => (t.AssignmentState ?? "Unassigned") == assignmentState);
            }
        }

        var total = await query.CountAsync();

        // Sắp xếp theo SLA nghiêm trọng -> OK,
        // sau đó Unassigned trước, rồi đến hạn phản hồi / hạn giải quyết.
        query = query
            // 1) Mức SLA: Overdue -> Warning -> OK -> khác
            .OrderBy(t =>
                t.SlaStatus == SlaState.Overdue.ToString() ? 0 :
                t.SlaStatus == SlaState.Warning.ToString() ? 1 :
                t.SlaStatus == SlaState.OK.ToString() ? 2 : 3)
            // 2) Ticket chưa gán (Unassigned) trước, đã gán (Assigned/Technical/...) sau
            .ThenBy(t => (t.AssignmentState ?? "Unassigned") == "Unassigned" ? 0 : 1)
            // 3) Với Unassigned: dùng FirstResponseDueAt, với ticket đã gán: dùng ResolutionDueAt
            .ThenBy(t =>
                ((t.AssignmentState ?? "Unassigned") == "Unassigned"
                    ? t.FirstResponseDueAt
                    : t.ResolutionDueAt) ?? DateTime.MaxValue)
            // 4) Fallback để ổn định phân trang
            .ThenByDescending(t => t.TicketCode);

        var raw = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = raw.Select(t => new TicketListItemWithSlaDto
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

            PriorityLevel = t.PriorityLevel,
            FirstResponseDueAt = t.FirstResponseDueAt,
            ResolutionDueAt = t.ResolutionDueAt,

            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();

        return Ok(new PagedResult<TicketListItemWithSlaDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            Items = items
        });
    }

    // ============ LIST: Ticket của chính khách hàng đang đăng nhập ============
    [HttpGet("customer")]
    [Authorize]
    public async Task<ActionResult<PagedResult<CustomerTicketListItemDto>>> MyTickets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Lấy UserId từ claim
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Bạn cần đăng nhập để xem ticket của mình." });
        }

        // ✅ NEW: chỉ cho phép Customer xem list ticket của mình
        var me = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (me is null)
            return Unauthorized();

        if ((me.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsCustomer(me))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bạn không có quyền truy cập chức năng này." });
        }

        // Chỉ lấy ticket của chính user đang đăng nhập
        var query = BaseQuery(_db).Where(t => t.UserId == userId);

        var total = await query.CountAsync();

        // Phân trang tương tự các list khác (order theo TicketCode giảm dần)
        var raw = await query
            .OrderByDescending(t => t.TicketCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = raw.Select(t => new CustomerTicketListItemDto
        {
            TicketId = t.TicketId,
            TicketCode = t.TicketCode ?? "",
            Subject = t.Subject ?? "",
            Status = NormStatus(t.Status),
            Severity = ParseSeverity(t.Severity),
            SlaStatus = ParseSla(t.SlaStatus),

            AssigneeName = t.Assignee != null ? (t.Assignee.FullName ?? t.Assignee.Email) : null,
            AssigneeEmail = t.Assignee?.Email,

            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();

        return Ok(new PagedResult<CustomerTicketListItemDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            Items = items
        });
    }

    // ============ DETAIL ============
    [HttpGet("{id:guid}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<ActionResult<TicketDetailDto>> Detail(Guid id)
    {
        var t = await _db.Tickets
            .Include(x => x.User)
            .Include(x => x.Assignee)
            .FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        // ✅ NEW: Customer chỉ được xem ticket của chính mình, Staff/Admin xem được
        var meStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meStr, out var meId))
            return Unauthorized();

        var me = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == meId);

        if (me is null)
            return Unauthorized();

        if ((me.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (IsCustomer(me))
        {
            if (t.UserId != me.UserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Bạn không có quyền truy cập ticket này." });
            }
        }
        else
        {
            if (!IsStaffOrAdmin(me))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Bạn không có quyền truy cập chức năng này." });
            }
        }

        var replies = await _db.TicketReplies.AsNoTracking()
            .Include(r => r.Sender)
            .Where(r => r.TicketId == id)
            .OrderBy(r => r.SentAt)
            .Select(r => new TicketReplyDto
            {
                ReplyId = r.ReplyId,
                SenderId = r.SenderId,
                SenderName = r.Sender != null ? (r.Sender.FullName ?? r.Sender.Email) : "Không rõ",

                // ✅ NEW: map avatar theo đúng user gửi tin
                SenderAvatarUrl = r.Sender != null ? r.Sender.AvatarUrl : null,

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

    [HttpGet("customer/{id:guid}")]
    [Authorize]
    public async Task<ActionResult<TicketDetailDto>> GetCustomerTicketDetail(Guid id)
    {
        // Lấy UserId từ claim
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Bạn cần đăng nhập để xem ticket của mình." });
        }

        var t = await _db.Tickets
            .Include(x => x.User)
            .Include(x => x.Assignee)
            .FirstOrDefaultAsync(x => x.TicketId == id);

        if (t == null) return NotFound();

        // Kiểm tra ownership: chỉ cho phép chủ ticket xem
        if (t.UserId != userId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bạn không có quyền xem ticket này." });
        }

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
    [HttpPost("create")]
    [Authorize]
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

        // ✅ NEW: chặn user bị khoá
        if ((sender.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        // Chỉ cho phép khách hàng tạo ticket (lọc theo Role.Code chứa "customer")
        var isCustomer = IsCustomer(sender);

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

    // ============ SUBJECT TEMPLATES (Customer create) ============
    [HttpGet("subject-templates")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TicketSubjectTemplateDto>>> GetSubjectTemplates([FromQuery] bool activeOnly = true)
    {
        // ✅ NEW: chỉ Customer mới được xem templates (vì phục vụ create ticket)
        var meStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meStr, out var meId))
            return Unauthorized();

        var me = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == meId);

        if (me is null)
            return Unauthorized();

        if ((me.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsCustomer(me))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Chỉ khách hàng mới được phép tạo ticket." });
        }

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
    [Authorize]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketDto dto)
    {
        // ✅ NEW: chỉ Staff/Admin (ưu tiên Admin) mới được gán người khác
        var meStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meStr, out var meId))
            return Unauthorized();

        var actor = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == meId);

        if (actor is null)
            return Unauthorized();

        if ((actor.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsAdmin(actor))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bạn không có quyền gán ticket." });
        }

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

        var before = new
        {
            t.TicketId,
            t.AssigneeId,
            AssignmentState = t.AssignmentState,
            Status = t.Status
        };

        if (asg == "Unassigned") t.AssignmentState = "Assigned";
        if (st == "New") t.Status = "InProgress";

        t.AssigneeId = dto.AssigneeId;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var after = new
        {
            t.TicketId,
            t.AssigneeId,
            AssignmentState = t.AssignmentState,
            Status = t.Status
        };

        // 🔐 AUDIT LOG – ASSIGN TICKET
        await _auditLogger.LogAsync(
            HttpContext,
            action: "AssignStaffToTicket",
            entityType: "Ticket",
            entityId: t.TicketId.ToString(),
            before: before,
            after: after
        );
        // ✅ System notification: Admin gán ticket -> notify nhân viên được gán
        try
        {
            var actorName = actor.FullName ?? "(unknown)";
            var actorEmail = actor.Email ?? "(unknown)";
            var ticketCode = t.TicketCode ?? t.TicketId.ToString();
            var origin = PublicUrlHelper.GetPublicOrigin(HttpContext, _config);
            var relatedUrl = $"{origin}/staff/tickets/{id}";

            await _notificationSystemService.CreateForUserIdsAsync(new SystemNotificationCreateRequest
            {
                Title = "Bạn được gán ticket mới",
                Message =
                    $"Admin {actorName} đã gán ticket cho bạn.\n" +
                    $"-Mã ticket: {ticketCode}\n" +
                    $"- Nội dung: {t.Subject ?? ""}",
                Severity = 0, // Info
                CreatedByUserId = actor.UserId,
                CreatedByEmail = actorEmail,
                Type = "Ticket.Assigned",
                RelatedEntityType = "Ticket",
                RelatedEntityId = t.TicketId.ToString(),

                // ✅ bạn đổi route FE staff ticket detail
                RelatedUrl = relatedUrl,

                TargetUserIds = new List<Guid> { dto.AssigneeId }
            });
        }
        catch { }

        return NoContent();
    }

    /// <summary>
    /// Staff tự nhận ticket về mình (dùng cho hàng đợi Unassigned bên màn Staff).
    /// POST /api/tickets/{id}/assign-me
    /// </summary>
    [HttpPost("{id:guid}/assign-me")]
    [Authorize]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<IActionResult> AssignToMe(Guid id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(new { message = "Không xác định được người dùng hiện tại." });
        }

        var ticket = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (ticket == null) return NotFound();

        var st = NormStatus(ticket.Status);
        var asg = NormAssign(ticket.AssignmentState);

        if (st is "Closed" or "Completed")
        {
            return BadRequest(new { message = "Ticket đã khoá, không thể nhận thêm." });
        }
        if (ticket.AssigneeId.HasValue)
        {
            return BadRequest(new { message = "Ticket đã có người xử lý, không thể nhận thêm." });
        }

        // Validate staff: Active + Role.Code chứa "care"
        var me = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u =>
                u.UserId == currentUserId &&
                ((u.Status ?? "Active") == "Active") &&
                u.Roles.Any(r => (r.Code ?? string.Empty).ToLower().Contains("care")));

        if (me == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bạn không có quyền nhận ticket này." });
        }

        var before = new
        {
            ticket.TicketId,
            ticket.AssigneeId,
            AssignmentState = ticket.AssignmentState,
            Status = ticket.Status
        };

        ticket.AssigneeId = currentUserId;

        if (asg == "Unassigned")
        {
            ticket.AssignmentState = "Assigned";
        }

        if (st == "New")
        {
            ticket.Status = "InProgress";
        }

        ticket.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var after = new
        {
            ticket.TicketId,
            ticket.AssigneeId,
            AssignmentState = ticket.AssignmentState,
            Status = ticket.Status
        };

        // 🔐 AUDIT LOG – ASSIGN TO ME
        await _auditLogger.LogAsync(
            HttpContext,
            action: "AssignToMe",
            entityType: "Ticket",
            entityId: ticket.TicketId.ToString(),
            before: before,
            after: after
        );

        return NoContent();
    }

    [HttpPost("{id:guid}/transfer-tech")]
    [Authorize]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<IActionResult> TransferToTech(Guid id, [FromBody] AssignTicketDto dto)
    {
        // ✅ NEW: chỉ assignee hoặc admin mới được transfer
        var meStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meStr, out var meId))
            return Unauthorized();

        var actor = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == meId);

        if (actor is null)
            return Unauthorized();

        if ((actor.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsStaffOrAdmin(actor))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bạn không có quyền truy cập chức năng này." });
        }

        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        if (!IsAdmin(actor))
        {
            var isAssignee = t.AssigneeId.HasValue && t.AssigneeId.Value == actor.UserId;
            if (!isAssignee)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Người dùng không có quyền hạn để chuyển ticket." });
            }
        }

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

        var before = new
        {
            t.TicketId,
            t.AssigneeId,
            AssignmentState = t.AssignmentState,
            Status = t.Status
        };

        if (asg != "Technical") t.AssignmentState = "Technical";
        if (st == "New") t.Status = "InProgress";

        t.AssigneeId = dto.AssigneeId;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var after = new
        {
            t.TicketId,
            t.AssigneeId,
            AssignmentState = t.AssignmentState,
            Status = t.Status
        };

        // 🔐 AUDIT LOG – TRANSFER TO TECH
        await _auditLogger.LogAsync(
            HttpContext,
            action: "TransferToTech",
            entityType: "Ticket",
            entityId: t.TicketId.ToString(),
            before: before,
            after: after
        );
        // ✅ System notification: chuyển ticket -> notify nhân viên được chuyển tới (best-effort)
        try
        {
            var actorName = actor.FullName ?? "(unknown)";
            var actorEmail = actor.Email ?? "(unknown)";
            var ticketCode = t.TicketCode ?? t.TicketId.ToString();
            var origin = PublicUrlHelper.GetPublicOrigin(HttpContext, _config);
            var relatedUrl = $"{origin}/staff/tickets/{id}";
            await _notificationSystemService.CreateForUserIdsAsync(new SystemNotificationCreateRequest
            {
                Title = "Ticket đã được chuyển cho bạn",
                Message =
                    $"Nhân viên {actorName} đã chuyển ticket cho bạn.\n" +
                    $"- Mã ticket: {ticketCode}\n" +
                    $"- Nội dung: {t.Subject ?? ""}",
                Severity = 0, // Info
                CreatedByUserId = actor.UserId,
                CreatedByEmail = actorEmail,
                Type = "Ticket.Transferred",

                RelatedEntityType = "Ticket",
                RelatedEntityId = t.TicketId.ToString(),

                // ✅ đổi theo route FE staff ticket detail của bạn
                RelatedUrl = relatedUrl,

                TargetUserIds = new List<Guid> { dto.AssigneeId }
            });
        }
        catch { }

        return NoContent();
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<IActionResult> Complete(Guid id)
    {
        // ✅ NEW: chỉ assignee hoặc admin mới được complete
        var meStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meStr, out var meId))
            return Unauthorized();

        var actor = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == meId);

        if (actor is null)
            return Unauthorized();

        if ((actor.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsStaffOrAdmin(actor))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bạn không có quyền truy cập chức năng này." });
        }

        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        if (!IsAdmin(actor))
        {
            var isAssignee = t.AssigneeId.HasValue && t.AssigneeId.Value == actor.UserId;
            if (!isAssignee)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Người dùng không có quyền hạn để hoàn thành ticket." });
            }
        }

        var st = NormStatus(t.Status);
        if (st is "Closed" or "Completed")
            return BadRequest(new { message = "Ticket đã khoá." });
        if (st != "InProgress")
            return BadRequest(new { message = "Chỉ hoàn thành khi trạng thái Đang xử lý." });

        var before = new
        {
            t.TicketId,
            t.Status,
            t.SlaStatus,
            t.ResolvedAt
        };

        var now = DateTime.UtcNow;

        t.Status = "Completed";

        // SLA: đánh dấu thời điểm giải quyết nếu chưa có
        if (!t.ResolvedAt.HasValue)
            t.ResolvedAt = now;

        t.UpdatedAt = now;

        // Cập nhật SlaStatus (OK / Warning / Overdue)
        TicketSlaHelper.UpdateSlaStatus(t, now);

        await _db.SaveChangesAsync();

        var after = new
        {
            t.TicketId,
            t.Status,
            t.SlaStatus,
            t.ResolvedAt
        };

        // 🔐 AUDIT LOG – COMPLETE TICKET
        await _auditLogger.LogAsync(
            HttpContext,
            action: "CompleteTicket",
            entityType: "Ticket",
            entityId: t.TicketId.ToString(),
            before: before,
            after: after
        );

        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [Authorize]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<IActionResult> Close(Guid id)
    {
        // ✅ NEW: chỉ Admin mới được close (tránh staff/customer tự đóng ticket)
        var meStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meStr, out var meId))
            return Unauthorized();

        var actor = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == meId);

        if (actor is null)
            return Unauthorized();

        if ((actor.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsAdmin(actor))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bạn không có quyền đóng ticket." });
        }

        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id);
        if (t == null) return NotFound();

        var st = NormStatus(t.Status);
        if (st is "Closed" or "Completed")
            return BadRequest(new { message = "Ticket đã khoá." });
        if (st != "New")
            return BadRequest(new { message = "Chỉ đóng khi trạng thái Mới." });

        var before = new
        {
            t.TicketId,
            t.Status,
            t.SlaStatus,
            t.ResolvedAt
        };

        var now = DateTime.UtcNow;

        t.Status = "Closed";

        // SLA: đóng ticket cũng xem như đã giải quyết
        if (!t.ResolvedAt.HasValue)
            t.ResolvedAt = now;

        t.UpdatedAt = now;

        TicketSlaHelper.UpdateSlaStatus(t, now);

        await _db.SaveChangesAsync();

        var after = new
        {
            t.TicketId,
            t.Status,
            t.SlaStatus,
            t.ResolvedAt
        };

        // 🔐 AUDIT LOG – CLOSE TICKET
        await _auditLogger.LogAsync(
            HttpContext,
            action: "CloseTicket",
            entityType: "Ticket",
            entityId: t.TicketId.ToString(),
            before: before,
            after: after
        );

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
    [Authorize]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<ActionResult<List<StaffMiniDto>>> GetAssignableStaff(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        // ✅ NEW: chỉ Staff/Admin mới được xem danh sách assignees
        var meStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meStr, out var meId))
            return Unauthorized();

        var actor = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == meId);

        if (actor is null)
            return Unauthorized();

        if ((actor.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsStaffOrAdmin(actor))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bạn không có quyền truy cập chức năng này." });
        }

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
    [Authorize]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
    public async Task<ActionResult<List<StaffMiniDto>>> GetTransferAssignees(
        [FromQuery] Guid? excludeUserId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        // ✅ NEW: chỉ Staff/Admin mới được xem danh sách transfer assignees
        var meStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meStr, out var meId))
            return Unauthorized();

        var actor = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == meId);

        if (actor is null)
            return Unauthorized();

        if ((actor.Status ?? "Active") != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Tài khoản đã bị khoá." });
        }

        if (!IsStaffOrAdmin(actor))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Bạn không có quyền truy cập chức năng này." });
        }

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
