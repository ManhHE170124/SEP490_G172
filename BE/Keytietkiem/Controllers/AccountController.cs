using Keytietkiem.DTOs;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IAuditLogger _auditLogger;

    public AccountController(IAccountService accountService, IAuditLogger auditLogger)
    {
        _accountService = accountService;
        _auditLogger = auditLogger;
    }

    // ========= OTP & Auth flow cơ bản (không audit vì dễ spam) =========

    [HttpPost]
    [Route("send-otp")]
    public async Task<IActionResult> SendOtp(SendOtpDto dto)
    {
        try
        {
            var isExist = await _accountService.IsEmailExistsAsync(dto.Email);
            if (isExist)
            {
                // Không ghi audit log cho SendOtp (dễ spam, không quá cần cho audit)
                return BadRequest(new { message = "Email đã được sử dụng" });
            }

            var response = await _accountService.SendOtpAsync(dto);

            // Không ghi audit log cho SendOtp
            return Ok(new { message = response });
        }
        catch (InvalidOperationException ex)
        {
            // Không ghi audit log cho SendOtp
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Không ghi audit log cho SendOtp
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

            // Không ghi audit log cho VerifyOtp
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // Không ghi audit log cho VerifyOtp
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Không ghi audit log cho VerifyOtp
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi xác thực OTP", detail = ex.Message });
        }
    }

    // ========= Register / Login =========

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        try
        {
            var response = await _accountService.RegisterAsync(dto);

            // Audit: đăng ký tài khoản mới (không log password)
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Register",
                entityType: "Account",
                entityId: null,
                before: null,
                after: null // tránh serialize full dto vì có password
            );

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Không audit lỗi
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Không audit lỗi
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Không audit lỗi
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi đăng ký tài khoản", detail = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        try
        {
            var response = await _accountService.LoginAsync(dto);

            // Audit: login thành công (không log password/credential)
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Login",
                entityType: "Account",
                entityId: null,
                before: null,
                after: null
            );

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Không audit lỗi login
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Không audit lỗi login
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Không audit lỗi login
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi đăng nhập", detail = ex.Message });
        }
    }

    // ========= Change password =========

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var accountId = Guid.Parse(User.FindFirst("AccountId")!.Value);
        try
        {
            await _accountService.ChangePasswordAsync(accountId, dto);

            // Audit: đổi mật khẩu (không log password)
            await _auditLogger.LogAsync(
                HttpContext,
                action: "ChangePassword",
                entityType: "Account",
                entityId: accountId.ToString(),
                before: null,
                after: null
            );

            return Ok(new { message = "Đổi mật khẩu thành công" });
        }
        catch (UnauthorizedAccessException ex)
        {
            // Không audit lỗi
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Không audit lỗi
            return BadRequest(new { message = ex.Message });
        }
    }

    // ========= Profile (GET/PUT) =========

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
            // Không audit GET profile (GET thường xuyên)
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

            // Audit: cập nhật thông tin tài khoản
            await _auditLogger.LogAsync(
                HttpContext,
                action: "UpdateProfile",
                entityType: "Account",
                entityId: accountId.ToString(),
                before: null,
                after: null
            );

            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            // Không audit lỗi
            return NotFound(new { message = ex.Message });
        }
    }

    // ========= Forgot / Reset password =========

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            var response = await _accountService.ForgotPasswordAsync(dto);

            // Không audit forgot-password (dễ spam)
            return Ok(new { message = response });
        }
        catch (InvalidOperationException ex)
        {
            // Không audit lỗi
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Không audit lỗi
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi gửi yêu cầu đặt lại mật khẩu", detail = ex.Message });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            await _accountService.ResetPasswordAsync(dto);

            // Audit: reset password thành công (từ OTP/link)
            await _auditLogger.LogAsync(
                HttpContext,
                action: "ResetPassword",
                entityType: "Account",
                entityId: null,
                before: null,
                after: null
            );

            return Ok(new { message = "Đặt lại mật khẩu thành công" });
        }
        catch (UnauthorizedAccessException ex)
        {
            // Không audit lỗi
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Không audit lỗi
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Không audit lỗi
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi đặt lại mật khẩu", detail = ex.Message });
        }
    }

    // ========= Token (refresh / revoke) =========

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        try
        {
            var response = await _accountService.RefreshTokenAsync(dto);

            // Không audit refresh-token (dễ spam, không critical như login)
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Không audit lỗi
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Không audit lỗi
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Không audit lỗi
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi làm mới token", detail = ex.Message });
        }
    }

    [HttpPost("revoke-token")]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenDto dto)
    {
        try
        {
            await _accountService.RevokeTokenAsync(dto);

            // Audit: revoke token (logout) là hành vi bảo mật nên vẫn log
            await _auditLogger.LogAsync(
                HttpContext,
                action: "RevokeToken",
                entityType: "Account",
                entityId: null,
                before: null,
                after: null
            );

            return Ok(new { message = "Đăng xuất thành công" });
        }
        catch (Exception ex)
        {
            // Không audit lỗi
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi đăng xuất", detail = ex.Message });
        }
    }
}
