using Keytietkiem.DTOs;
using Keytietkiem.Mapping;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Keytietkiem.Services
{
    public class AuditLogService : IAuditLogService
    {
        private const int ExportLimit = 50_000; // rule
        private const int MaxRangeDays = 90;    // rule

        private readonly KeytietkiemContext _db;
        public AuditLogService(KeytietkiemContext db) => _db = db;

        public async Task<PagedResultDto<AuditLogDto>> SearchAsync(AuditQueryDto q, bool maskSensitive, CancellationToken ct = default)
        {
            Normalize(q);
            ValidateRange(q);

            var query = BuildQuery(q);
            var total = await query.LongCountAsync(ct);

            var items = await query.OrderByDescending(x => x.OccurredAt)
                                   .Skip((q.Page - 1) * q.PageSize)
                                   .Take(q.PageSize)
                                   .AsNoTracking()
                                   .ToListAsync(ct);

            return new PagedResultDto<AuditLogDto>(
                items.Select(x => x.ToDto(maskSensitive)).ToList(),
                q.Page, q.PageSize, total);
        }

        public async Task<(string fileName, byte[] bytes)> ExportCsvAsync(AuditQueryDto q, bool maskSensitive, CancellationToken ct = default)
        {
            Normalize(q);
            ValidateRange(q);

            var sb = new StringBuilder();
            sb.AppendLine("OccurredAt,ActorEmail,Action,Resource,EntityId,IP,UserAgent,DetailJson,CorrelationId,IntegrityAlert");

            static string Esc(string? s) => string.IsNullOrEmpty(s) ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";

            var data = BuildQuery(q).OrderByDescending(x => x.OccurredAt)
                                    .Take(ExportLimit)
                                    .AsNoTracking()
                                    .AsAsyncEnumerable();

            await foreach (var x in data.WithCancellation(ct))
            {
                var dto = x.ToDto(maskSensitive);
                sb.Append(dto.OccurredAt).Append(',')
                  .Append(Esc(dto.ActorEmail)).Append(',')
                  .Append(dto.Action).Append(',')
                  .Append(dto.Resource).Append(',')
                  .Append(Esc(dto.EntityId)).Append(',')
                  .Append(Esc(dto.IpAddress)).Append(',')
                  .Append(Esc(dto.UserAgent)).Append(',')
                  .Append(Esc(dto.DetailJson)).Append(',')
                  .Append(dto.CorrelationId ?? "").Append(',')
                  .AppendLine(dto.IntegrityAlert ? "1" : "0");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return ($"audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv", bytes);
        }

        public async Task<long> AppendAsync(AuditLog entry, CancellationToken ct = default)
        {
            entry.OccurredAt = DateTime.UtcNow; // trigger DB sẽ set hash-chain
            _db.AuditLogs.Add(entry);
            await _db.SaveChangesAsync(ct);
            return entry.AuditId;
        }

        // ---- helpers ----
        private static void Normalize(AuditQueryDto q)
        {
            if (q.Page < 1) q.Page = 1;
            q.PageSize = Math.Clamp(q.PageSize, 1, 100);
            if (!q.From.HasValue && !q.To.HasValue) { q.To = DateTime.UtcNow; q.From = q.To.Value.AddDays(-30); }
        }

        private static void ValidateRange(AuditQueryDto q)
        {
            if (q.From.HasValue && q.To.HasValue && q.From > q.To)
                throw new ArgumentException("From must be earlier than To.");
            var from = q.From ?? DateTime.UtcNow.AddDays(-30);
            var to = q.To ?? DateTime.UtcNow;
            if ((to - from).TotalDays > MaxRangeDays)
                throw new ArgumentException($"Date range cannot exceed {MaxRangeDays} days.");
        }

        private IQueryable<AuditLog> BuildQuery(AuditQueryDto q)
        {
            var from = q.From ?? DateTime.UtcNow.AddDays(-30);
            var to = (q.To ?? DateTime.UtcNow);

            var query = _db.AuditLogs.Where(x => x.OccurredAt >= from && x.OccurredAt <= to);
            if (!string.IsNullOrWhiteSpace(q.Actor)) query = query.Where(x => x.ActorEmail.Contains(q.Actor!));
            if (!string.IsNullOrWhiteSpace(q.Action)) query = query.Where(x => x.Action == q.Action);
            if (!string.IsNullOrWhiteSpace(q.Resource)) query = query.Where(x => x.Resource == q.Resource);
            return query;
        }
    }
}