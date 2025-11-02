namespace Keytietkiem.Services.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends an OTP code to the specified email address
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="otpCode">OTP code to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a password reset email
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="resetToken">Password reset token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default);
}
