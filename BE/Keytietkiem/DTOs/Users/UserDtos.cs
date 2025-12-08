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

        // Theo UserStatus: Active | Locked | Disabled, cột Status NVARCHAR(12)
        [Required]
        [RegularExpression("Active|Locked|Disabled|Temp", ErrorMessage = "Status must be Active, Locked, Disabled or Temp")]
        public string Status { get; set; } = "Active";

        public string? RoleId { get; set; }

        // Theo Account.Username NVARCHAR(60) UNIQUE
        [StringLength(60)]
        public string? Username { get; set; }

        /// <summary>
        /// Mật khẩu khi tạo tài khoản:
        /// - BẮT BUỘC (không được để trống).
        /// - Ít nhất 6 ký tự.
        /// - Giới hạn 200 ký tự để tránh input quá dài (hash lưu vào PasswordHash max 256 bytes).
        /// </summary>
        [Required]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string NewPassword { get; set; } = "";
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
        [RegularExpression("Active|Locked|Disabled", ErrorMessage = "Status must be Active, Locked or Disabled")]
        public string Status { get; set; } = "Active";

        public string? RoleId { get; set; }

        [StringLength(60)]
        public string? Username { get; set; }

        /// <summary>
        /// Mật khẩu mới khi update:
        /// - TÙY CHỌN (cho phép null/empty).
        /// - Nếu có giá trị thì phải ≥ 6 ký tự, tối đa 200 ký tự.
        /// </summary>
        [StringLength(200, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string? NewPassword { get; set; }
    }
}
