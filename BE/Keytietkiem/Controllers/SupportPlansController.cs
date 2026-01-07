// File: Controllers/SupportPlansController.cs
using Keytietkiem.DTOs.Payments;
using Keytietkiem.DTOs.Support;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SupportPlansController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IAuditLogger _auditLogger;
        private readonly IClock _clock;

        public SupportPlansController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IAuditLogger auditLogger,
            IClock clock)
        {
            _dbFactory = dbFactory;
            _auditLogger = auditLogger;
            _clock = clock;
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
            await using var db = await _dbFactory.CreateDbContextAsync();

            var plans = await db.SupportPlans
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

        // ===== CUSTOMER: Lấy mức ưu tiên/gói hỗ trợ hiện tại của user =====
        // GET /api/supportplans/me/current
        [HttpGet("me/current")]
        [Authorize]
        public async Task<ActionResult<SupportPlanCurrentSubscriptionDto?>> GetMyCurrentSubscription()
        {
            var userId = GetCurrentUserIdOrNull();
            if (userId == null) return Unauthorized();

            var nowUtc = _clock.UtcNow;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId.Value);

            if (user == null) return Unauthorized();

            var sub = await db.UserSupportPlanSubscriptions
                .AsNoTracking()
                .Where(s =>
                    s.UserId == userId.Value &&
                    s.Status == "Active" &&
                    (!s.ExpiresAt.HasValue || s.ExpiresAt > nowUtc))
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            var effectivePriorityLevel = user.SupportPriorityLevel;

            if (sub == null)
            {
                return Ok(new SupportPlanCurrentSubscriptionDto
                {
                    SubscriptionId = Guid.Empty,
                    SupportPlanId = 0,
                    PlanName = string.Empty,
                    PlanDescription = null,
                    PriorityLevel = effectivePriorityLevel,
                    Price = 0,
                    Status = "None",
                    StartedAt = nowUtc,
                    ExpiresAt = null
                });
            }

            var plan = await db.SupportPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.SupportPlanId == sub.SupportPlanId);

            var planPriority = plan?.PriorityLevel ?? 0;
            if (planPriority > effectivePriorityLevel)
                effectivePriorityLevel = planPriority;

            return Ok(new SupportPlanCurrentSubscriptionDto
            {
                SubscriptionId = sub.SubscriptionId,
                SupportPlanId = sub.SupportPlanId,
                PlanName = plan?.Name ?? string.Empty,
                PlanDescription = plan?.Description,
                PriorityLevel = effectivePriorityLevel,
                Price = plan?.Price ?? 0,
                Status = sub.Status,
                StartedAt = sub.StartedAt,
                ExpiresAt = sub.ExpiresAt
            });
        }

        // ===== CUSTOMER: Xác nhận thanh toán thành công và tạo subscription 1 tháng =====
        // POST /api/supportplans/confirm-payment
        [HttpPost("confirm-payment")]
        [Authorize]
        public async Task<IActionResult> ConfirmSupportPlanPayment([FromBody] ConfirmSupportPlanPaymentDTO dto)
        {
            if (dto == null || dto.PaymentId == Guid.Empty || dto.SupportPlanId <= 0)
                return BadRequest(new { message = "Dữ liệu thanh toán không hợp lệ" });

            var userId = GetCurrentUserIdOrNull();
            if (userId == null) return Unauthorized();

            var nowUtc = _clock.UtcNow;

            await using var db = await _dbFactory.CreateDbContextAsync();

            // user (tracked vì sẽ update SupportPriorityLevel)
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);
            if (user == null) return Unauthorized();

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.PaymentId == dto.PaymentId);
            if (payment == null) return NotFound(new { message = "Payment không tồn tại" });

            // DB mới: Payment dùng TargetType/TargetId (không có TransactionType)
            if (!string.Equals(payment.TargetType, "SupportPlan", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Payment không thuộc loại SupportPlan" });

            if (string.IsNullOrWhiteSpace(payment.TargetId) ||
                !string.Equals(payment.TargetId.Trim(), dto.SupportPlanId.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Payment không khớp SupportPlanId" });
            }

            var status = payment.Status ?? string.Empty;
            if (!status.Equals("Paid", StringComparison.OrdinalIgnoreCase) &&
                !status.Equals("Success", StringComparison.OrdinalIgnoreCase) &&
                !status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Thanh toán chưa thành công hoặc đang chờ xử lý." });
            }

            var plan = await db.SupportPlans
                .FirstOrDefaultAsync(p => p.SupportPlanId == dto.SupportPlanId && p.IsActive);

            if (plan == null)
                return BadRequest(new { message = "Gói hỗ trợ không tồn tại hoặc đã bị khóa." });

            if (payment.Amount <= 0)
                return BadRequest(new { message = "Số tiền thanh toán không hợp lệ." });

            // Payment.Email trong DB mới NOT NULL -> so sánh trực tiếp
            if (!string.Equals(payment.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Payment không thuộc về tài khoản hiện tại." });

            // marker để idempotent (vì bảng UserSupportPlanSubscription DB mới không có PaymentId)
            var paymentMark = $"[PAYMENT:{payment.PaymentId}]";

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            // Idempotent: nếu đã tạo subscription từ payment này rồi -> trả luôn
            var existing = await db.UserSupportPlanSubscriptions
                .Where(s => s.UserId == user.UserId && s.Note != null && s.Note.Contains(paymentMark))
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                // đảm bảo chỉ 1 gói Active
                var otherActiveSubs = await db.UserSupportPlanSubscriptions
                    .Where(s =>
                        s.UserId == user.UserId &&
                        s.Status == "Active" &&
                        s.SubscriptionId != existing.SubscriptionId)
                    .ToListAsync();

                foreach (var sub in otherActiveSubs)
                {
                    sub.Status = "Cancelled";
                    sub.ExpiresAt = nowUtc;
                }

                if (otherActiveSubs.Count > 0)
                    await db.SaveChangesAsync();

                await tx.CommitAsync();

                var effectivePriorityLevelExisting = user.SupportPriorityLevel;
                if (plan.PriorityLevel > effectivePriorityLevelExisting)
                    effectivePriorityLevelExisting = plan.PriorityLevel;

                var existingDto = new SupportPlanCurrentSubscriptionDto
                {
                    SubscriptionId = existing.SubscriptionId,
                    SupportPlanId = existing.SupportPlanId,
                    PlanName = plan.Name,
                    PlanDescription = plan.Description,
                    PriorityLevel = effectivePriorityLevelExisting,
                    Price = plan.Price,
                    Status = existing.Status,
                    StartedAt = existing.StartedAt,
                    ExpiresAt = existing.ExpiresAt
                };

                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "ConfirmSupportPlanPayment",
                    entityType: "UserSupportPlanSubscription",
                    entityId: existing.SubscriptionId.ToString(),
                    before: null,
                    after: new
                    {
                        existing.SubscriptionId,
                        existing.UserId,
                        existing.SupportPlanId,
                        existing.Status,
                        existing.StartedAt,
                        existing.ExpiresAt,
                        PaymentId = payment.PaymentId,
                        EffectivePriorityLevel = effectivePriorityLevelExisting
                    }
                );

                return Ok(existingDto);
            }

            // huỷ các subscription Active cũ (đảm bảo chỉ 1 active)
            var oldActiveSubsForUser = await db.UserSupportPlanSubscriptions
                .Where(s => s.UserId == user.UserId && s.Status == "Active")
                .ToListAsync();

            foreach (var sub in oldActiveSubsForUser)
            {
                sub.Status = "Cancelled";
                sub.ExpiresAt = nowUtc;
            }

            // tạo subscription mới 1 tháng
            var subscription = new UserSupportPlanSubscription
            {
                UserId = user.UserId,
                SupportPlanId = plan.SupportPlanId,
                Status = "Active",
                StartedAt = nowUtc,
                ExpiresAt = nowUtc.AddMonths(1),
                Note = string.IsNullOrWhiteSpace(dto.Note) ? paymentMark : $"{paymentMark} {dto.Note}"
            };

            db.UserSupportPlanSubscriptions.Add(subscription);

            // nâng priority nếu gói cao hơn
            if (plan.PriorityLevel > user.SupportPriorityLevel)
                user.SupportPriorityLevel = plan.PriorityLevel;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var effectivePriorityLevelNew = user.SupportPriorityLevel;
            if (plan.PriorityLevel > effectivePriorityLevelNew)
                effectivePriorityLevelNew = plan.PriorityLevel;

            var result = new SupportPlanCurrentSubscriptionDto
            {
                SubscriptionId = subscription.SubscriptionId,
                SupportPlanId = subscription.SupportPlanId,
                PlanName = plan.Name,
                PlanDescription = plan.Description,
                PriorityLevel = effectivePriorityLevelNew,
                Price = plan.Price,
                Status = subscription.Status,
                StartedAt = subscription.StartedAt,
                ExpiresAt = subscription.ExpiresAt
            };

            await _auditLogger.LogAsync(
                HttpContext,
                action: "ConfirmSupportPlanPayment",
                entityType: "UserSupportPlanSubscription",
                entityId: subscription.SubscriptionId.ToString(),
                before: new
                {
                    UserId = user.UserId,
                    OldActiveSubscriptionCount = oldActiveSubsForUser.Count,
                    PaymentId = payment.PaymentId,
                    PlanId = plan.SupportPlanId
                },
                after: new
                {
                    subscription.SubscriptionId,
                    subscription.UserId,
                    subscription.SupportPlanId,
                    subscription.Status,
                    subscription.StartedAt,
                    subscription.ExpiresAt,
                    subscription.Note,
                    PaymentId = payment.PaymentId,
                    EffectivePriorityLevel = effectivePriorityLevelNew
                }
            );

            return Ok(result);
        }
    }
}
