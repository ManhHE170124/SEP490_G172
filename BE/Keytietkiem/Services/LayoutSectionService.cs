/**
 * File: LayoutSectionService.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 
 * Purpose:
 *   Service implementation for managing layout sections within the website.
 *   Provides CRUD operations for layout sections (e.g., header, footer, sidebar, etc.)
 *   that define configurable display areas of the web application.
 */
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
        /**
         * Summary: Retrieve all layout sections ordered by display order.
         * Returns: IEnumerable of LayoutSectionDto
         */

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
        /**
         * Summary: Retrieve a layout section by ID.
         * Params: id (int) - layout section identifier
         * Returns: LayoutSectionDto or null if not found
         */
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
        /**
         * Summary: Create a new layout section.
         * Params: dto (LayoutSectionDto) - layout section data
         * Returns: LayoutSectionDto - created layout section
         */
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
        /**
       * Summary: Update an existing layout section.
       * Params: id (int) - layout section identifier
       *         dto (LayoutSectionDto) - updated section data
       * Returns: LayoutSectionDto - updated layout section, or null if not found
       */
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
        /**
         * Summary: Delete a layout section by ID.
         * Params: id (int) - layout section identifier
         * Returns: true if deleted successfully, false if not found
         */
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
