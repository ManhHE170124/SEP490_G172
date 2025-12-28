using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
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

    public ProductReportController(
        IProductReportService productReportService,
        IAuditLogger auditLogger,
        INotificationSystemService notificationSystemService)
    {
        _productReportService = productReportService ?? throw new ArgumentNullException(nameof(productReportService));
        _auditLogger = auditLogger;
        _notificationSystemService = notificationSystemService ?? throw new ArgumentNullException(nameof(notificationSystemService));
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

            var report = await _productReportService.CreateProductReportAsync(dto, dto.UserId!.Value);

            await _auditLogger.LogAsync(
                HttpContext,
                action: "Create",
                entityType: "ProductReport",
                entityId: report.Id.ToString(),
                before: null,
                after: report
            );
            // ✅ System notification: creates report -> notify STORAGE_STAFF (best-effort)
            try
            {
                await _notificationSystemService.CreateForRoleCodesAsync(new SystemNotificationCreateRequest
                {
                    Title = "Có báo cáo lỗi sản phẩm mới",
                    Message =
                        $"Người dùng {actorEmail} đã tạo báo cáo lỗi sản phẩm.\n" +
                        $"- ReportId: {report.Id}\n" +
                        $"- Trạng thái: {report.Status}\n" +
                        $"- Thời gian: {DateTime.UtcNow:yyyy-MM-dd HH:mm} (UTC)\n" +
                        $"Vui lòng vào màn Báo cáo lỗi để kiểm tra và xử lý.",
                    Severity = 2, // Warning
                    CreatedByUserId = actorId,
                    CreatedByEmail = actorEmail,
                    Type = "Product.ReportCreated",

                    RelatedEntityType = "ProductReport",
                    RelatedEntityId = report.Id.ToString(),

                    // ✅ bạn đổi route FE cho đúng trang xử lý report
                    RelatedUrl = $"/admin/product-reports/{report.Id}",

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
    [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
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
