// Keytietkiem/Services/Interfaces/IBannerService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Keytietkiem.Dtos;

namespace Keytietkiem.Services.Interfaces;

public interface IBannerService
{
    Task<List<BannerPublicDto>> GetPublicByPlacementAsync(string placement);
    Task<List<BannerAdminDto>> GetAdminListAsync(string? placement);

    Task<BannerAdminDto?> GetByIdAsync(long id);
    Task<BannerAdminDto> CreateAsync(BannerUpsertDto dto);
    Task<BannerAdminDto?> UpdateAsync(long id, BannerUpsertDto dto);
    Task<bool> DeleteAsync(long id);
}
