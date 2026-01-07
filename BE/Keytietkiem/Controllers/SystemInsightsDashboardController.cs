// File: Controllers/SystemInsightsDashboardController.cs
using Keytietkiem.Constants;
using Keytietkiem.DTOs.SystemInsights;
using Keytietkiem.Services;
using Keytietkiem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Controllers
{
    [ApiController]
    [Route("api/system-insights-dashboard")]
    [Authorize]
    public sealed class SystemInsightsDashboardController : ControllerBase
    {
        private readonly ISystemInsightsDashboardService _svc;
        private readonly ILogger<SystemInsightsDashboardController> _logger;

        public SystemInsightsDashboardController(
            ISystemInsightsDashboardService svc,
            ILogger<SystemInsightsDashboardController> logger)
        {
            _svc = svc;
            _logger = logger;
        }

        [HttpGet("overview")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<ActionResult<SystemInsightsOverviewResponse>> Overview(
            [FromQuery] DateTime? fromLocal,
            [FromQuery] DateTime? toLocalExclusive,
            [FromQuery] string bucket = "day",
            CancellationToken ct = default)
        {
            try
            {
                var data = await _svc.GetOverviewAsync(fromLocal, toLocalExclusive, bucket, ct);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "System insights dashboard error");
                return StatusCode(500, new { message = "Lỗi khi tải dashboard giám sát hệ thống. Vui lòng thử lại." });
            }
        }
    }
}
