using System;

namespace Keytietkiem.DTOs
{
    public class UserDto
    {
        public Guid UserId { get; set; }
        public Guid AccountId { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Account fields
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Status { get; set; }
        public bool EmailVerified { get; set; }
        public bool TwoFaEnabled { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}
