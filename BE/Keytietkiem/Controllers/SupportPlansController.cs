// File: Controllers/SupportPlansController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Keytietkiem.DTOs.Payments;
using Keytietkiem.DTOs.SupportPlans;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SupportPlansController : ControllerBase
    {
        private readonly KeytietkiemDbContext _db;

        public SupportPlansController(KeytietkiemDbContext db)
        {
            _db = db;
        }

        private Guid? GetCurrentUserIdOrNull()
        {
            var claim = User.FindFirst("uid") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return null;

            return Guid.TryParse(claim.Value, out var id) ? id : (Guid?)null;
        }

        // ===== PUBLIC: List các gói hỗ trợ đang mở bán =====
        // GET /api/supportplans/active
        [HttpGet("active")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<SupportPlanListItemDto>>> GetActivePlans()
        {
            var plans = await _db.SupportPlans
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.PriorityLevel)
                .ThenBy(p => p.Price)
                .Select(p => new SupportPlanListItemDto
                {
                    SupportPlanId = p.SupportPlanId,
                    Name = p.Name,
                    Description = p.Description,
                    PriorityLevel = p.PriorityLevel,
                    Price = p.Price,
                    IsActive = p.IsActive
                })
                .ToListAsync();

            return Ok(plans);
        }

        // ===== CUSTOMER: Lấy gói hỗ trợ hiện tại của user (nếu có) =====
        // GET /api/supportplans/me/current
        [HttpGet("me/current")]
        [Authorize]
        public async Task<ActionResult<SupportPlanCurrentSubscriptionDto?>> GetMyCurrentSubscription()
        {
            var userId = GetCurrentUserIdOrNull();
            if (userId == null) return Unauthorized();

            var nowUtc = DateTime.UtcNow;

            var sub = await _db.UserSupportPlanSubscriptions
                .AsNoTracking()
                .Include(s => s.SupportPlan)
                .Where(s =>
                    s.UserId == userId.Value &&
                    s.Status == "Active" &&
                    (!s.ExpiresAt.HasValue || s.ExpiresAt > nowUtc))
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            if (sub == null)
            {
                // Không có gói active -> trả về 200 với body null để FE dễ xử lý.
                return Ok(null);
            }

            var dto = new SupportPlanCurrentSubscriptionDto
            {
                SubscriptionId = sub.SubscriptionId,
                SupportPlanId = sub.SupportPlanId,
                PlanName = sub.SupportPlan?.Name ?? string.Empty,
                PlanDescription = sub.SupportPlan?.Description,
                PriorityLevel = sub.SupportPlan?.PriorityLevel ?? 0,
                Price = sub.SupportPlan?.Price ?? 0,
                Status = sub.Status,
                StartedAt = sub.StartedAt,
                ExpiresAt = sub.ExpiresAt
            };

            return Ok(dto);
        }

        // ===== CUSTOMER: Xác nhận thanh toán thành công và tạo subscription 1 tháng =====
        // POST /api/supportplans/confirm-payment
        [HttpPost("confirm-payment")]
        [Authorize]
        public async Task<IActionResult> ConfirmSupportPlanPayment([FromBody] ConfirmSupportPlanPaymentDTO dto)
        {
            if (dto == null || dto.PaymentId == Guid.Empty || dto.SupportPlanId <= 0)
            {
                return BadRequest(new { message = "Dữ liệu thanh toán không hợp lệ" });
            }

            var userId = GetCurrentUserIdOrNull();
            if (userId == null) return Unauthorized();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);
            if (user == null) return Unauthorized();

            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == dto.PaymentId);

            if (payment == null)
            {
                return NotFound(new { message = "Payment không tồn tại" });
            }

            // Chỉ nhận payment dành cho gói hỗ trợ
            if (!string.Equals(payment.TransactionType, "SUPPORT_PLAN", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Payment không thuộc loại SUPPORT_PLAN" });
            }

            // Chỉ chấp nhận khi đã Paid / Success / Completed
            var status = payment.Status ?? string.Empty;
            if (!status.Equals("Paid", StringComparison.OrdinalIgnoreCase) &&
                !status.Equals("Success", StringComparison.OrdinalIgnoreCase) &&
                !status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Thanh toán chưa thành công hoặc đang chờ xử lý." });
            }

            var plan = await _db.SupportPlans
                .FirstOrDefaultAsync(p =>
                    p.SupportPlanId == dto.SupportPlanId &&
                    p.IsActive);

            if (plan == null)
            {
                return BadRequest(new { message = "Gói hỗ trợ không tồn tại hoặc đã bị khóa." });
            }

            // Bảo vệ: số tiền thanh toán phải khớp giá gói (1 tháng)
            if (payment.Amount != plan.Price)
            {
                return BadRequest(new { message = "Số tiền thanh toán không khớp với giá gói hỗ trợ." });
            }

            // Bảo vệ: email payment phải trùng email user (nếu payment có email)
            if (!string.IsNullOrEmpty(payment.Email) &&
                !string.Equals(payment.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Payment không thuộc về tài khoản hiện tại." });
            }

            var nowUtc = DateTime.UtcNow;

            // Idempotent: nếu đã có subscription gắn với payment này -> trả luôn
            var existing = await _db.UserSupportPlanSubscriptions
                .Include(s => s.SupportPlan)
                .Where(s => s.PaymentId == payment.PaymentId)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                if (existing.UserId != user.UserId)
                {
                    return BadRequest(new { message = "Payment đã được gán cho người dùng khác." });
                }

                var existingDto = new SupportPlanCurrentSubscriptionDto
                {
                    SubscriptionId = existing.SubscriptionId,
                    SupportPlanId = existing.SupportPlanId,
                    PlanName = existing.SupportPlan?.Name ?? plan.Name,
                    PlanDescription = existing.SupportPlan?.Description ?? plan.Description,
                    PriorityLevel = existing.SupportPlan?.PriorityLevel ?? plan.PriorityLevel,
                    Price = existing.SupportPlan?.Price ?? plan.Price,
                    Status = existing.Status,
                    StartedAt = existing.StartedAt,
                    ExpiresAt = existing.ExpiresAt
                };

                return Ok(existingDto);
            }

            // Tạo subscription 1 tháng
            var subscription = new UserSupportPlanSubscription
            {
                UserId = user.UserId,
                SupportPlanId = plan.SupportPlanId,
                Status = "Active",
                StartedAt = nowUtc,
                ExpiresAt = nowUtc.AddMonths(1),
                PaymentId = payment.PaymentId,
                Note = dto.Note
            };

            _db.UserSupportPlanSubscriptions.Add(subscription);

            // Option: nâng SupportPriorityLevel của user nếu gói cao hơn
            if (plan.PriorityLevel > user.SupportPriorityLevel)
            {
                user.SupportPriorityLevel = plan.PriorityLevel;
            }

            await _db.SaveChangesAsync();

            var result = new SupportPlanCurrentSubscriptionDto
            {
                SubscriptionId = subscription.SubscriptionId,
                SupportPlanId = subscription.SupportPlanId,
                PlanName = plan.Name,
                PlanDescription = plan.Description,
                PriorityLevel = plan.PriorityLevel,
                Price = plan.Price,
                Status = subscription.Status,
                StartedAt = subscription.StartedAt,
                ExpiresAt = subscription.ExpiresAt
            };

            return Ok(result);
        }
    }
}
