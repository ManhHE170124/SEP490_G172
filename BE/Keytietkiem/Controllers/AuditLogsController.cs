using Keytietkiem.DTOs;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/audit")]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogService _svc;

        public AuditLogsController(IAuditLogService svc) => _svc = svc;

        // GET /api/audit?actor=...&action=...&resource=...&from=...&to=...&page=1&pageSize=20
        [HttpGet]
        [Authorize(Policy = "Audit.Read")]
        public async Task<ActionResult<PagedResultDto<AuditLogDto>>> Get([FromQuery] AuditQueryDto q, CancellationToken ct)
            => Ok(await _svc.SearchAsync(q, maskSensitive: !User.HasClaim("perm", "Audit.ViewSensitive"), ct));

        // GET /api/audit/export?... -> CSV (<= 50k rows)
        [HttpGet("export")]
        [Authorize(Policy = "Audit.Export")]
        public async Task<IActionResult> Export([FromQuery] AuditQueryDto q, CancellationToken ct)
        {
            var (name, bytes) = await _svc.ExportCsvAsync(q, maskSensitive: !User.HasClaim("perm", "Audit.ViewSensitive"), ct);
            return File(bytes, "text/csv", name);
        }

        // POST /api/audit  (nội bộ: ghi log)
        [HttpPost]
        [Authorize] // hoặc chỉ allow service account
        public async Task<ActionResult<long>> Append([FromBody] CreateAuditDto dto, CancellationToken ct)
        {
            var actorEmail = User?.Identity?.Name ?? dto.ActorEmail ?? "system";
            Guid? actorId = Guid.TryParse(User.FindFirst("sub")?.Value, out var g) ? g : dto.ActorId;

            var entry = new AuditLog
            {
                ActorId = actorId,
                ActorEmail = actorEmail,
                Action = dto.Action,
                Resource = dto.Resource,
                EntityId = dto.EntityId,
                DetailJson = dto.DetailJson,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                CorrelationId = dto.CorrelationId ?? Guid.NewGuid()
            };

            var id = await _svc.AppendAsync(entry, ct);
            return Created($"api/audit/{id}", id);
        }

        [HttpPut("{id:long}"), HttpDelete("{id:long}")]
        public IActionResult NotAllowed() => StatusCode(405, "Audit logs are immutable.");
    }
}
