using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs.Products
{
    public record StorefrontBadgeMiniDto(
        string BadgeCode,
        string DisplayName,
        string? ColorHex,
        string? Icon
    );

    public record StorefrontCategoryMiniDto(
        int CategoryId,
        string CategoryCode,
        string CategoryName
    );

    // LIST ITEM: thêm SellPrice, CogsPrice
    public record StorefrontVariantListItemDto(
        Guid VariantId,
        Guid ProductId,
        string ProductCode,
        string ProductName,
        string ProductType,
        string VariantTitle,
        string? Thumbnail,
        string Status,
        decimal SellPrice,
        decimal CogsPrice,
        IReadOnlyCollection<StorefrontBadgeMiniDto> Badges
    );

    public class StorefrontVariantListQuery
    {
        public string? Q { get; set; }
        public int? CategoryId { get; set; }
        public string? ProductType { get; set; }
        public decimal? MinPrice { get; set; }   // filter theo SellPrice
        public decimal? MaxPrice { get; set; }   // filter theo SellPrice
        public string? Sort { get; set; }        // default|updated|price-asc|price-desc|name-asc|name-desc|sold...
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 8;
    }

    public record StorefrontCategoryFilterItemDto(
        int CategoryId,
        string CategoryCode,
        string CategoryName
    );

    public record StorefrontFiltersDto(
        IReadOnlyCollection<StorefrontCategoryFilterItemDto> Categories,
        IReadOnlyCollection<string> ProductTypes
    );

    public record StorefrontSiblingVariantDto(
        Guid VariantId,
        string VariantTitle,
        string Status
    );

    public record StorefrontSectionDto(
        Guid SectionId,
        string SectionType,
        string Title,
        string Content
    );

    public record StorefrontFaqItemDto(
        int FaqId,
        string Question,
        string Answer,
        string Source
    );

    // DETAIL: thêm SellPrice, CogsPrice
    public record StorefrontVariantDetailDto(
        Guid VariantId,
        Guid ProductId,
        string ProductCode,
        string ProductName,
        string ProductType,
        string VariantTitle,
        string Status,
        int StockQty,
        string? Thumbnail,
        IReadOnlyCollection<StorefrontCategoryMiniDto> Categories,
        IReadOnlyCollection<StorefrontSiblingVariantDto> SiblingVariants,
        decimal SellPrice,
        decimal CogsPrice,
        IReadOnlyCollection<StorefrontSectionDto> Sections,
        IReadOnlyCollection<StorefrontFaqItemDto> Faqs
    );
}
