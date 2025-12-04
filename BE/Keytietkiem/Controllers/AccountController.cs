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
        try
        {
            var isExist = await _accountService.IsEmailExistsAsync(dto.Email);
            if (isExist)
            {
                return BadRequest(new { message = "Email đã được sử dụng" });
            }
            var response = await _accountService.SendOtpAsync(dto);
            return Ok(new { message = response });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi gửi OTP", detail = ex.Message });
        }
    }

    [HttpPost]
    [Route("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpDto dto)
    {
        try
        {
            var response = await _accountService.VerifyOtpAsync(dto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi xác thực OTP", detail = ex.Message });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        try
        {
            var response = await _accountService.RegisterAsync(dto);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi đăng ký tài khoản", detail = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        try
        {
            var response = await _accountService.LoginAsync(dto);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi đăng nhập", detail = ex.Message });
        }
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
        try
        {
            var response = await _accountService.ForgotPasswordAsync(dto);
            return Ok(new { message = response });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi gửi yêu cầu đặt lại mật khẩu", detail = ex.Message });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            await _accountService.ResetPasswordAsync(dto);
            return Ok(new { message = "Đặt lại mật khẩu thành công" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi đặt lại mật khẩu", detail = ex.Message });
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        try
        {
            var response = await _accountService.RefreshTokenAsync(dto);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi làm mới token", detail = ex.Message });
        }
    }

    [HttpPost("revoke-token")]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenDto dto)
    {
        try
        {
            await _accountService.RevokeTokenAsync(dto);
            return Ok(new { message = "Đăng xuất thành công" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi đăng xuất", detail = ex.Message });
        }
    }
}
