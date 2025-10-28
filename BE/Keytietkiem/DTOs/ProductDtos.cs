using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs
{
    public static class ProductEnums
    {
        // FE hiển thị tiếng Việt; BE lưu code ổn định
        public const string SHARED_KEY = "SHARED_KEY";        // Key dùng chung
        public const string PERSONAL_KEY = "PERSONAL_KEY";      // Key cá nhân
        public const string SHARED_ACCOUNT = "SHARED_ACCOUNT";    // Tài khoản dùng chung
        public const string PERSONAL_ACCOUNT = "PERSONAL_ACCOUNT";  // Tài khoản cá nhân

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
        int SupplierId,
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

    // JSON create/update (không kèm file)
    public record ProductCreateDto(
        string ProductCode,
        string ProductName,
        int SupplierId,
        string ProductType,
        decimal? CostPrice,
        decimal SalePrice,
        int StockQty,
        int WarrantyDays,
        DateOnly? ExpiryDate,
        bool AutoDelivery,
        string? Status,          // null => BE tự suy theo tồn kho
        string? Description,
        string? ThumbnailUrl,    // nếu FE đã có URL có sẵn (CDN, v.v.)
        IEnumerable<int> CategoryIds,
        IEnumerable<string> BadgeCodes
    );

    public record ProductUpdateDto(
        string ProductName,
        int SupplierId,
        string ProductType,
        decimal? CostPrice,
        decimal SalePrice,
        int StockQty,
        int WarrantyDays,
        DateOnly? ExpiryDate,
        bool AutoDelivery,
        string? Status,          // null => BE tự suy theo tồn kho
        string? Description,
        string? ThumbnailUrl,
        IEnumerable<int> CategoryIds,
        IEnumerable<string> BadgeCodes
    );

    public record BulkPriceUpdateDto(IEnumerable<int>? CategoryIds, string? ProductType, decimal Percent);
    public record PriceImportResult(int TotalRows, int Updated, int NotFound, int Invalid);
}
