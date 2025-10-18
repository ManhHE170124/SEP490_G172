using Keytietkiem.DTOs;
using Keytietkiem.Models;

namespace Keytietkiem.Services
{
    public interface IAuditLogService
    {
        Task<PagedResultDto<AuditLogDto>> SearchAsync(AuditQueryDto q, bool maskSensitive, CancellationToken ct = default);
        Task<(string fileName, byte[] bytes)> ExportCsvAsync(AuditQueryDto q, bool maskSensitive, CancellationToken ct = default);
        Task<long> AppendAsync(AuditLog entry, CancellationToken ct = default);
    }
}
