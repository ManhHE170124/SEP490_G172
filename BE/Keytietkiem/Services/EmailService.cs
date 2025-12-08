using System.Net;
using System.Net.Mail;
using Keytietkiem.Options;
using Keytietkiem.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Keytietkiem.Services;

public class EmailService : IEmailService
{
    private readonly MailConfig _mailConfig;
    private readonly ClientConfig _clientConfig;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<MailConfig> mailOptions,
        IOptions<ClientConfig> clientOptions,
        ILogger<EmailService> logger)
    {
        _mailConfig = mailOptions?.Value ?? throw new ArgumentNullException(nameof(mailOptions));
        _clientConfig = clientOptions?.Value ?? throw new ArgumentNullException(nameof(clientOptions));
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
        var resetLink = $"{_clientConfig.ClientUrl}/reset-password?token={resetToken}";
        var expiryMinutes = _clientConfig.ResetLinkExpiryInMinutes;
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
                            Link này có hiệu lực trong <strong>{expiryMinutes} phút</strong>.
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

    public async Task SendProductKeyEmailAsync(
        string toEmail,
        string productName,
        string variantTitle,
        string keyString,
        DateTime? expiryDate = null,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Product Key - {productName}";
        var expiryInfo = expiryDate.HasValue
            ? $"<p style='color: #666; font-size: 14px; line-height: 1.6;'><strong>Ngày hết hạn:</strong> {expiryDate.Value:dd/MM/yyyy}</p>"
            : "";

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>
                    <div style='background-color: white; padding: 30px; border-radius: 10px;'>
                        <h2 style='color: #333; margin-bottom: 20px;'>Thông tin Product Key</h2>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Cảm ơn bạn đã mua hàng tại Keytietkiem!
                        </p>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Dưới đây là thông tin product key của bạn:
                        </p>
                        <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #007bff;'>
                            <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Sản phẩm:</strong> {productName}</p>
                            <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Gói:</strong> {variantTitle}</p>
                            <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Product Key:</strong></p>
                            <div style='background-color: white; padding: 15px; text-align: center; border-radius: 5px; margin-top: 10px;'>
                                <code style='color: #007bff; font-size: 16px; font-weight: bold; word-break: break-all;'>{keyString}</code>
                            </div>
                            {expiryInfo}
                        </div>
                        <p style='color: #666; font-size: 14px; line-height: 1.6;'>
                            Vui lòng lưu giữ thông tin này cẩn thận. Nếu có bất kỳ vấn đề gì, xin liên hệ với chúng tôi.
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

    public async Task SendProductAccountEmailAsync(
        string toEmail,
        string productName,
        string variantTitle,
        string accountEmail,
        string? accountUsername,
        string accountPassword,
        DateTime? expiryDate = null,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Account Credentials - {productName}";
        var usernameInfo = !string.IsNullOrWhiteSpace(accountUsername)
            ? $"<p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Username:</strong> {accountUsername}</p>"
            : "";
        var expiryInfo = expiryDate.HasValue
            ? $"<p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Ngày hết hạn:</strong> {expiryDate.Value:dd/MM/yyyy}</p>"
            : "";

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>
                    <div style='background-color: white; padding: 30px; border-radius: 10px;'>
                        <h2 style='color: #333; margin-bottom: 20px;'>Thông tin tài khoản sản phẩm</h2>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Cảm ơn bạn đã mua hàng tại Keytietkiem!
                        </p>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Dưới đây là thông tin đăng nhập tài khoản của bạn:
                        </p>
                        <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;'>
                            <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Sản phẩm:</strong> {productName}</p>
                            <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Gói:</strong> {variantTitle}</p>
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 15px 0;'>
                            <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Email:</strong> {accountEmail}</p>
                            {usernameInfo}
                            <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Mật khẩu:</strong></p>
                            <div style='background-color: white; padding: 15px; border-radius: 5px; margin-top: 10px;'>
                                <code style='color: #28a745; font-size: 16px; font-weight: bold; word-break: break-all;'>{accountPassword}</code>
                            </div>
                            {expiryInfo}
                        </div>
                        <p style='color: #dc3545; font-size: 14px; line-height: 1.6; background-color: #fff3cd; padding: 10px; border-radius: 5px;'>
                            <strong>⚠️ Lưu ý:</strong> Vui lòng không chia sẻ thông tin đăng nhập này với người khác để đảm bảo an toàn tài khoản.
                        </p>
                        <p style='color: #666; font-size: 14px; line-height: 1.6;'>
                            Nếu có bất kỳ vấn đề gì, xin liên hệ với chúng tôi.
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

    public async Task SendOrderProductsEmailAsync(
        string toEmail,
        List<OrderProductEmailDto> orderProducts,
        CancellationToken cancellationToken = default)
    {
        if (orderProducts == null || !orderProducts.Any())
        {
            _logger.LogWarning("No products to send in order email to {Email}", toEmail);
            return;
        }

        var subject = "Thông tin đơn hàng - Keytietkiem";

        // Build product sections
        var productSections = new List<string>();
        var productNumber = 1;

        foreach (var product in orderProducts)
        {
            if (product.ProductType == "KEY")
            {
                var expiryInfo = product.ExpiryDate.HasValue
                    ? $"<p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Ngày hết hạn:</strong> {product.ExpiryDate.Value:dd/MM/yyyy}</p>"
                    : "";

                var keySection = $@"
                    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #007bff;'>
                        <h3 style='color: #007bff; font-size: 16px; margin: 0 0 15px 0;'>#{productNumber} - Product Key</h3>
                        <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Sản phẩm:</strong> {product.ProductName}</p>
                        <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Gói:</strong> {product.VariantTitle}</p>
                        <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Product Key:</strong></p>
                        <div style='background-color: white; padding: 15px; text-align: center; border-radius: 5px; margin-top: 10px;'>
                            <code style='color: #007bff; font-size: 16px; font-weight: bold; word-break: break-all;'>{product.KeyString}</code>
                        </div>
                        {expiryInfo}
                    </div>";

                productSections.Add(keySection);
            }
            else if (product.ProductType == "ACCOUNT")
            {
                var usernameInfo = !string.IsNullOrWhiteSpace(product.AccountUsername)
                    ? $"<p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Username:</strong> {product.AccountUsername}</p>"
                    : "";
                var expiryInfo = product.ExpiryDate.HasValue
                    ? $"<p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Ngày hết hạn:</strong> {product.ExpiryDate.Value:dd/MM/yyyy}</p>"
                    : "";

                var accountSection = $@"
                    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;'>
                        <h3 style='color: #28a745; font-size: 16px; margin: 0 0 15px 0;'>#{productNumber} - Account Credentials</h3>
                        <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Sản phẩm:</strong> {product.ProductName}</p>
                        <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Gói:</strong> {product.VariantTitle}</p>
                        <hr style='border: none; border-top: 1px solid #ddd; margin: 15px 0;'>
                        <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Email:</strong> {product.AccountEmail}</p>
                        {usernameInfo}
                        <p style='color: #333; font-size: 14px; margin: 10px 0;'><strong>Mật khẩu:</strong></p>
                        <div style='background-color: white; padding: 15px; border-radius: 5px; margin-top: 10px;'>
                            <code style='color: #28a745; font-size: 16px; font-weight: bold; word-break: break-all;'>{product.AccountPassword}</code>
                        </div>
                        {expiryInfo}
                    </div>";

                productSections.Add(accountSection);
            }

            productNumber++;
        }

        var allProductsSections = string.Join("\n", productSections);

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>
                    <div style='background-color: white; padding: 30px; border-radius: 10px;'>
                        <h2 style='color: #333; margin-bottom: 20px;'>Thông tin đơn hàng của bạn</h2>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Cảm ơn bạn đã mua hàng tại Keytietkiem!
                        </p>
                        <p style='color: #666; font-size: 16px; line-height: 1.6;'>
                            Dưới đây là thông tin tất cả sản phẩm trong đơn hàng của bạn:
                        </p>

                        {allProductsSections}

                        <p style='color: #dc3545; font-size: 14px; line-height: 1.6; background-color: #fff3cd; padding: 10px; border-radius: 5px;'>
                            <strong>⚠️ Lưu ý:</strong> Vui lòng lưu giữ thông tin này cẩn thận và không chia sẻ với người khác để đảm bảo an toàn tài khoản.
                        </p>
                        <p style='color: #666; font-size: 14px; line-height: 1.6;'>
                            Nếu có bất kỳ vấn đề gì, xin liên hệ với chúng tôi.
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
