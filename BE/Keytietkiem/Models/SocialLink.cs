using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SocialLink
{
    public long SocialId { get; set; }

    public string Network { get; set; } = null!;

    public string Url { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }
}
