// File: Controllers/TicketSubjectTemplatesAdminController.cs
using System.Collections.Generic;
using Keytietkiem.Utils;
using Keytietkiem.Constants;
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Tickets;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/ticket-subject-templates-admin")]
    public class TicketSubjectTemplatesAdminController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IAuditLogger _auditLogger;

        // ==== CONSTANTS: Severity & Category fix-cứng ====

        private static readonly string[] AllowedSeverities = new[]
        {
            "Low",
            "Medium",
            "High",
            "Critical"
        };

        private static readonly string[] AllowedCategories = new[]
        {
            "Account",
            "General",
            "Key",
            "Payment",
            "Refund",
            "Security",
            "Support"
        };

        public TicketSubjectTemplatesAdminController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IAuditLogger auditLogger)
        {
            _dbFactory = dbFactory;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// GET: /api/ticket-subject-templates-admin
        /// List TicketSubjectTemplate có filter + paging.
        /// Sort mặc định: Category -> Severity -> TemplateCode.
        /// </summary>
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<ActionResult<PagedResult<TicketSubjectTemplateAdminListItemDto>>> List(
            [FromQuery] string? keyword,
            [FromQuery] string? severity,
            [FromQuery] string? category,
            [FromQuery] bool? active,
            [FromQuery] string? sort = null,
            [FromQuery] string? direction = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var q = db.TicketSubjectTemplates
                .AsNoTracking()
                .AsQueryable();

            // Filter keyword: TemplateCode / Title / Category
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();

                q = q.Where(t =>
                    t.TemplateCode.Contains(kw) ||
                    t.Title.Contains(kw) ||
                    (t.Category != null && t.Category.Contains(kw)));
            }

            // Filter severity
            if (!string.IsNullOrWhiteSpace(severity))
            {
                var sev = severity.Trim();
                q = q.Where(t => t.Severity == sev);
            }

            // Filter category (code BE: Payment, Key, Account, ...)
            if (!string.IsNullOrWhiteSpace(category))
            {
                var cat = category.Trim();
                q = q.Where(t => t.Category == cat);
            }

            // Filter IsActive
            if (active.HasValue)
            {
                q = q.Where(t => t.IsActive == active.Value);
            }

            // Sort
            var sortSafe = (sort ?? "").Trim().ToLowerInvariant();
            var directionSafe = (direction ?? "asc").Trim().ToLowerInvariant();
            var desc = directionSafe == "desc";

            // Mặc định: Category -> Severity (Low..Critical) -> TemplateCode
            switch (sortSafe)
            {
                case "title":
                    q = desc
                        ? q.OrderByDescending(t => t.Title).ThenBy(t => t.TemplateCode)
                        : q.OrderBy(t => t.Title).ThenBy(t => t.TemplateCode);
                    break;

                case "severity":
                    // Sort theo Severity với thứ tự Low -> Medium -> High -> Critical
                    if (desc)
                    {
                        q = q
                            .OrderByDescending(t =>
                                t.Severity == "Low" ? 1 :
                                t.Severity == "Medium" ? 2 :
                                t.Severity == "High" ? 3 :
                                t.Severity == "Critical" ? 4 : 99
                            )
                            .ThenBy(t => t.TemplateCode);
                    }
                    else
                    {
                        q = q
                            .OrderBy(t =>
                                t.Severity == "Low" ? 1 :
                                t.Severity == "Medium" ? 2 :
                                t.Severity == "High" ? 3 :
                                t.Severity == "Critical" ? 4 : 99
                            )
                            .ThenBy(t => t.TemplateCode);
                    }
                    break;

                case "category":
                    // Sort Category trước, rồi Severity (Low -> Medium -> High -> Critical), rồi TemplateCode
                    if (desc)
                    {
                        q = q
                            .OrderByDescending(t => t.Category)
                            .ThenByDescending(t =>
                                t.Severity == "Low" ? 1 :
                                t.Severity == "Medium" ? 2 :
                                t.Severity == "High" ? 3 :
                                t.Severity == "Critical" ? 4 : 99
                            )
                            .ThenByDescending(t => t.TemplateCode);
                    }
                    else
                    {
                        q = q
                            .OrderBy(t => t.Category)
                            .ThenBy(t =>
                                t.Severity == "Low" ? 1 :
                                t.Severity == "Medium" ? 2 :
                                t.Severity == "High" ? 3 :
                                t.Severity == "Critical" ? 4 : 99
                            )
                            .ThenBy(t => t.TemplateCode);
                    }
                    break;

                case "active":
                case "isactive":
                    q = desc
                        ? q.OrderByDescending(t => t.IsActive).ThenBy(t => t.TemplateCode)
                        : q.OrderBy(t => t.IsActive).ThenBy(t => t.TemplateCode);
                    break;

                default:
                    // Trường hợp không truyền sort hoặc sort=templateCode
                    // => vẫn dùng Category -> Severity -> TemplateCode
                    if (desc)
                    {
                        q = q
                            .OrderByDescending(t => t.Category)
                            .ThenByDescending(t =>
                                t.Severity == "Low" ? 1 :
                                t.Severity == "Medium" ? 2 :
                                t.Severity == "High" ? 3 :
                                t.Severity == "Critical" ? 4 : 99
                            )
                            .ThenByDescending(t => t.TemplateCode);
                    }
                    else
                    {
                        q = q
                            .OrderBy(t => t.Category)
                            .ThenBy(t =>
                                t.Severity == "Low" ? 1 :
                                t.Severity == "Medium" ? 2 :
                                t.Severity == "High" ? 3 :
                                t.Severity == "Critical" ? 4 : 99
                            )
                            .ThenBy(t => t.TemplateCode);
                    }
                    break;
            }

            // Paging giống style SupportPlansAdmin
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var totalItems = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TicketSubjectTemplateAdminListItemDto
                {
                    TemplateCode = t.TemplateCode,
                    Title = t.Title,
                    Severity = t.Severity,
                    Category = t.Category,
                    IsActive = t.IsActive
                })
                .ToListAsync();

            var result = new PagedResult<TicketSubjectTemplateAdminListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Items = items
            };

            return Ok(result);
        }

        /// <summary>
        /// GET: /api/ticket-subject-templates-admin/{templateCode}
        /// Lấy chi tiết 1 TicketSubjectTemplate.
        /// </summary>
        [HttpGet("{templateCode}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<ActionResult<TicketSubjectTemplateAdminDetailDto>> GetByCode(string templateCode)
        {
            if (string.IsNullOrWhiteSpace(templateCode))
            {
                return BadRequest(new
                {
                    message = "Mã template không hợp lệ."
                });
            }

            var code = templateCode.Trim();

            await using var db = await _dbFactory.CreateDbContextAsync();

            var dto = await db.TicketSubjectTemplates
                .AsNoTracking()
                .Where(t => t.TemplateCode == code)
                .Select(t => new TicketSubjectTemplateAdminDetailDto
                {
                    TemplateCode = t.TemplateCode,
                    Title = t.Title,
                    Severity = t.Severity,
                    Category = t.Category,
                    IsActive = t.IsActive
                })
                .FirstOrDefaultAsync();

            if (dto == null) return NotFound();

            return Ok(dto);
        }

        /// <summary>
        /// POST: /api/ticket-subject-templates-admin
        /// Tạo mới 1 TicketSubjectTemplate.
        /// </summary>
        [HttpPost]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<ActionResult<TicketSubjectTemplateAdminDetailDto>> Create(
            TicketSubjectTemplateAdminCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Validate TemplateCode
            if (string.IsNullOrWhiteSpace(dto.TemplateCode))
            {
                return BadRequest(new
                {
                    message = "Mã template không được để trống."
                });
            }

            var code = dto.TemplateCode.Trim();

            if (code.Length > 50)
            {
                return BadRequest(new
                {
                    message = "Mã template không được vượt quá 50 ký tự."
                });
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9_\\-]+$"))
            {
                return BadRequest(new
                {
                    message = "Mã template chỉ được chứa chữ, số, dấu gạch ngang (-) và gạch dưới (_), không chứa khoảng trắng."
                });
            }

            var exists = await db.TicketSubjectTemplates
                .AnyAsync(t => t.TemplateCode == code);

            if (exists)
            {
                return BadRequest(new
                {
                    message = "Mã template đã tồn tại."
                });
            }

            // Validate Title
            var titleRaw = (dto.Title ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(titleRaw))
            {
                return BadRequest(new
                {
                    message = "Tiêu đề không được để trống."
                });
            }

            if (titleRaw.Length > 200)
            {
                return BadRequest(new
                {
                    message = "Tiêu đề không được vượt quá 200 ký tự."
                });
            }

            // Check Title unique (không trùng tiêu đề)
            var titleExists = await db.TicketSubjectTemplates
                .AnyAsync(t => t.Title == titleRaw);

            if (titleExists)
            {
                return BadRequest(new
                {
                    message = "Tiêu đề template đã tồn tại. Vui lòng nhập tiêu đề khác."
                });
            }

            // Validate Severity
            var severityRaw = (dto.Severity ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(severityRaw))
            {
                return BadRequest(new
                {
                    message = "Độ ưu tiên (Severity) không được để trống."
                });
            }

            if (severityRaw.Length > 10)
            {
                return BadRequest(new
                {
                    message = "Độ ưu tiên (Severity) không được vượt quá 10 ký tự."
                });
            }

            // Check Severity thuộc danh sách cho phép
            var normalizedSeverity = AllowedSeverities.FirstOrDefault(s =>
                string.Equals(s, severityRaw, StringComparison.OrdinalIgnoreCase));

            if (normalizedSeverity == null)
            {
                return BadRequest(new
                {
                    message = "Giá trị Severity không hợp lệ. Vui lòng chọn một trong: " +
                              string.Join(", ", AllowedSeverities)
                });
            }

            // Validate Category với list fix-cứng
            var categoryRaw = (dto.Category ?? string.Empty).Trim();
            if (categoryRaw.Length > 100)
            {
                return BadRequest(new
                {
                    message = "Category không được vượt quá 100 ký tự."
                });
            }

            string? normalizedCategory = null;
            if (!string.IsNullOrEmpty(categoryRaw))
            {
                normalizedCategory = AllowedCategories.FirstOrDefault(c =>
                    string.Equals(c, categoryRaw, StringComparison.OrdinalIgnoreCase));

                if (normalizedCategory == null)
                {
                    return BadRequest(new
                    {
                        message = "Category không hợp lệ. Vui lòng chọn một trong: " +
                                  string.Join(", ", AllowedCategories)
                    });
                }
            }

            var entity = new TicketSubjectTemplate
            {
                TemplateCode = code,
                Title = titleRaw,
                Severity = normalizedSeverity,
                Category = normalizedCategory,
                IsActive = dto.IsActive
            };

            db.TicketSubjectTemplates.Add(entity);
            await db.SaveChangesAsync();

            // 🔐 AUDIT LOG – CREATE TEMPLATE
            await _auditLogger.LogAsync(
                HttpContext,
                action: "CreateTicketSubjectTemplate",
                entityType: "TicketSubjectTemplate",
                entityId: entity.TemplateCode,
                before: null,
                after: new
                {
                    entity.TemplateCode,
                    entity.Title,
                    entity.Severity,
                    entity.Category,
                    entity.IsActive
                }
            );

            var result = new TicketSubjectTemplateAdminDetailDto
            {
                TemplateCode = entity.TemplateCode,
                Title = entity.Title,
                Severity = entity.Severity,
                Category = entity.Category,
                IsActive = entity.IsActive
            };

            return CreatedAtAction(
                nameof(GetByCode),
                new { templateCode = entity.TemplateCode },
                result);
        }

        /// <summary>
        /// PUT: /api/ticket-subject-templates-admin/{templateCode}
        /// Cập nhật TicketSubjectTemplate (không cho đổi TemplateCode).
        /// </summary>
        [HttpPut("{templateCode}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<IActionResult> Update(
            string templateCode,
            TicketSubjectTemplateAdminUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(templateCode))
            {
                return BadRequest(new
                {
                    message = "Mã template không hợp lệ."
                });
            }

            await using var db = await _dbFactory.CreateDbContextAsync();

            var code = templateCode.Trim();

            var entity = await db.TicketSubjectTemplates
                .FirstOrDefaultAsync(t => t.TemplateCode == code);

            if (entity == null) return NotFound();

            var before = new
            {
                entity.TemplateCode,
                entity.Title,
                entity.Severity,
                entity.Category,
                entity.IsActive
            };

            // TemplateCode không đổi

            var titleRaw = (dto.Title ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(titleRaw))
            {
                return BadRequest(new
                {
                    message = "Tiêu đề không được để trống."
                });
            }

            if (titleRaw.Length > 200)
            {
                return BadRequest(new
                {
                    message = "Tiêu đề không được vượt quá 200 ký tự."
                });
            }

            // Check Title unique khi update (không trùng với template khác)
            var titleExists = await db.TicketSubjectTemplates
                .AnyAsync(t => t.Title == titleRaw && t.TemplateCode != code);

            if (titleExists)
            {
                return BadRequest(new
                {
                    message = "Tiêu đề template đã tồn tại. Vui lòng nhập tiêu đề khác."
                });
            }

            var severityRaw = (dto.Severity ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(severityRaw))
            {
                return BadRequest(new
                {
                    message = "Độ ưu tiên (Severity) không được để trống."
                });
            }

            if (severityRaw.Length > 10)
            {
                return BadRequest(new
                {
                    message = "Độ ưu tiên (Severity) không được vượt quá 10 ký tự."
                });
            }

            var normalizedSeverity = AllowedSeverities.FirstOrDefault(s =>
                string.Equals(s, severityRaw, StringComparison.OrdinalIgnoreCase));

            if (normalizedSeverity == null)
            {
                return BadRequest(new
                {
                    message = "Giá trị Severity không hợp lệ. Vui lòng chọn một trong: " +
                              string.Join(", ", AllowedSeverities)
                });
            }

            var categoryRaw = (dto.Category ?? string.Empty).Trim();
            if (categoryRaw.Length > 100)
            {
                return BadRequest(new
                {
                    message = "Category không được vượt quá 100 ký tự."
                });
            }

            string? normalizedCategory = null;
            if (!string.IsNullOrEmpty(categoryRaw))
            {
                normalizedCategory = AllowedCategories.FirstOrDefault(c =>
                    string.Equals(c, categoryRaw, StringComparison.OrdinalIgnoreCase));

                if (normalizedCategory == null)
                {
                    return BadRequest(new
                    {
                        message = "Category không hợp lệ. Vui lòng chọn một trong: " +
                                  string.Join(", ", AllowedCategories)
                    });
                }
            }

            entity.Title = titleRaw;
            entity.Severity = normalizedSeverity;
            entity.Category = normalizedCategory;
            entity.IsActive = dto.IsActive;

            await db.SaveChangesAsync();

            var after = new
            {
                entity.TemplateCode,
                entity.Title,
                entity.Severity,
                entity.Category,
                entity.IsActive
            };

            // 🔐 AUDIT LOG – UPDATE TEMPLATE
            await _auditLogger.LogAsync(
                HttpContext,
                action: "UpdateTicketSubjectTemplate",
                entityType: "TicketSubjectTemplate",
                entityId: entity.TemplateCode,
                before: before,
                after: after
            );

            return NoContent();
        }

        /// <summary>
        /// DELETE: /api/ticket-subject-templates-admin/{templateCode}
        /// Xoá hẳn 1 TicketSubjectTemplate.
        /// Nếu sau này có FK từ Ticket -> TemplateCode thì nên bổ sung check trước khi xoá.
        /// </summary>
        [HttpDelete("{templateCode}")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<IActionResult> Delete(string templateCode)
        {
            if (string.IsNullOrWhiteSpace(templateCode))
            {
                return BadRequest(new
                {
                    message = "Mã template không hợp lệ."
                });
            }

            var code = templateCode.Trim();

            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.TicketSubjectTemplates
                .FirstOrDefaultAsync(t => t.TemplateCode == code);

            if (entity == null) return NotFound();

            var before = new
            {
                entity.TemplateCode,
                entity.Title,
                entity.Severity,
                entity.Category,
                entity.IsActive
            };

            db.TicketSubjectTemplates.Remove(entity);
            await db.SaveChangesAsync();

            // 🔐 AUDIT LOG – DELETE TEMPLATE
            await _auditLogger.LogAsync(
                HttpContext,
                action: "DeleteTicketSubjectTemplate",
                entityType: "TicketSubjectTemplate",
                entityId: code,
                before: before,
                after: null
            );

            return NoContent();
        }

        /// <summary>
        /// PATCH: /api/ticket-subject-templates-admin/{templateCode}/toggle
        /// Bật / tắt IsActive cho TicketSubjectTemplate.
        /// </summary>
        [HttpPatch("{templateCode}/toggle")]
        [RequireRole(RoleCodes.ADMIN, RoleCodes.CUSTOMER_CARE)]
        public async Task<IActionResult> Toggle(string templateCode)
        {
            if (string.IsNullOrWhiteSpace(templateCode))
            {
                return BadRequest(new
                {
                    message = "Mã template không hợp lệ."
                });
            }

            var code = templateCode.Trim();

            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.TicketSubjectTemplates
                .FirstOrDefaultAsync(t => t.TemplateCode == code);

            if (entity == null) return NotFound();

            var before = new
            {
                entity.TemplateCode,
                entity.Title,
                entity.Severity,
                entity.Category,
                entity.IsActive
            };

            entity.IsActive = !entity.IsActive;

            await db.SaveChangesAsync();

            var after = new
            {
                entity.TemplateCode,
                entity.Title,
                entity.Severity,
                entity.Category,
                entity.IsActive
            };

            // 🔐 AUDIT LOG – TOGGLE TEMPLATE
            await _auditLogger.LogAsync(
                HttpContext,
                action: "ToggleTicketSubjectTemplateActive",
                entityType: "TicketSubjectTemplate",
                entityId: entity.TemplateCode,
                before: before,
                after: after
            );

            return NoContent();
        }
    }
}
