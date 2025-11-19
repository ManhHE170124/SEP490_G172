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

namespace Keytietkiem.DTOs.Products
{
    public static class ProductEnums
    {
        public const string SHARED_KEY = "SHARED_KEY";
        public const string PERSONAL_KEY = "PERSONAL_KEY";
        public const string SHARED_ACCOUNT = "SHARED_ACCOUNT";
        public const string PERSONAL_ACCOUNT = "PERSONAL_ACCOUNT";

        public static readonly HashSet<string> Types =
            new(StringComparer.OrdinalIgnoreCase)
            { SHARED_KEY, PERSONAL_KEY, SHARED_ACCOUNT, PERSONAL_ACCOUNT };

        public static readonly HashSet<string> Statuses =
            new(StringComparer.OrdinalIgnoreCase)
            { "ACTIVE", "INACTIVE", "OUT_OF_STOCK" };
    }

    // Image (dùng cho response)
    public record ProductImageDto(int ImageId, string Url, int SortOrder, bool IsPrimary, string? AltText);

    // FAQ (dùng cho response)
    public record ProductFaqDto(Guid FaqId, string Question, string Answer, int SortOrder, bool IsActive);

    // Variant (dùng trong ProductDetail)
    public record ProductVariantMiniDto(
        Guid VariantId,
        string VariantCode,
        string Title,
        int? DurationDays,
        int StockQty,
        string Status
    );

    // LIST ITEM (không có giá)
    public record ProductListItemDto(
        Guid ProductId,
        string ProductCode,
        string ProductName,
        string ProductType,
        int TotalStockQty,                 // Tổng stock của các biến thể
        string Status,
        IEnumerable<int> CategoryIds,
        IEnumerable<string> BadgeCodes
    );

    // DETAIL (gom Images + FAQs + Variants)
    public record ProductDetailDto(
        Guid ProductId,
        string ProductCode,
        string ProductName,
        string ProductType,
        string Status,
        IEnumerable<int> CategoryIds,
        IEnumerable<string> BadgeCodes,
        IEnumerable<ProductVariantMiniDto> Variants
    );

    // CREATE / UPDATE (không có giá/bảo hành/hết hạn/desc)
    public record ProductCreateDto(
        string ProductCode,
        string ProductName,
        string ProductType,
        string? Status,
        IEnumerable<int>? CategoryIds,
        IEnumerable<string>? BadgeCodes,
        string? Slug
    );

    public record ProductUpdateDto(
        string ProductName,
        string ProductType,
        string? Status,
        IEnumerable<int>? CategoryIds,
        IEnumerable<string>? BadgeCodes,
        string? Slug,
        string? ProductCode
    );
}
