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

    /// <summary>
    /// Sends product key credentials to the user
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="productName">Name of the product</param>
    /// <param name="variantTitle">Variant title</param>
    /// <param name="keyString">Product key string</param>
    /// <param name="expiryDate">Key expiry date (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendProductKeyEmailAsync(string toEmail, string productName, string variantTitle, string keyString, DateTime? expiryDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends product account credentials to the user
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="productName">Name of the product</param>
    /// <param name="variantTitle">Variant title</param>
    /// <param name="accountEmail">Account email</param>
    /// <param name="accountUsername">Account username (optional)</param>
    /// <param name="accountPassword">Account password</param>
    /// <param name="expiryDate">Account expiry date (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendProductAccountEmailAsync(string toEmail, string productName, string variantTitle, string accountEmail, string? accountUsername, string accountPassword, DateTime? expiryDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a consolidated email with all products (keys and accounts) from an order
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="orderProducts">List of products to include in the email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendOrderProductsEmailAsync(string toEmail, List<OrderProductEmailDto> orderProducts, CancellationToken cancellationToken = default);
}

public class OrderProductEmailDto
{
    public string ProductName { get; set; } = string.Empty;
    public string VariantTitle { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty; // "KEY" or "ACCOUNT"

    // For product keys
    public string? KeyString { get; set; }

    // For product accounts
    public string? AccountEmail { get; set; }
    public string? AccountUsername { get; set; }
    public string? AccountPassword { get; set; }

    public DateTime? ExpiryDate { get; set; }
}
