// File: DTOs/SupportPlans/SupportPlanAdminDtos.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Support
{
    /// <summary>
    /// Item dùng cho list màn cấu hình SupportPlan (grid/list).
    /// </summary>
    public class SupportPlanAdminListItemDto
    {
        public int SupportPlanId { get; set; }

        /// <summary>
        /// Tên gói (Standard / Priority / VIP / ...)
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Mô tả ngắn về quyền lợi gói.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Level ưu tiên: 0 = Standard (default), 1 = Priority, 2 = VIP, ...
        /// </summary>
        public int PriorityLevel { get; set; }

        /// <summary>
        /// Giá gói (theo tháng) – đơn vị: VNĐ.
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Gói có đang hiển thị / cho phép user đăng ký hay không.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Thời điểm gói được tạo.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO chi tiết 1 SupportPlan (dùng cho form edit/detail).
    /// </summary>
    public class SupportPlanAdminDetailDto
    {
        public int SupportPlanId { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public int PriorityLevel { get; set; }

        public decimal Price { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO tạo mới SupportPlan.
    /// </summary>
    public class SupportPlanAdminCreateDto
    {
        /// <summary>
        /// Tên gói (Standard / Priority / VIP / ...)
        /// </summary>
        [Required(ErrorMessage = "Tên gói không được để trống.")]
        [StringLength(120, ErrorMessage = "Tên gói không được vượt quá 120 ký tự.")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Mô tả ngắn về quyền lợi gói.
        /// </summary>
        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
        public string? Description { get; set; }

        /// <summary>
        /// Level ưu tiên: 0 = Standard (default), 1 = Priority, 2 = VIP, ...
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0.")]
        public int PriorityLevel { get; set; }

        /// <summary>
        /// Giá gói theo tháng (>= 0).
        /// </summary>
        [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Giá gói phải lớn hơn hoặc bằng 0.")]
        public decimal Price { get; set; }

        /// <summary>
        /// Có bật gói ngay sau khi tạo hay không.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// DTO update SupportPlan.
    /// </summary>
    public class SupportPlanAdminUpdateDto
    {
        [Required(ErrorMessage = "Tên gói không được để trống.")]
        [StringLength(120, ErrorMessage = "Tên gói không được vượt quá 120 ký tự.")]
        public string Name { get; set; } = null!;

        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
        public string? Description { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0.")]
        public int PriorityLevel { get; set; }

        [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Giá gói phải lớn hơn hoặc bằng 0.")]
        public decimal Price { get; set; }

        public bool IsActive { get; set; }
    }
}
