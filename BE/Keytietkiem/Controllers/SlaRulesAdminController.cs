// File: Controllers/SlaRulesAdminController.cs
using System;
using System.Collections.Generic;
using System.Linq;
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

        // Dùng để đánh rank độ nghiêm trọng: Low < Medium < High < Critical (cho in-memory)
        private static readonly string[] SeverityOrder = new[]
        {
            "Low",
            "Medium",
            "High",
            "Critical"
        };

        // Data mặc định cho đủ matrix Severity x PriorityLevel (0=Standard,1=Priority,2=VIP)
        private sealed class DefaultSlaRuleDefinition
        {
            public string Severity { get; init; } = "";
            public int PriorityLevel { get; init; }
            public string Name { get; init; } = "";
            public int FirstResponseMinutes { get; init; }
            public int ResolutionMinutes { get; init; }
        }

        private static readonly DefaultSlaRuleDefinition[] DefaultSlaRulesMatrix = new[]
        {
            // Standard (0)
            new DefaultSlaRuleDefinition { Severity = "Low",      PriorityLevel = 0, Name = "Standard - Low",      FirstResponseMinutes = 480, ResolutionMinutes = 7200 },
            new DefaultSlaRuleDefinition { Severity = "Medium",   PriorityLevel = 0, Name = "Standard - Medium",   FirstResponseMinutes = 240, ResolutionMinutes = 4320 },
            new DefaultSlaRuleDefinition { Severity = "High",     PriorityLevel = 0, Name = "Standard - High",     FirstResponseMinutes = 120, ResolutionMinutes = 1440 },
            new DefaultSlaRuleDefinition { Severity = "Critical", PriorityLevel = 0, Name = "Standard - Critical", FirstResponseMinutes =  60, ResolutionMinutes =  720 },

            // Priority (1)
            new DefaultSlaRuleDefinition { Severity = "Low",      PriorityLevel = 1, Name = "Priority - Low",      FirstResponseMinutes = 240, ResolutionMinutes = 4320 },
            new DefaultSlaRuleDefinition { Severity = "Medium",   PriorityLevel = 1, Name = "Priority - Medium",   FirstResponseMinutes = 120, ResolutionMinutes = 1440 },
            new DefaultSlaRuleDefinition { Severity = "High",     PriorityLevel = 1, Name = "Priority - High",     FirstResponseMinutes =  60, ResolutionMinutes =  480 },
            new DefaultSlaRuleDefinition { Severity = "Critical", PriorityLevel = 1, Name = "Priority - Critical", FirstResponseMinutes =  30, ResolutionMinutes =  240 },

            // VIP (2)
            new DefaultSlaRuleDefinition { Severity = "Low",      PriorityLevel = 2, Name = "VIP - Low",           FirstResponseMinutes = 120, ResolutionMinutes = 1440 },
            new DefaultSlaRuleDefinition { Severity = "Medium",   PriorityLevel = 2, Name = "VIP - Medium",        FirstResponseMinutes =  60, ResolutionMinutes =  480 },
            new DefaultSlaRuleDefinition { Severity = "High",     PriorityLevel = 2, Name = "VIP - High",          FirstResponseMinutes =  30, ResolutionMinutes =  240 },
            new DefaultSlaRuleDefinition { Severity = "Critical", PriorityLevel = 2, Name = "VIP - Critical",      FirstResponseMinutes =  15, ResolutionMinutes =  120 },
        };

        public SlaRulesAdminController(IDbContextFactory<KeytietkiemDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// GET: /api/sla-rules-admin
        /// List SlaRule có filter + paging.
        /// Sort cố định: PriorityLevel -> SeverityRank -> FirstResponseMinutes -> ResolutionMinutes.
        /// Khi vào màn quản lý, nếu thiếu combo (Severity, PriorityLevel) trong matrix mặc định
        /// thì tự động insert thêm rule IsActive = false cho đủ.
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

            // Đảm bảo luôn có đủ matrix mặc định trong DB (ở trạng thái IsActive=false nếu được auto thêm)
            await EnsureDefaultSlaRulesAsync(db);

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

            // Sort cố định: PriorityLevel -> SeverityRank -> FirstResponseMinutes -> ResolutionMinutes
            // Dùng biểu thức ? : để EF dịch được, tránh gọi method C#
            q = q
                .OrderBy(r => r.PriorityLevel)
                .ThenBy(r =>
                    r.Severity == "Low"
                        ? 0
                        : r.Severity == "Medium"
                            ? 1
                            : r.Severity == "High"
                                ? 2
                                : r.Severity == "Critical"
                                    ? 3
                                    : 4)
                .ThenBy(r => r.FirstResponseMinutes)
                .ThenBy(r => r.ResolutionMinutes);

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
        /// Cho phép trùng (Severity, PriorityLevel).
        /// Ràng buộc chỉ áp dụng khi ACTIVE.
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

            // Cho phép trùng (Severity, PriorityLevel).
            // Ràng buộc "1 ACTIVE / cặp & SLA monotonic" xử lý khi IsActive = true.

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

            // Nếu tạo rule đang ACTIVE → phải pass constraint SLA & đảm bảo 1 active/cặp
            if (entity.IsActive)
            {
                var error = await ValidateSlaConstraintsAsync(db, entity, willBeActive: true);
                if (error != null)
                {
                    return BadRequest(new { message = error });
                }

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
        /// Cho phép trùng (Severity, PriorityLevel) với rule khác, chỉ ràng buộc khi ACTIVE.
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

            // Cho phép trùng cặp (Severity, PriorityLevel) với rule khác.
            // Ràng buộc chỉ áp dụng khi ACTIVE.

            // Cập nhật giá trị
            entity.Name = trimmedName;
            entity.Severity = normalizedSeverity;
            entity.PriorityLevel = dto.PriorityLevel;
            entity.FirstResponseMinutes = dto.FirstResponseMinutes;
            entity.ResolutionMinutes = dto.ResolutionMinutes;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            // Nếu sau update rule đang ACTIVE → phải pass constraint SLA & đảm bảo 1 active/cặp
            if (entity.IsActive)
            {
                var error = await ValidateSlaConstraintsAsync(db, entity, willBeActive: true);
                if (error != null)
                {
                    return BadRequest(new { message = error });
                }

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
        /// - Khi bật:
        ///   + Đảm bảo không có rule nào khác cùng Severity + PriorityLevel đang active.
        ///   + Đảm bảo toàn bộ rule đang ACTIVE thoả constraint:
        ///       * Cùng Severity: PriorityLevel cao hơn phải có thời gian phản hồi / xử lý ngắn hơn.
        ///       * Cùng PriorityLevel: Severity nghiêm trọng hơn phải có thời gian phản hồi / xử lý ngắn hơn.
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
                // Chuẩn bị bật rule => validate constraint SLA
                var error = await ValidateSlaConstraintsAsync(db, entity, willBeActive: true);
                if (error != null)
                {
                    return BadRequest(new { message = error });
                }

                // Đảm bảo 1 ACTIVE / (Severity, PriorityLevel)
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

        /// <summary>
        /// Lấy rank độ nghiêm trọng của Severity (Low &lt; Medium &lt; High &lt; Critical).
        /// Dùng cho so sánh logic "nghiêm trọng hơn" trong in-memory LINQ.
        /// </summary>
        private static int GetSeverityRank(string severity)
        {
            for (var i = 0; i < SeverityOrder.Length; i++)
            {
                if (string.Equals(severity, SeverityOrder[i], StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return SeverityOrder.Length; // Unknown -> lớn nhất (coi như ít quan trọng nhất)
        }

        /// <summary>
        /// Validate toàn bộ tập SLA rule đang ACTIVE (sau khi giả lập bật/tắt).
        /// - Cùng Severity: PriorityLevel cao hơn phải có FirstResponse & Resolution nhỏ hơn.
        /// - Cùng PriorityLevel: Severity nghiêm trọng hơn phải có FirstResponse & Resolution nhỏ hơn.
        /// Trả về null nếu OK, hoặc message lỗi nếu vi phạm.
        /// </summary>
        private static string? ValidateSlaMonotonicity(List<SlaRule> activeRules)
        {
            if (activeRules == null || activeRules.Count == 0) return null;

            // 1) Cùng Severity: PriorityLevel tăng -> thời gian giảm
            foreach (var group in activeRules.GroupBy(r => r.Severity, StringComparer.OrdinalIgnoreCase))
            {
                var ordered = group
                    .OrderBy(r => r.PriorityLevel)
                    .ToList();

                for (int i = 1; i < ordered.Count; i++)
                {
                    var lowerPriority = ordered[i - 1]; // Priority thấp hơn
                    var higherPriority = ordered[i];    // Priority cao hơn

                    // PriorityLevel cao hơn phải có thời gian nhỏ hơn
                    if (!(lowerPriority.FirstResponseMinutes > higherPriority.FirstResponseMinutes &&
                          lowerPriority.ResolutionMinutes > higherPriority.ResolutionMinutes))
                    {
                        return
                            $"Với cùng Severity = {group.Key}, PriorityLevel {higherPriority.PriorityLevel} (ưu tiên cao hơn) " +
                            $"phải có thời gian phản hồi và xử lý ngắn hơn PriorityLevel {lowerPriority.PriorityLevel}.";
                    }
                }
            }

            // 2) Cùng PriorityLevel: Severity nghiêm trọng hơn -> thời gian giảm
            foreach (var group in activeRules.GroupBy(r => r.PriorityLevel))
            {
                var ordered = group
                    .OrderBy(r => GetSeverityRank(r.Severity))
                    .ToList();

                for (int i = 1; i < ordered.Count; i++)
                {
                    var lessSevere = ordered[i - 1]; // ít nghiêm trọng hơn
                    var moreSevere = ordered[i];     // nghiêm trọng hơn

                    var lessRank = GetSeverityRank(lessSevere.Severity);
                    var moreRank = GetSeverityRank(moreSevere.Severity);

                    if (moreRank <= lessRank)
                    {
                        continue; // Không phải "nghiêm trọng hơn" thực sự, bỏ qua
                    }

                    if (!(lessSevere.FirstResponseMinutes > moreSevere.FirstResponseMinutes &&
                          lessSevere.ResolutionMinutes > moreSevere.ResolutionMinutes))
                    {
                        return
                            $"Với cùng PriorityLevel = {group.Key}, Severity {moreSevere.Severity} (nghiêm trọng hơn) " +
                            $"phải có thời gian phản hồi và xử lý ngắn hơn Severity {lessSevere.Severity}.";
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Helper validate constraint SLA:
        /// - Lấy tất cả rule ACTIVE hiện tại từ DB.
        /// - Thay thế/giả lập candidate sẽ ACTIVE.
        /// - Bỏ các rule khác cùng (Severity, PriorityLevel) với candidate (vì sẽ bị tắt khi bật candidate).
        /// - Chạy ValidateSlaMonotonicity trên tập active mới.
        /// </summary>
        private static async Task<string?> ValidateSlaConstraintsAsync(
            KeytietkiemDbContext db,
            SlaRule candidate,
            bool willBeActive)
        {
            // Lấy tất cả rule đang ACTIVE hiện tại
            var active = await db.SlaRules
                .Where(r => r.IsActive)
                .AsNoTracking()
                .ToListAsync();

            // Xoá bản cũ của candidate (nếu đã tồn tại)
            if (candidate.SlaRuleId != 0)
            {
                active.RemoveAll(r => r.SlaRuleId == candidate.SlaRuleId);
            }

            if (willBeActive)
            {
                // Nếu candidate sẽ được bật:
                //   - Bỏ các rule khác cùng (Severity, PriorityLevel) (vì bật cái này sẽ tắt cái kia)
                active.RemoveAll(r =>
                    r.Severity == candidate.Severity &&
                    r.PriorityLevel == candidate.PriorityLevel);

                //   - Thêm candidate (ACTIVE) vào snapshot
                active.Add(new SlaRule
                {
                    SlaRuleId = candidate.SlaRuleId,
                    Name = candidate.Name,
                    Severity = candidate.Severity,
                    PriorityLevel = candidate.PriorityLevel,
                    FirstResponseMinutes = candidate.FirstResponseMinutes,
                    ResolutionMinutes = candidate.ResolutionMinutes,
                    IsActive = true,
                    CreatedAt = candidate.CreatedAt,
                    UpdatedAt = candidate.UpdatedAt
                });
            }

            if (active.Count == 0)
            {
                return null;
            }

            return ValidateSlaMonotonicity(active);
        }

        /// <summary>
        /// Đảm bảo luôn có đủ 12 rule mặc định (Standard/Priority/VIP x Low/Medium/High/Critical).
        /// - Nếu thiếu cặp (Severity, PriorityLevel) nào thì tự động thêm rule mới IsActive=false.
        /// - Không đụng vào các rule đã có (kể cả đã chỉnh Name/timeout).
        /// </summary>
        private static async Task EnsureDefaultSlaRulesAsync(KeytietkiemDbContext db)
        {
            var existingPairs = await db.SlaRules
                .Select(r => new { r.Severity, r.PriorityLevel })
                .Distinct()
                .ToListAsync();

            var existingSet = new HashSet<(string Severity, int PriorityLevel)>(
                existingPairs.Select(e => (e.Severity, e.PriorityLevel)));

            var now = DateTime.UtcNow;
            var hasChanges = false;

            foreach (var def in DefaultSlaRulesMatrix)
            {
                var key = (def.Severity, def.PriorityLevel);
                if (!existingSet.Contains(key))
                {
                    db.SlaRules.Add(new SlaRule
                    {
                        Name = def.Name,
                        Severity = def.Severity,
                        PriorityLevel = def.PriorityLevel,
                        FirstResponseMinutes = def.FirstResponseMinutes,
                        ResolutionMinutes = def.ResolutionMinutes,
                        IsActive = false,
                        CreatedAt = now
                    });

                    existingSet.Add(key);
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await db.SaveChangesAsync();
            }
        }
    }
}
