namespace Keytietkiem.Services.Interfaces;

public interface ISendPulseService
{
    Task<bool> SendEmailAsync(string toEmail, string subject, string combinedHtmlBody);
}
