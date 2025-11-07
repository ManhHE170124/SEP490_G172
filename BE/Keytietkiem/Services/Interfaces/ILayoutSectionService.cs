/**
 * File: ILayoutSectionService.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 
 * Description:
 *   Defines the service interface for managing layout sections within the website system.
 *   Provides asynchronous CRUD operations for LayoutSection entities.
 */
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
