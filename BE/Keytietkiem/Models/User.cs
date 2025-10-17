using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class User
{
    public Guid UserId { get; set; }

    public Guid AccountId { get; set; }

    public string FullName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Account Account { get; set; } = null!;
}
