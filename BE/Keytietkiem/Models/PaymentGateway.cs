using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class PaymentGateway
{
    public long GatewayId { get; set; }

    public string Name { get; set; } = null!;

    public string? CallbackUrl { get; set; }

    public byte[]? PublicKey { get; set; }

    public byte[]? SecretEnc { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }
}
