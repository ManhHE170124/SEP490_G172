using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs.Products
{
    public record StorefrontHomepageProductsDto(
        // Ưu đãi hôm nay: 4 sản phẩm có % giảm giá cao nhất
        IReadOnlyCollection<StorefrontVariantListItemDto> TodayBestDeals,

        // Sản phẩm bán chạy: sort theo "bán chạy" giống trang list products
        IReadOnlyCollection<StorefrontVariantListItemDto> BestSellers,

        // Xu hướng tuần này: sản phẩm có nhiều ViewCount nhất trong tuần
        IReadOnlyCollection<StorefrontVariantListItemDto> WeeklyTrends,

        // Mới cập nhật: sản phẩm sort theo UpdatedAt mới nhất
        IReadOnlyCollection<StorefrontVariantListItemDto> NewlyUpdated
    );
}
