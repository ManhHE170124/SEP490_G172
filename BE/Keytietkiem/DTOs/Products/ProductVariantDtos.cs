// DTOs/Products/ProductVariantDtos.cs
using System;

namespace Keytietkiem.DTOs.Products
{
    using Microsoft.AspNetCore.Http;

    public record ProductVariantListQuery(
        string? Q,
        string? Status,          // ACTIVE | INACTIVE | OUT_OF_STOCK
        string? Dur,
        decimal? MinPrice = null, // lọc theo giá bán tối thiểu (SellPrice)
        decimal? MaxPrice = null, // lọc theo giá bán tối đa (SellPrice) // "<=30" | "31-180" | ">180"
        string? Sort = "created", // created|title|duration|stock|status|views|price
        string? Dir = "desc",
        int Page = 1,
        int PageSize = 10
    );

    // List: hiển thị nhanh + thumbnail + viewcount + 3 loại giá
    public record ProductVariantListItemDto(
        Guid VariantId,
        string VariantCode,
        string Title,
        int? DurationDays,
        int StockQty,
        string Status,
        string? Thumbnail,
        int ViewCount,
        decimal SellPrice,   // Giá bán
        decimal ListPrice,   // Giá niêm yết
        decimal CogsPrice    // Giá vốn (read-only cho FE, không sửa qua API này)
    );

    // Detail: đầy đủ SEO field + 3 loại giá
    public record ProductVariantDetailDto(
        Guid VariantId,
        Guid ProductId,
        string VariantCode,
        string Title,
        int? DurationDays,
        int StockQty,
        int? WarrantyDays,
        string? Thumbnail,
        string? MetaTitle,
        string? MetaDescription,
        int ViewCount,
        string Status,
        decimal SellPrice,   // Giá bán
        decimal ListPrice,   // Giá niêm yết
        decimal CogsPrice    // Giá vốn (hiển thị cho admin, không cho sửa từ đây)
    );

    // Create: KHÔNG cho truyền CogsPrice, chỉ SellPrice + ListPrice
    public record ProductVariantCreateDto(
        string VariantCode,
        string Title,
        int? DurationDays,
        int StockQty,
        int? WarrantyDays,
        string? Thumbnail,
        string? MetaTitle,
        string? MetaDescription,
        decimal? SellPrice,     // bắt buộc, nullable để tự validate
        decimal? ListPrice,     // bắt buộc, nullable để tự validate
        string? Status
    );

    // Update: cũng KHÔNG cho chỉnh CogsPrice
    public record ProductVariantUpdateDto(
        string Title,
        string? VariantCode,
        int? DurationDays,
        int StockQty,
        int? WarrantyDays,
        string? Thumbnail,
        string? MetaTitle,
        string? MetaDescription,
        string? Status,
        decimal? SellPrice,
        decimal? ListPrice
    );

    public class VariantImageUploadRequest
    {
        public IFormFile File { get; set; } = default!;
    }

    public class VariantImageDeleteRequest
    {
        public string PublicId { get; set; } = default!;
    }

    public record VariantReorderDto(Guid[] VariantIdsInOrder);
}
