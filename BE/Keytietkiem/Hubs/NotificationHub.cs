// File: Hubs/NotificationHub.cs
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Keytietkiem.Hubs
{
    /// <summary>
    /// Hub realtime cho thông báo hệ thống.
    /// Mỗi user sẽ join vào group "user:{UserId}" theo claim.
    /// </summary>
    public class NotificationHub : Hub
    {
        public static string UserGroup(Guid userId) => $"user:{userId:D}";

        public override async Task OnConnectedAsync()
        {
            var userIdStr = Context.User?
                                .FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? Context.User?
                                .FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdStr, out var userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userIdStr = Context.User?
                                .FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? Context.User?
                                .FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdStr, out var userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
