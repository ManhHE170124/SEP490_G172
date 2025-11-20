// File: Hubs/SupportChatHub.cs
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Keytietkiem.Hubs
{
    /// <summary>
    /// Hub SignalR dùng cho realtime Support Chat (widget hỗ trợ).
    /// Client:
    /// - Sau khi kết nối => gọi JoinSession(chatSessionId) để vào group của phiên chat.
    /// - Nhân viên có thể gọi JoinStaffQueue() để nhận realtime queue unassigned.
    /// </summary>
    public class SupportChatHub : Hub
    {
        private static string SessionGroup(Guid sessionId) => $"support:{sessionId:D}";
        private const string QueueGroup = "support:queue";

        public Task JoinSession(Guid chatSessionId)
            => Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(chatSessionId));

        public Task LeaveSession(Guid chatSessionId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(chatSessionId));

        /// <summary>
        /// Nhân viên mở màn queue unassigned.
        /// </summary>
        public Task JoinStaffQueue()
            => Groups.AddToGroupAsync(Context.ConnectionId, QueueGroup);

        /// <summary>
        /// Nhân viên đóng màn queue unassigned.
        /// </summary>
        public Task LeaveStaffQueue()
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, QueueGroup);
    }
}
