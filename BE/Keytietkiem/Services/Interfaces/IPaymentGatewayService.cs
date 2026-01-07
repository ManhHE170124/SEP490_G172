/**
 * File: IPaymentGatewayService.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 
 * Description:
 *   Defines the service interface for managing payment gateway configurations.
 *   Provides asynchronous CRUD operations for PaymentGateway entities.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using Keytietkiem.DTOs;

namespace Keytietkiem.Services.Interfaces
{
    public interface IPaymentGatewayService
    {
        Task<PayOSConfigViewDto> GetPayOSAsync();
        Task<PayOSConfigViewDto> UpdatePayOSAsync(PayOSConfigUpdateDto dto);
    }
}
