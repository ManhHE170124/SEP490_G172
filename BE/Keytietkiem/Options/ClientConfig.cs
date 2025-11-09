namespace Keytietkiem.Options;

public class ClientConfig
{
    public string ClientUrl { get; set; } = string.Empty;
    public int ResetLinkExpiryInMinutes { get; set; } = 30;
}
