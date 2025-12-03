/**
 * File: PaymentGatewayDto.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated: 
 * Purpose:
 *   Data Transfer Object (DTO) representing a payment gateway configuration.
 *   Used to define available payment methods, their callback URLs, and activation state
 *   for integration with checkout or donation workflows.
 */
namespace Keytietkiem.DTOs
{
    public class PaymentGatewayDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}