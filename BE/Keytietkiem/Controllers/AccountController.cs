using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)                              
    {
        _accountService = accountService;
    }

    [HttpPost]
    [Route("send-otp")]
    public async Task<IActionResult> SendOtp(SendOtpDto dto)
    {
        var isExist = await _accountService.IsEmailExistsAsync(dto.Email);
        if (isExist)
        {
            return BadRequest("Email đã được sử dụng");
        }
        var response = await _accountService.SendOtpAsync(dto);
        return Ok(response);
    }

    [HttpPost]
    [Route("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpDto dto)
    {
        var response = await _accountService.VerifyOtpAsync(dto);
        return Ok(response);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)                 
    {
        var response = await _accountService.RegisterAsync(dto);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)                       
    {
        var response = await _accountService.LoginAsync(dto);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody]
        ChangePasswordDto dto)
    {
        var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
        try
        {
            await _accountService.ChangePasswordAsync(accountId, dto);
            return Ok(new { message = "Đổi mật khẩu thành công" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("profile")]
    [HttpGet("~/api/admin/account/profile")]
    [HttpGet("~/api/customer/account/profile")]
    public async Task<IActionResult> GetProfile()
    {
        var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
        try
        {
            var profile = await _accountService.GetProfileAsync(accountId);
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("profile")]
    [HttpPut("~/api/admin/account/profile")]
    [HttpPut("~/api/customer/account/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateAccountProfileDto dto)
    {
        var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
        try
        {
            var profile = await _accountService.UpdateProfileAsync(accountId, dto);
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var response = await _accountService.ForgotPasswordAsync(dto);
        return Ok(new { message = response });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        await _accountService.ResetPasswordAsync(dto);
        return Ok(new { message = "Đặt lại mật khẩu thành công" });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        var response = await _accountService.RefreshTokenAsync(dto);
        return Ok(response);
    }

    [HttpPost("revoke-token")]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenDto dto)
    {
        await _accountService.RevokeTokenAsync(dto);
        return Ok(new { message = "Đăng xuất thành công" });
    }
}
