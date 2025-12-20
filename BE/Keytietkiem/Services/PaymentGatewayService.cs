/**
 * File: PaymentGatewayService.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 
 * Description:
 *   Service implementation for managing payment gateway configurations.
 *   Provides CRUD operations for PaymentGateway entities and DTO mapping.
 */

using Keytietkiem.DTOs;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Services
{
    public class PaymentGatewayService : IPaymentGatewayService
    {
        private readonly KeytietkiemDbContext _context;

        public PaymentGatewayService(KeytietkiemDbContext context)
        {
            _context = context;
        }

        private async Task<PaymentGateway> EnsurePayOSEntityAsync()
        {
            var gw = await _context.PaymentGateways.FirstOrDefaultAsync(x => x.Name == "PayOS");
            if (gw != null) return gw;

            gw = new PaymentGateway
            {
                Name = "PayOS",
                CallbackUrl = "",    // DB NOT NULL -> luôn set ""
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PaymentGateways.Add(gw);
            await _context.SaveChangesAsync();
            return gw;
        }

        public async Task<PayOSConfigViewDto> GetPayOSAsync()
        {
            var gw = await EnsurePayOSEntityAsync();

            return new PayOSConfigViewDto
            {
                ClientId = gw.ClientId ?? "",
                HasApiKey = !string.IsNullOrWhiteSpace(gw.ApiKey),
                HasChecksumKey = !string.IsNullOrWhiteSpace(gw.ChecksumKey)
            };
        }

        public async Task<PayOSConfigViewDto> UpdatePayOSAsync(PayOSConfigUpdateDto dto)
        {
            var gw = await EnsurePayOSEntityAsync();

            gw.ClientId = (dto.ClientId ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(dto.ApiKey))
                gw.ApiKey = dto.ApiKey.Trim();

            if (!string.IsNullOrWhiteSpace(dto.ChecksumKey))
                gw.ChecksumKey = dto.ChecksumKey.Trim();

            if (dto.IsActive.HasValue)
                gw.IsActive = dto.IsActive;

            // vẫn giữ CallbackUrl cho hợp schema
            if (gw.CallbackUrl == null) gw.CallbackUrl = "";

            gw.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new PayOSConfigViewDto
            {
                ClientId = gw.ClientId ?? "",
                HasApiKey = !string.IsNullOrWhiteSpace(gw.ApiKey),
                HasChecksumKey = !string.IsNullOrWhiteSpace(gw.ChecksumKey)
            };
        }
    }
}
