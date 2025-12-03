using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SlaRule
{
    public int SlaRuleId { get; set; }

    public string Name { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public int PriorityLevel { get; set; }

    public int FirstResponseMinutes { get; set; }

    public int ResolutionMinutes { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
