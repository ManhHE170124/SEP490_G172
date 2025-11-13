// File: Hubs/TicketHub.cs
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Keytietkiem.Hubs
{
    /// <summary>
    /// SignalR hub dùng cho realtime chat của Ticket.
    /// Mỗi ticket là một group: "ticket:{ticketId}".
    /// </summary>
    public class TicketHub : Hub
    {
        public async Task JoinTicketGroup(string ticketId)
        {
            if (!string.IsNullOrWhiteSpace(ticketId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");
            }
        }

        public async Task LeaveTicketGroup(string ticketId)
        {
            if (!string.IsNullOrWhiteSpace(ticketId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");
            }
        }
    }
}
