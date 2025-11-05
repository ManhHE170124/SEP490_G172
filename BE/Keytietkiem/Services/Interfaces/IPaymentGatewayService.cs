using System.Collections.Generic;
using System.Threading.Tasks;
using Keytietkiem.DTOs;

namespace Keytietkiem.Services.Interfaces
{
    public interface IPaymentGatewayService
    {
        Task<IEnumerable<PaymentGatewayDto>> GetAllAsync();
        Task<PaymentGatewayDto> GetByIdAsync(int id);
        Task<PaymentGatewayDto> CreateAsync(PaymentGatewayDto dto);
        Task<PaymentGatewayDto> UpdateAsync(int id, PaymentGatewayDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
