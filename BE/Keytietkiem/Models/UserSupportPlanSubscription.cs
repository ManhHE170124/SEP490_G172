using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class UserSupportPlanSubscription
{
    public Guid SubscriptionId { get; set; }

    public Guid UserId { get; set; }

    public int SupportPlanId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public Guid? PaymentId { get; set; }

    public string? Note { get; set; }

    public virtual Payment? Payment { get; set; }

    public virtual SupportPlan SupportPlan { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
