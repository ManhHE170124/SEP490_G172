using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs;

/// <summary>
/// Request DTO for sending OTP to email
/// </summary>
public class SendOtpDto
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for verifying OTP
/// </summary>
public class VerifyOtpDto
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "OTP là bắt buộc")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP phải có 6 ký tự")]
    public string Otp { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO after OTP verification
/// </summary>
public class OtpVerificationResponseDto
{
    public bool IsVerified { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? VerificationToken { get; set; }
}

/// <summary>
/// Request DTO for user registration with OTP verification
/// </summary>
public class RegisterDto
{
    [Required(ErrorMessage = "Username là bắt buộc")]
    [StringLength(60, MinimumLength = 3, ErrorMessage = "Username phải từ 3-60 ký tự")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password là bắt buộc")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password phải từ 8-100 ký tự")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password phải chứa ít nhất 1 chữ cái và 1 số")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "FirstName là bắt buộc")]
    [StringLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "LastName là bắt buộc")]
    [StringLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(32)]
    public string? Phone { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [Required(ErrorMessage = "VerificationToken là bắt buộc")]
    public string VerificationToken { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for user login
/// </summary>
public class LoginDto
{
    [Required(ErrorMessage = "Username là bắt buộc")]
    [MinLength(1, ErrorMessage = "Username không được để trống")]
    [RegularExpression(@"^\S+.*", ErrorMessage = "Username không được chỉ chứa khoảng trắng")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password là bắt buộc")]
    [MinLength(1, ErrorMessage = "Password không được để trống")]
    [RegularExpression(@"^\S+.*", ErrorMessage = "Password không được chỉ chứa khoảng trắng")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO returned after successful login or registration
/// </summary>
public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserInfoDto User { get; set; } = null!;
}

/// <summary>
/// User information included in login response
/// </summary>
public class UserInfoDto
{
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public int SupportPriorityLevel { get; set; }
}

/// <summary>
/// Response DTO for account profile
/// </summary>
public class AccountProfileDto
{
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// Request DTO for updating current user's profile information
/// </summary>
public class UpdateAccountProfileDto
{
    [Required(ErrorMessage = "FullName là bắt buộc")]
    [StringLength(160, ErrorMessage = "FullName tối đa 160 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(32)]
    public string? Phone { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(500)]
    public string? AvatarUrl { get; set; }
}

/// <summary>
/// Request DTO for refreshing access token
/// </summary>
public class RefreshTokenDto
{
    [Required(ErrorMessage = "RefreshToken là bắt buộc")]
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for changing password
/// </summary>
public class ChangePasswordDto
{
    [Required(ErrorMessage = "CurrentPassword là bắt buộc")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới phải từ 8-100 ký tự")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Mật khẩu mới phải chứa ít nhất 1 chữ cái và 1 số")]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for initiating forgot password process
/// </summary>
public class ForgotPasswordDto
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for resetting password with token from email link
/// </summary>
public class ResetPasswordDto
{
    [Required(ErrorMessage = "Token là bắt buộc")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới phải từ 8-100 ký tự")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Mật khẩu mới phải chứa ít nhất 1 chữ cái và 1 số")]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for revoking access token and refresh token (logout)
/// </summary>
public class RevokeTokenDto
{
    [Required(ErrorMessage = "AccessToken là bắt buộc")]
    public string AccessToken { get; set; } = string.Empty;

    [Required(ErrorMessage = "RefreshToken là bắt buộc")]
    public string RefreshToken { get; set; } = string.Empty;
}
public class ResetPasswordResultDto
{
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public string Email { get; set; } = string.Empty;
}