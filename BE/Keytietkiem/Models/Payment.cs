using System;

namespace Keytietkiem.Models
{
    public partial class Payment
    {
        public Guid PaymentId { get; set; }

        public string? Email { get; set; }

        public decimal Amount { get; set; }

        public string Status { get; set; } = null!;

        public string TransactionType { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public long? ProviderOrderCode { get; set; }

        public string? Provider { get; set; }

    }
}
