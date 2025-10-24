namespace Keytietkiem.DTOs;

public static class ProductEnums
{
    public static readonly HashSet<string> Types =
        new(StringComparer.OrdinalIgnoreCase) { "Single", "Combo", "Pool", "Account" };

    public static readonly HashSet<string> Statuses =
        new(StringComparer.OrdinalIgnoreCase) { "Available", "Sold", "OutOfStock", "Expired", "Recalled", "Error" };
}

public record ProductListItemDto(
    Guid ProductId,
    string ProductCode,     // = SKU
    string ProductName,
    string ProductType,
    decimal? SalePrice,
    int StockQty,
    int WarrantyDays,
    string Status,
    IEnumerable<int> CategoryIds
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
    IEnumerable<int> CategoryIds
);

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
    string Status,
    string? Description,
    IEnumerable<int> CategoryIds
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
    string Status,
    string? Description,
    IEnumerable<int> CategoryIds
);

// Tăng/giảm theo %
public record BulkPriceUpdateDto(
    IEnumerable<int>? CategoryIds,
    string? ProductType,
    decimal Percent
);

// Kết quả import CSV
public record PriceImportResult(
    int TotalRows,
    int Updated,
    int NotFound,
    int Invalid);
