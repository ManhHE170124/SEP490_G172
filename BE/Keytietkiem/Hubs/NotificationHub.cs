// File: Hubs/NotificationHub.cs
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Keytietkiem.Hubs
{
    /// <summary>
    /// Hub realtime cho thông báo hệ thống.
    /// Mỗi user sẽ join vào group "user:{UserId}" theo claim.
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        public const string GlobalGroup = "global";
        public static string UserGroup(Guid userId) => $"user:{userId:D}";
        public static string RoleGroup(string roleId) => $"role:{roleId}";

        public override async Task OnConnectedAsync()
        {
            if (Context.User?.Identity?.IsAuthenticated != true)
            {
                Context.Abort();
                return;
            }

            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? Context.User?.FindFirst("uid")?.Value
                            ?? Context.User?.FindFirst("sub")?.Value;

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
            await Groups.AddToGroupAsync(Context.ConnectionId, GlobalGroup);

            // (Optional) Join role groups để broadcast theo role mà không cần enumerate userIds
            // Lấy role từ claim (tuỳ hệ thống bạn phát role claim kiểu nào)
            var roleValues =
                Context.User.FindAll(ClaimTypes.Role).Select(c => c.Value)
                .Concat(Context.User.FindAll("role").Select(c => c.Value))
                .Concat(Context.User.FindAll("roles").Select(c => c.Value));

            foreach (var roleId in roleValues
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Select(x => x.Trim())
                         .Distinct()
                         .Take(20))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(roleId));
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // SignalR tự remove khỏi group khi disconnect.
            // Giữ đoạn remove này cũng OK (best-effort) nếu bạn muốn rõ ràng.

            var userIdStr = Context.User?
                                .FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? Context.User?
                                .FindFirst("uid")?.Value
                            ?? Context.User?
                                .FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdStr, out var userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GlobalGroup);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
