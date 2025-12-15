// File: Controllers/ProductAccountController.cs
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using static Keytietkiem.Constants.ModuleCodes;
using static Keytietkiem.Constants.PermissionCodes;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductAccountController : ControllerBase
{
    private readonly IProductAccountService _productAccountService;
    private readonly IAuditLogger _auditLogger;

    public ProductAccountController(
        IProductAccountService productAccountService,
        IAuditLogger auditLogger)
    {
        _productAccountService = productAccountService;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Get paginated list of product accounts with filters
    /// </summary>
    [HttpGet]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.VIEW_LIST)]
    public async Task<IActionResult> GetList([FromQuery] ProductAccountFilterDto filterDto)
    {
        var response = await _productAccountService.GetListAsync(filterDto);
        return Ok(response);
    }

    /// <summary>
    /// Get a single product account by ID (password masked)
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.VIEW_DETAIL)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _productAccountService.GetByIdAsync(id, includePassword: false);
        return Ok(response);
    }

    /// <summary>
    /// Get decrypted password for a product account (requires authorization)
    /// </summary>
    [HttpGet("{id}/password")]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.VIEW_DETAIL)]
    public async Task<IActionResult> GetPassword(Guid id)
    {
        // Nếu có lỗi sẽ bubble lên 500 – không audit lỗi để tránh spam,
        // chỉ audit thành công vì lý do bảo mật.
        var password = await _productAccountService.GetDecryptedPasswordAsync(id);

        await _auditLogger.LogAsync(
            HttpContext,
            action: "GetProductAccountPassword",
            entityType: "ProductAccount",
            entityId: id.ToString(),
            before: null,
            after: new
            {
                ProductAccountId = id,
                Operation = "GetPassword",
                RetrievedPassword = true
            });

        return Ok(new { password });
    }

    /// <summary>
    /// Create a new product account
    /// </summary>
    [HttpPost]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.CREATE)]
    public async Task<IActionResult> Create([FromBody] CreateProductAccountDto createDto)
    {
        try
        {
            var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);

            var exist =
                await _productAccountService.CheckAccountEmailOrUsernameExists(
                    createDto.VariantId,
                    createDto.AccountEmail,
                    createDto.AccountUsername);

            // Nếu trùng email/username → trả 400 nhưng không audit (tránh spam)
            if (exist.Item1 != null && exist.Item2)
            {
                return BadRequest(new { message = "Tên đăng nhập hoặc email đã tồn tại" });
            }

            var response = await _productAccountService.CreateAsync(createDto, accountId);

            // Log success (CUD)
            await _auditLogger.LogAsync(
                HttpContext,
                action: "CreateProductAccount",
                entityType: "ProductAccount",
                entityId: response.ProductAccountId.ToString(),
                before: null,
                after: response);

            return CreatedAtAction(nameof(GetById), new { id = response.ProductAccountId }, response);
        }
        catch (ValidationException ex)
        {
            // Validation 4xx có thể xảy ra rất nhiều → không audit để tránh spam
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing product account
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.EDIT)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductAccountDto updateDto)
    {
        if (id != updateDto.ProductAccountId)
        {
            // Id mismatch là lỗi 4xx, không quan trọng về bảo mật → không audit
            return BadRequest("ID không khớp");
        }

        try
        {
            var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Get the existing account to retrieve VariantId for validation
            var existingAccount = await _productAccountService.GetByIdAsync(id, includePassword: false);

            var exist =
                await _productAccountService.CheckAccountEmailOrUsernameExists(
                    existingAccount.VariantId,
                    updateDto.AccountEmail,
                    updateDto.AccountUsername);

            // Trùng email/username → trả 400, không audit
            if (exist.Item1 != id && exist.Item2)
            {
                return BadRequest(new { message = "Tên đăng nhập hoặc email đã tồn tại" });
            }

            var response = await _productAccountService.UpdateAsync(updateDto, accountId, userId);

            // Log success (CUD)
            await _auditLogger.LogAsync(
                HttpContext,
                action: "UpdateProductAccount",
                entityType: "ProductAccount",
                entityId: id.ToString(),
                before: existingAccount,
                after: response);

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            // Validation 4xx → không audit
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a product account
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.DELETE)]
    public async Task<IActionResult> Delete(Guid id)
    {
        // Nếu có lỗi khi DeleteAsync sẽ bubble lên (500/4xx tuỳ service),
        // không audit lỗi theo yêu cầu.
        await _productAccountService.DeleteAsync(id);

        // Log success – quan trọng cho trace xoá tài khoản
        await _auditLogger.LogAsync(
            HttpContext,
            action: "DeleteProductAccount",
            entityType: "ProductAccount",
            entityId: id.ToString(),
            before: null,
            after: new
            {
                ProductAccountId = id,
                Deleted = true
            });

        return Ok(new { message = "Xóa tài khoản sản phẩm thành công" });
    }

    /// <summary>
    /// Add a customer to a product account
    /// </summary>
    [HttpPost("{id}/customers")]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.EDIT)]
    public async Task<IActionResult> AddCustomer(Guid id, [FromBody] AddCustomerToAccountDto addDto)
    {
        if (id != addDto.ProductAccountId)
        {
            // Id mismatch 4xx – không audit
            return BadRequest("ID không khớp");
        }

        var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
        var response = await _productAccountService.AddCustomerAsync(addDto, accountId);

        // Log success – quan trọng vì thay đổi quyền truy cập account
        await _auditLogger.LogAsync(
            HttpContext,
            action: "AddCustomerToProductAccount",
            entityType: "ProductAccountCustomer",
            entityId: addDto.ProductAccountId.ToString(),
            before: new
            {
                addDto.ProductAccountId,
                addDto.UserId
            },
            after: response);

        return Ok(response);
    }

    /// <summary>
    /// Remove a customer from a product account
    /// </summary>
    [HttpPost("{id}/customers/remove")]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.EDIT)]
    public async Task<IActionResult> RemoveCustomer(Guid id, [FromBody] RemoveCustomerFromAccountDto removeDto)
    {
        if (id != removeDto.ProductAccountId)
        {
            // Id mismatch 4xx – không audit
            return BadRequest("ID không khớp");
        }

        var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
        await _productAccountService.RemoveCustomerAsync(removeDto, accountId);

        // Log success – thay đổi quyền truy cập account, nên giữ
        await _auditLogger.LogAsync(
            HttpContext,
            action: "RemoveCustomerFromProductAccount",
            entityType: "ProductAccountCustomer",
            entityId: removeDto.ProductAccountId.ToString(),
            before: new
            {
                removeDto.ProductAccountId,
                removeDto.UserId
            },
            after: new
            {
                removeDto.ProductAccountId,
                removeDto.UserId,
                Removed = true
            });

        return Ok(new { message = "Xóa khách hàng khỏi tài khoản thành công" });
    }

    /// <summary>
    /// Get history of a product account
    /// </summary>
    [HttpGet("{id}/history")]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.VIEW_DETAIL)]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        var response = await _productAccountService.GetHistoryAsync(id);
        return Ok(response);
    }

    /// <summary>
    /// Extend expiry date of a product account
    /// </summary>
    [HttpPost("{id}/extend-expiry")]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.EDIT)]
    public async Task<IActionResult> ExtendExpiryDate(Guid id, [FromBody] ExtendExpiryDateDto extendDto)
    {
        if (id != extendDto.ProductAccountId)
        {
            // Id mismatch 4xx – không audit để tránh rác
            return BadRequest(new { message = "ID không khớp" });
        }

        try
        {
            var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
            var response = await _productAccountService.ExtendExpiryDateAsync(extendDto, accountId);

            // Log success – thay đổi hạn account, nên giữ
            await _auditLogger.LogAsync(
                HttpContext,
                action: "ExtendProductAccountExpiry",
                entityType: "ProductAccount",
                entityId: extendDto.ProductAccountId.ToString(),
                before: new
                {
                    extendDto.ProductAccountId
                },
                after: response);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            // 404 business – có thể xảy ra nhiều, không audit
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // 400 business / lỗi khác – không audit để tránh spam
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get product accounts expiring within specified days (default 5)
    /// </summary>
    [HttpGet("expiring-soon")]
    [RequirePermission(ModuleCodes.PRODUCT_ACCOUNT, PermissionCodes.VIEW_LIST)]
    public async Task<IActionResult> GetAccountsExpiringSoon([FromQuery] int days = 5)
    {
        try
        {
            var response = await _productAccountService.GetAccountsExpiringSoonAsync(days);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
