using System.Collections.Generic;
using System.Threading.Tasks;
using Keytietkiem.DTOs;

namespace Keytietkiem.Services.Interfaces
{
    public interface ILayoutSectionService
    {
        Task<IEnumerable<LayoutSectionDto>> GetAllAsync();
        Task<LayoutSectionDto> GetByIdAsync(int id);
        Task<LayoutSectionDto> CreateAsync(LayoutSectionDto dto);
        Task<LayoutSectionDto> UpdateAsync(int id, LayoutSectionDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
