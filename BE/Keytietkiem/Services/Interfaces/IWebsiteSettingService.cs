using Keytietkiem.DTOs;

namespace Keytietkiem.Services.Interfaces
{
    public interface IWebsiteSettingService
    {
        Task<WebsiteSettingDto> GetAsync();
        Task<WebsiteSettingDto> UpdateAsync(int id, WebsiteSettingDto dto);
    }
}
