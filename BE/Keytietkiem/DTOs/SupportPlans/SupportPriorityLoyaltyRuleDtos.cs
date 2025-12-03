using System;

namespace Keytietkiem.DTOs.SupportPlans
{
    /// <summary>
    /// Item dùng cho list màn cấu hình rules (grid/list).
    /// </summary>
    public class SupportPriorityLoyaltyRuleListItemDto
    {
        public int RuleId { get; set; }

        /// <summary>
        /// Tổng tiền tối thiểu user đã chi (TotalProductSpend) để áp dụng rule này.
        /// </summary>
        public decimal MinTotalSpend { get; set; }

        /// <summary>
        /// Level ưu tiên được set cho user nếu thỏa mãn mức MinTotalSpend.
        /// </summary>
        public int PriorityLevel { get; set; }

        /// <summary>
        /// Rule có đang được sử dụng hay không.
        /// </summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// DTO chi tiết 1 rule (dùng cho form edit/detail).
    /// </summary>
    public class SupportPriorityLoyaltyRuleDetailDto
    {
        public int RuleId { get; set; }
        public decimal MinTotalSpend { get; set; }
        public int PriorityLevel { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// DTO tạo mới rule.
    /// </summary>
    public class SupportPriorityLoyaltyRuleCreateDto
    {
        /// <summary>
        /// Tổng tiền tối thiểu user đã chi (TotalProductSpend) để áp dụng rule này.
        /// </summary>
        public decimal MinTotalSpend { get; set; }

        /// <summary>
        /// Level ưu tiên (0=Standard,1=Priority,2=VIP, ...).
        /// </summary>
        public int PriorityLevel { get; set; }

        /// <summary>
        /// Rule có active ngay sau khi tạo không.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// DTO update rule.
    /// </summary>
    public class SupportPriorityLoyaltyRuleUpdateDto
    {
        public decimal MinTotalSpend { get; set; }
        public int PriorityLevel { get; set; }
        public bool IsActive { get; set; }
    }
}
