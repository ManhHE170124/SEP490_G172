// Keytietkiem/Services/BannerService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keytietkiem.Dtos;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Services;

public class BannerService : IBannerService
{
    private readonly KeytietkiemDbContext _db;

    public BannerService(KeytietkiemDbContext db)
    {
        _db = db;
    }

    public async Task<List<BannerPublicDto>> GetPublicByPlacementAsync(string placement)
    {
        placement = (placement ?? "").Trim();
        if (string.IsNullOrWhiteSpace(placement)) return new List<BannerPublicDto>();

        var now = DateTime.UtcNow;

        return await _db.Banners
            .AsNoTracking()
            .Where(x => x.Placement == placement)
            .Where(x => x.IsActive)
            .Where(x => x.StartAt == null || x.StartAt <= now)
            .Where(x => x.EndAt == null || x.EndAt >= now)
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => new BannerPublicDto
            {
                Id = x.Id,
                Placement = x.Placement,
                Title = x.Title,
                MediaUrl = x.MediaUrl,
                MediaType = x.MediaType,
                LinkUrl = x.LinkUrl,
                LinkTarget = x.LinkTarget,
                SortOrder = x.SortOrder
            })
            .ToListAsync();
    }

    public async Task<List<BannerAdminDto>> GetAdminListAsync(string? placement)
    {
        placement = placement?.Trim();
        var query = _db.Banners.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(placement))
            query = query.Where(x => x.Placement == placement);

        return await query
            .OrderBy(x => x.Placement)
            .ThenBy(x => x.SortOrder)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => ToAdminDto(x))
            .ToListAsync();
    }

    public async Task<BannerAdminDto?> GetByIdAsync(long id)
    {
        var entity = await _db.Banners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return entity == null ? null : ToAdminDto(entity);
    }

    public async Task<BannerAdminDto> CreateAsync(BannerUpsertDto dto)
    {
        Validate(dto);

        var now = DateTime.UtcNow;

        var entity = new Banner
        {
            Placement = dto.Placement.Trim(),
            Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim(),

            MediaUrl = dto.MediaUrl.Trim(),
            MediaType = string.IsNullOrWhiteSpace(dto.MediaType) ? "image" : dto.MediaType.Trim(),

            LinkUrl = string.IsNullOrWhiteSpace(dto.LinkUrl) ? null : dto.LinkUrl.Trim(),
            LinkTarget = string.IsNullOrWhiteSpace(dto.LinkTarget) ? "_self" : dto.LinkTarget.Trim(),

            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,

            StartAt = dto.StartAt,
            EndAt = dto.EndAt,

            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Banners.Add(entity);
        await _db.SaveChangesAsync();

        return ToAdminDto(entity);
    }

    public async Task<BannerAdminDto?> UpdateAsync(long id, BannerUpsertDto dto)
    {
        Validate(dto);

        var entity = await _db.Banners.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return null;

        entity.Placement = dto.Placement.Trim();
        entity.Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();

        entity.MediaUrl = dto.MediaUrl.Trim();
        entity.MediaType = string.IsNullOrWhiteSpace(dto.MediaType) ? "image" : dto.MediaType.Trim();

        entity.LinkUrl = string.IsNullOrWhiteSpace(dto.LinkUrl) ? null : dto.LinkUrl.Trim();
        entity.LinkTarget = string.IsNullOrWhiteSpace(dto.LinkTarget) ? "_self" : dto.LinkTarget.Trim();

        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;

        entity.StartAt = dto.StartAt;
        entity.EndAt = dto.EndAt;

        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToAdminDto(entity);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _db.Banners.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return false;

        _db.Banners.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    private static BannerAdminDto ToAdminDto(Banner x) => new BannerAdminDto
    {
        Id = x.Id,
        Placement = x.Placement,
        Title = x.Title,
        MediaUrl = x.MediaUrl,
        MediaType = x.MediaType,
        LinkUrl = x.LinkUrl,
        LinkTarget = x.LinkTarget,
        SortOrder = x.SortOrder,
        IsActive = x.IsActive,
        StartAt = x.StartAt,
        EndAt = x.EndAt,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    private static void Validate(BannerUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Placement))
            throw new ArgumentException("Placement không được để trống");

        if (dto.Placement.Length > 100)
            throw new ArgumentException("Placement tối đa 100 ký tự");

        if (string.IsNullOrWhiteSpace(dto.MediaUrl))
            throw new ArgumentException("MediaUrl không được để trống");

        if (dto.MediaUrl.Length > 500)
            throw new ArgumentException("MediaUrl tối đa 500 ký tự");

        if (string.IsNullOrWhiteSpace(dto.MediaType))
            throw new ArgumentException("MediaType không được để trống");

        if (dto.MediaType.Length > 30)
            throw new ArgumentException("MediaType tối đa 30 ký tự");

        if (!string.IsNullOrWhiteSpace(dto.LinkTarget) && dto.LinkTarget.Length > 20)
            throw new ArgumentException("LinkTarget tối đa 20 ký tự");

        if (!string.IsNullOrWhiteSpace(dto.LinkUrl))
        {
            if (dto.LinkUrl.Length > 500)
                throw new ArgumentException("LinkUrl tối đa 500 ký tự");

            var u = dto.LinkUrl.Trim();
            var ok =
                u.StartsWith("/") ||
                u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                u.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            if (!ok)
                throw new ArgumentException("LinkUrl chỉ hỗ trợ route nội bộ (/...) hoặc http/https");
        }

        if (dto.StartAt.HasValue && dto.EndAt.HasValue && dto.StartAt > dto.EndAt)
            throw new ArgumentException("StartAt không được lớn hơn EndAt");
    }
}
