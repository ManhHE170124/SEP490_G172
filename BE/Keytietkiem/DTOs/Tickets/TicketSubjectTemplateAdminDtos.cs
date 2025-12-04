// File: DTOs/Tickets/TicketSubjectTemplateAdminDtos.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Tickets
{
    /// <summary>
    /// Item dùng cho list màn cấu hình TicketSubjectTemplate (grid/list).
    /// </summary>
    public class TicketSubjectTemplateAdminListItemDto
    {
        /// <summary>
        /// Mã template (primary key, unique, dùng cho BE/FE).
        /// Ví dụ: GENERAL_SUPPORT, PAYMENT_ISSUE, ACCOUNT_PROBLEM, ...
        /// </summary>
        public string TemplateCode { get; set; } = null!;

        /// <summary>
        /// Tiêu đề hiển thị cho ticket khi user chọn template này.
        /// Ví dụ: "[Hỗ trợ chung] Cần tư vấn thêm về sản phẩm".
        /// </summary>
        public string Title { get; set; } = null!;

        /// <summary>
        /// Mức độ ưu tiên (severity) gợi ý cho ticket tạo từ template này.
        /// Gợi ý: Low / Medium / High / Critical.
        /// </summary>
        public string Severity { get; set; } = null!;

        /// <summary>
        /// Nhóm / Category của template (ví dụ: "Thanh toán", "Tài khoản", "Kỹ thuật").
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Template đang được phép sử dụng hay không.
        /// </summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// DTO chi tiết 1 TicketSubjectTemplate (dùng cho form edit).
    /// </summary>
    public class TicketSubjectTemplateAdminDetailDto
    {
        public string TemplateCode { get; set; } = null!;

        public string Title { get; set; } = null!;

        public string Severity { get; set; } = null!;

        public string? Category { get; set; }

        public bool IsActive { get; set; }
    }

    /// <summary>
    /// DTO tạo mới TicketSubjectTemplate.
    /// </summary>
    public class TicketSubjectTemplateAdminCreateDto
    {
        [Required(ErrorMessage = "Mã template không được để trống.")]
        [StringLength(50, ErrorMessage = "Mã template không được vượt quá 50 ký tự.")]
        public string TemplateCode { get; set; } = null!;

        [Required(ErrorMessage = "Tiêu đề không được để trống.")]
        [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Độ ưu tiên (Severity) không được để trống.")]
        [StringLength(10, ErrorMessage = "Độ ưu tiên (Severity) không được vượt quá 10 ký tự.")]
        public string Severity { get; set; } = null!;

        [StringLength(100, ErrorMessage = "Category không được vượt quá 100 ký tự.")]
        public string? Category { get; set; }

        /// <summary>
        /// Mặc định template mới tạo là Active.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// DTO cập nhật TicketSubjectTemplate.
    /// TemplateCode là khóa chính, không cho đổi.
    /// </summary>
    public class TicketSubjectTemplateAdminUpdateDto
    {
        [Required(ErrorMessage = "Tiêu đề không được để trống.")]
        [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Độ ưu tiên (Severity) không được để trống.")]
        [StringLength(10, ErrorMessage = "Độ ưu tiên (Severity) không được vượt quá 10 ký tự.")]
        public string Severity { get; set; } = null!;

        [StringLength(100, ErrorMessage = "Category không được vượt quá 100 ký tự.")]
        public string? Category { get; set; }

        public bool IsActive { get; set; }
    }
}
