using Keytietkiem.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Services.Interfaces
{
    /// <summary>
    /// Service interface for managing product keys in the warehouse
    /// </summary>
    public interface IProductKeyService
    {
        /// <summary>
        /// Get a paginated and filtered list of product keys
        /// </summary>
        Task<ProductKeyListResponseDto> GetProductKeysAsync(
            ProductKeyFilterDto filter,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get detailed information about a specific product key
        /// </summary>
        Task<ProductKeyDetailDto> GetProductKeyByIdAsync(
            Guid keyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a new product key
        /// </summary>
        Task<ProductKeyDetailDto> CreateProductKeyAsync(
            CreateProductKeyDto dto,
            Guid actorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Update an existing product key
        /// </summary>
        Task<ProductKeyDetailDto> UpdateProductKeyAsync(
            UpdateProductKeyDto dto,
            Guid actorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a product key
        /// </summary>
        Task DeleteProductKeyAsync(
            Guid keyId,
            Guid actorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Assign a product key to an order
        /// </summary>
        Task AssignKeyToOrderAsync(
            AssignKeyToOrderDto dto,
            Guid actorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Unassign a product key from an order
        /// </summary>
        Task UnassignKeyFromOrderAsync(
            Guid keyId,
            Guid actorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk update status for multiple product keys
        /// </summary>
        Task<int> BulkUpdateKeyStatusAsync(
            BulkUpdateKeyStatusDto dto,
            Guid actorId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Export product keys to CSV
        /// </summary>
        Task<byte[]> ExportKeysToCSVAsync(
            ProductKeyFilterDto filter,
            CancellationToken cancellationToken = default);
    }
}
