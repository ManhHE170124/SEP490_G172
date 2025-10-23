using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Keytietkiem.Data;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers.Admin
{
    [Route("api/admin/config")]
    [ApiController]
    public class ConfigController : ControllerBase
    {
        private readonly IConfigService _configService;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;

        public ConfigController(IConfigService configService, IWebHostEnvironment env, ApplicationDbContext db)
        {
            _configService = configService;
            _env = env;
            _db = db;
        }

        // GET - composed payload
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var json = await _configService.GetAsync("website_configuration") ?? "{}";
            var generalObj = JsonSerializer.Deserialize<object>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var sections = await _db.LayoutSections.OrderBy(s => s.DisplayOrder).ToListAsync();
            var gateways = await _db.PaymentGateways.OrderBy(g => g.CreatedAt).ToListAsync();

            var composed = new
            {
                general = generalObj,
                layoutSections = sections,
                paymentGateways = gateways
            };
            return Ok(composed);
        }

        // PUT legacy: save general only
        [HttpPut]
        public async Task<IActionResult> SaveGeneral([FromBody] JsonElement payload)
        {
            var json = JsonSerializer.Serialize(payload);
            await _configService.SetAsync("website_configuration", json);
            return Ok();
        }

        // PUT /api/admin/config/full - save ALL (general + lists) atomically
        [HttpPut("full")]
        public async Task<IActionResult> SaveFull([FromBody] JsonElement payload)
        {
            if (!payload.TryGetProperty("general", out var generalElem))
                return BadRequest("Missing 'general' in payload");

            var generalJson = generalElem.GetRawText();

            using var trx = await _db.Database.BeginTransactionAsync();
            try
            {
                // save general
                await _configService.SetAsync("website_configuration", generalJson);

                // SYNC LayoutSections
                var incomingSections = new List<LayoutSection>();
                if (payload.TryGetProperty("layoutSections", out var sectionsElem) && sectionsElem.ValueKind == JsonValueKind.Array)
                {
                    incomingSections = JsonSerializer.Deserialize<List<LayoutSection>>(sectionsElem.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<LayoutSection>();
                }

                var existingSections = await _db.LayoutSections.ToListAsync();
                var incomingSectionIds = new HashSet<long>();

                foreach (var inc in incomingSections)
                {
                    if (inc.LayoutSectionId > 0)
                    {
                        var ex = existingSections.FirstOrDefault(x => x.LayoutSectionId == inc.LayoutSectionId);
                        if (ex != null)
                        {
                            ex.SectionKey = inc.SectionKey;
                            ex.DisplayName = inc.DisplayName;
                            ex.DisplayOrder = inc.DisplayOrder;
                            ex.IsVisible = inc.IsVisible;
                            _db.LayoutSections.Update(ex);
                            incomingSectionIds.Add(ex.LayoutSectionId);
                        }
                        else
                        {
                            inc.CreatedAt = DateTime.UtcNow;
                            _db.LayoutSections.Add(inc);
                            await _db.SaveChangesAsync();
                            incomingSectionIds.Add(inc.LayoutSectionId);
                        }
                    }
                    else
                    {
                        inc.CreatedAt = DateTime.UtcNow;
                        _db.LayoutSections.Add(inc);
                        await _db.SaveChangesAsync();
                        incomingSectionIds.Add(inc.LayoutSectionId);
                    }
                }

                var toDeleteSections = existingSections.Where(x => !incomingSectionIds.Contains(x.LayoutSectionId)).ToList();
                if (toDeleteSections.Any()) _db.LayoutSections.RemoveRange(toDeleteSections);
                await _db.SaveChangesAsync();

                // SYNC PaymentGateways
                var incomingGateways = new List<PaymentGateway>();
                if (payload.TryGetProperty("paymentGateways", out var gatewaysElem) && gatewaysElem.ValueKind == JsonValueKind.Array)
                {
                    incomingGateways = JsonSerializer.Deserialize<List<PaymentGateway>>(gatewaysElem.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<PaymentGateway>();
                }

                var existingGateways = await _db.PaymentGateways.ToListAsync();
                var incomingGatewayIds = new HashSet<long>();

                foreach (var inc in incomingGateways)
                {
                    if (inc.PaymentGatewayId > 0)
                    {
                        var ex = existingGateways.FirstOrDefault(x => x.PaymentGatewayId == inc.PaymentGatewayId);
                        if (ex != null)
                        {
                            ex.Name = inc.Name;
                            ex.CallbackUrl = inc.CallbackUrl;
                            ex.IsActive = inc.IsActive;
                            ex.ConfigJson = inc.ConfigJson;
                            _db.PaymentGateways.Update(ex);
                            incomingGatewayIds.Add(ex.PaymentGatewayId);
                        }
                        else
                        {
                            inc.CreatedAt = DateTime.UtcNow;
                            _db.PaymentGateways.Add(inc);
                            await _db.SaveChangesAsync();
                            incomingGatewayIds.Add(inc.PaymentGatewayId);
                        }
                    }
                    else
                    {
                        inc.CreatedAt = DateTime.UtcNow;
                        _db.PaymentGateways.Add(inc);
                        await _db.SaveChangesAsync();
                        incomingGatewayIds.Add(inc.PaymentGatewayId);
                    }
                }

                var toDeleteGateways = existingGateways.Where(x => !incomingGatewayIds.Contains(x.PaymentGatewayId)).ToList();
                if (toDeleteGateways.Any()) _db.PaymentGateways.RemoveRange(toDeleteGateways);
                await _db.SaveChangesAsync();

                await trx.CommitAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // Upload logo (kept for convenience)
        [HttpPost("upload-logo")]
        public async Task<IActionResult> UploadLogo([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file");

            var uploads = System.IO.Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
            if (!System.IO.Directory.Exists(uploads)) System.IO.Directory.CreateDirectory(uploads);

            var filename = $"logo_{DateTime.UtcNow.Ticks}{System.IO.Path.GetExtension(file.FileName)}";
            var filePath = System.IO.Path.Combine(uploads, filename);
            using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var url = $"{baseUrl}/uploads/{filename}";

            // Optionally update general.logoUrl in AppSettings here if you want immediate persist
            var json = await _configService.GetAsync("website_configuration");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                    dict["logoUrl"] = url;
                    var updated = JsonSerializer.Serialize(dict);
                    await _configService.SetAsync("website_configuration", updated);
                }
                catch { /* ignore parse errors */ }
            }

            return Ok(new { url });
        }
    }
}