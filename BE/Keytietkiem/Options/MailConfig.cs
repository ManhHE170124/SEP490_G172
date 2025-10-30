namespace Keytietkiem.Options;

public class MailConfig
{
    public string Mail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Smtp { get; set; }  = string.Empty;
    public int Port { get; set; } = 587;
}
