// File: DTOs/SlaRules/SlaRuleAdminDtos.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.SlaRules
{
    /// <summary>
    /// Item dùng cho list màn cấu hình SlaRule (grid/list).
    /// </summary>
    public class SlaRuleAdminListItemDto
    {
        public int SlaRuleId { get; set; }

        /// <summary>
        /// Tên SLA rule (hiển thị cho admin).
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Mức độ ưu tiên của ticket: Low / Medium / High / Critical.
        /// </summary>
        public string Severity { get; set; } = null!;

        /// <summary>
        /// Priority level của user: 0 = Standard, 1 = Priority, 2 = VIP, ...
        /// </summary>
        public int PriorityLevel { get; set; }

        /// <summary>
        /// Thời gian tối đa (phút) cho phản hồi đầu tiên.
        /// </summary>
        public int FirstResponseMinutes { get; set; }

        /// <summary>
        /// Thời gian tối đa (phút) để xử lý / đóng ticket.
        /// </summary>
        public int ResolutionMinutes { get; set; }

        /// <summary>
        /// Rule có đang được áp dụng hay không.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Thời điểm rule được tạo.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO chi tiết 1 SlaRule (dùng cho form edit/detail).
    /// </summary>
    public class SlaRuleAdminDetailDto
    {
        public int SlaRuleId { get; set; }

        public string Name { get; set; } = null!;

        public string Severity { get; set; } = null!;

        public int PriorityLevel { get; set; }

        public int FirstResponseMinutes { get; set; }

        public int ResolutionMinutes { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO tạo mới SlaRule.
    /// </summary>
    public class SlaRuleAdminCreateDto
    {
        /// <summary>
        /// Tên SLA rule (hiển thị cho admin).
        /// </summary>
        [Required(ErrorMessage = "Tên SLA rule không được để trống.")]
        [StringLength(120, ErrorMessage = "Tên SLA rule không được vượt quá 120 ký tự.")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Mức độ ưu tiên của ticket: Low / Medium / High / Critical.
        /// </summary>
        [Required(ErrorMessage = "Mức độ (Severity) không được để trống.")]
        [StringLength(10, ErrorMessage = "Severity không được vượt quá 10 ký tự.")]
        public string Severity { get; set; } = null!;

        /// <summary>
        /// Priority level của user: 0 = Standard, 1 = Priority, 2 = VIP, ...
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0.")]
        public int PriorityLevel { get; set; }

        /// <summary>
        /// Thời gian tối đa (phút) cho phản hồi đầu tiên (>= 1).
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Thời gian phản hồi đầu tiên (phút) phải lớn hơn 0.")]
        public int FirstResponseMinutes { get; set; }

        /// <summary>
        /// Thời gian tối đa (phút) để xử lý / đóng ticket (>= 1).
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Thời gian xử lý (phút) phải lớn hơn 0.")]
        public int ResolutionMinutes { get; set; }

        /// <summary>
        /// Có bật rule ngay sau khi tạo hay không.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// DTO update SlaRule.
    /// </summary>
    public class SlaRuleAdminUpdateDto
    {
        [Required(ErrorMessage = "Tên SLA rule không được để trống.")]
        [StringLength(120, ErrorMessage = "Tên SLA rule không được vượt quá 120 ký tự.")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "Mức độ (Severity) không được để trống.")]
        [StringLength(10, ErrorMessage = "Severity không được vượt quá 10 ký tự.")]
        public string Severity { get; set; } = null!;

        [Range(0, int.MaxValue, ErrorMessage = "Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0.")]
        public int PriorityLevel { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Thời gian phản hồi đầu tiên (phút) phải lớn hơn 0.")]
        public int FirstResponseMinutes { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Thời gian xử lý (phút) phải lớn hơn 0.")]
        public int ResolutionMinutes { get; set; }

        public bool IsActive { get; set; }
    }
}
