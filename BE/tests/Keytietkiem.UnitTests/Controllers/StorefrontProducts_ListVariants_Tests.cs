using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.ProductClient;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Keytietkiem.Tests.StorefrontProducts
{
    public class StorefrontProducts_ListVariants_Tests
    {
        #region Helper

        private static StorefrontProductsController CreateController(DbContextOptions<KeytietkiemDbContext> options)
        {
            var factoryMock = new Mock<IDbContextFactory<KeytietkiemDbContext>>();

            factoryMock
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new KeytietkiemDbContext(options));

            return new StorefrontProductsController(factoryMock.Object);
        }

        private static Badge CreateBadge(string code, string name)
            => new Badge
            {
                BadgeCode = code,
                DisplayName = name,
                ColorHex = "#FF0000",
                Icon = "icon"
            };

        private static Product CreateProduct(Guid id, string code, string name, string type, string status)
            => new Product
            {
                ProductId = id,
                ProductCode = code,
                // Slug là required trong model => luôn set giá trị hợp lệ
                Slug = $"slug-{code}".ToLowerInvariant(),
                ProductName = name,
                ProductType = type,
                Status = status,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

        private static ProductVariant CreateVariant(
            Guid id,
            Product product,
            string title,
            string status,
            int viewCount,
            decimal sellPrice,
            decimal listPrice,
            int stockQty,
            DateTime createdAt,
            DateTime? updatedAt = null)
            => new ProductVariant
            {
                VariantId = id,
                ProductId = product.ProductId,
                Product = product,
                Title = title,
                Status = status,
                ViewCount = viewCount,
                SellPrice = sellPrice,
                ListPrice = listPrice,
                StockQty = stockQty,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };

        /// <summary>
        /// Lấy tổng số bản ghi từ PagedResult bằng reflection.
        /// Hỗ trợ các tên property phổ biến: TotalCount / Total / TotalItems / TotalRecords.
        /// </summary>
        private static int GetTotalCount<T>(PagedResult<T> paged)
        {
            var type = paged.GetType();

            var prop =
                type.GetProperty("TotalCount") ??
                type.GetProperty("Total") ??
                type.GetProperty("TotalItems") ??
                type.GetProperty("TotalRecords");

            if (prop == null)
            {
                throw new InvalidOperationException(
                    "PagedResult<T> không có property tổng số bản ghi (TotalCount/Total/TotalItems/TotalRecords).");
            }

            var value = prop.GetValue(paged);
            return value is int i ? i : Convert.ToInt32(value);
        }

        #endregion

        // =========================================================
        // TC1 – Không filter, sort = default:
        //  - chỉ lấy product/variant ACTIVE & OUT_OF_STOCK
        //  - mapping status theo StockQty
        //  - badge có metadata và badge không có metadata
        //  - sort default dùng rank theo ViewCount trong từng product
        // =========================================================
        [Fact]
        public async Task ListVariants_DefaultSort_StatusAndBadgeMapping()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(ListVariants_DefaultSort_StatusAndBadgeMapping))
                .Options;

            using (var ctx = new KeytietkiemDbContext(options))
            {
                // Category
                var cat = new Category
                {
                    CategoryId = 1,
                    CategoryCode = "WIN",
                    CategoryName = "Windows",
                    IsActive = true
                };

                // Badges
                var hotBadgeMeta = CreateBadge("HOT", "Hot sale");
                ctx.Badges.Add(hotBadgeMeta);

                // Products
                var p1 = CreateProduct(Guid.NewGuid(), "P1", "Product One", "KEY", "ACTIVE");
                var p2 = CreateProduct(Guid.NewGuid(), "P2", "Product Two", "KEY", "ACTIVE");
                var pInactive = CreateProduct(Guid.NewGuid(), "P3", "Inactive Product", "KEY", "INACTIVE");

                p1.Categories = new List<Category> { cat };
                p2.Categories = new List<Category> { cat };

                p1.ProductBadges = new List<ProductBadge>
                {
                    new ProductBadge { ProductId = p1.ProductId, Badge = "HOT" }
                };
                p2.ProductBadges = new List<ProductBadge>
                {
                    new ProductBadge { ProductId = p2.ProductId, Badge = "UNKNOWN" }
                };

                // Variants (3 cái hợp lệ, 2 cái bị filter bởi status)
                var now = DateTime.UtcNow;

                var v1 = CreateVariant(Guid.NewGuid(), p1, "Alpha", "ACTIVE",
                    viewCount: 10, sellPrice: 100, listPrice: 150, stockQty: 5,
                    createdAt: now.AddDays(-10), updatedAt: now.AddDays(-1));

                var v2_1 = CreateVariant(Guid.NewGuid(), p2, "Beta", "ACTIVE",
                    viewCount: 5, sellPrice: 200, listPrice: 200, stockQty: 0,
                    createdAt: now.AddDays(-9));

                var v2_2 = CreateVariant(Guid.NewGuid(), p2, "Gamma", "OUT_OF_STOCK",
                    viewCount: 8, sellPrice: 300, listPrice: 300, stockQty: 10,
                    createdAt: now.AddDays(-8));

                // Bị loại do product INACTIVE
                var vInactiveProduct = CreateVariant(Guid.NewGuid(), pInactive, "X", "ACTIVE",
                    viewCount: 1, sellPrice: 50, listPrice: 50, stockQty: 1,
                    createdAt: now.AddDays(-5));

                // Bị loại do variant status không hợp lệ
                var vInactiveVariant = CreateVariant(Guid.NewGuid(), p1, "Y", "INACTIVE",
                    viewCount: 1, sellPrice: 50, listPrice: 50, stockQty: 1,
                    createdAt: now.AddDays(-5));

                ctx.Categories.Add(cat);
                ctx.Products.AddRange(p1, p2, pInactive);
                ctx.ProductVariants.AddRange(v1, v2_1, v2_2, vInactiveProduct, vInactiveVariant);
                ctx.ProductBadges.AddRange(p1.ProductBadges.Concat(p2.ProductBadges));
                ctx.SaveChanges();
            }

            var controller = CreateController(options);

            var query = new StorefrontVariantListQuery
            {
                Page = 1,
                PageSize = 8,
                Sort = null // default
            };

            var action = await controller.ListVariants(query);

            var ok = Assert.IsType<OkObjectResult>(action.Result);
            var result = Assert.IsType<PagedResult<StorefrontVariantListItemDto>>(ok.Value);

            // Chỉ 3 variant hợp lệ
            Assert.Equal(3, GetTotalCount(result));

            var items = result.Items.ToList();
            Assert.Equal(3, items.Count);

            // Sort default: v1 (view 10), v2_2 (view 8), v2_1 (view 5)
            Assert.Equal("Alpha", items[0].VariantTitle);
            Assert.Equal("Gamma", items[1].VariantTitle);
            Assert.Equal("Beta", items[2].VariantTitle);

            // Mapping status theo tồn kho và status ban đầu
            var alpha = items.Single(i => i.VariantTitle == "Alpha");
            var beta = items.Single(i => i.VariantTitle == "Beta");
            var gamma = items.Single(i => i.VariantTitle == "Gamma");

            Assert.Equal("ACTIVE", alpha.Status);          // ACTIVE + StockQty > 0
            Assert.Equal("OUT_OF_STOCK", beta.Status);     // ACTIVE + StockQty = 0 -> ép OUT_OF_STOCK
            Assert.Equal("OUT_OF_STOCK", gamma.Status);    // Status đã OUT_OF_STOCK

            // Badge mapping
            var alphaBadge = Assert.Single(alpha.Badges);
            Assert.Equal("HOT", alphaBadge.BadgeCode);
            Assert.Equal("Hot sale", alphaBadge.DisplayName);
            Assert.NotNull(alphaBadge.ColorHex);           // Có metadata

            var betaBadge = Assert.Single(beta.Badges);
            Assert.Equal("UNKNOWN", betaBadge.BadgeCode);
            Assert.Equal("UNKNOWN", betaBadge.DisplayName); // Fallback
            Assert.Null(betaBadge.ColorHex);
        }

        // =========================================================
        // TC2 – Kết hợp nhiều filter (Q, CategoryId, ProductType, Min/MaxPrice)
        //      để ra total = 0 → nhánh early-return.
        // =========================================================
        [Fact]
        public async Task ListVariants_AllFilters_NoResult_ReturnsEmptyPage()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(ListVariants_AllFilters_NoResult_ReturnsEmptyPage))
                .Options;

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var cat = new Category
                {
                    CategoryId = 1,
                    CategoryCode = "WIN",
                    CategoryName = "Windows",
                    IsActive = true
                };

                var p1 = CreateProduct(Guid.NewGuid(), "P1", "AAA Windows Key", "KEY", "ACTIVE");
                p1.Categories = new List<Category> { cat };

                var v1 = CreateVariant(Guid.NewGuid(), p1, "Alpha", "ACTIVE",
                    viewCount: 3, sellPrice: 100, listPrice: 100, stockQty: 5,
                    createdAt: DateTime.UtcNow.AddDays(-1));

                ctx.Categories.Add(cat);
                ctx.Products.Add(p1);
                ctx.ProductVariants.Add(v1);
                ctx.SaveChanges();
            }

            var controller = CreateController(options);

            var query = new StorefrontVariantListQuery
            {
                Q = "NoMatch",
                CategoryId = 999,       // không tồn tại
                ProductType = "ACCOUNT",
                MinPrice = 1000m,
                MaxPrice = 2000m,
                Sort = "updated",
                Page = 1,
                PageSize = 8
            };

            var action = await controller.ListVariants(query);

            var ok = Assert.IsType<OkObjectResult>(action.Result);
            var result = Assert.IsType<PagedResult<StorefrontVariantListItemDto>>(ok.Value);

            Assert.Equal(0, GetTotalCount(result));
            Assert.Empty(result.Items);
            Assert.Equal(1, result.Page);
            Assert.Equal(8, result.PageSize);
        }

        // =========================================================
        // TC3 – Filter CategoryId + ProductType + Min/MaxPrice
        //      Sort = updated, Page > 1 để test phân trang sau sort.
        // =========================================================
        [Fact]
        public async Task ListVariants_FilterAndSortUpdated_WithPaging()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(ListVariants_FilterAndSortUpdated_WithPaging))
                .Options;

            var catId = 1;

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var cat = new Category
                {
                    CategoryId = catId,
                    CategoryCode = "OFF",
                    CategoryName = "Office",
                    IsActive = true
                };

                var product = CreateProduct(Guid.NewGuid(), "OFF1", "Office 365", "KEY", "ACTIVE");
                product.Categories = new List<Category> { cat };

                var baseDate = new DateTime(2025, 1, 1);

                var v1 = CreateVariant(Guid.NewGuid(), product, "Plan A", "ACTIVE",
                    viewCount: 10, sellPrice: 100, listPrice: 100, stockQty: 5,
                    createdAt: baseDate.AddDays(1), updatedAt: baseDate.AddDays(1));

                var v2 = CreateVariant(Guid.NewGuid(), product, "Plan B", "ACTIVE",
                    viewCount: 5, sellPrice: 150, listPrice: 150, stockQty: 5,
                    createdAt: baseDate.AddDays(2), updatedAt: baseDate.AddDays(2));

                var v3 = CreateVariant(Guid.NewGuid(), product, "Plan C", "ACTIVE",
                    viewCount: 1, sellPrice: 200, listPrice: 200, stockQty: 5,
                    createdAt: baseDate.AddDays(3), updatedAt: baseDate.AddDays(3));

                // Một biến thể ngoài range giá để chắc chắn bị loại
                var vOutOfPrice = CreateVariant(Guid.NewGuid(), product, "Plan D", "ACTIVE",
                    viewCount: 100, sellPrice: 500, listPrice: 500, stockQty: 5,
                    createdAt: baseDate.AddDays(4), updatedAt: baseDate.AddDays(4));

                ctx.Categories.Add(cat);
                ctx.Products.Add(product);
                ctx.ProductVariants.AddRange(v1, v2, v3, vOutOfPrice);
                ctx.SaveChanges();
            }

            var controller = CreateController(options);

            var query = new StorefrontVariantListQuery
            {
                CategoryId = catId,
                ProductType = "KEY",
                MinPrice = 90m,
                MaxPrice = 250m,
                Sort = "updated",
                Page = 2,
                PageSize = 2
            };

            var action = await controller.ListVariants(query);

            var ok = Assert.IsType<OkObjectResult>(action.Result);
            var result = Assert.IsType<PagedResult<StorefrontVariantListItemDto>>(ok.Value);

            // Sau filter giá: còn 3 (A,B,C)
            Assert.Equal(3, GetTotalCount(result));

            var items = result.Items.ToList();

            // Page 2, pageSize 2 => chỉ còn 1 item (Plan A – updatedAt thấp nhất)
            Assert.Single(items);
            Assert.Equal("Plan A", items[0].VariantTitle);
            Assert.Equal(2, result.Page);
            Assert.Equal(2, result.PageSize);
        }

        // =========================================================
        // TC4 – Sort = price-asc:
        //  - Kiểm tra Order theo SellPrice asc, rồi ViewCount desc
        //  - PageSize > 8 → bị clamp về 8
        //  - MinPrice set, MaxPrice null
        // =========================================================
        [Fact]
        public async Task ListVariants_SortPriceAsc_AndClampPageSize()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(ListVariants_SortPriceAsc_AndClampPageSize))
                .Options;

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var product = CreateProduct(Guid.NewGuid(), "P1", "Price Test", "KEY", "ACTIVE");

                var v1 = CreateVariant(Guid.NewGuid(), product, "V1", "ACTIVE",
                    viewCount: 5, sellPrice: 100, listPrice: 100, stockQty: 5,
                    createdAt: DateTime.UtcNow.AddDays(-3));

                var v2 = CreateVariant(Guid.NewGuid(), product, "V2", "ACTIVE",
                    viewCount: 10, sellPrice: 100, listPrice: 100, stockQty: 5,
                    createdAt: DateTime.UtcNow.AddDays(-2));

                var v3 = CreateVariant(Guid.NewGuid(), product, "V3", "ACTIVE",
                    viewCount: 1, sellPrice: 200, listPrice: 200, stockQty: 5,
                    createdAt: DateTime.UtcNow.AddDays(-1));

                ctx.Products.Add(product);
                ctx.ProductVariants.AddRange(v1, v2, v3);
                ctx.SaveChanges();
            }

            var controller = CreateController(options);

            var query = new StorefrontVariantListQuery
            {
                Sort = "price-asc",
                Page = 1,
                PageSize = 100,   // > 8 → bị clamp
                MinPrice = 50m,
                MaxPrice = null
            };

            var action = await controller.ListVariants(query);

            var ok = Assert.IsType<OkObjectResult>(action.Result);
            var result = Assert.IsType<PagedResult<StorefrontVariantListItemDto>>(ok.Value);

            var items = result.Items.ToList();

            // Order: V2 (100, view 10), V1 (100, view 5), V3 (200, view 1)
            Assert.Equal(new[] { "V2", "V1", "V3" },
                items.Select(i => i.VariantTitle).ToArray());

            Assert.Equal(3, GetTotalCount(result));
            Assert.Equal(8, result.PageSize); // clamp
        }

        // =========================================================
        // TC5 – Sort = name-desc:
        //  - Kiểm tra order theo VariantTitle desc
        //  - MaxPrice set, MinPrice null
        // =========================================================
        [Fact]
        public async Task ListVariants_SortNameDesc_WithMaxPriceFilter()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(ListVariants_SortNameDesc_WithMaxPriceFilter))
                .Options;

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var product = CreateProduct(Guid.NewGuid(), "P2", "Name Sort", "KEY", "ACTIVE");

                var vA = CreateVariant(Guid.NewGuid(), product, "AAA", "ACTIVE",
                    viewCount: 1, sellPrice: 50, listPrice: 50, stockQty: 5,
                    createdAt: DateTime.UtcNow.AddDays(-3));
                var vB = CreateVariant(Guid.NewGuid(), product, "BBB", "ACTIVE",
                    viewCount: 1, sellPrice: 60, listPrice: 60, stockQty: 5,
                    createdAt: DateTime.UtcNow.AddDays(-2));
                var vC = CreateVariant(Guid.NewGuid(), product, "CCC", "ACTIVE",
                    viewCount: 1, sellPrice: 70, listPrice: 70, stockQty: 5,
                    createdAt: DateTime.UtcNow.AddDays(-1));

                ctx.Products.Add(product);
                ctx.ProductVariants.AddRange(vA, vB, vC);
                ctx.SaveChanges();
            }

            var controller = CreateController(options);

            var query = new StorefrontVariantListQuery
            {
                Sort = "name-desc",
                Page = 1,
                PageSize = 8,
                MaxPrice = 100m,
                MinPrice = null
            };

            var action = await controller.ListVariants(query);

            var ok = Assert.IsType<OkObjectResult>(action.Result);
            var result = Assert.IsType<PagedResult<StorefrontVariantListItemDto>>(ok.Value);

            var items = result.Items.ToList();

            Assert.Equal(new[] { "CCC", "BBB", "AAA" },
                items.Select(i => i.VariantTitle).ToArray());
        }

        // =========================================================
        // TC6 – SortKey invalid → dùng nhánh default (viewCount desc, createdAt desc)
        // =========================================================
        [Fact]
        public async Task ListVariants_InvalidSortKey_FallbackToViewCountThenCreatedAt()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(nameof(ListVariants_InvalidSortKey_FallbackToViewCountThenCreatedAt))
                .Options;

            using (var ctx = new KeytietkiemDbContext(options))
            {
                var product = CreateProduct(Guid.NewGuid(), "P3", "Fallback Sort", "KEY", "ACTIVE");

                var v1 = CreateVariant(Guid.NewGuid(), product, "V1", "ACTIVE",
                    viewCount: 10, sellPrice: 100, listPrice: 100, stockQty: 5,
                    createdAt: DateTime.UtcNow.AddDays(-2));
                var v2 = CreateVariant(Guid.NewGuid(), product, "V2", "ACTIVE",
                    viewCount: 5, sellPrice: 100, listPrice: 100, stockQty: 5,
                    createdAt: DateTime.UtcNow.AddDays(-1));
                var v3 = CreateVariant(Guid.NewGuid(), product, "V3", "ACTIVE",
                    viewCount: 10, sellPrice: 100, listPrice: 100, stockQty: 5,
                    createdAt: DateTime.UtcNow); // cùng viewCount, createdAt mới hơn

                ctx.Products.Add(product);
                ctx.ProductVariants.AddRange(v1, v2, v3);
                ctx.SaveChanges();
            }

            var controller = CreateController(options);

            var query = new StorefrontVariantListQuery
            {
                Sort = "something-strange",
                Page = 1,
                PageSize = 8
            };

            var action = await controller.ListVariants(query);

            var ok = Assert.IsType<OkObjectResult>(action.Result);
            var result = Assert.IsType<PagedResult<StorefrontVariantListItemDto>>(ok.Value);

            var items = result.Items.ToList();

            // Fallback sort: viewCount desc, rồi CreatedAt desc
            Assert.Equal(new[] { "V3", "V1", "V2" },
                items.Select(i => i.VariantTitle).ToArray());
        }
    }
}
