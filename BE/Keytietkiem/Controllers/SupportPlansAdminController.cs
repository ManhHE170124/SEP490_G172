// File: Controllers/SupportPlansAdminController.cs
using System;
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.SupportPlans;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/support-plans-admin")]
    public class SupportPlansAdminController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;

        public SupportPlansAdminController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// GET: /api/support-plans-admin
        /// List gói SupportPlan có filter + paging.
        /// Sort cố định: PriorityLevel tăng dần -> Price tăng dần.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<SupportPlanAdminListItemDto>>> List(
            [FromQuery] int? priorityLevel,
            [FromQuery] bool? active,
            // giữ sort/direction để đồng bộ pattern, hiện chưa dùng
            [FromQuery] string? sort = null,
            [FromQuery] string? direction = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var q = db.SupportPlans
                      .AsNoTracking()
                      .AsQueryable();

            // Filter PriorityLevel
            if (priorityLevel.HasValue)
            {
                q = q.Where(p => p.PriorityLevel == priorityLevel.Value);
            }

            // Filter IsActive
            if (active.HasValue)
            {
                q = q.Where(p => p.IsActive == active.Value);
            }

            // Sort cố định: Level -> Price
            q = q.OrderBy(p => p.PriorityLevel)
                 .ThenBy(p => p.Price);

            // Paging giống style ở các controller khác
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var totalItems = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new SupportPlanAdminListItemDto
                {
                    SupportPlanId = p.SupportPlanId,
                    Name = p.Name,
                    Description = p.Description,
                    PriorityLevel = p.PriorityLevel,
                    Price = p.Price,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            var result = new PagedResult<SupportPlanAdminListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Items = items
            };

            return Ok(result);
        }

        /// <summary>
        /// GET: /api/support-plans-admin/{supportPlanId}
        /// Lấy chi tiết 1 SupportPlan.
        /// </summary>
        [HttpGet("{supportPlanId:int}")]
        public async Task<ActionResult<SupportPlanAdminDetailDto>> GetById(int supportPlanId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var dto = await db.SupportPlans
                .AsNoTracking()
                .Where(p => p.SupportPlanId == supportPlanId)
                .Select(p => new SupportPlanAdminDetailDto
                {
                    SupportPlanId = p.SupportPlanId,
                    Name = p.Name,
                    Description = p.Description,
                    PriorityLevel = p.PriorityLevel,
                    Price = p.Price,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (dto == null) return NotFound();

            return Ok(dto);
        }

        /// <summary>
        /// POST: /api/support-plans-admin
        /// Tạo mới 1 SupportPlan.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SupportPlanAdminDetailDto>> Create(
            SupportPlanAdminCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Validate Name
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(new
                {
                    message = "Tên gói không được để trống."
                });
            }

            var trimmedName = dto.Name.Trim();
            if (trimmedName.Length > 120)
            {
                return BadRequest(new
                {
                    message = "Tên gói không được vượt quá 120 ký tự."
                });
            }

            // Validate Description length
            if (dto.Description != null && dto.Description.Length > 500)
            {
                return BadRequest(new
                {
                    message = "Mô tả không được vượt quá 500 ký tự."
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

            // Price >= 0
            if (dto.Price < 0)
            {
                return BadRequest(new
                {
                    message = "Giá gói phải lớn hơn hoặc bằng 0."
                });
            }

            // Không cho trùng cả PriorityLevel lẫn Price
            var duplicate = await db.SupportPlans.AnyAsync(p =>
                p.PriorityLevel == dto.PriorityLevel &&
                p.Price == dto.Price);

            if (duplicate)
            {
                return BadRequest(new
                {
                    message = "Đã tồn tại gói hỗ trợ khác có cùng mức ưu tiên và giá tiền. Vui lòng chọn giá khác."
                });
            }

            var entity = new SupportPlan
            {
                Name = trimmedName,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                PriorityLevel = dto.PriorityLevel,
                Price = dto.Price,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            // Nếu tạo gói đang ACTIVE -> kiểm tra quy tắc giá / priority
            if (entity.IsActive)
            {
                // Các gói active khác ở level khác (level cùng sẽ bị tắt nên không cần check)
                var otherActive = await db.SupportPlans
                    .Where(p => p.IsActive && p.PriorityLevel != entity.PriorityLevel)
                    .ToListAsync();

                var violationMessage = ValidatePriceOrderingForActivePlans(entity, otherActive);
                if (violationMessage != null)
                {
                    return BadRequest(new { message = violationMessage });
                }

                // Đảm bảo mỗi PriorityLevel chỉ có 1 gói ACTIVE
                var othersSameLevel = await db.SupportPlans
                    .Where(p => p.PriorityLevel == entity.PriorityLevel && p.IsActive)
                    .ToListAsync();

                foreach (var other in othersSameLevel)
                {
                    other.IsActive = false;
                }
            }

            db.SupportPlans.Add(entity);
            await db.SaveChangesAsync();

            var result = new SupportPlanAdminDetailDto
            {
                SupportPlanId = entity.SupportPlanId,
                Name = entity.Name,
                Description = entity.Description,
                PriorityLevel = entity.PriorityLevel,
                Price = entity.Price,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt
            };

            return CreatedAtAction(nameof(GetById), new { supportPlanId = entity.SupportPlanId }, result);
        }

        /// <summary>
        /// PUT: /api/support-plans-admin/{supportPlanId}
        /// Cập nhật 1 SupportPlan.
        /// </summary>
        [HttpPut("{supportPlanId:int}")]
        public async Task<IActionResult> Update(
            int supportPlanId,
            SupportPlanAdminUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SupportPlans
                .FirstOrDefaultAsync(p => p.SupportPlanId == supportPlanId);

            if (entity == null) return NotFound();

            // Validate Name
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(new
                {
                    message = "Tên gói không được để trống."
                });
            }

            var trimmedName = dto.Name.Trim();
            if (trimmedName.Length > 120)
            {
                return BadRequest(new
                {
                    message = "Tên gói không được vượt quá 120 ký tự."
                });
            }

            // Validate Description length
            if (dto.Description != null && dto.Description.Length > 500)
            {
                return BadRequest(new
                {
                    message = "Mô tả không được vượt quá 500 ký tự."
                });
            }

            if (dto.PriorityLevel < 0)
            {
                return BadRequest(new
                {
                    message = "Mức ưu tiên (PriorityLevel) phải lớn hơn hoặc bằng 0."
                });
            }

            if (dto.Price < 0)
            {
                return BadRequest(new
                {
                    message = "Giá gói phải lớn hơn hoặc bằng 0."
                });
            }

            // Không cho trùng cả PriorityLevel lẫn Price (trừ chính nó)
            var duplicate = await db.SupportPlans.AnyAsync(p =>
                p.SupportPlanId != supportPlanId &&
                p.PriorityLevel == dto.PriorityLevel &&
                p.Price == dto.Price);

            if (duplicate)
            {
                return BadRequest(new
                {
                    message = "Đã tồn tại gói hỗ trợ khác có cùng mức ưu tiên và giá tiền. Vui lòng chọn giá khác."
                });
            }

            // Cập nhật giá trị
            entity.Name = trimmedName;
            entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            entity.PriorityLevel = dto.PriorityLevel;
            entity.Price = dto.Price;
            entity.IsActive = dto.IsActive;

            // Nếu sau update gói đang ACTIVE → kiểm tra quy tắc giá / priority và tắt các gói cùng level
            if (entity.IsActive)
            {
                // Các gói active khác ở level khác
                var otherActive = await db.SupportPlans
                    .Where(p => p.IsActive
                                && p.SupportPlanId != entity.SupportPlanId
                                && p.PriorityLevel != entity.PriorityLevel)
                    .ToListAsync();

                var violationMessage = ValidatePriceOrderingForActivePlans(entity, otherActive);
                if (violationMessage != null)
                {
                    return BadRequest(new { message = violationMessage });
                }

                // Tắt các gói cùng PriorityLevel
                var sameLevelActive = await db.SupportPlans
                    .Where(p => p.IsActive
                                && p.SupportPlanId != entity.SupportPlanId
                                && p.PriorityLevel == entity.PriorityLevel)
                    .ToListAsync();

                foreach (var other in sameLevelActive)
                {
                    other.IsActive = false;
                }
            }

            await db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// DELETE: /api/support-plans-admin/{supportPlanId}
        /// Xoá hẳn 1 SupportPlan.
        /// Không cho xoá nếu đã có UserSupportPlanSubscription tham chiếu.
        /// </summary>
        [HttpDelete("{supportPlanId:int}")]
        public async Task<IActionResult> Delete(int supportPlanId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SupportPlans
                .FirstOrDefaultAsync(p => p.SupportPlanId == supportPlanId);

            if (entity == null) return NotFound();

            var hasSubscriptions = await db.UserSupportPlanSubscriptions
                .AnyAsync(s => s.SupportPlanId == supportPlanId);

            if (hasSubscriptions)
            {
                return BadRequest(new
                {
                    message = "Không thể xoá gói hỗ trợ đã (hoặc đang) được người dùng đăng ký. Vui lòng tắt trạng thái hoạt động (IsActive) thay vì xoá."
                });
            }

            db.SupportPlans.Remove(entity);
            await db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// PATCH: /api/support-plans-admin/{supportPlanId}/toggle
        /// Bật / tắt IsActive cho SupportPlan.
        /// - Khi bật: kiểm tra quy tắc giá / priority, sau đó tự động tắt các gói khác cùng PriorityLevel.
        /// - Khi tắt: chỉ tắt gói hiện tại.
        /// </summary>
        [HttpPatch("{supportPlanId:int}/toggle")]
        public async Task<IActionResult> Toggle(int supportPlanId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SupportPlans
                .FirstOrDefaultAsync(p => p.SupportPlanId == supportPlanId);

            if (entity == null) return NotFound();

            if (!entity.IsActive)
            {
                // Đang tắt -> chuẩn bị bật lên: kiểm tra quy tắc giá với các gói đang active khác level
                var otherActive = await db.SupportPlans
                    .Where(p => p.IsActive
                                && p.SupportPlanId != entity.SupportPlanId
                                && p.PriorityLevel != entity.PriorityLevel)
                    .ToListAsync();

                var violationMessage = ValidatePriceOrderingForActivePlans(entity, otherActive);
                if (violationMessage != null)
                {
                    return BadRequest(new { message = violationMessage });
                }

                // Bật gói hiện tại
                entity.IsActive = true;

                // Tắt các gói khác cùng PriorityLevel
                var othersSameLevel = await db.SupportPlans
                    .Where(p => p.PriorityLevel == entity.PriorityLevel
                                && p.SupportPlanId != entity.SupportPlanId
                                && p.IsActive)
                    .ToListAsync();

                foreach (var other in othersSameLevel)
                {
                    other.IsActive = false;
                }
            }
            else
            {
                // Đang bật -> tắt đi
                entity.IsActive = false;
            }

            await db.SaveChangesAsync();

            return Ok(new { entity.SupportPlanId, entity.IsActive });
        }

        /// <summary>
        /// Quy tắc giá cho các gói đang ACTIVE:
        /// - Gói có PriorityLevel cao hơn phải có giá CAO hơn các gói active có PriorityLevel thấp hơn.
        /// - Gói có PriorityLevel thấp hơn phải có giá THẤP hơn các gói active có PriorityLevel cao hơn.
        /// Trả về null nếu hợp lệ, hoặc message lỗi (tiếng Việt) nếu vi phạm.
        /// </summary>
        private static string? ValidatePriceOrderingForActivePlans(
            SupportPlan currentPlan,
            List<SupportPlan> otherActivePlans)
        {
            foreach (var other in otherActivePlans)
            {
                if (other.PriorityLevel == currentPlan.PriorityLevel)
                {
                    // đã xử lý logic 1 gói active / level ở chỗ khác
                    continue;
                }

                // other có level thấp hơn current
                if (other.PriorityLevel < currentPlan.PriorityLevel)
                {
                    // giá current phải CAO hơn giá other
                    if (currentPlan.Price <= other.Price)
                    {
                        return
                            $"Không thể bật gói ở PriorityLevel {currentPlan.PriorityLevel} với giá {currentPlan.Price:#,0.##} " +
                            $"vì đang có gói ở PriorityLevel {other.PriorityLevel} với giá {other.Price:#,0.##}. " +
                            "Gói có PriorityLevel cao hơn phải có giá CAO hơn các gói có PriorityLevel thấp hơn.";
                    }
                }
                // other có level cao hơn current
                else if (other.PriorityLevel > currentPlan.PriorityLevel)
                {
                    // giá current phải THẤP hơn giá other
                    if (currentPlan.Price >= other.Price)
                    {
                        return
                            $"Không thể bật gói ở PriorityLevel {currentPlan.PriorityLevel} với giá {currentPlan.Price:#,0.##} " +
                            $"vì đang có gói ở PriorityLevel {other.PriorityLevel} với giá {other.Price:#,0.##}. " +
                            "Gói có PriorityLevel thấp hơn phải có giá THẤP hơn các gói có PriorityLevel cao hơn.";
                    }
                }
            }

            return null;
        }
    }
}
