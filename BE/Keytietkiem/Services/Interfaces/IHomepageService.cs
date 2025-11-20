using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.DTOs.Homepage;

namespace Keytietkiem.Services.Interfaces
{
    public interface IHomepageService
    {
        Task<HomepageResponseDto> GetAsync(CancellationToken cancellationToken = default);
    }
}
