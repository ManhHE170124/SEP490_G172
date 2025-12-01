using System;

namespace Keytietkiem.Models;

public partial class SupportPriorityLoyaltyRule
{
    public int RuleId { get; set; }

    public decimal MinTotalSpend { get; set; }

    public int PriorityLevel { get; set; }

    public bool IsActive { get; set; }
}
