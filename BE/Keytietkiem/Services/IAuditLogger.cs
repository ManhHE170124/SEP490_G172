using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Keytietkiem.Services
{
    public interface IAuditLogger
    {
        Task LogAsync(
            HttpContext httpContext,
            string action,
            string? entityType = null,
            string? entityId = null,
            object? before = null,
            object? after = null);
    }
}
