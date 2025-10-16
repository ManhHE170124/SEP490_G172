using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class PasswordResetToken
{
    public Guid TokenId { get; set; }

    public Guid AccountId { get; set; }

    public byte[] TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedIp { get; set; }

    public virtual Account Account { get; set; } = null!;
}
