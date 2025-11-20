//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using Keytietkiem.DTOs.Homepage;
//using Keytietkiem.Infrastructure;
//using Keytietkiem.Models;
//using Keytietkiem.Services.Interfaces;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;

//namespace Keytietkiem.Services
//{
//    public class HomepageService : IHomepageService
//    {
//        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
//        private readonly ILogger<HomepageService> _logger;

//        public HomepageService(
//            IDbContextFactory<KeytietkiemDbContext> dbFactory,
//            ILogger<HomepageService> logger)
//        {
//            _dbFactory = dbFactory;
//            _logger = logger;
//        }

//        public async Task<HomepageResponseDto> GetAsync(CancellationToken cancellationToken = default)
//        {
//            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

//            var settings = await db.WebsiteSettings
//                .AsNoTracking()
//                .FirstOrDefaultAsync(cancellationToken);
//            var topSearches = await BuildTopSearchesAsync(db, cancellationToken);
//            var priceFilters = BuildPriceFilters();
//            var services = BuildServices();

//            var baseQuery = BuildProductQuery(db);
//            var dealsShelf = await BuildShelfAsync(
//                query: baseQuery,
//                order: q => q.OrderByDescending(p =>
//                        p.Price.HasValue && p.OriginalPrice.HasValue && p.OriginalPrice > 0
//                            ? (double)((p.OriginalPrice.Value - p.Price.Value) / p.OriginalPrice.Value)
//                            : 0d)
//                    .ThenBy(p => p.Price ?? decimal.MaxValue),
//                title: "Ưu đãi hôm nay",
//                subtitle: "Giảm sâu trong thời gian có hạn.",
//                limit: 4,
//                cancellationToken);

//            var bestSellersShelf = await BuildShelfAsync(
//                query: baseQuery,
//                order: q => q.OrderByDescending(p => p.SoldCount)
//                    .ThenByDescending(p => p.ReviewCount),
//                title: "Sản phẩm bán chạy",
//                subtitle: "Được mua nhiều nhất tuần qua.",
//                limit: 8,
//                cancellationToken);

//            var newArrivalsShelf = await BuildShelfAsync(
//                query: baseQuery,
//                order: q => q.OrderByDescending(p => p.CreatedAt),
//                title: "Mới cập nhật",
//                subtitle: "Sản phẩm/phiên bản mới vừa lên kệ.",
//                limit: 4,
//                cancellationToken);

//            var heroBanners = BuildHeroBanners(settings, dealsShelf);

//            return new HomepageResponseDto(
//                heroBanners,
//                topSearches,
//                priceFilters,
//                dealsShelf,
//                bestSellersShelf,
//                newArrivalsShelf,
//                services
//            );
//        }

//        private static async Task<IReadOnlyList<FilterChipDto>> BuildTopSearchesAsync(
//            KeytietkiemDbContext db,
//            CancellationToken cancellationToken)
//        {
//            var categories = await db.Categories
//                .AsNoTracking()
//                .Where(c => c.IsActive)
//                .OrderBy(c => c.CategoryName)
//                .Select(c => new
//                {
//                    c.CategoryId,
//                    c.CategoryName,
//                    c.CategoryCode
//                })
//                .Take(6)
//                .ToListAsync(cancellationToken);

//            var chips = categories
//                .Select(c => new FilterChipDto(
//                    c.CategoryName,
//                    $"/product-list?category={c.CategoryCode}",
//                    "solid",
//                    null))
//                .ToList();

//            string[] fallbacks =
//            {
//                "Làm việc",
//                "Giải trí",
//                "Học tập",
//                "AI",
//                "Windows",
//                "Steam"
//            };

//            var index = 0;
//            while (chips.Count < 6 && index < fallbacks.Length)
//            {
//                chips.Add(new FilterChipDto(
//                    fallbacks[index],
//                    "/product-list",
//                    "solid",
//                    null));
//                index++;
//            }

//            return chips;
//        }

//        private static IReadOnlyList<FilterChipDto> BuildPriceFilters()
//        {
//            decimal[] budgets = { 20000, 50000, 100000, 200000, 500000, 1000000 };
//            return budgets
//                .Select(budget => new FilterChipDto(
//                    $"{budget:N0}đ",
//                    $"/product-list?budget={Convert.ToInt32(budget)}",
//                    "outline",
//                    null))
//                .ToList();
//        }

