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
        try
        {
            var response = await _accountService.RegisterAsync(dto);

            // Lấy info user mới đăng ký từ LoginResponseDto
            var userInfo = response.User;

            // Override actor vì lúc này HttpContext.User chưa có claim
            HttpContext.Items["Audit:ActorId"] = userInfo.UserId;
            HttpContext.Items["Audit:ActorEmail"] = userInfo.Email;
            HttpContext.Items["Audit:ActorRole"] = userInfo.Roles?.FirstOrDefault() ?? "customer";

            // Audit: đăng ký tài khoản mới (không log password)
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Register",
                entityType: "Account",
                entityId: userInfo.AccountId.ToString(),
                before: null,
                after: new
                {
                    userInfo.UserId,
                    userInfo.AccountId,
                    userInfo.Email,
                    userInfo.FullName,
                    userInfo.Status,
                    Roles = userInfo.Roles
                }
            );

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

            var userInfo = response.User;

            // Override actor vì request login chưa có HttpContext.User
            HttpContext.Items["Audit:ActorId"] = userInfo.UserId;
            HttpContext.Items["Audit:ActorEmail"] = userInfo.Email;
            HttpContext.Items["Audit:ActorRole"] = userInfo.Roles?.FirstOrDefault();

            // Audit: login thành công (không log password/credential)
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Login",
                entityType: "Account",
                entityId: userInfo.AccountId.ToString(),
                before: null,
                after: new
                {
                    userInfo.UserId,
                    userInfo.AccountId,
                    userInfo.Email,
                    userInfo.FullName,
                    Roles = userInfo.Roles
                }
            );

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


    // ========= Change password =========

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
            // Lấy snapshot trước khi update
            var beforeProfile = await _accountService.GetProfileAsync(accountId);

            // Update
            var profile = await _accountService.UpdateProfileAsync(accountId, dto);

            // Audit: cập nhật thông tin tài khoản (chỉ log field profile, không log token)
            await _auditLogger.LogAsync(
                HttpContext,
                action: "UpdateProfile",
                entityType: "Account",
                entityId: accountId.ToString(),
                before: new
                {
                    beforeProfile.FullName,
                    beforeProfile.Phone,
                    beforeProfile.Address,
                    beforeProfile.AvatarUrl
                },
                after: new
                {
                    profile.FullName,
                    profile.Phone,
                    profile.Address,
                    profile.AvatarUrl
                }
            );

            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }


    // ========= Forgot / Reset password =========

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var response = await _accountService.ForgotPasswordAsync(dto);
        return Ok(new { message = response });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            // Lấy meta từ service (UserId, AccountId, Email)
            var result = await _accountService.ResetPasswordAsync(dto);

            // Override actor: coi chính chủ email là actor thực hiện reset
            HttpContext.Items["Audit:ActorId"] = result.UserId;
            HttpContext.Items["Audit:ActorEmail"] = result.Email;

            // Audit: reset password thành công (từ OTP/link)
            await _auditLogger.LogAsync(
                HttpContext,
                action: "ResetPassword",
                entityType: "Account",
                entityId: result.AccountId.ToString(),
                before: null,
                after: new
                {
                    result.UserId,
                    result.AccountId,
                    result.Email
                }
            );

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


    // ========= Token (refresh / revoke) =========

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
