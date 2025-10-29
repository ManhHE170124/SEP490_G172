/**
 * File: ProductDtos.cs
 * Author: ManhLDHE170124
 * Created: 24/10/2025
 * Last Updated: 28/10/2025
 * Version: 1.0.0
 * Purpose: Data Transfer Objects and enums for Product feature. Provide request/response
 *          contracts (list/detail/create/update), price-import/export payloads, and image DTOs.
 *
 * DTOs & Types Included:
 *   - ProductEnums            : Stable string enums & validation sets (Types, Statuses)
 *   - ProductImageDto         : Image metadata for a product
 *   - ProductListItemDto      : Lightweight item for product listing
 *   - ProductDetailDto        : Full product details
 *   - ProductCreateDto        : Payload for creating a product (JSON)
 *   - ProductUpdateDto        : Payload for updating a product (JSON)
 *   - BulkPriceUpdateDto      : Payload for bulk percentage price updates
 *   - PriceImportResult       : Result summary for CSV price import
 *
 * Usage:
 *   - API request/response shaping for product management
 *   - Admin screens: listing, detail, images, pricing tools, import/export
 */

using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs
{
    public static class ProductEnums
    {
        public const string SHARED_KEY = "SHARED_KEY";
        public const string PERSONAL_KEY = "PERSONAL_KEY";
        public const string SHARED_ACCOUNT = "SHARED_ACCOUNT";
        public const string PERSONAL_ACCOUNT = "PERSONAL_ACCOUNT";

        public static readonly HashSet<string> Types =
            new(StringComparer.OrdinalIgnoreCase)
            {
                SHARED_KEY, PERSONAL_KEY, SHARED_ACCOUNT, PERSONAL_ACCOUNT
            };

        public static readonly HashSet<string> Statuses =
            new(StringComparer.OrdinalIgnoreCase) { "ACTIVE", "INACTIVE", "OUT_OF_STOCK" };
    }

    public record ProductImageDto(int ImageId, string Url, int SortOrder, bool IsPrimary);

    public record ProductListItemDto(
        Guid ProductId,
        string ProductCode,
        string ProductName,
        string ProductType,
        decimal? SalePrice,
        int StockQty,
        int WarrantyDays,
        string Status,
        string? ThumbnailUrl,
        IEnumerable<int> CategoryIds,
        IEnumerable<string> BadgeCodes
    );

    public record ProductDetailDto(
        Guid ProductId,
        string ProductCode,
        string ProductName,
        string ProductType,
        decimal? CostPrice,
        decimal? SalePrice,
        int StockQty,
        int WarrantyDays,
        DateOnly? ExpiryDate,
        bool AutoDelivery,
        string Status,
        string? Description,
        string? ThumbnailUrl,
        IEnumerable<int> CategoryIds,
        IEnumerable<string> BadgeCodes,
        IEnumerable<ProductImageDto> Images
    );

    public record ProductCreateDto(
        string ProductCode,
        string ProductName,
        string ProductType,
        decimal? CostPrice,
        decimal SalePrice,
        int StockQty,
        int WarrantyDays,
        DateOnly? ExpiryDate,
        bool AutoDelivery,
        string? Status,
        string? Description,
        string? ThumbnailUrl,
        IEnumerable<int> CategoryIds,
        IEnumerable<string> BadgeCodes
    );

    public record ProductUpdateDto(
        string ProductName,
        string ProductType,
        decimal? CostPrice,
        decimal SalePrice,
        int StockQty,
        int WarrantyDays,
        DateOnly? ExpiryDate,
        bool AutoDelivery,
        string? Status,
        string? Description,
        string? ThumbnailUrl,
        IEnumerable<int> CategoryIds,
        IEnumerable<string> BadgeCodes
    );

    public record BulkPriceUpdateDto(IEnumerable<int>? CategoryIds, string? ProductType, decimal Percent);
    public record PriceImportResult(int TotalRows, int Updated, int NotFound, int Invalid);
}
