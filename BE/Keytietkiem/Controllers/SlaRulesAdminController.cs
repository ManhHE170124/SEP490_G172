// File: Controllers/SlaRulesAdminController.cs
using System;
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.SlaRules;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/sla-rules-admin")]
    public class SlaRulesAdminController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;

        // Danh sách Severity cho phép (fix-cứng để đồng bộ với Ticket / TicketSubject / FE)
        private static readonly string[] AllowedSeverities = new[]
        {
            "Low",
            "Medium",
            "High",
            "Critical"
        };

        public SlaRulesAdminController(IDbContextFactory<KeytietkiemDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// GET: /api/sla-rules-admin
        /// List SlaRule có filter + paging.
        /// Sort cố định: Severity -> PriorityLevel -> FirstResponseMinutes.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<SlaRuleAdminListItemDto>>> List(
            [FromQuery] string? severity,
            [FromQuery] int? priorityLevel,
            [FromQuery] bool? active,
            // giữ sort/direction để đồng bộ pattern, hiện chưa dùng
            [FromQuery] string? sort = null,
            [FromQuery] string? direction = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var q = db.SlaRules
                      .AsNoTracking()
                      .AsQueryable();

            // Filter Severity
            if (!string.IsNullOrWhiteSpace(severity))
            {
                var normalizedSeverity = NormalizeSeverity(severity.Trim());
                if (normalizedSeverity == null)
                {
                    return BadRequest(new
                    {
                        message = "Mức độ (Severity) không hợp lệ. Giá trị hợp lệ: " +
                                  string.Join(", ", AllowedSeverities) + "."
                    });
                }

                q = q.Where(r => r.Severity == normalizedSeverity);
            }

            // Filter PriorityLevel
            if (priorityLevel.HasValue)
            {
                q = q.Where(r => r.PriorityLevel == priorityLevel.Value);
            }

            // Filter IsActive
            if (active.HasValue)
            {
                q = q.Where(r => r.IsActive == active.Value);
            }

            // Sort cố định: Severity -> PriorityLevel -> FirstResponseMinutes
            q = q.OrderBy(r => r.Severity)
                 .ThenBy(r => r.PriorityLevel)
                 .ThenBy(r => r.FirstResponseMinutes);

            // Paging giống style ở các controller khác
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var totalItems = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new SlaRuleAdminListItemDto
                {
                    SlaRuleId = r.SlaRuleId,
                    Name = r.Name,
                    Severity = r.Severity,
                    PriorityLevel = r.PriorityLevel,
                    FirstResponseMinutes = r.FirstResponseMinutes,
                    ResolutionMinutes = r.ResolutionMinutes,
                    IsActive = r.IsActive,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            var result = new PagedResult<SlaRuleAdminListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Items = items
            };

            return Ok(result);
        }

        /// <summary>
        /// GET: /api/sla-rules-admin/{slaRuleId}
        /// Lấy chi tiết 1 SlaRule.
        /// </summary>
        [HttpGet("{slaRuleId:int}")]
        public async Task<ActionResult<SlaRuleAdminDetailDto>> GetById(int slaRuleId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var dto = await db.SlaRules
                .AsNoTracking()
                .Where(r => r.SlaRuleId == slaRuleId)
                .Select(r => new SlaRuleAdminDetailDto
                {
                    SlaRuleId = r.SlaRuleId,
                    Name = r.Name,
                    Severity = r.Severity,
                    PriorityLevel = r.PriorityLevel,
                    FirstResponseMinutes = r.FirstResponseMinutes,
                    ResolutionMinutes = r.ResolutionMinutes,
                    IsActive = r.IsActive,
                    CreatedAt = r.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (dto == null) return NotFound();

            return Ok(dto);
        }

        /// <summary>
        /// POST: /api/sla-rules-admin
        /// Tạo mới 1 SlaRule.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SlaRuleAdminDetailDto>> Create(
            SlaRuleAdminCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Validate Name
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(new
                {
                    message = "Tên SLA rule không được để trống."
                });
            }

            var trimmedName = dto.Name.Trim();
            if (trimmedName.Length > 120)
            {
                return BadRequest(new
                {
                    message = "Tên SLA rule không được vượt quá 120 ký tự."
                });
            }

            // Validate Severity
            if (string.IsNullOrWhiteSpace(dto.Severity))
            {
                return BadRequest(new
                {
                    message = "Mức độ (Severity) không được để trống."
                });
            }

            var normalizedSeverity = NormalizeSeverity(dto.Severity.Trim());
            if (normalizedSeverity == null)
            {
                return BadRequest(new
                {
                    message = "Mức độ (Severity) không hợp lệ. Giá trị hợp lệ: " +
                              string.Join(", ", AllowedSeverities) + "."
                });
            }

            // PriorityLevel >= 0
            if (dto.PriorityLevel < 0)
            {
                return BadRequest(new
                {
                    message = "Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0."
                });
            }

            // FirstResponseMinutes > 0
            if (dto.FirstResponseMinutes <= 0)
            {
                return BadRequest(new
                {
                    message = "Thời gian phản hồi đầu tiên (phút) phải lớn hơn 0."
                });
            }

            // ResolutionMinutes > 0
            if (dto.ResolutionMinutes <= 0)
            {
                return BadRequest(new
                {
                    message = "Thời gian xử lý (phút) phải lớn hơn 0."
                });
            }

            // ResolutionMinutes >= FirstResponseMinutes
            if (dto.ResolutionMinutes < dto.FirstResponseMinutes)
            {
                return BadRequest(new
                {
                    message = "Thời gian xử lý (ResolutionMinutes) phải lớn hơn hoặc bằng thời gian phản hồi đầu tiên (FirstResponseMinutes)."
                });
            }

            // Không cho trùng cặp (Severity, PriorityLevel)
            var duplicate = await db.SlaRules.AnyAsync(r =>
                r.Severity == normalizedSeverity &&
                r.PriorityLevel == dto.PriorityLevel);

            if (duplicate)
            {
                return BadRequest(new
                {
                    message = "Đã tồn tại SLA rule khác với cùng Severity và PriorityLevel. Vui lòng chọn kết hợp khác."
                });
            }

            var entity = new SlaRule
            {
                Name = trimmedName,
                Severity = normalizedSeverity,
                PriorityLevel = dto.PriorityLevel,
                FirstResponseMinutes = dto.FirstResponseMinutes,
                ResolutionMinutes = dto.ResolutionMinutes,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            // Nếu tạo rule đang ACTIVE → tắt các rule khác cùng (Severity, PriorityLevel)
            if (entity.IsActive)
            {
                await EnsureSingleActivePerSeverityAndPriority(db, entity);
            }

            db.SlaRules.Add(entity);
            await db.SaveChangesAsync();

            var result = new SlaRuleAdminDetailDto
            {
                SlaRuleId = entity.SlaRuleId,
                Name = entity.Name,
                Severity = entity.Severity,
                PriorityLevel = entity.PriorityLevel,
                FirstResponseMinutes = entity.FirstResponseMinutes,
                ResolutionMinutes = entity.ResolutionMinutes,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt
            };

            return CreatedAtAction(nameof(GetById), new { slaRuleId = entity.SlaRuleId }, result);
        }

        /// <summary>
        /// PUT: /api/sla-rules-admin/{slaRuleId}
        /// Cập nhật 1 SlaRule.
        /// </summary>
        [HttpPut("{slaRuleId:int}")]
        public async Task<IActionResult> Update(
            int slaRuleId,
            SlaRuleAdminUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SlaRules
                .FirstOrDefaultAsync(r => r.SlaRuleId == slaRuleId);

            if (entity == null) return NotFound();

            // Validate Name
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(new
                {
                    message = "Tên SLA rule không được để trống."
                });
            }

            var trimmedName = dto.Name.Trim();
            if (trimmedName.Length > 120)
            {
                return BadRequest(new
                {
                    message = "Tên SLA rule không được vượt quá 120 ký tự."
                });
            }

            // Validate Severity
            if (string.IsNullOrWhiteSpace(dto.Severity))
            {
                return BadRequest(new
                {
                    message = "Mức độ (Severity) không được để trống."
                });
            }

            var normalizedSeverity = NormalizeSeverity(dto.Severity.Trim());
            if (normalizedSeverity == null)
            {
                return BadRequest(new
                {
                    message = "Mức độ (Severity) không hợp lệ. Giá trị hợp lệ: " +
                              string.Join(", ", AllowedSeverities) + "."
                });
            }

            if (dto.PriorityLevel < 0)
            {
                return BadRequest(new
                {
                    message = "Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0."
                });
            }

            if (dto.FirstResponseMinutes <= 0)
            {
                return BadRequest(new
                {
                    message = "Thời gian phản hồi đầu tiên (phút) phải lớn hơn 0."
                });
            }

            if (dto.ResolutionMinutes <= 0)
            {
                return BadRequest(new
                {
                    message = "Thời gian xử lý (phút) phải lớn hơn 0."
                });
            }

            if (dto.ResolutionMinutes < dto.FirstResponseMinutes)
            {
                return BadRequest(new
                {
                    message = "Thời gian xử lý (ResolutionMinutes) phải lớn hơn hoặc bằng thời gian phản hồi đầu tiên (FirstResponseMinutes)."
                });
            }

            // Không cho trùng cặp (Severity, PriorityLevel) trừ chính nó
            var duplicate = await db.SlaRules.AnyAsync(r =>
                r.SlaRuleId != slaRuleId &&
                r.Severity == normalizedSeverity &&
                r.PriorityLevel == dto.PriorityLevel);

            if (duplicate)
            {
                return BadRequest(new
                {
                    message = "Đã tồn tại SLA rule khác với cùng Severity và PriorityLevel. Vui lòng chọn kết hợp khác."
                });
            }

            // Cập nhật giá trị
            entity.Name = trimmedName;
            entity.Severity = normalizedSeverity;
            entity.PriorityLevel = dto.PriorityLevel;
            entity.FirstResponseMinutes = dto.FirstResponseMinutes;
            entity.ResolutionMinutes = dto.ResolutionMinutes;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            // Nếu sau update rule đang ACTIVE → tắt các rule cùng (Severity, PriorityLevel)
            if (entity.IsActive)
            {
                await EnsureSingleActivePerSeverityAndPriority(db, entity);
            }

            await db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// DELETE: /api/sla-rules-admin/{slaRuleId}
        /// Xoá hẳn 1 SlaRule.
        /// Không cho xoá nếu đã có Ticket tham chiếu.
        /// </summary>
        [HttpDelete("{slaRuleId:int}")]
        public async Task<IActionResult> Delete(int slaRuleId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SlaRules
                .FirstOrDefaultAsync(r => r.SlaRuleId == slaRuleId);

            if (entity == null) return NotFound();

            var hasTickets = await db.Tickets
                .AnyAsync(t => t.SlaRuleId == slaRuleId);

            if (hasTickets)
            {
                return BadRequest(new
                {
                    message = "Không thể xoá SLA rule đang được áp dụng cho ticket. " +
                              "Vui lòng tắt trạng thái hoạt động (IsActive) thay vì xoá."
                });
            }

            db.SlaRules.Remove(entity);
            await db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// PATCH: /api/sla-rules-admin/{slaRuleId}/toggle
        /// Bật / tắt IsActive cho SlaRule.
        /// - Khi bật: đảm bảo không có rule nào khác cùng Severity + PriorityLevel đang active.
        /// - Khi tắt: chỉ tắt rule hiện tại.
        /// </summary>
        [HttpPatch("{slaRuleId:int}/toggle")]
        public async Task<IActionResult> Toggle(int slaRuleId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SlaRules
                .FirstOrDefaultAsync(r => r.SlaRuleId == slaRuleId);

            if (entity == null) return NotFound();

            if (!entity.IsActive)
            {
                // Đang tắt -> chuẩn bị bật lên: tắt các rule khác cùng (Severity, PriorityLevel)
                await EnsureSingleActivePerSeverityAndPriority(db, entity);
                entity.IsActive = true;
            }
            else
            {
                // Đang bật -> tắt đi
                entity.IsActive = false;
            }

            await db.SaveChangesAsync();

            return Ok(new { entity.SlaRuleId, entity.IsActive });
        }

        /// <summary>
        /// Chuẩn hoá severity theo danh sách cho phép (case-insensitive).
        /// Trả về null nếu không hợp lệ.
        /// </summary>
        private static string? NormalizeSeverity(string severity)
        {
            if (string.IsNullOrWhiteSpace(severity)) return null;

            foreach (var allowed in AllowedSeverities)
            {
                if (string.Equals(severity, allowed, StringComparison.OrdinalIgnoreCase))
                {
                    return allowed;
                }
            }

            return null;
        }

        /// <summary>
        /// Đảm bảo chỉ có duy nhất 1 rule ACTIVE cho mỗi cặp (Severity, PriorityLevel).
        /// Tự động tắt các rule khác cùng cặp nếu cần.
        /// </summary>
        private static async Task EnsureSingleActivePerSeverityAndPriority(
            KeytietkiemDbContext db,
            SlaRule current)
        {
            var sameGroupActive = await db.SlaRules
                .Where(r => r.SlaRuleId != current.SlaRuleId
                            && r.Severity == current.Severity
                            && r.PriorityLevel == current.PriorityLevel
                            && r.IsActive)
                .ToListAsync();

            foreach (var other in sameGroupActive)
            {
                other.IsActive = false;
            }
        }
    }
}
