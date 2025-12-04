using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.SupportPlans;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/support-priority-loyalty-rules")]
    public class SupportPriorityLoyaltyRulesController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;

        public SupportPriorityLoyaltyRulesController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// GET: /api/support-priority-loyalty-rules
        /// List rules có filter + paging.
        /// Luôn sắp xếp theo PriorityLevel tăng dần, sau đó MinTotalSpend tăng dần.
        /// Bỏ qua toàn bộ level 0 (default).
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<SupportPriorityLoyaltyRuleListItemDto>>> List(
            [FromQuery] int? priorityLevel,
            [FromQuery] bool? active,
            // sort/direction tạm giữ nhưng không dùng
            [FromQuery] string? sort = null,
            [FromQuery] string? direction = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var q = db.SupportPriorityLoyaltyRules
                      .AsNoTracking()
                      // loại bỏ level 0 khỏi tất cả logic
                      .Where(r => r.PriorityLevel > 0)
                      .AsQueryable();

            // Filter theo PriorityLevel (>0)
            if (priorityLevel.HasValue && priorityLevel.Value > 0)
            {
                q = q.Where(r => r.PriorityLevel == priorityLevel.Value);
            }

            // Filter theo IsActive
            if (active.HasValue)
            {
                q = q.Where(r => r.IsActive == active.Value);
            }

            // Sort cố định: PriorityLevel -> MinTotalSpend
            q = q.OrderBy(r => r.PriorityLevel)
                 .ThenBy(r => r.MinTotalSpend);

            // Paging giống style FaqsController
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var totalItems = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new SupportPriorityLoyaltyRuleListItemDto
                {
                    RuleId = r.RuleId,
                    MinTotalSpend = r.MinTotalSpend,
                    PriorityLevel = r.PriorityLevel,
                    IsActive = r.IsActive
                })
                .ToListAsync();

            var result = new PagedResult<SupportPriorityLoyaltyRuleListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                Items = items
            };

            return Ok(result);
        }

        /// <summary>
        /// GET: /api/support-priority-loyalty-rules/{ruleId}
        /// Lấy chi tiết 1 rule.
        /// </summary>
        [HttpGet("{ruleId:int}")]
        public async Task<ActionResult<SupportPriorityLoyaltyRuleDetailDto>> GetById(int ruleId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var dto = await db.SupportPriorityLoyaltyRules
                .AsNoTracking()
                .Where(r => r.RuleId == ruleId)
                .Select(r => new SupportPriorityLoyaltyRuleDetailDto
                {
                    RuleId = r.RuleId,
                    MinTotalSpend = r.MinTotalSpend,
                    PriorityLevel = r.PriorityLevel,
                    IsActive = r.IsActive
                })
                .FirstOrDefaultAsync();

            if (dto == null) return NotFound();

            return Ok(dto);
        }

        /// <summary>
        /// Rule business: kiểm tra thứ tự mức chi tiêu của các rule đang ACTIVE.
        /// - Level thấp hơn phải có MinTotalSpend &lt; level cao hơn.
        /// - Chỉ xét các rule đang IsActive = true, PriorityLevel &gt; 0.
        /// </summary>
        private static IQueryable<SupportPriorityLoyaltyRule> BuildConflictQuery(
            IQueryable<SupportPriorityLoyaltyRule> source,
            int level,
            decimal minTotalSpend,
            int? selfRuleId = null)
        {
            var q = source.Where(r => r.IsActive && r.PriorityLevel > 0);

            if (selfRuleId.HasValue)
            {
                q = q.Where(r => r.RuleId != selfRuleId.Value);
            }

            // Tìm rule active xung đột:
            // - level thấp hơn nhưng MinTotalSpend >= newMin
            // - level cao hơn nhưng MinTotalSpend <= newMin
            q = q.Where(r =>
                (r.PriorityLevel < level && r.MinTotalSpend >= minTotalSpend) ||
                (r.PriorityLevel > level && r.MinTotalSpend <= minTotalSpend));

            return q;
        }

        /// <summary>
        /// POST: /api/support-priority-loyalty-rules
        /// Tạo mới rule.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SupportPriorityLoyaltyRuleDetailDto>> Create(
            SupportPriorityLoyaltyRuleCreateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Level 0 là default, không được cấu hình
            if (dto.PriorityLevel <= 0)
            {
                return BadRequest(new
                {
                    message = "PriorityLevel must be greater than 0 because level 0 is the default level."
                });
            }

            // Validate MinTotalSpend
            if (dto.MinTotalSpend < 0)
            {
                return BadRequest(new
                {
                    message = "MinTotalSpend must be greater than or equal to 0."
                });
            }

            // Tránh trùng rule hoàn toàn giống nhau (cùng PriorityLevel + MinTotalSpend)
            var exists = await db.SupportPriorityLoyaltyRules.AnyAsync(r =>
                r.PriorityLevel == dto.PriorityLevel &&
                r.MinTotalSpend == dto.MinTotalSpend);

            if (exists)
            {
                return BadRequest(new
                {
                    message = "A rule with the same MinTotalSpend and PriorityLevel already exists."
                });
            }

            // Nếu tạo rule đang bật → kiểm tra thứ tự so với các rule ACTIVE khác
            if (dto.IsActive)
            {
                var conflict = await BuildConflictQuery(
                        db.SupportPriorityLoyaltyRules.AsNoTracking(),
                        dto.PriorityLevel,
                        dto.MinTotalSpend)
                    .Select(r => new { r.PriorityLevel, r.MinTotalSpend })
                    .FirstOrDefaultAsync();

                if (conflict != null)
                {
                    return BadRequest(new
                    {
                        message = "Không thể bật rule này do không đảm bảo thứ tự mức chi tiêu giữa các level. " +
                                  "Level cao hơn phải có tổng chi tiêu tối thiểu cao hơn level thấp hơn. " +
                                  "Vui lòng điều chỉnh lại giá trị hoặc tắt/bật các rule khác."
                    });
                }
            }

            var entity = new SupportPriorityLoyaltyRule
            {
                MinTotalSpend = dto.MinTotalSpend,
                PriorityLevel = dto.PriorityLevel,
                IsActive = dto.IsActive
            };

            // Đảm bảo mỗi level chỉ có một rule ACTIVE
            if (entity.IsActive)
            {
                var othersSamePriority = await db.SupportPriorityLoyaltyRules
                    .Where(r => r.PriorityLevel == entity.PriorityLevel && r.IsActive)
                    .ToListAsync();

                foreach (var other in othersSamePriority)
                {
                    other.IsActive = false;
                }
            }

            db.SupportPriorityLoyaltyRules.Add(entity);
            await db.SaveChangesAsync();

            var result = new SupportPriorityLoyaltyRuleDetailDto
            {
                RuleId = entity.RuleId,
                MinTotalSpend = entity.MinTotalSpend,
                PriorityLevel = entity.PriorityLevel,
                IsActive = entity.IsActive
            };

            return CreatedAtAction(nameof(GetById), new { ruleId = entity.RuleId }, result);
        }

        /// <summary>
        /// PUT: /api/support-priority-loyalty-rules/{ruleId}
        /// Cập nhật rule.
        /// </summary>
        [HttpPut("{ruleId:int}")]
        public async Task<IActionResult> Update(
            int ruleId,
            SupportPriorityLoyaltyRuleUpdateDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SupportPriorityLoyaltyRules
                .FirstOrDefaultAsync(r => r.RuleId == ruleId);

            if (entity == null) return NotFound();

            if (dto.PriorityLevel <= 0)
            {
                return BadRequest(new
                {
                    message = "PriorityLevel must be greater than 0 because level 0 is the default level."
                });
            }

            if (dto.MinTotalSpend < 0)
            {
                return BadRequest(new
                {
                    message = "MinTotalSpend must be greater than or equal to 0."
                });
            }

            var duplicate = await db.SupportPriorityLoyaltyRules.AnyAsync(r =>
                r.RuleId != ruleId &&
                r.PriorityLevel == dto.PriorityLevel &&
                r.MinTotalSpend == dto.MinTotalSpend);

            if (duplicate)
            {
                return BadRequest(new
                {
                    message = "Another rule with the same MinTotalSpend and PriorityLevel already exists."
                });
            }

            // Nếu sau update rule đang bật → kiểm tra thứ tự
            if (dto.IsActive)
            {
                var conflict = await BuildConflictQuery(
                        db.SupportPriorityLoyaltyRules.AsNoTracking(),
                        dto.PriorityLevel,
                        dto.MinTotalSpend,
                        selfRuleId: ruleId)
                    .Select(r => new { r.PriorityLevel, r.MinTotalSpend })
                    .FirstOrDefaultAsync();

                if (conflict != null)
                {
                    return BadRequest(new
                    {
                        message = "Không thể bật rule này do không đảm bảo thứ tự mức chi tiêu giữa các level. " +
                                  "Level cao hơn phải có tổng chi tiêu tối thiểu cao hơn level thấp hơn. " +
                                  "Vui lòng điều chỉnh lại giá trị hoặc tắt/bật các rule khác."
                    });
                }
            }

            // Cập nhật giá trị
            entity.MinTotalSpend = dto.MinTotalSpend;
            entity.PriorityLevel = dto.PriorityLevel;
            entity.IsActive = dto.IsActive;

            // Nếu sau update rule đang bật → tắt các rule khác cùng PriorityLevel
            if (entity.IsActive)
            {
                var othersSamePriority = await db.SupportPriorityLoyaltyRules
                    .Where(r => r.PriorityLevel == entity.PriorityLevel
                                && r.RuleId != entity.RuleId
                                && r.IsActive)
                    .ToListAsync();

                foreach (var other in othersSamePriority)
                {
                    other.IsActive = false;
                }
            }

            await db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// DELETE: /api/support-priority-loyalty-rules/{ruleId}
        /// Xoá hẳn 1 rule.
        /// </summary>
        [HttpDelete("{ruleId:int}")]
        public async Task<IActionResult> Delete(int ruleId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SupportPriorityLoyaltyRules
                .FirstOrDefaultAsync(r => r.RuleId == ruleId);

            if (entity == null) return NotFound();

            db.SupportPriorityLoyaltyRules.Remove(entity);
            await db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// PATCH: /api/support-priority-loyalty-rules/{ruleId}/toggle
        /// Bật / tắt IsActive cho rule.
        /// - Khi bật một rule: 
        ///     + Kiểm tra thứ tự MinTotalSpend so với các rule ACTIVE khác.
        ///     + Nếu hợp lệ: tự động tắt các rule khác cùng PriorityLevel.
        /// - Khi tắt: chỉ tắt rule hiện tại.
        /// </summary>
        [HttpPatch("{ruleId:int}/toggle")]
        public async Task<IActionResult> Toggle(int ruleId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.SupportPriorityLoyaltyRules
                .FirstOrDefaultAsync(r => r.RuleId == ruleId);

            if (entity == null) return NotFound();

            // Không cho phép toggle rule level 0
            if (entity.PriorityLevel <= 0)
            {
                return BadRequest(new
                {
                    message = "Level 0 is the default level and cannot be configured."
                });
            }

            if (!entity.IsActive)
            {
                // Đang tắt -> chuẩn bị bật lên: kiểm tra thứ tự trước
                var conflict = await BuildConflictQuery(
                        db.SupportPriorityLoyaltyRules.AsNoTracking(),
                        entity.PriorityLevel,
                        entity.MinTotalSpend,
                        selfRuleId: entity.RuleId)
                    .Select(r => new { r.PriorityLevel, r.MinTotalSpend })
                    .FirstOrDefaultAsync();

                if (conflict != null)
                {
                    return BadRequest(new
                    {
                        message = "Không thể bật rule này do không đảm bảo thứ tự mức chi tiêu giữa các level. " +
                                  "Level cao hơn phải có tổng chi tiêu tối thiểu cao hơn level thấp hơn. " +
                                  "Vui lòng điều chỉnh lại giá trị hoặc tắt/bật các rule khác."
                    });
                }

                // Hợp lệ: bật rule này
                entity.IsActive = true;

                // Và tắt các rule khác cùng PriorityLevel
                var othersSamePriority = await db.SupportPriorityLoyaltyRules
                    .Where(r => r.PriorityLevel == entity.PriorityLevel
                                && r.RuleId != entity.RuleId
                                && r.IsActive)
                    .ToListAsync();

                foreach (var other in othersSamePriority)
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

            return Ok(new { entity.RuleId, entity.IsActive });
        }

        // =========================
        //  LOYALTY HELPER FUNCTIONS
        // =========================

        /// <summary>
        /// Tính tổng số tiền user đã tiêu cho ORDER_PAYMENT với trạng thái Paid,
        /// dựa theo email của user.
        /// </summary>
        /// <param name="db">DbContext hiện tại</param>
        /// <param name="email">Email người dùng</param>
        /// <returns>Tổng số tiền đã thanh toán (decimal)</returns>
        [NonAction]
        private static async Task<decimal> CalculateUserTotalPaidOrderAmountAsync(
            KeytietkiemDbContext db,
            string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return 0m;

            var normalizedEmail = email.Trim();

            var total = await db.Payments
                .Where(p =>
                    p.Status == "Paid" &&
                    p.TransactionType == "ORDER_PAYMENT" &&
                    p.Email == normalizedEmail)
                .SumAsync(p => (decimal?)p.Amount);

            return total ?? 0m;
        }

        /// <summary>
        /// Tính toán & đồng bộ cấp ưu tiên loyalty cho user dựa trên tổng chi tiêu:
        ///  - Tính tổng tiền ORDER_PAYMENT (Paid) theo email.
        ///  - So với các rule loyalty đang ACTIVE (PriorityLevel &gt; 0).
        ///  - Xác định PriorityLevel loyalty phù hợp nhất.
        ///  - Nếu khác với Users.SupportPriorityLevel hiện tại thì cập nhật lại.
        ///  - Trả về PriorityLevel mới (0 nếu không đạt rule nào hoặc không tìm thấy user).
        /// 
        /// Có thể gọi hàm này từ các controller khác mỗi khi cần refresh loyalty:
        ///  - VD: sau khi Payment được đổi sang trạng thái Paid.
        /// </summary>
        /// <param name="email">Email người dùng cần tính loyalty</param>
        /// <returns>PriorityLevel loyalty hiện tại sau khi đồng bộ</returns>
        [NonAction]
        public async Task<int> RecalculateUserLoyaltyPriorityLevelAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return 0;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var normalizedEmail = email.Trim();

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

            // Không tìm thấy user → coi như level 0 (không update được gì)
            if (user == null)
                return 0;

            // 1. Tính tổng tiền user đã tiêu (ORDER_PAYMENT, Paid)
            var totalSpend = await CalculateUserTotalPaidOrderAmountAsync(db, normalizedEmail);

            // 2. Lấy rule đang active, level > 0
            //    Chọn level cao nhất sao cho MinTotalSpend <= totalSpend
            var newLevel = await db.SupportPriorityLoyaltyRules
                .Where(r =>
                    r.IsActive &&
                    r.PriorityLevel > 0 &&
                    r.MinTotalSpend <= totalSpend)
                .OrderByDescending(r => r.PriorityLevel)
                .Select(r => (int?)r.PriorityLevel)
                .FirstOrDefaultAsync() ?? 0;

            // 3. Nếu khác với SupportPriorityLevel hiện tại → update lại
            if (user.SupportPriorityLevel != newLevel)
            {
                user.SupportPriorityLevel = newLevel;
                await db.SaveChangesAsync();
            }

            return newLevel;
        }
    }
}