//        private static IReadOnlyList<ServiceCardDto> BuildServices()
//        {
//            return new List<ServiceCardDto>
//            {
//                new("Cài đặt từ xa",
//                    "Cài Windows/Office chuẩn qua TeamViewer/AnyDesk. Bảo hành thao tác 7 ngày.",
//                    "Đặt lịch",
//                    "/support-service/remote"),
//                new("Hướng dẫn sử dụng",
//                    "Cấu hình, backup, mẹo tối ưu quy trình làm việc theo nhu cầu.",
//                    "Trao đổi ngay",
//                    "/support-service/manual"),
//                new("Fix lỗi phần mềm đã mua",
//                    "Xử lý lỗi kích hoạt, xung đột bản cũ, không nhận bản quyền, cập nhật driver.",
//                    "Yêu cầu hỗ trợ",
//                    "/support-service/fix"),
//            };
//        }

//        private static List<HeroBannerDto> BuildHeroBanners(
//            WebsiteSetting? settings,
//            ProductShelfDto dealsShelf)
//        {
//            var banners = new List<HeroBannerDto>
//            {
//                new(
//                    settings?.Slogan?.Trim().Length > 0
//                        ? settings.Slogan!
//                        : "Key chính hãng - Kích hoạt trong 1 phút",
//                    "Windows, Office, Adobe, tài khoản AI. Bảo hành rõ ràng & hỗ trợ từ xa.",
//                    "Khám phá ngay",
//                    "/product-list",
//                    "primary")
//            };

//            var firstDeal = dealsShelf.Items.FirstOrDefault();
//            banners.Add(new HeroBannerDto(
//                firstDeal != null ? $"Flash Sale: {firstDeal.Name}" : "Flash Sale hôm nay",
//                firstDeal != null && firstDeal.DiscountPercent.HasValue
//                    ? $"Giảm {firstDeal.DiscountPercent.Value:F0}% cho gói {firstDeal.Variant ?? firstDeal.Name}."
//                    : "Giảm sâu các gói phổ biến. Số lượng có hạn.",
//                "Xem ưu đãi",
//                firstDeal != null ? $"/product-list?highlight={firstDeal.Slug}" : "/product-list",
//                "ghost"));

//            return banners;
//        }

//        private static IQueryable<HomeProductProjection> BuildProductQuery(KeytietkiemDbContext db)
//        {
//            return db.Products
//                .AsNoTracking()
//                .Where(p => p.Status == "ACTIVE" && p.ProductVariants.Any(v => v.Status == "ACTIVE"))
//                .Select(p => new HomeProductProjection
//                {
//                    ProductId = p.ProductId,
//                    ProductName = p.ProductName,
//                    ProductType = p.ProductType,
//                    CreatedAt = p.CreatedAt,
//                    Slug = p.Slug,
//                    VariantId = p.ProductVariants
//                        .Where(v => v.Status == "ACTIVE")
//                        .OrderBy(v => v.CreatedAt)
//                        .Select(v => (Guid?)v.VariantId)
//                        .FirstOrDefault(),
//                    VariantTitle = p.ProductVariants
//                        .Where(v => v.Status == "ACTIVE")
//                        .OrderBy(v => v.CreatedAt)
//                        .Select(v => v.Title)
//                        .FirstOrDefault(),
//                    Price = p.ProductVariants
//                        .Where(v => v.Status == "ACTIVE")
//                        .OrderBy(v => v.CreatedAt)
//                        .Select(v => (decimal?)v.Price)
//                        .FirstOrDefault(),
//                    OriginalPrice = p.ProductVariants
//                        .Where(v => v.Status == "ACTIVE")
//                        .OrderBy(v => v.SortOrder)
//                        .ThenBy(v => v.Price)
//                        .Select(v => v.OriginalPrice)
//                        .FirstOrDefault(),
//                    WarrantyDays = p.ProductVariants
//                        .Where(v => v.Status == "ACTIVE")
//                        .OrderBy(v => v.SortOrder)
//                        .ThenBy(v => v.Price)
//                        .Select(v => v.WarrantyDays)
//                        .FirstOrDefault(),
//                    DurationDays = p.ProductVariants
//                        .Where(v => v.Status == "ACTIVE")
//                        .OrderBy(v => v.SortOrder)
//                        .ThenBy(v => v.Price)
//                        .Select(v => v.DurationDays)
//                        .FirstOrDefault(),
//                    AvgRating = p.ProductVariants
//                        .SelectMany(v => v.ProductReviews)
//                        .Select(r => (double?)r.Rating)
//                        .Average(),
//                    ReviewCount = p.ProductVariants
//                        .SelectMany(v => v.ProductReviews)
//                        .Count(),
//                    SoldCount = p.OrderDetails.Sum(o => (int?)o.Quantity) ?? 0,
//                    Badges = p.ProductBadges
//                        .OrderBy(pb => pb.CreatedAt)
//                        .Select(pb => new HomeBadgeProjection
//                        {
//                            Code = pb.BadgeNavigation.BadgeCode,
//                            Label = pb.BadgeNavigation.DisplayName,
//                            ColorHex = pb.BadgeNavigation.ColorHex
//                        })
//                        .ToList(),
//                    PrimaryCategoryName = p.Categories
//                        .OrderBy(c => c.DisplayOrder)
//                        .Select(c => c.CategoryName)
//                        .FirstOrDefault()
//                });
//        }

