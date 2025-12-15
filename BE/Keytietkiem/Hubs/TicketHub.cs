// File: Hubs/TicketHub.cs
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Hubs;

[Authorize] // ✅ Bắt buộc có token
public class TicketHub : Hub
{
    private readonly KeytietkiemDbContext _db;

    public TicketHub(KeytietkiemDbContext db)
    {
        _db = db;
    }

    private static string BuildTicketGroup(Guid ticketId) => $"ticket:{ticketId:D}";

    private static bool IsCustomer(User u)
        => (u.Roles ?? Array.Empty<Role>()).Any(r =>
        {
            var code = (r.Code ?? string.Empty).Trim().ToLowerInvariant();
            return code.Contains("customer");
        });

    private static bool IsStaffLike(User u)
        => (u.Roles ?? Array.Empty<Role>()).Any(r =>
        {
            var code = (r.Code ?? string.Empty).Trim().ToLowerInvariant();
            return code.Contains("care") || code.Contains("admin");
        });

    public async Task JoinTicketGroup(Guid ticketId)
    {
        var meStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meStr, out var me))
            throw new HubException("Unauthorized.");

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == me);

        if (user is null) throw new HubException("Unauthorized.");

        var ticket = await _db.Tickets.AsNoTracking()
            .Select(t => new { t.TicketId, t.UserId })
            .FirstOrDefaultAsync(t => t.TicketId == ticketId);

        if (ticket is null) throw new HubException("Ticket not found.");

        // 🔐 Rule:
        // - Staff/Admin: join được
        // - Customer: chỉ join nếu là owner ticket
        if (IsStaffLike(user))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildTicketGroup(ticketId));
            return;
        }

        if (IsCustomer(user) && ticket.UserId == me)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildTicketGroup(ticketId));
            return;
        }

        throw new HubException("Forbidden.");
    }

    public async Task LeaveTicketGroup(Guid ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildTicketGroup(ticketId));
    }
}
