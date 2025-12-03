/**
 * File: IWebsiteSettingService.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 15/11/2025
 * Description: Interface for WebsiteSettingService
 */

using Keytietkiem.DTOs;
using Keytietkiem.Models;

namespace Keytietkiem.Services.Interfaces
{
    public interface IWebsiteSettingService
    {
        Task<WebsiteSettingDto?> GetAsync();
        Task<WebsiteSettingDto?> UpdateAsync(int id, WebsiteSettingDto dto);

        // ✅ NEW METHODS
        Task<WebsiteSetting> GetOrCreateAsync();
        Task<WebsiteSetting> SaveFromRequestAsync(WebsiteSettingsRequestDto dto, string? logoUrl);
    }
}