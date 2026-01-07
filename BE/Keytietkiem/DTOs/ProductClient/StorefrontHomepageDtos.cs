using System.Collections.Generic;

namespace Keytietkiem.DTOs.ProductClient
{
    public sealed record StorefrontHomepageProductsDto(
        IReadOnlyList<StorefrontVariantListItemDto> TodayBestDeals,
        IReadOnlyList<StorefrontVariantListItemDto> BestSellers,
        IReadOnlyList<StorefrontVariantListItemDto> WeeklyTrends,
        IReadOnlyList<StorefrontVariantListItemDto> NewlyUpdated,
        IReadOnlyList<StorefrontVariantListItemDto> LowStock
    );
}
