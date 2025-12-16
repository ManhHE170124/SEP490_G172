using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class PaymentGateway
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string CallbackUrl { get; set; } = null!;

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? ClientId { get; set; }

    public string? ApiKey { get; set; }

    public string? ChecksumKey { get; set; }
}
