// File: DTOs/Products/ProductVariantDtos.cs
using System;

namespace Keytietkiem.DTOs.Products
{
    public record ProductVariantListQuery(
       string? Q,            // tìm theo Title / VariantCode
       string? Status,      // ACTIVE | INACTIVE | OUT_OF_STOCK
       string? Dur,         // "<=30" | "31-180" | ">180"
       string? Sort = "created",  // created|title|duration|price|stock|status
       string? Dir = "desc",     // asc|desc
       int Page = 1,
       int PageSize = 10
   );
    public record ProductVariantListItemDto(
        Guid VariantId,
        string VariantCode,
        string Title,
        int? DurationDays,
        decimal? OriginalPrice,
        decimal Price,
        int StockQty,
        string Status,
        int SortOrder
    );

    public record ProductVariantDetailDto(
        Guid VariantId,
        Guid ProductId,
        string VariantCode,
        string Title,
        int? DurationDays,
        decimal? OriginalPrice,
        decimal Price,
        int StockQty,
        int? WarrantyDays,
        string Status,
        int SortOrder
    );

    public record ProductVariantCreateDto(
        string VariantCode,
        string Title,
        int? DurationDays,
        decimal? OriginalPrice,
        decimal Price,
        int StockQty,
        int? WarrantyDays,
        string? Status,
        int? SortOrder
    );

    public record ProductVariantUpdateDto(
        string Title,
        int? DurationDays,
        decimal? OriginalPrice,
        decimal Price,
        int StockQty,
        int? WarrantyDays,
        string? Status,
        int? SortOrder
    );

    public record VariantReorderDto(Guid[] VariantIdsInOrder);
}
