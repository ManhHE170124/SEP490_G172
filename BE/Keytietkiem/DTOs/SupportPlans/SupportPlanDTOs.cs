// File: DTOs/SupportPlans/SupportPlanDTOs.cs
using System;

namespace Keytietkiem.DTOs.SupportPlans
{
    /// <summary>
    /// Dùng cho màn FE list các gói hỗ trợ.
    /// </summary>
    public class SupportPlanListItemDto
    {
        public int SupportPlanId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// 0 = Standard, 1 = Priority, 2 = VIP...
        /// </summary>
        public int PriorityLevel { get; set; }

        public decimal Price { get; set; }

        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Gói hỗ trợ hiện tại của user (subscription active).
    /// </summary>
    public class SupportPlanCurrentSubscriptionDto
    {
        public Guid SubscriptionId { get; set; }

        public int SupportPlanId { get; set; }

        public string PlanName { get; set; } = string.Empty;

        public string? PlanDescription { get; set; }

        public int PriorityLevel { get; set; }

        public decimal Price { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }
}
