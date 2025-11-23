// DTOs/Products/ProductVariantDtos.cs
using System;

namespace Keytietkiem.DTOs.Products
{
    using Microsoft.AspNetCore.Http;

    public record ProductVariantListQuery(
        string? Q,
        string? Status,          // ACTIVE | INACTIVE | OUT_OF_STOCK
        string? Dur,             // "<=30" | "31-180" | ">180"
        string? Sort = "created",// created|title|duration|stock|status|views
        string? Dir = "desc",
        int Page = 1,
        int PageSize = 10
    );

    // List: hiển thị nhanh + thumbnail + viewcount
    public record ProductVariantListItemDto(
        Guid VariantId,
        string VariantCode,
        string Title,
        int? DurationDays,
        int StockQty,
        string Status,
        string? Thumbnail,
        int ViewCount
    );

    // Detail: đầy đủ SEO field
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
        decimal SellPrice,
        decimal CogsPrice
    );

    // Create/Update: giống Post (không có ViewCount vì server tự set)
    public record ProductVariantCreateDto(
        string VariantCode,
        string Title,
        int? DurationDays,
        int StockQty,
        int? WarrantyDays,
        string? Thumbnail,
        string? MetaTitle,
        string? MetaDescription,
        string? Status
    );

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
        decimal? SellPrice
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
