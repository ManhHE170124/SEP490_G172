using Keytietkiem.DTOs;

namespace Keytietkiem.Services.Interfaces;

public interface IAccountService
{
    /// <summary>
    /// Registers a new user account with provided credentials and profile information
    /// </summary>
    /// <param name="registerDto">Registration data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with access token and user information</returns>
    Task<LoginResponseDto> RegisterAsync(RegisterDto registerDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user with username and password
    /// </summary>
    /// <param name="loginDto">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with access token and user information</returns>
    Task<LoginResponseDto> LoginAsync(LoginDto loginDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token
    /// </summary>
    /// <param name="refreshTokenDto">Refresh token data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New access token and refresh token</returns>
    Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the password for an authenticated user
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="changePasswordDto">Password change data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ChangePasswordAsync(Guid accountId, ChangePasswordDto changePasswordDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a username is already taken
    /// </summary>
    /// <param name="username">Username to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if username exists, false otherwise</returns>
    Task<bool> IsUsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if an email is already registered
    /// </summary>
    /// <param name="email">Email to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email exists, false otherwise</returns>
    Task<bool> IsEmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an OTP code to the specified email address for registration verification
    /// </summary>
    /// <param name="sendOtpDto">Email to send OTP to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    Task<string> SendOtpAsync(SendOtpDto sendOtpDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the OTP code sent to the email
    /// </summary>
    /// <param name="verifyOtpDto">Email and OTP code to verify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Verification response with token if successful</returns>
    Task<OtpVerificationResponseDto> VerifyOtpAsync(VerifyOtpDto verifyOtpDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an OTP code to the email for password reset
    /// </summary>
    /// <param name="forgotPasswordDto">Email to send OTP to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    Task<string> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the user's password using OTP verification
    /// </summary>
    /// <param name="resetPasswordDto">Password reset data with OTP</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ResetPasswordAsync(ResetPasswordDto resetPasswordDto, CancellationToken cancellationToken = default);
}
