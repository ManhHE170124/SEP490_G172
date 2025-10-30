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
        await _accountService.ChangePasswordAsync(accountId, dto);
        return Ok(new { message = "Đổi mật khẩu thành công" });
    }
}
