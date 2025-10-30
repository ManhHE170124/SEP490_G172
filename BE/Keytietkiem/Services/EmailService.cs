using System.Net;
using System.Net.Mail;
using Keytietkiem.Options;
using Keytietkiem.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Keytietkiem.Services;

public class EmailService : IEmailService
{
    private readonly MailConfig _mailConfig;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<MailConfig> mailOptions, ILogger<EmailService> logger)
    {
        _mailConfig = mailOptions?.Value ?? throw new ArgumentNullException(nameof(mailOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
    {
        var subject = "Xác thực tài khoản - Mã OTP";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>
                    <div style='background-color: white; padding: 30px; border-radius: 10px;'>
                        <h2 style='color: #333; margin-bottom: 20px;'>Xác thực tài khoản Keytietkiem</h2>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Cảm ơn bạn đã đăng ký tài khoản tại Keytietkiem!
                        </p>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Mã OTP của bạn là:
                        </p>
                        <div style='background-color: #f0f0f0; padding: 20px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                            <h1 style='color: #007bff; margin: 0; letter-spacing: 5px;'>{otpCode}</h1>
                        </div>
                        <p style='color: #666; font-size: 14px; line-height: 1.6;'>
                            Mã OTP này có hiệu lực trong <strong>5 phút</strong>.
                        </p>
                        <p style='color: #666; font-size: 14px; line-height: 1.6;'>
                            Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này.
                        </p>
                        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
                        <p style='color: #999; font-size: 12px;'>
                            Email này được gửi tự động, vui lòng không trả lời.
                        </p>
                    </div>
                </div>
            </body>
            </html>
        ";

        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        var subject = "Đặt lại mật khẩu - Keytietkiem";
        var resetLink = $"http://localhost:5173/reset-password?token={resetToken}";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>
                    <div style='background-color: white; padding: 30px; border-radius: 10px;'>
                        <h2 style='color: #333; margin-bottom: 20px;'>Đặt lại mật khẩu</h2>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản Keytietkiem của mình.
                        </p>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Nhấp vào nút bên dưới để đặt lại mật khẩu:
                        </p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{resetLink}'
                               style='background-color: #007bff; color: white; padding: 12px 30px;
                                      text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Đặt lại mật khẩu
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px; line-height: 1.6;'>
                            Link này có hiệu lực trong <strong>30 phút</strong>.
                        </p>
                        <p style='color: #666; font-size: 14px; line-height: 1.6;'>
                            Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này.
                        </p>
                        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
                        <p style='color: #999; font-size: 12px;'>
                            Email này được gửi tự động, vui lòng không trả lời.
                        </p>
                    </div>
                </div>
            </body>
            </html>
        ";

        await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_mailConfig.Mail, "Keytietkiem"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(toEmail);

            using var smtpClient = new SmtpClient(_mailConfig.Smtp, _mailConfig.Port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_mailConfig.Mail, _mailConfig.Password)
            };

            await smtpClient.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw new InvalidOperationException($"Không thể gửi email đến {toEmail}", ex);
        }
    }
}
