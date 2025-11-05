using Microsoft.EntityFrameworkCore;
using Keytietkiem.DTOs;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;

    namespace Keytietkiem.Services
    {

        public class LayoutSectionService : ILayoutSectionService
    {
        private readonly KeytietkiemDbContext _context;
        public LayoutSectionService(KeytietkiemDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<LayoutSectionDto>> GetAllAsync()
        {
            return await _context.LayoutSections
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new LayoutSectionDto
                {
                    Id = x.Id,
                    SectionKey = x.SectionKey,
                    SectionName = x.SectionName,
                    DisplayOrder = x.DisplayOrder,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                }).ToListAsync();
        }

        public async Task<LayoutSectionDto> GetByIdAsync(int id)
        {
            var x = await _context.LayoutSections.FindAsync(id);
            if (x == null) return null;
            return new LayoutSectionDto
            {
                Id = x.Id,
                SectionKey = x.SectionKey,
                SectionName = x.SectionName,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            };
        }

        public async Task<LayoutSectionDto> CreateAsync(LayoutSectionDto dto)
        {
            var entity = new LayoutSection
            {
                SectionKey = dto.SectionKey,
                SectionName = dto.SectionName,
                DisplayOrder = dto.DisplayOrder,
                IsActive = dto.IsActive,
                CreatedAt = System.DateTime.UtcNow,
                UpdatedAt = System.DateTime.UtcNow
            };
            _context.LayoutSections.Add(entity);
            await _context.SaveChangesAsync();
            dto.Id = entity.Id;
            dto.CreatedAt = entity.CreatedAt;
            dto.UpdatedAt = entity.UpdatedAt;
            return dto;
        }

        public async Task<LayoutSectionDto> UpdateAsync(int id, LayoutSectionDto dto)
        {
            var entity = await _context.LayoutSections.FindAsync(id);
            if (entity == null) return null;

            entity.SectionKey = dto.SectionKey;
            entity.SectionName = dto.SectionName;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = System.DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return dto;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.LayoutSections.FindAsync(id);
            if (entity == null) return false;

            _context.LayoutSections.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
