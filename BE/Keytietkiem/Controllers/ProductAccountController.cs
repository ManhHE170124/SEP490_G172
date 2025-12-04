using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Storage Staff,Admin")]
public class ProductAccountController : ControllerBase
{
    private readonly IProductAccountService _productAccountService;

    public ProductAccountController(IProductAccountService productAccountService)
    {
        _productAccountService = productAccountService;
    }

    /// <summary>
    /// Get paginated list of product accounts with filters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ProductAccountFilterDto filterDto)
    {
        var response = await _productAccountService.GetListAsync(filterDto);
        return Ok(response);
    }

    /// <summary>
    /// Get a single product account by ID (password masked)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _productAccountService.GetByIdAsync(id, includePassword: false);
        return Ok(response);
    }

    /// <summary>
    /// Get decrypted password for a product account (requires authorization)
    /// </summary>
    [HttpGet("{id}/password")]
    public async Task<IActionResult> GetPassword(Guid id)
    {
        var password = await _productAccountService.GetDecryptedPasswordAsync(id);
        return Ok(new { password });
    }

    /// <summary>
    /// Create a new product account
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductAccountDto createDto)
    {
        try
        {
            var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
            var exist =
                await _productAccountService.CheckAccountEmailOrUsernameExists(createDto.VariantId, createDto.AccountEmail,
                    createDto.AccountUsername);
            if (exist.Item1 != null && exist.Item2)
            {
                return BadRequest(new { message = "Tên đăng nhập hoặc email đã tồn tại" });
            }
            var response = await _productAccountService.CreateAsync(createDto, accountId);
            return CreatedAtAction(nameof(GetById), new { id = response.ProductAccountId }, response);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing product account
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductAccountDto updateDto)
    {
        if (id != updateDto.ProductAccountId)
        {
            return BadRequest("ID không khớp");
        }

        try
        {
            var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Get the existing account to retrieve VariantId for validation
            var existingAccount = await _productAccountService.GetByIdAsync(id, includePassword: false);

            var exist =
                await _productAccountService.CheckAccountEmailOrUsernameExists(existingAccount.VariantId, updateDto.AccountEmail,
                    updateDto.AccountUsername);
            if (exist.Item1 != id && exist.Item2)
            {
                return BadRequest(new { message = "Tên đăng nhập hoặc email đã tồn tại" });
            }
            var response = await _productAccountService.UpdateAsync(updateDto, accountId, userId);
            return Ok(response);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a product account
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _productAccountService.DeleteAsync(id);
        return Ok(new { message = "Xóa tài khoản sản phẩm thành công" });
    }

    /// <summary>
    /// Add a customer to a product account
    /// </summary>
    [HttpPost("{id}/customers")]
    public async Task<IActionResult> AddCustomer(Guid id, [FromBody] AddCustomerToAccountDto addDto)
    {
        if (id != addDto.ProductAccountId)
        {
            return BadRequest("ID không khớp");
        }

        var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
        var response = await _productAccountService.AddCustomerAsync(addDto, accountId);
        return Ok(response);
    }

    /// <summary>
    /// Remove a customer from a product account
    /// </summary>
    [HttpPost("{id}/customers/remove")]
    public async Task<IActionResult> RemoveCustomer(Guid id, [FromBody] RemoveCustomerFromAccountDto removeDto)
    {
        if (id != removeDto.ProductAccountId)
        {
            return BadRequest("ID không khớp");
        }

        var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
        await _productAccountService.RemoveCustomerAsync(removeDto, accountId);
        return Ok(new { message = "Xóa khách hàng khỏi tài khoản thành công" });
    }

    /// <summary>
    /// Get history of a product account
    /// </summary>
    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        var response = await _productAccountService.GetHistoryAsync(id);
        return Ok(response);
    }

    /// <summary>
    /// Extend expiry date of a product account
    /// </summary>
    [HttpPost("{id}/extend-expiry")]
    public async Task<IActionResult> ExtendExpiryDate(Guid id, [FromBody] ExtendExpiryDateDto extendDto)
    {
        if (id != extendDto.ProductAccountId)
        {
            return BadRequest(new { message = "ID không khớp" });
        }

        try
        {
            var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
            var response = await _productAccountService.ExtendExpiryDateAsync(extendDto, accountId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get product accounts expiring within specified days (default 5)
    /// </summary>
    [HttpGet("expiring-soon")]
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
