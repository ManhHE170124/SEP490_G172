// File: DTOs/Products/ProductVariantDtos.cs
using System;

namespace Keytietkiem.DTOs.Products
{
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
