using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Unit test cho use case: Search Product Key List (SPK1.xx)
    /// Bao phủ các điều kiện trong decision table:
    /// - Existing data (keys exist / multiple pages)
    /// - Keyword presence
    /// - Filters: product, variant, supplier, status, type
    /// - Paging (next page, specific page)
    /// - Key actions: Available / Linked (AssignToOrder)
    /// </summary>
    public class ProductKeyServiceSearchTests
    {
        private const string STATUS_AVAILABLE = "Available";
        private const string STATUS_SOLD = "Sold";
        private const string STATUS_ERROR = "Error";

        private const string TYPE_INDIVIDUAL = "Individual";
        private const string TYPE_POOL = "Pool";

        private static (
            ProductKeyService service,
            KeytietkiemDbContext context,
            Product product1,
            Product product2,
            ProductVariant variant1,
            ProductVariant variant2,
            List<ProductKey> keys) CreateServiceWithSeedData(
                bool manyKeysForPaging = false)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new KeytietkiemDbContext(options);
            var repository = new GenericRepository<ProductKey>(context);
            var service = new ProductKeyService(context, repository);

            var now = DateTime.UtcNow;

            // *** SỬA Ở ĐÂY: set đủ ProductType, Slug, Status cho Product ***
            var product1 = new Product
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Windows 10 Pro",
                ProductCode = "WIN10-PRO",
                ProductType = "Software",
                Slug = "windows-10-pro",
                Status = "Active"
            };

            var product2 = new Product
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Office 365 Personal",
                ProductCode = "O365-PERS",
                ProductType = "Software",
                Slug = "office-365-personal",
                Status = "Active"
            };
            // *** Hết phần sửa Product ***

            var variant1 = new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = product1.ProductId,
                Product = product1,
                Title = "Win10 Pro Retail 1PC",
                Status = "Active",
                CreatedAt = now.AddDays(-30),
                UpdatedAt = now.AddDays(-20),
                StockQty = 100,
                SellPrice = 200,
                ListPrice = 250,
                CogsPrice = 120
            };

            var variant2 = new ProductVariant
            {
                VariantId = Guid.NewGuid(),
                ProductId = product2.ProductId,
                Product = product2,
                Title = "O365 1 năm",
                Status = "Active",
                CreatedAt = now.AddDays(-25),
                UpdatedAt = now.AddDays(-15),
                StockQty = 50,
                SellPrice = 300,
                ListPrice = 350,
                CogsPrice = 200
            };

            var keys = new List<ProductKey>
            {
                // K1: Windows, Available, Individual, chưa link đơn
                new ProductKey
                {
                    KeyId = Guid.NewGuid(),
                    KeyString = "WIN10-AAA-111",
                    Status = STATUS_AVAILABLE,
                    SupplierId = 1,
                    Type = TYPE_INDIVIDUAL,
                    VariantId = variant1.VariantId,
                    Variant = variant1,
                    ImportedAt = now.AddMinutes(-40),
                    UpdatedAt = now.AddMinutes(-35)
                },
                // K2: Windows, Sold, Individual, đã link đơn
                new ProductKey
                {
                    KeyId = Guid.NewGuid(),
                    KeyString = "WIN10-BBB-222",
                    Status = STATUS_SOLD,
                    SupplierId = 1,
                    Type = TYPE_INDIVIDUAL,
                    VariantId = variant1.VariantId,
                    Variant = variant1,
                    ImportedAt = now.AddMinutes(-30),
                    UpdatedAt = now.AddMinutes(-25),
                    AssignedToOrderId = Guid.NewGuid()
                },
                // K3: Office, Available, Pool
                new ProductKey
                {
                    KeyId = Guid.NewGuid(),
                    KeyString = "O365-AAA-333",
                    Status = STATUS_AVAILABLE,
                    SupplierId = 2,
                    Type = TYPE_POOL,
                    VariantId = variant2.VariantId,
                    Variant = variant2,
                    ImportedAt = now.AddMinutes(-20),
                    UpdatedAt = now.AddMinutes(-15)
                },
                // K4: Office, Error, Individual
                new ProductKey
                {
                    KeyId = Guid.NewGuid(),
                    KeyString = "O365-ERR-444",
                    Status = STATUS_ERROR,
                    SupplierId = 2,
                    Type = TYPE_INDIVIDUAL,
                    VariantId = variant2.VariantId,
                    Variant = variant2,
                    ImportedAt = now.AddMinutes(-10),
                    UpdatedAt = now.AddMinutes(-5)
                }
            };

            if (manyKeysForPaging)
            {
                // Thêm key cho variant1 để có nhiều trang
                for (var i = 0; i < 8; i++)
                {
                    keys.Add(new ProductKey
                    {
                        KeyId = Guid.NewGuid(),
                        KeyString = $"WIN10-EXTRA-{i:D2}",
                        Status = STATUS_AVAILABLE,
                        SupplierId = 1,
                        Type = TYPE_INDIVIDUAL,
                        VariantId = variant1.VariantId,
                        Variant = variant1,
                        ImportedAt = now.AddMinutes(-60 - i),
                        UpdatedAt = now.AddMinutes(-55 - i)
                    });
                }
            }

            context.AddRange(product1, product2, variant1, variant2);
            context.AddRange(keys);
            context.SaveChanges();

            return (service, context, product1, product2, variant1, variant2, keys);
        }

        // SPK1.01 – Load list cơ bản, không filter
        [Fact(DisplayName = "SPK1.01_LoadProductKeyList_NoFilter_ReturnsAllKeys")]
        public async Task SPK1_01_LoadProductKeyList_NoFilter_ReturnsAllKeys()
        {
            var (service, _, _, _, _, _, keys) = CreateServiceWithSeedData();

            var filter = new ProductKeyFilterDto
            {
                PageNumber = 1,
                PageSize = 20
            };

            var result = await service.GetProductKeysAsync(filter);

            Assert.Equal(keys.Count, result.TotalCount);
            Assert.Equal(keys.Count, result.Items.Count);
            Assert.Equal(1, result.PageNumber);
            Assert.Equal(20, result.PageSize);

            var imported = result.Items.Select(i => i.ImportedAt).ToList();
            var expected = imported.OrderByDescending(x => x).ToList();
            Assert.Equal(expected, imported);
        }

        // SPK1.02 – Search theo product name (keyword có kết quả)
        [Fact(DisplayName = "SPK1.02_SearchByProductName_ReturnsMatchingKeys")]
        public async Task SPK1_02_SearchByProductName_ReturnsMatchingKeys()
        {
            var (service, _, product1, _, _, _, _) = CreateServiceWithSeedData();

            var filter = new ProductKeyFilterDto
            {
                SearchTerm = "windows",
                PageNumber = 1,
                PageSize = 20
            };

            var result = await service.GetProductKeysAsync(filter);

            Assert.NotEmpty(result.Items);
            Assert.All(result.Items, item =>
                Assert.Equal(product1.ProductName, item.ProductName));
        }

        // SPK1.03 – Search không có kết quả
        [Fact(DisplayName = "SPK1.03_SearchWithNoMatch_ReturnsEmptyResult")]
        public async Task SPK1_03_SearchWithNoMatch_ReturnsEmptyResult()
        {
            var (service, _, _, _, _, _, _) = CreateServiceWithSeedData();

            var filter = new ProductKeyFilterDto
            {
                SearchTerm = "THIS-KEY-DOES-NOT-EXIST",
                PageNumber = 1,
                PageSize = 20
            };

            var result = await service.GetProductKeysAsync(filter);

            Assert.Equal(0, result.TotalCount);
            Assert.Empty(result.Items);
        }

        // SPK1.04 – Filter theo ProductId
        [Fact(DisplayName = "SPK1.04_FilterByProductId_ReturnsOnlyThatProduct")]
        public async Task SPK1_04_FilterByProductId_ReturnsOnlyThatProduct()
        {
            var (service, _, product1, product2, _, _, _) =
                CreateServiceWithSeedData();

            var filter = new ProductKeyFilterDto
            {
                ProductId = product1.ProductId,
                PageNumber = 1,
                PageSize = 20
            };

            var result = await service.GetProductKeysAsync(filter);

            Assert.NotEmpty(result.Items);
            Assert.All(result.Items, item =>
            {
                Assert.Equal(product1.ProductName, item.ProductName);
                Assert.NotEqual(product2.ProductName, item.ProductName);
            });
        }

        // SPK1.05 – Filter theo Status + Type
        [Fact(DisplayName = "SPK1.05_FilterByStatusAndType_ReturnsOnlyMatchedKeys")]
        public async Task SPK1_05_FilterByStatusAndType_ReturnsOnlyMatchedKeys()
        {
            var (service, _, _, _, _, _, _) = CreateServiceWithSeedData();

            var filter = new ProductKeyFilterDto
            {
                Status = STATUS_AVAILABLE,
                Type = TYPE_INDIVIDUAL,
                PageNumber = 1,
                PageSize = 20
            };

            var result = await service.GetProductKeysAsync(filter);

            Assert.NotEmpty(result.Items);
            Assert.All(result.Items, item =>
            {
                Assert.Equal(STATUS_AVAILABLE, item.Status);
                Assert.Equal(TYPE_INDIVIDUAL, item.Type);
            });
        }

        // SPK1.06 – Filter theo Variant + Supplier
        [Fact(DisplayName = "SPK1.06_FilterByVariantAndSupplier_ReturnsOnlyMatchedKeys")]
        public async Task SPK1_06_FilterByVariantAndSupplier_ReturnsOnlyMatchedKeys()
        {
            var (service, _, _, _, variant1, _, _) =
                CreateServiceWithSeedData();

            var filter = new ProductKeyFilterDto
            {
                VariantId = variant1.VariantId,
                SupplierId = 1,
                PageNumber = 1,
                PageSize = 20
            };

            var result = await service.GetProductKeysAsync(filter);

            Assert.NotEmpty(result.Items);
            Assert.All(result.Items, item =>
                Assert.Contains("Windows", item.ProductName, StringComparison.OrdinalIgnoreCase));
        }

        // SPK1.07 – Paging: first page, multiple pages exist
        [Fact(DisplayName = "SPK1.07_Paging_FirstPageOfMultiplePages")]
        public async Task SPK1_07_Paging_FirstPageOfMultiplePages()
        {
            var (service, _, _, _, _, _, keys) =
                CreateServiceWithSeedData(manyKeysForPaging: true);

            var filter = new ProductKeyFilterDto
            {
                PageNumber = 1,
                PageSize = 3
            };

            var result = await service.GetProductKeysAsync(filter);

            Assert.Equal(keys.Count, result.TotalCount);
            Assert.Equal(3, result.Items.Count);
        }

        // SPK1.08 – Paging: next page
        [Fact(DisplayName = "SPK1.08_Paging_SecondPage_ReturnsDifferentItems")]
        public async Task SPK1_08_Paging_SecondPage_ReturnsDifferentItems()
        {
            var (service, _, _, _, _, _, keys) =
                CreateServiceWithSeedData(manyKeysForPaging: true);

            var filterPage1 = new ProductKeyFilterDto
            {
                PageNumber = 1,
                PageSize = 3
            };
            var page1 = await service.GetProductKeysAsync(filterPage1);

            var filterPage2 = new ProductKeyFilterDto
            {
                PageNumber = 2,
                PageSize = 3
            };
            var page2 = await service.GetProductKeysAsync(filterPage2);

            Assert.Equal(keys.Count, page1.TotalCount);
            Assert.Equal(keys.Count, page2.TotalCount);

            Assert.Equal(3, page1.Items.Count);
            Assert.Equal(3, page2.Items.Count);

            var ids1 = page1.Items.Select(i => i.KeyId).ToHashSet();
            var ids2 = page2.Items.Select(i => i.KeyId).ToHashSet();
            Assert.Empty(ids1.Intersect(ids2));
        }

        // SPK1.09 – Paging: last page
        [Fact(DisplayName = "SPK1.09_Paging_LastPage_ReturnsRemainingItems")]
        public async Task SPK1_09_Paging_LastPage_ReturnsRemainingItems()
        {
            var (service, _, _, _, _, _, keys) =
                CreateServiceWithSeedData(manyKeysForPaging: true);

            var pageSize = 4;
            var totalPages = (int)Math.Ceiling(keys.Count / (double)pageSize);

            var filterLastPage = new ProductKeyFilterDto
            {
                PageNumber = totalPages,
                PageSize = pageSize
            };

            var lastPage = await service.GetProductKeysAsync(filterLastPage);

            var expectedLastPageCount = keys.Count - (totalPages - 1) * pageSize;
            Assert.Equal(expectedLastPageCount, lastPage.Items.Count);
        }

        // SPK1.10 – Key Available vs Linked
        [Fact(DisplayName = "SPK1.10_KeyStatusAndAssignToOrder_MappedCorrectly")]
        public async Task SPK1_10_KeyStatusAndAssignToOrder_MappedCorrectly()
        {
            var (service, _, _, _, _, _, _) = CreateServiceWithSeedData();

            var filter = new ProductKeyFilterDto
            {
                PageNumber = 1,
                PageSize = 20
            };

            var result = await service.GetProductKeysAsync(filter);

            Assert.Contains(result.Items, k => k.Status == STATUS_AVAILABLE && k.AssignToOrder == null);
            Assert.Contains(result.Items, k => k.Status == STATUS_SOLD && k.AssignToOrder != null);
        }

        // SPK1.11 – Ordering ImportedAt desc
        [Fact(DisplayName = "SPK1.11_Ordering_ByImportedAtDesc")]
        public async Task SPK1_11_Ordering_ByImportedAtDesc()
        {
            var (service, _, _, _, _, _, _) = CreateServiceWithSeedData();

            var filter = new ProductKeyFilterDto
            {
                PageNumber = 1,
                PageSize = 20
            };

            var result = await service.GetProductKeysAsync(filter);

            var importedList = result.Items.Select(i => i.ImportedAt).ToList();
            var sorted = importedList.OrderByDescending(t => t).ToList();

            Assert.Equal(sorted, importedList);
        }

        // SPK1.12 – Combined filters
        [Fact(DisplayName = "SPK1.12_CombinedFilters_Keyword_Product_Status_Type")]
        public async Task SPK1_12_CombinedFilters_Keyword_Product_Status_Type()
        {
            var (service, _, product1, _, _, _, _) = CreateServiceWithSeedData();

            var filter = new ProductKeyFilterDto
            {
                SearchTerm = "WIN10",
                ProductId = product1.ProductId,
                Status = STATUS_AVAILABLE,
                Type = TYPE_INDIVIDUAL,
                PageNumber = 1,
                PageSize = 20
            };

            var result = await service.GetProductKeysAsync(filter);

            Assert.NotEmpty(result.Items);
            Assert.All(result.Items, item =>
            {
                Assert.Contains("WIN10", item.ProductSku, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(product1.ProductName, item.ProductName);
                Assert.Equal(STATUS_AVAILABLE, item.Status);
                Assert.Equal(TYPE_INDIVIDUAL, item.Type);
            });
        }
    }
}
