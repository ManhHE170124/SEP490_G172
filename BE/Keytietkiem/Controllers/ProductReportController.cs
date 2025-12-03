using System.Security.Claims;
using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductReportController : ControllerBase
{
    private readonly IProductReportService _productReportService;

    public ProductReportController(IProductReportService productReportService)
    {
        _productReportService = productReportService ?? throw new ArgumentNullException(nameof(productReportService));
    }

    /// <summary>
    /// Get all product reports with pagination and filtering
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10)</param>
    /// <param name="status">Optional status filter (Pending, Processing, Resolved)</param>
    /// <param name="userId">Optional user ID filter (for getting user's own reports)</param>
    [HttpGet]
    public async Task<IActionResult> GetAllProductReports(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] Guid? userId = null)
    {
        if (pageNumber < 1)
            return BadRequest(new { message = "Số trang phải lớn hơn 0" });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Kích thước trang phải từ 1 đến 100" });

        var result = await _productReportService.GetAllProductReportsAsync(
            pageNumber,
            pageSize,
            status,
            userId);

        return Ok(result);
    }

    /// <summary>
    /// Get product report by ID
    /// </summary>
    /// <param name="id">Product report ID</param>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProductReportById(Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest(new { message = "ID báo cáo không hợp lệ" });

        var report = await _productReportService.GetProductReportByIdAsync(id);
        return Ok(report);
    }

    /// <summary>
    /// Create a new product report
    /// </summary>
    /// <param name="dto">Product report creation data</param>
    [HttpPost]
    public async Task<IActionResult> CreateProductReport([FromBody] CreateProductReportDto dto)
    {
        var report = await _productReportService.CreateProductReportAsync(dto, dto.UserId.Value);
        return CreatedAtAction(nameof(GetProductReportById), new { id = report.Id }, report);
    }

    /// <summary>
    /// Update product report status (Admin/Staff only)
    /// </summary>
    /// <param name="id">Product report ID</param>
    /// <param name="dto">Product report update data</param>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin,Support Staff")]
    public async Task<IActionResult> UpdateProductReportStatus(Guid id, [FromBody] UpdateProductReportDto dto)
    {
        if (id != dto.Id)
            return BadRequest(new { message = "ID trong URL và body không khớp" });

        var actorId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var actorEmail = User.FindFirst(ClaimTypes.Email)!.Value;

        var report = await _productReportService.UpdateProductReportStatusAsync(dto, actorId, actorEmail);
        return Ok(report);
    }

    /// <summary>
    /// Get current user's product reports
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10)</param>
    /// <param name="status">Optional status filter</param>
    [HttpGet("my-reports")]
    public async Task<IActionResult> GetMyProductReports(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null)
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
            userId);

        return Ok(result);
    }

    /// <summary>
    /// Get key error reports with pagination (reports with ProductKeyId)
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10)</param>
    [HttpGet("key-errors")]
    [Authorize(Roles = "Admin,Support Staff")]
    public async Task<IActionResult> GetKeyErrors(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1)
            return BadRequest(new { message = "Số trang phải lớn hơn 0" });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Kích thước trang phải từ 1 đến 100" });

        var result = await _productReportService.GetKeyErrorsAsync(pageNumber, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Get account error reports with pagination (reports with ProductAccountId)
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10)</param>
    [HttpGet("account-errors")]
    [Authorize(Roles = "Admin,Support Staff")]
    public async Task<IActionResult> GetAccountErrors(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        if (pageNumber < 1)
            return BadRequest(new { message = "Số trang phải lớn hơn 0" });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Kích thước trang phải từ 1 đến 100" });

        var result = await _productReportService.GetAccountErrorsAsync(pageNumber, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Get total count of key error reports
    /// </summary>
    [HttpGet("key-errors/count")]
    [Authorize(Roles = "Admin,Support Staff")]
    public async Task<IActionResult> CountKeyErrors()
    {
        var count = await _productReportService.CountKeyErrorsAsync();
        return Ok(new { count });
    }

    /// <summary>
    /// Get total count of account error reports
    /// </summary>
    [HttpGet("account-errors/count")]
    [Authorize(Roles = "Admin,Support Staff")]
    public async Task<IActionResult> CountAccountErrors()
    {
        var count = await _productReportService.CountAccountErrorsAsync();
        return Ok(new { count });
    }
}
