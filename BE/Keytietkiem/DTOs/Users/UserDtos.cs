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

        // NEW: phản ánh Account.Username để FE cho phép tạo/sửa username
        [StringLength(60)]
        public string Username { get; set; } = "";
        // ĐÃ BỎ: PasswordPlain
    }

    public class UserCreateDto
    {
        [Required, StringLength(100)]
        public string FirstName { get; set; } = "";

        [Required, StringLength(100)]
        public string LastName { get; set; } = "";

        [Required, EmailAddress, StringLength(255)]
        public string Email { get; set; } = "";

        [Phone, StringLength(30)]
        public string? Phone { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [Required, RegularExpression("Active|Locked|Disabled", ErrorMessage = "Status must be Active, Locked or Disabled")]
        public string Status { get; set; } = "Active";

        public string? RoleId { get; set; }

        // NEW: cho phép nhập username ngay khi tạo (nếu để trống sẽ dùng email)
        [StringLength(60)]
        public string? Username { get; set; }

        // Nếu có => tạo Account + băm 1 chiều
        [StringLength(200)]
        public string? NewPassword { get; set; }
    }

    public class UserUpdateDto
    {
        [Required]
        public Guid UserId { get; set; }

        [Required, StringLength(100)]
        public string FirstName { get; set; } = "";

        [Required, EmailAddress, StringLength(255)]
        public string Email { get; set; } = "";

        [Required, StringLength(100)]
        public string LastName { get; set; } = "";

        [Phone, StringLength(30)]
        public string? Phone { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [Required, RegularExpression("Active|Locked|Disabled", ErrorMessage = "Status must be Active, Locked or Disabled")]
        public string Status { get; set; } = "Active";

        public string? RoleId { get; set; }

        // NEW: cho phép đổi username trong quá trình cập nhật
        [StringLength(60)]
        public string? Username { get; set; }

        // Nếu có => reset password (băm 1 chiều)
        [StringLength(200)]
        public string? NewPassword { get; set; }
    }
}
