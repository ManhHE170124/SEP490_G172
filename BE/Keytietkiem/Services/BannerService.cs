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

    // ====== PUBLIC LIST ======
    public async Task<List<BannerPublicDto>> GetPublicByPlacementAsync(string placement)
    {
        placement = (placement ?? "").Trim();
        if (string.IsNullOrWhiteSpace(placement)) return new List<BannerPublicDto>();

        var now = DateTime.UtcNow;

        // Query chuẩn (active + time window + sort)
        async Task<List<Banner>> LoadPublicEntities(string p)
        {
            return await _db.Banners
                .AsNoTracking()
                .Where(x => x.Placement == p)
                .Where(x => x.IsActive)
                .Where(x => x.StartAt == null || x.StartAt <= now)
                .Where(x => x.EndAt == null || x.EndAt >= now)
                .OrderBy(x => x.SortOrder)
                .ThenByDescending(x => x.UpdatedAt)
                .ToListAsync();
        }

        // Case đặc biệt: HOME_SIDE phải luôn có đủ 2
        if (string.Equals(placement, "HOME_SIDE", StringComparison.OrdinalIgnoreCase))
        {
            var side = await LoadPublicEntities("HOME_SIDE");
            if (side.Count >= 2)
            {
                return side.Select(ToPublicDto).ToList();
            }

            var needed = 2 - side.Count;

            // “mượn” từ HOME_MAIN: lấy banner xếp cuối (tức sortOrder lớn nhất)
            var main = await LoadPublicEntities("HOME_MAIN");

            // lấy từ cuối lên (nhưng giữ thứ tự append cho đẹp)
            var fillers = main
                .OrderBy(x => x.SortOrder).ThenByDescending(x => x.UpdatedAt)
                .Reverse()
                .Take(needed)
                .Reverse()
                .ToList();

            // gắn sortOrder tiếp theo để FE sort lên đúng (FE đang sort theo sortOrder) :contentReference[oaicite:1]{index=1}
            var startSort = side.Count == 0 ? 0 : (side.Max(x => x.SortOrder) + 1);

            var result = new List<BannerPublicDto>();
            result.AddRange(side.Select(ToPublicDto));

            for (int i = 0; i < fillers.Count; i++)
            {
                var b = fillers[i];
                result.Add(new BannerPublicDto
                {
                    Id = b.Id,
                    Placement = "HOME_SIDE",           // trả về như HOME_SIDE để FE hiểu cùng nhóm
                    Title = b.Title,
                    MediaUrl = b.MediaUrl,
                    MediaType = b.MediaType,
                    LinkUrl = b.LinkUrl,
                    LinkTarget = b.LinkTarget,
                    SortOrder = startSort + i
                });
            }

            return result;
        }

        // placement khác: trả bình thường
        var data = await LoadPublicEntities(placement);
        return data.Select(ToPublicDto).ToList();
    }

    // ====== ADMIN LIST ======
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

    // ====== CREATE (HƯỚNG B: INSERT SORTORDER + SHIFT) ======
    public async Task<BannerAdminDto> CreateAsync(BannerUpsertDto dto)
    {
        Validate(dto);

        var placement = dto.Placement.Trim();
        var now = DateTime.UtcNow;

        // normalize sortOrder: coi như “vị trí chèn” (0..count)
        var count = await _db.Banners.CountAsync(x => x.Placement == placement);
        var desired = Clamp(dto.SortOrder, 0, count);

        await using var tx = await _db.Database.BeginTransactionAsync();

        // shift các item >= desired lên +1
        var toShift = await _db.Banners
            .Where(x => x.Placement == placement && x.SortOrder >= desired)
            .OrderByDescending(x => x.SortOrder)
            .ToListAsync();

        foreach (var b in toShift) b.SortOrder += 1;

        var entity = new Banner
        {
            Placement = placement,
            Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim(),

            MediaUrl = dto.MediaUrl.Trim(),
            MediaType = string.IsNullOrWhiteSpace(dto.MediaType) ? "image" : dto.MediaType.Trim(),

            LinkUrl = string.IsNullOrWhiteSpace(dto.LinkUrl) ? null : dto.LinkUrl.Trim(),
            LinkTarget = string.IsNullOrWhiteSpace(dto.LinkTarget) ? "_self" : dto.LinkTarget.Trim(),

            SortOrder = desired,
            IsActive = dto.IsActive,

            StartAt = dto.StartAt,
            EndAt = dto.EndAt,

            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Banners.Add(entity);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return ToAdminDto(entity);
    }

    // ====== UPDATE (HƯỚNG B: MOVE + SHIFT) ======
    public async Task<BannerAdminDto?> UpdateAsync(long id, BannerUpsertDto dto)
    {
        Validate(dto);

        var entity = await _db.Banners.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return null;

        var oldPlacement = entity.Placement;
        var oldSort = entity.SortOrder;

        var newPlacement = dto.Placement.Trim();

        await using var tx = await _db.Database.BeginTransactionAsync();

        // tính desired theo placement mới (coi như index chèn)
        var countNewPlacement = await _db.Banners.CountAsync(x => x.Placement == newPlacement && x.Id != id);
        var desired = Clamp(dto.SortOrder, 0, countNewPlacement);

        if (string.Equals(oldPlacement, newPlacement, StringComparison.OrdinalIgnoreCase))
        {
            // cùng placement
            if (desired != oldSort)
            {
                if (desired < oldSort)
                {
                    // move up: [desired .. oldSort-1] +1
                    var toShiftUp = await _db.Banners
                        .Where(x => x.Placement == oldPlacement && x.Id != id)
                        .Where(x => x.SortOrder >= desired && x.SortOrder < oldSort)
                        .OrderByDescending(x => x.SortOrder)
                        .ToListAsync();
                    foreach (var b in toShiftUp) b.SortOrder += 1;
                }
                else
                {
                    // move down: [oldSort+1 .. desired] -1
                    var toShiftDown = await _db.Banners
                        .Where(x => x.Placement == oldPlacement && x.Id != id)
                        .Where(x => x.SortOrder > oldSort && x.SortOrder <= desired)
                        .OrderBy(x => x.SortOrder)
                        .ToListAsync();
                    foreach (var b in toShiftDown) b.SortOrder -= 1;
                }

                entity.SortOrder = desired;
            }
        }
        else
        {
            // đổi placement
            // 1) đóng gap ở placement cũ: các item > oldSort -1
            var closeGap = await _db.Banners
                .Where(x => x.Placement == oldPlacement && x.Id != id && x.SortOrder > oldSort)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
            foreach (var b in closeGap) b.SortOrder -= 1;

            // 2) chèn vào placement mới: các item >= desired +1
            var shiftNew = await _db.Banners
                .Where(x => x.Placement == newPlacement && x.Id != id && x.SortOrder >= desired)
                .OrderByDescending(x => x.SortOrder)
                .ToListAsync();
            foreach (var b in shiftNew) b.SortOrder += 1;

            entity.Placement = newPlacement;
            entity.SortOrder = desired;
        }

        // update fields
        entity.Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
        entity.MediaUrl = dto.MediaUrl.Trim();
        entity.MediaType = string.IsNullOrWhiteSpace(dto.MediaType) ? "image" : dto.MediaType.Trim();

        entity.LinkUrl = string.IsNullOrWhiteSpace(dto.LinkUrl) ? null : dto.LinkUrl.Trim();
        entity.LinkTarget = string.IsNullOrWhiteSpace(dto.LinkTarget) ? "_self" : dto.LinkTarget.Trim();

        entity.IsActive = dto.IsActive;
        entity.StartAt = dto.StartAt;
        entity.EndAt = dto.EndAt;

        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return ToAdminDto(entity);
    }

    // ====== DELETE (đóng gap để sortOrder luôn “đẹp”) ======
    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _db.Banners.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return false;

        var placement = entity.Placement;
        var oldSort = entity.SortOrder;

        await using var tx = await _db.Database.BeginTransactionAsync();

        _db.Banners.Remove(entity);

        // kéo các item phía sau lên -1
        var toShift = await _db.Banners
            .Where(x => x.Placement == placement && x.SortOrder > oldSort)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        foreach (var b in toShift) b.SortOrder -= 1;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }

    // ====== DTO MAP ======
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

    private static BannerPublicDto ToPublicDto(Banner x) => new BannerPublicDto
    {
        Id = x.Id,
        Placement = x.Placement,
        Title = x.Title,
        MediaUrl = x.MediaUrl,
        MediaType = x.MediaType,
        LinkUrl = x.LinkUrl,
        LinkTarget = x.LinkTarget,
        SortOrder = x.SortOrder
    };

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    // ====== VALIDATE ======
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

        if (dto.SortOrder < 0)
            throw new ArgumentException("SortOrder phải >= 0");

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
