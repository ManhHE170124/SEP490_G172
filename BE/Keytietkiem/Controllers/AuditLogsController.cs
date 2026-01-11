// File: Controllers/AuditLogsController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Keytietkiem.DTOs.AuditLogs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize(Roles = "Admin")] // TODO: bật khi cấu hình auth/roles cho admin
    public class AuditLogsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _db;

        public AuditLogsController(KeytietkiemDbContext db)
        {
            _db = db;
        }

        // ✅ Timezone helper (IANA trên Linux, Windows fallback)
        private static TimeZoneInfo GetBkkTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok"); }
            catch
            {
                // Windows timezone id
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
        }

        private static DateTime ToUtcFromBkkDateStart(DateTime dateOnly)
        {
            var tz = GetBkkTimeZone();
            var localStart = DateTime.SpecifyKind(dateOnly.Date, DateTimeKind.Unspecified); // 00:00:00
            return TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        }

        private static DateTime ToUtcFromBkkDateEnd(DateTime dateOnly)
        {
            var tz = GetBkkTimeZone();
            var localEnd = DateTime.SpecifyKind(dateOnly.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified); // 23:59:59.9999999
            return TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);
        }

        /// <summary>
        /// Search + xem danh sách AuditLog có filter + phân trang.
        /// GET /api/auditlogs?Page=1&PageSize=20&ActorEmail=...
        /// (ActorEmail = ô search chung)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<AuditLogListResponseDto>> GetAuditLogs([FromQuery] AuditLogListFilterDto filter)
        {
            if (filter.Page <= 0) filter.Page = 1;
            if (filter.PageSize <= 0 || filter.PageSize > 200) filter.PageSize = 20;

            var query = _db.AuditLogs.AsNoTracking();

            // ===== Time range filter (FE gửi YYYY-MM-DD => coi là ngày theo UTC+7) =====
            if (filter.From.HasValue)
            {
                var fromUtc = ToUtcFromBkkDateStart(filter.From.Value);
                query = query.Where(x => x.OccurredAt >= fromUtc);
            }

            if (filter.To.HasValue)
            {
                var toUtc = ToUtcFromBkkDateEnd(filter.To.Value);
                query = query.Where(x => x.OccurredAt <= toUtc);
            }

            // ===== Search chung (ActorEmail là keyword) =====
            // Gộp search cho: ActorEmail, ActorRole, Action, EntityType, EntityId
            if (!string.IsNullOrWhiteSpace(filter.ActorEmail))
            {
                var keyword = filter.ActorEmail.Trim();

                query = query.Where(x =>
                    (x.ActorEmail != null && x.ActorEmail.Contains(keyword)) ||
                    (x.ActorRole != null && x.ActorRole.Contains(keyword)) ||
                    (x.Action != null && x.Action.Contains(keyword)) ||
                    (x.EntityType != null && x.EntityType.Contains(keyword)) ||
                    (x.EntityId != null && x.EntityId.Contains(keyword)));
            }

            // ===== Filter dropdown: ActorRole, Action, EntityType (exact match) =====
            if (!string.IsNullOrWhiteSpace(filter.ActorRole))
            {
                var role = filter.ActorRole.Trim();

                // ✅ Hỗ trợ filter "System" cho case DB lưu ActorRole null/"" (hoặc "System")
                if (role.Equals("System", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x =>
                        x.ActorRole == null || x.ActorRole == "" ||
                        x.ActorRole == "System" || x.ActorRole == "SYSTEM");
                }
                else
                {
                    query = query.Where(x => x.ActorRole == role);
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.Action))
            {
                var action = filter.Action.Trim();
                query = query.Where(x => x.Action == action);
            }

            if (!string.IsNullOrWhiteSpace(filter.EntityType))
            {
                var entityType = filter.EntityType.Trim();
                query = query.Where(x => x.EntityType == entityType);
            }

            // ===== Count total sau khi filter =====
            var totalItems = await query.CountAsync();
            var skip = (filter.Page - 1) * filter.PageSize;

            // ===== Sort động theo SortBy / SortDirection =====
            // Mặc định: OccurredAt DESC
            var sortBy = (filter.SortBy ?? "OccurredAt").Trim();
            var sortDir = (filter.SortDirection ?? "desc").Trim().ToLowerInvariant();
            var desc = sortDir != "asc"; // chỉ "asc" mới là asc, còn lại = desc

            var sortKey = sortBy.ToLowerInvariant();
            IQueryable<AuditLog> orderedQuery;

            switch (sortKey)
            {
                case "actoremail":
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.ActorEmail).ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.ActorEmail).ThenBy(x => x.AuditId);
                    break;

                case "actorrole":
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.ActorRole).ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.ActorRole).ThenBy(x => x.AuditId);
                    break;

                case "action":
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.Action).ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.Action).ThenBy(x => x.AuditId);
                    break;

                case "entitytype":
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.EntityType).ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.EntityType).ThenBy(x => x.AuditId);
                    break;

                case "entityid":
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.EntityId).ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.EntityId).ThenBy(x => x.AuditId);
                    break;

                case "occurredat":
                default:
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.OccurredAt).ThenBy(x => x.AuditId);
                    break;
            }

            var pageEntities = await orderedQuery
                .Skip(skip)
                .Take(filter.PageSize)
                .ToListAsync();

            var items = pageEntities
                .Select(x => new AuditLogListItemDto
                {
                    AuditId = x.AuditId,
                    OccurredAt = x.OccurredAt,
                    ActorId = x.ActorId,
                    ActorEmail = x.ActorEmail,
                    ActorRole = x.ActorRole,
                    SessionId = x.SessionId,
                    IpAddress = x.IpAddress,
                    Action = x.Action ?? "",
                    EntityType = x.EntityType,
                    // ✅ KHÔNG TRẢ EntityId ở list
                    Changes = AuditDiffHelper.BuildDiff(x.BeforeDataJson, x.AfterDataJson)
                })
                .ToList();

            var result = new AuditLogListResponseDto
            {
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalItems = totalItems,
                Items = items
            };

            return Ok(result);
        }

        /// <summary>
        /// GET /api/auditlogs/options
        /// </summary>
        [HttpGet("options")]
        public async Task<ActionResult<AuditLogFilterOptionsDto>> GetFilterOptions()
        {
            var actions = await _db.AuditLogs
                .AsNoTracking()
                .Where(x => x.Action != null && x.Action != "")
                .Select(x => x.Action!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var entityTypes = await _db.AuditLogs
                .AsNoTracking()
                .Where(x => x.EntityType != null && x.EntityType != "")
                .Select(x => x.EntityType!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var actorRoles = await _db.AuditLogs
                .AsNoTracking()
                .Where(x => x.ActorRole != null && x.ActorRole != "")
                .Select(x => x.ActorRole!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            // ✅ Nếu có record ActorRole null/"" => thêm option "System"
            var hasEmptyRole = await _db.AuditLogs.AsNoTracking()
                .AnyAsync(x => x.ActorRole == null || x.ActorRole == "");
            if (hasEmptyRole && !actorRoles.Any(r => r.Equals("System", StringComparison.OrdinalIgnoreCase)))
            {
                actorRoles.Insert(0, "System");
            }

            var dto = new AuditLogFilterOptionsDto
            {
                Actions = actions,
                EntityTypes = entityTypes,
                ActorRoles = actorRoles
            };

            return Ok(dto);
        }

        /// <summary>
        /// GET /api/auditlogs/{id}
        /// </summary>
        [HttpGet("{id:long}")]
        public async Task<ActionResult<AuditLogDetailDto>> GetAuditLog(long id)
        {
            var entity = await _db.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AuditId == id);

            if (entity == null)
            {
                return NotFound();
            }

            var dto = new AuditLogDetailDto
            {
                AuditId = entity.AuditId,
                OccurredAt = entity.OccurredAt,
                ActorId = entity.ActorId,
                ActorEmail = entity.ActorEmail,
                ActorRole = entity.ActorRole,
                SessionId = entity.SessionId,
                IpAddress = entity.IpAddress,
                Action = entity.Action ?? "",
                EntityType = entity.EntityType,
                EntityId = entity.EntityId,
                BeforeDataJson = entity.BeforeDataJson,
                AfterDataJson = entity.AfterDataJson,
                Changes = AuditDiffHelper.BuildDiff(entity.BeforeDataJson, entity.AfterDataJson)
            };

            return Ok(dto);
        }
    }

    public class AuditLogFilterOptionsDto
    {
        public System.Collections.Generic.List<string> Actions { get; set; } = new();
        public System.Collections.Generic.List<string> EntityTypes { get; set; } = new();
        public System.Collections.Generic.List<string> ActorRoles { get; set; } = new();
    }
}
