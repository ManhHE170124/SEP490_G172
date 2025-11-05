namespace Keytietkiem.DTOs
{
    public class PaymentGatewayDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CallbackUrl { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
