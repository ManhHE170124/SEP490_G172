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
                .OrderBy(x => x.Name) // Sort by name
                .Select(x => new PaymentGatewayDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    CallbackUrl = x.CallbackUrl,
                    IsActive = x.IsActive ?? false,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<PaymentGatewayDto> GetByIdAsync(int id)
        {
            var entity = await _context.PaymentGateways.FindAsync(id);
            if (entity == null) return null;

            return new PaymentGatewayDto
            {
                Id = entity.Id,
                Name = entity.Name,
                CallbackUrl = entity.CallbackUrl,
                IsActive = entity.IsActive ?? false,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public async Task<PaymentGatewayDto> CreateAsync(PaymentGatewayDto dto)
        {
            var entity = new PaymentGateway
            {
                Name = dto.Name.Trim(),
                CallbackUrl = dto.CallbackUrl.Trim(),
                IsActive = dto.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PaymentGateways.Add(entity);
            await _context.SaveChangesAsync();

            return new PaymentGatewayDto
            {
                Id = entity.Id,
                Name = entity.Name,
                CallbackUrl = entity.CallbackUrl,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public async Task<PaymentGatewayDto> UpdateAsync(int id, PaymentGatewayDto dto)
        {
            var entity = await _context.PaymentGateways.FindAsync(id);
            if (entity == null) return null;

            entity.Name = dto.Name.Trim();
            entity.CallbackUrl = dto.CallbackUrl.Trim();
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new PaymentGatewayDto
            {
                Id = entity.Id,
                Name = entity.Name,
                CallbackUrl = entity.CallbackUrl,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.PaymentGateways.FindAsync(id);
            if (entity == null) return false;

            _context.PaymentGateways.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PaymentGatewayDto> ToggleActiveAsync(int id)
        {
            var entity = await _context.PaymentGateways.FindAsync(id);
            if (entity == null) return null;

            entity.IsActive = !(entity.IsActive ?? false);
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new PaymentGatewayDto
            {
                Id = entity.Id,
                Name = entity.Name,
                CallbackUrl = entity.CallbackUrl,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}