// File: DTOs/SupportChat/SupportChatDtos.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.SupportChat
{
    /// <summary>
    /// Trạng thái của phiên chat hỗ trợ.
    /// Lưu trong DB dưới dạng chuỗi (Waiting / Active / Closed).
    /// </summary>

    /// <summary>
    /// Thông tin cơ bản về 1 phiên chat hỗ trợ (dùng cho list, queue, my-sessions).
    /// </summary>
    public class SupportChatSessionItemDto
    {
        public Guid ChatSessionId { get; set; }

        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerEmail { get; set; } = "";

        public Guid? AssignedStaffId { get; set; }
        public string? AssignedStaffName { get; set; }
        public string? AssignedStaffEmail { get; set; }

        /// <summary>
        /// Chuỗi trạng thái hiện tại (Waiting / Active / Closed).
        /// </summary>
        public string Status { get; set; } = "";

        /// <summary>
        /// Mức ưu tiên (1..3) lấy từ User.SupportPriorityLevel.
        /// </summary>
        public int PriorityLevel { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public string? LastMessagePreview { get; set; }
    }

    /// <summary>
    /// DTO trả về 1 message trong khung chat hỗ trợ.
    /// </summary>
    public class SupportChatMessageDto
    {
        public long MessageId { get; set; }

        public Guid ChatSessionId { get; set; }

        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = "";

        /// <summary>
        /// true = tin nhắn do nhân viên (staff/admin) gửi.
        /// false = tin nhắn do khách hàng gửi.
        /// </summary>
        public bool IsFromStaff { get; set; }

        public string Content { get; set; } = "";

        public DateTime SentAt { get; set; }
    }

    /// <summary>
    /// Body khi customer mở widget chat lần đầu (có thể gửi kèm tin nhắn đầu tiên).
    /// </summary>
    public class OpenSupportChatDto
    {
        /// <summary>
        /// Nội dung tin nhắn đầu tiên (optional).
        /// Nếu null/empty => chỉ mở hoặc lấy lại phiên chat hiện có.
        /// </summary>
        public string? InitialMessage { get; set; }
    }

    /// <summary>
    /// Body khi tạo tin nhắn mới trong phiên chat hỗ trợ.
    /// </summary>
    public class CreateSupportChatMessageDto
    {
        [Required, MinLength(1)]
        public string Content { get; set; } = "";
    }
}
