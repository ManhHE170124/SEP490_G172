namespace Keytietkiem.DTOs
{
    public class PayOSConfigViewDto
    {
        public string ClientId { get; set; } = "";
        public bool HasApiKey { get; set; }
        public bool HasChecksumKey { get; set; }
    }

    public class PayOSConfigUpdateDto
    {
        public string ClientId { get; set; } = "";
        public string? ApiKey { get; set; }        // nhập mới thì update, để trống/null thì giữ
        public string? ChecksumKey { get; set; }   // nhập mới thì update, để trống/null thì giữ
        public bool? IsActive { get; set; }        // optional: cho phép bật/tắt PayOS
    }
}
