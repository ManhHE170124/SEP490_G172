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
        /// (Mức ưu tiên "tự thân" của gói)
        /// </summary>
        public int PriorityLevel { get; set; }

        public decimal Price { get; set; }

        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Thông tin gói / mức ưu tiên hỗ trợ hiện tại của user.
    /// PriorityLevel được tính dựa trên Users.SupportPriorityLevel,
    /// kết hợp với subscription active (nếu có).
    /// </summary>
    public class SupportPlanCurrentSubscriptionDto
    {
        /// <summary>
        /// Id subscription nếu có; nếu không có subscription active
        /// thì có thể là Guid.Empty.
        /// </summary>
        public Guid SubscriptionId { get; set; }

        /// <summary>
        /// Id gói hỗ trợ nếu đang dùng; nếu không có subscription
        /// thì bằng 0.
        /// </summary>
        public int SupportPlanId { get; set; }

        /// <summary>
        /// Tên gói hỗ trợ (nếu có).
        /// </summary>
        public string PlanName { get; set; } = string.Empty;

        public string? PlanDescription { get; set; }

        /// <summary>
        /// Mức ưu tiên support hiện tại của user:
        /// 0 = Standard, 1 = Priority, 2 = VIP...
        /// (dựa trên Users.SupportPriorityLevel, không chỉ subscription)
        /// </summary>
        public int PriorityLevel { get; set; }

        /// <summary>
        /// Giá gói hiện tại (nếu có); nếu không có subscription thì 0.
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Trạng thái subscription: "Active", "Expired", "None"...
        /// </summary>
        public string Status { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }
}
