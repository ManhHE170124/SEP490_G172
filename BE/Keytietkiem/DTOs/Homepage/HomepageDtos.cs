using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs.Homepage
{
    public record HomepageResponseDto(
        IReadOnlyList<FilterChipDto> TopSearches,
        IReadOnlyList<ServiceCardDto> Services
    );

    public record HeroBannerDto(
        string Title,
        string Description,
        string CtaText,
        string CtaLink,
        string Accent
    );

    public record FilterChipDto(
        string Label,
        string Href,
        string Tone = "neutral",
        string? Icon = null
    );

    public record ProductShelfDto(
        string Title,
        string Subtitle,
        IReadOnlyList<ProductCardDto> Items
    );

    public record ProductCardDto(
        Guid ProductId,
        Guid VariantId,
        string Name,
        string? Variant,
        string ProductType,
        string ProductTypeLabel,
        string? Category,
        string? ThumbnailUrl,
        decimal Price,
        decimal? OriginalPrice,
        double? DiscountPercent,
        double? Rating,
        int ReviewCount,
        int SoldCount,
        int? WarrantyDays,
        int? DurationDays,
        bool AutoDelivery,
        IReadOnlyList<CardBadgeDto> Badges,
        string Slug
    );

    public record CardBadgeDto(
        string Code,
        string Label,
        string? ColorHex
    );

    public record ServiceCardDto(
        string Title,
        string Description,
        string ActionText,
        string ActionUrl
    );
}
