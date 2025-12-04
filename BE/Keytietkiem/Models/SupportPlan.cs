using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SupportPlan
{
    public int SupportPlanId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int PriorityLevel { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<UserSupportPlanSubscription> UserSupportPlanSubscriptions { get; set; } = new List<UserSupportPlanSubscription>();
}
