/**
 * File: IWebsiteSettingService.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 
 * Description:
 *   Defines the service interface for retrieving and updating website-wide configuration settings.
 *   Supports reading the current settings and applying updates to the existing record.
 */
using Keytietkiem.DTOs;

namespace Keytietkiem.Services.Interfaces
{
    public interface IWebsiteSettingService
    {
        Task<WebsiteSettingDto> GetAsync();
        Task<WebsiteSettingDto> UpdateAsync(int id, WebsiteSettingDto dto);
    }
}
