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

        /// <summary>
        /// Search + xem danh sách AuditLog có filter + phân trang.
        /// GET /api/auditlogs?Page=1&PageSize=20&ActorEmail=... 
        /// (ActorEmail = ô search chung)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<AuditLogListResponseDto>> GetAuditLogs(
            [FromQuery] AuditLogListFilterDto filter)
        {
            if (filter.Page <= 0) filter.Page = 1;
            if (filter.PageSize <= 0 || filter.PageSize > 200) filter.PageSize = 20;

            var query = _db.AuditLogs.AsNoTracking();

            // ===== Time range filter =====
            if (filter.From.HasValue)
            {
                query = query.Where(x => x.OccurredAt >= filter.From.Value);
            }

            if (filter.To.HasValue)
            {
                query = query.Where(x => x.OccurredAt <= filter.To.Value);
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
                query = query.Where(x => x.ActorRole == role);
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
            var desc = sortDir != "asc"; // nếu không truyền hoặc truyền khác "asc" → desc

            var sortKey = sortBy.ToLowerInvariant();

            IQueryable<AuditLog> orderedQuery;

            switch (sortKey)
            {
                case "actoremail":
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.ActorEmail)
                               .ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.ActorEmail)
                               .ThenBy(x => x.AuditId);
                    break;

                case "actorrole":
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.ActorRole)
                               .ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.ActorRole)
                               .ThenBy(x => x.AuditId);
                    break;

                case "action":
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.Action)
                               .ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.Action)
                               .ThenBy(x => x.AuditId);
                    break;

                case "entitytype":
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.EntityType)
                               .ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.EntityType)
                               .ThenBy(x => x.AuditId);
                    break;

                case "entityid":
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.EntityId)
                               .ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.EntityId)
                               .ThenBy(x => x.AuditId);
                    break;

                // Default: OccurredAt
                case "occurredat":
                default:
                    orderedQuery = desc
                        ? query.OrderByDescending(x => x.OccurredAt)
                               .ThenByDescending(x => x.AuditId)
                        : query.OrderBy(x => x.OccurredAt)
                               .ThenBy(x => x.AuditId);
                    break;
            }

            // Lấy page entity trước, rồi build DTO + Changes ở memory
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
                    Action = x.Action,
                    EntityType = x.EntityType,
                    EntityId = x.EntityId,
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
        /// Endpoint trả về danh sách option không trùng nhau
        /// cho dropdown: Action, EntityType, ActorRole.
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

            var dto = new AuditLogFilterOptionsDto
            {
                Actions = actions,
                EntityTypes = entityTypes,
                ActorRoles = actorRoles
            };

            return Ok(dto);
        }

        /// <summary>
        /// Xem chi tiết 1 audit log.
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
                Action = entity.Action,
                EntityType = entity.EntityType,
                EntityId = entity.EntityId,
                BeforeDataJson = entity.BeforeDataJson,
                AfterDataJson = entity.AfterDataJson,
                Changes = AuditDiffHelper.BuildDiff(entity.BeforeDataJson, entity.AfterDataJson)
            };

            return Ok(dto);
        }
    }

    /// <summary>
    /// DTO trả về cho endpoint /api/auditlogs/options
    /// </summary>
    public class AuditLogFilterOptionsDto
    {
        public System.Collections.Generic.List<string> Actions { get; set; }
            = new System.Collections.Generic.List<string>();

        public System.Collections.Generic.List<string> EntityTypes { get; set; }
            = new System.Collections.Generic.List<string>();

        public System.Collections.Generic.List<string> ActorRoles { get; set; }
            = new System.Collections.Generic.List<string>();
    }
}
