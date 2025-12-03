// File: DTOs/SupportChat/SupportChatDtos.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs.SupportChat
{
    /// <summary>
    /// Thông tin cơ bản về 1 phiên chat hỗ trợ (dùng cho list, queue, my-sessions).
    /// </summary>
    public class SupportChatSessionItemDto
    {
        /// <summary>
        /// Id phiên chat (PK).
        /// </summary>
        public Guid ChatSessionId { get; set; }

        /// <summary>
        /// Id khách hàng mở phiên chat.
        /// </summary>
        public Guid CustomerId { get; set; }

        /// <summary>
        /// Tên hiển thị của khách hàng (FullName nếu có, fallback Email).
        /// </summary>
        public string CustomerName { get; set; } = "";

        /// <summary>
        /// Email khách hàng (nếu có).
        /// </summary>
        public string? CustomerEmail { get; set; }

        /// <summary>
        /// Id nhân viên đang được gán xử lý phiên chat (nullable khi còn trong queue).
        /// </summary>
        public Guid? AssignedStaffId { get; set; }

        /// <summary>
        /// Tên nhân viên phụ trách (FullName nếu có, fallback Email).
        /// </summary>
        public string? AssignedStaffName { get; set; }

        /// <summary>
        /// Email nhân viên phụ trách (nếu có).
        /// </summary>
        public string? AssignedStaffEmail { get; set; }

        /// <summary>
        /// Trạng thái phiên chat: Waiting / Active / Closed.
        /// </summary>
        public string Status { get; set; } = "Waiting";

        /// <summary>
        /// Cấp ưu tiên: 1 = cao nhất, 3 = thấp nhất.
        /// </summary>
        public int PriorityLevel { get; set; }

        /// <summary>
        /// Thời điểm bắt đầu phiên chat.
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Thời điểm tin nhắn cuối cùng (nếu có).
        /// </summary>
        public DateTime? LastMessageAt { get; set; }

        /// <summary>
        /// Nội dung rút gọn của tin nhắn cuối cùng (max 255 ký tự).
        /// </summary>
        public string? LastMessagePreview { get; set; }
    }

    /// <summary>
    /// DTO tin nhắn trong phiên chat hỗ trợ.
    /// </summary>
    public class SupportChatMessageDto
    {
        /// <summary>
        /// Id tin nhắn (bigint).
        /// </summary>
        public long MessageId { get; set; }

        /// <summary>
        /// Id phiên chat chứa tin nhắn này.
        /// </summary>
        public Guid ChatSessionId { get; set; }

        /// <summary>
        /// Id người gửi (UserId).
        /// </summary>
        public Guid SenderId { get; set; }

        /// <summary>
        /// Tên hiển thị người gửi (FullName nếu có, fallback Email).
        /// </summary>
        public string SenderName { get; set; } = "";

        /// <summary>
        /// true = tin nhắn do nhân viên (staff/admin) gửi.
        /// false = tin nhắn do khách hàng gửi.
        /// </summary>
        public bool IsFromStaff { get; set; }

        /// <summary>
        /// Nội dung tin nhắn.
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Thời điểm gửi.
        /// </summary>
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

    /// <summary>
    /// Filter cho màn admin lịch sử chat.
    /// Dùng với [FromQuery].
    /// </summary>
    public class SupportChatAdminSessionFilterDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public Guid? CustomerId { get; set; }
        public Guid? StaffId { get; set; }

        public int? PriorityLevel { get; set; }

        /// <summary>
        /// Trạng thái: Waiting / Active / Closed (optional).
        /// Nếu null => không filter theo trạng thái.
        /// </summary>
        public string? Status { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Item cho màn admin list session, kế thừa thông tin cơ bản + số tin nhắn.
    /// </summary>
    public class SupportChatAdminSessionListItemDto : SupportChatSessionItemDto
    {
        /// <summary>
        /// Tổng số tin nhắn trong phiên.
        /// </summary>
        public int MessageCount { get; set; }
    }
    /// <summary>
    /// Kết quả khi customer mở/tìm phiên chat (widget).
    /// Kế thừa thông tin cơ bản của phiên + flag hỗ trợ UI.
    /// </summary>
    public class OpenSupportChatResultDto : SupportChatSessionItemDto
    {
        /// <summary>
        /// true nếu đây là phiên chat mới được tạo.
        /// false nếu dùng lại phiên đang mở (Waiting/Active).
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// true nếu trước khi mở phiên này, user đã từng có ít nhất 1 phiên chat đã Closed.
        /// Dùng để hiển thị message "Phiên chat trước đã kết thúc..."
        /// </summary>
        public bool HasPreviousClosedSession { get; set; }

        /// <summary>
        /// Id phiên chat đã Closed gần nhất (nếu có).
        /// </summary>
        public Guid? LastClosedSessionId { get; set; }

        /// <summary>
        /// Thời điểm đóng của phiên chat đã Closed gần nhất (nếu có).
        /// </summary>
        public DateTime? LastClosedAt { get; set; }
    }
}
