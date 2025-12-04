using System;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.Users
{
    public class UserListItemDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? RoleName { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Mức độ ưu tiên hiện tại của user (Users.SupportPriorityLevel).
        /// </summary>
        public int SupportPriorityLevel { get; set; }

        /// <summary>
        /// Có phải người dùng tạm thời (IsTemp) hay không.
        /// </summary>
        public bool IsTemp { get; set; }
    }

    public class UserDetailDto
    {
        public Guid UserId { get; set; }

        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public string? Address { get; set; }

        public string Status { get; set; } = "Active";
        public DateTime? LastLoginAt { get; set; }
        public string? RoleId { get; set; }
        public bool HasAccount { get; set; }

        // Phản ánh Account.Username để FE cho phép tạo/sửa username
        [StringLength(60)]
        public string Username { get; set; } = "";
        // Không có passwordPlain vì mật khẩu băm 1 chiều.

        /// <summary>
        /// Mức độ ưu tiên hiện tại của user (Users.SupportPriorityLevel).
        /// </summary>
        public int SupportPriorityLevel { get; set; }

        /// <summary>
        /// Có phải người dùng tạm thời (IsTemp) hay không.
        /// </summary>
        public bool IsTemp { get; set; }

        /// <summary>
        /// Thông tin gói hỗ trợ đang active (nếu có).
        /// </summary>
        public int? ActiveSupportPlanId { get; set; }
        public string? ActiveSupportPlanName { get; set; }
        public DateTime? ActiveSupportPlanStartedAt { get; set; }
        public DateTime? ActiveSupportPlanExpiresAt { get; set; }
        public string? ActiveSupportPlanStatus { get; set; }
        public decimal TotalProductSpend { get; set; }
    }

    public class UserCreateDto
    {
        // Theo DB: FirstName NVARCHAR(80)
        [Required]
        [StringLength(80)]
        public string FirstName { get; set; } = "";

        // Theo DB: LastName NVARCHAR(80)
        [Required]
        [StringLength(80)]
        public string LastName { get; set; } = "";

        // Theo DB: Email NVARCHAR(254) UNIQUE
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = "";

        // Theo DB: Phone NVARCHAR(32)
        [Phone]
        [StringLength(32)]
        public string? Phone { get; set; }

        // Theo DB: Address NVARCHAR(300)
        [StringLength(300)]
        public string? Address { get; set; }

        // Status: Active | Locked | Disabled
        [Required]
        [RegularExpression("Active|Locked|Disabled",
            ErrorMessage = "Status must be Active, Locked or Disabled")]
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Mức độ ưu tiên gốc của user (0 = Standard, 1 = Priority, 2 = VIP ...).
        /// Cho phép set khi tạo user.
        /// </summary>
        public int SupportPriorityLevel { get; set; } = 0;

        public string? RoleId { get; set; }

        // Theo Account.Username NVARCHAR(60) UNIQUE
        [StringLength(60)]
        public string? Username { get; set; }

        /// <summary>
        /// Mật khẩu khi tạo tài khoản:
        /// - BẮT BUỘC (không được để trống).
        /// - Ít nhất 6 ký tự.
        /// - Giới hạn 200 ký tự để tránh input quá dài.
        /// </summary>
        [Required]
        [StringLength(200, MinimumLength = 6,
            ErrorMessage = "Password must be at least 6 characters.")]
        public string NewPassword { get; set; } = "";

        /// <summary>
        /// Gói hỗ trợ được gán cho user khi tạo (tuỳ chọn).
        /// Nếu null hoặc &lt;= 0 thì không tạo subscription.
        /// </summary>
        public int? ActiveSupportPlanId { get; set; }
    }

    public class UserUpdateDto
    {
        [Required]
        public Guid UserId { get; set; }

        // Giống constraint của Create
        [Required]
        [StringLength(80)]
        public string FirstName { get; set; } = "";

        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(80)]
        public string LastName { get; set; } = "";

        [Phone]
        [StringLength(32)]
        public string? Phone { get; set; }

        [StringLength(300)]
        public string? Address { get; set; }

        [Required]
        [RegularExpression("Active|Locked|Disabled",
            ErrorMessage = "Status must be Active, Locked or Disabled")]
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Mức độ ưu tiên gốc của user.
        /// Cho phép chỉnh sửa từ màn admin.
        /// </summary>
        public int SupportPriorityLevel { get; set; }

        public string? RoleId { get; set; }

        [StringLength(60)]
        public string? Username { get; set; }

        /// <summary>
        /// Mật khẩu mới khi update:
        /// - TÙY CHỌN (cho phép null/empty).
        /// - Nếu có giá trị thì phải ≥ 6 ký tự, tối đa 200 ký tự.
        /// </summary>
        [StringLength(200, MinimumLength = 6,
            ErrorMessage = "Password must be at least 6 characters.")]
        public string? NewPassword { get; set; }

        /// <summary>
        /// Gói hỗ trợ muốn áp dụng sau khi cập nhật (tùy chọn).
        /// Nếu null: không thay đổi.
        /// Nếu &lt;= 0: huỷ mọi subscription đang active.
        /// Nếu &gt; 0: chuyển sang gói mới.
        /// </summary>
        public int? ActiveSupportPlanId { get; set; }
    }
}
