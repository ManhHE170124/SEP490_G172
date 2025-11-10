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

        public async Task<IEnumerable<PaymentGatewayDto>> GetAllAsync()
        {
            return await _context.PaymentGateways
                .Select(x => new PaymentGatewayDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    CallbackUrl = x.CallbackUrl,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                }).ToListAsync();
        }

        public async Task<PaymentGatewayDto> GetByIdAsync(int id)
        {
            var x = await _context.PaymentGateways.FindAsync(id);
            if (x == null) return null;
            return new PaymentGatewayDto
            {
                Id = x.Id,
                Name = x.Name,
                CallbackUrl = x.CallbackUrl,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            };
        }

        public async Task<PaymentGatewayDto> CreateAsync(PaymentGatewayDto dto)
        {
            var entity = new PaymentGateway
            {
                Name = dto.Name,
                CallbackUrl = dto.CallbackUrl,
                IsActive = dto.IsActive,
                CreatedAt = System.DateTime.UtcNow,
                UpdatedAt = System.DateTime.UtcNow
            };
            _context.PaymentGateways.Add(entity);
            await _context.SaveChangesAsync();
            dto.Id = entity.Id;
            dto.CreatedAt = entity.CreatedAt;
            dto.UpdatedAt = entity.UpdatedAt;
            return dto;
        }

        public async Task<PaymentGatewayDto> UpdateAsync(int id, PaymentGatewayDto dto)
        {
            var entity = await _context.PaymentGateways.FindAsync(id);
            if (entity == null) return null;
            entity.Name = dto.Name;
            entity.CallbackUrl = dto.CallbackUrl;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = System.DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return dto;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.PaymentGateways.FindAsync(id);
            if (entity == null) return false;
            _context.PaymentGateways.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