//        private static async Task<ProductShelfDto> BuildShelfAsync(
//            IQueryable<HomeProductProjection> query,
//            Func<IQueryable<HomeProductProjection>, IQueryable<HomeProductProjection>> order,
//            string title,
//            string subtitle,
//            int limit,
//            CancellationToken cancellationToken)
//        {
//            var ordered = order(query);
//            var rows = await ordered.Take(limit).ToListAsync(cancellationToken);
//            var items = rows
//                .Where(p => p.VariantId.HasValue && p.Price.HasValue)
//                .Select(MapToCard)
//                .ToList();

//            return new ProductShelfDto(title, subtitle, items);
//        }

//        private static ProductCardDto MapToCard(HomeProductProjection product)
//        {
//            var price = product.Price ?? 0m;
//            var originalPrice = product.OriginalPrice ?? price;
//            double? discount = null;
//            if (originalPrice > 0 && originalPrice > price)
//            {
//                var percent = (double)((originalPrice - price) / originalPrice * 100m);
//                discount = Math.Round(percent, 1);
//            }

//            var rating = product.AvgRating.HasValue
//                ? Math.Round(product.AvgRating.Value, 1)
//                : (double?)null;

//            var badges = product.Badges?
//                .Select(b => new CardBadgeDto(b.Code, b.Label, b.ColorHex))
//                .ToList() ?? new List<CardBadgeDto>();

//            return new ProductCardDto(
//                product.ProductId,
//                product.VariantId ?? Guid.Empty,
//                product.ProductName,
//                product.VariantTitle,
//                product.ProductType,
//                ResolveProductTypeLabel(product.ProductType),
//                product.PrimaryCategoryName,
//                product.ThumbnailUrl,
//                price,
//                product.OriginalPrice,
//                discount,
//                rating,
//                product.ReviewCount,
//                product.SoldCount,
//                product.WarrantyDays,
//                product.DurationDays,
//                product.AutoDelivery,
//                badges,
//                product.Slug
//            );
//        }

//        private static string ResolveProductTypeLabel(string typeCode)
//        {
//            return typeCode?.ToUpperInvariant() switch
//            {
//                "SHARED_KEY" => "Key dùng chung",
//                "PERSONAL_KEY" => "Key cá nhân",
//                "SHARED_ACCOUNT" => "Tài khoản dùng chung",
//                "PERSONAL_ACCOUNT" => "Tài khoản cá nhân",
//                _ => typeCode ?? "Sản phẩm"
//            };
//        }

//        private class HomeProductProjection
//        {
//            public Guid ProductId { get; set; }
//            public string ProductName { get; set; } = string.Empty;
//            public string ProductType { get; set; } = string.Empty;
//            public bool AutoDelivery { get; set; }
//            public string? ThumbnailUrl { get; set; }
//            public DateTime CreatedAt { get; set; }
//            public string Slug { get; set; } = string.Empty;
//            public Guid? VariantId { get; set; }
//            public string? VariantTitle { get; set; }
//            public decimal? Price { get; set; }
//            public decimal? OriginalPrice { get; set; }
//            public int? WarrantyDays { get; set; }
//            public int? DurationDays { get; set; }
//            public double? AvgRating { get; set; }
//            public int ReviewCount { get; set; }
//            public int SoldCount { get; set; }
//            public List<HomeBadgeProjection> Badges { get; set; } = new();
//            public string? PrimaryCategoryName { get; set; }
//        }

//        private class HomeBadgeProjection
//        {
//            public string Code { get; set; } = string.Empty;
//            public string Label { get; set; } = string.Empty;
//            public string? ColorHex { get; set; }
//        }
//    }
//}
