using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Keytietkiem.Utils; // ✅ NEW
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic; // ✅ NEW
using System.Security.Claims;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductReportController : ControllerBase
{
    private readonly IProductReportService _productReportService;
    private readonly IAuditLogger _auditLogger;
    private readonly INotificationSystemService _notificationSystemService;
    private readonly IConfiguration _config;

    public ProductReportController(
        IProductReportService productReportService,
        IAuditLogger auditLogger,
        INotificationSystemService notificationSystemService, IConfiguration config)
    {
        _productReportService = productReportService ?? throw new ArgumentNullException(nameof(productReportService));
        _auditLogger = auditLogger;
        _notificationSystemService = notificationSystemService ?? throw new ArgumentNullException(nameof(notificationSystemService));
        _config = config;
    }

    [HttpGet]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> GetAllProductReports(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? searchTerm = null)
    {
        if (pageNumber < 1)
            return BadRequest(new { message = "Số trang phải lớn hơn 0" });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Kích thước trang phải từ 1 đến 100" });

        var result = await _productReportService.GetAllProductReportsAsync(
            pageNumber,
            pageSize,
            status,
            userId,
            searchTerm);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> GetProductReportById(Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest(new { message = "ID báo cáo không hợp lệ" });

        var report = await _productReportService.GetProductReportByIdAsync(id);
        return Ok(report);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProductReport([FromBody] CreateProductReportDto dto)
    {
        try
        {
            var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "(unknown)";

            // ✅ Ưu tiên username/display name (ClaimTypes.Name) -> fallback email
            var actorDisplayName = (User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(actorDisplayName))
                actorDisplayName = actorEmail;

            var report = await _productReportService.CreateProductReportAsync(dto, dto.UserId!.Value);

            await _auditLogger.LogAsync(
                HttpContext,
                action: "CreateProductReport",
                entityType: "ProductReport",
                entityId: report.Id.ToString(),
                before: null,
                after: report
            );

            try
            {
                static string StatusVi(string? s)
                {
                    var v = (s ?? "").Trim();
                    return v.Equals("Pending", StringComparison.OrdinalIgnoreCase) ? "Chờ xử lý"
                         : v.Equals("Processing", StringComparison.OrdinalIgnoreCase) ? "Đang xử lý"
                         : v.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ? "Đã giải quyết"
                         : v.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ? "Từ chối"
                         : string.IsNullOrWhiteSpace(v) ? "—" : v;
                }

                var origin = PublicUrlHelper.GetPublicOrigin(HttpContext, _config);
                var relatedUrl = $"{origin}/reports/{report.Id}"; // route FE: /reports/:id

                await _notificationSystemService.CreateForRoleCodesAsync(new SystemNotificationCreateRequest
                {
                    Title = "Có báo cáo lỗi sản phẩm mới",
                    Message =
                        $"Nhân viên tạo: {actorDisplayName}\n" +
                        $"Mã báo cáo: {report.Id}\n" +
                        $"Trạng thái: {StatusVi(report.Status)}\n" +
                        $"Thời gian tạo: {DateTime.UtcNow:dd/MM/yyyy HH:mm} (UTC)\n" +
                        $"Vui lòng vào mục \"Báo cáo Sản phẩm\" để kiểm tra và xử lý.",
                    Severity = 2, // Warning
                    CreatedByUserId = actorId,
                    CreatedByEmail = actorEmail,
                    Type = "Product.ReportCreated",

                    RelatedEntityType = "ProductReport",
                    RelatedEntityId = report.Id.ToString(),
                    RelatedUrl = relatedUrl,

                    TargetRoleCodes = new List<string> { RoleCodes.STORAGE_STAFF }
                });
            }
            catch { }

            return CreatedAtAction(nameof(GetProductReportById), new { id = report.Id }, report);
        }
        catch (Exception)
        {
            // Giữ nguyên behavior: exception -> 500, không log để tránh spam
            throw;
        }
    }

    [HttpPatch("{id:guid}/status")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> UpdateProductReportStatus(Guid id, [FromBody] UpdateProductReportDto dto)
    {
        if (id != dto.Id)
        {
            return BadRequest(new { message = "ID trong URL và body không khớp" });
        }

        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        try
        {
            var report = await _productReportService.UpdateProductReportStatusAsync(dto, actorId, actorEmail);

            await _auditLogger.LogAsync(
                HttpContext,
                action: "UpdateStatus",
                entityType: "ProductReport",
                entityId: report.Id.ToString(),
                before: null,
                after: report
            );

            return Ok(report);
        }
        catch (Exception)
        {
            throw;
        }
    }

    [HttpGet("my-reports")]
    public async Task<IActionResult> GetMyProductReports(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null)
    {
        if (pageNumber < 1)
            return BadRequest(new { message = "Số trang phải lớn hơn 0" });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Kích thước trang phải từ 1 đến 100" });

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var result = await _productReportService.GetAllProductReportsAsync(
            pageNumber,
            pageSize,
            status,
            userId,
            searchTerm);

        return Ok(result);
    }

    [HttpGet("key-errors")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> GetKeyErrors(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null)
    {
        if (pageNumber < 1)
            return BadRequest(new { message = "Số trang phải lớn hơn 0" });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Kích thước trang phải từ 1 đến 100" });

        var result = await _productReportService.GetKeyErrorsAsync(pageNumber, pageSize, searchTerm);
        return Ok(result);
    }

    [HttpGet("account-errors")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> GetAccountErrors(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null)
    {
        if (pageNumber < 1)
            return BadRequest(new { message = "Số trang phải lớn hơn 0" });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Kích thước trang phải từ 1 đến 100" });

        var result = await _productReportService.GetAccountErrorsAsync(pageNumber, pageSize, searchTerm);
        return Ok(result);
    }

    [HttpGet("key-errors/count")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> CountKeyErrors()
    {
        var count = await _productReportService.CountKeyErrorsAsync();
        return Ok(new { count });
    }

    [HttpGet("account-errors/count")]
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE, RoleCodes.STORAGE_STAFF)]
    public async Task<IActionResult> CountAccountErrors()
    {
        var count = await _productReportService.CountAccountErrorsAsync();
        return Ok(new { count });
    }
}
