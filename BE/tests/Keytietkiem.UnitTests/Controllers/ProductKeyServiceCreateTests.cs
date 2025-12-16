using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Matrix test cho CreateProductKeyAsync, map với CPK01..CPK13 trong sheet.
    /// </summary>
    public class ProductKeyServiceCreateTests
    {
        // ---------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------

        private static (ProductKeyService service,
                        KeytietkiemDbContext context,
                        IGenericRepository<ProductKey> repo)
            CreateServiceWithContext(IGenericRepository<ProductKey>? repoOverride = null)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var ctx = new KeytietkiemDbContext(options);
            var repo = repoOverride ?? new GenericRepository<ProductKey>(ctx);
            var service = new ProductKeyService(ctx, repo);

            return (service, ctx, repo);
        }

        /// <summary>
        /// Seed Product + ProductVariant + Supplier vào cùng DbContext với service.
        /// Phải set đủ các trường bắt buộc của Product: ProductCode, ProductName,
        /// ProductType, Slug, Status.
        /// </summary>
        private static async Task<(ProductVariant Variant, Supplier Supplier)>
            SeedProductVariantAndSupplierAsync(
                KeytietkiemDbContext ctx,
                Guid? variantIdOverride = null,
                int supplierIdOverride = 1)
        {
            var productId = Guid.NewGuid();
            var variantId = variantIdOverride ?? Guid.NewGuid();

            // Product tối thiểu nhưng đủ mọi trường required
            var product = new Product
            {
                ProductId = productId,
                ProductCode = "TEST-P-" + productId.ToString("N")[..8],
                ProductName = "Test Product",
                ProductType = "SOFTWARE",
                Slug = "test-product-" + productId.ToString("N")[..8],
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };

            var variant = new ProductVariant
            {
                VariantId = variantId,
                ProductId = productId,
                Title = "Test Variant",
                Status = "ACTIVE",
                StockQty = 0,
                CogsPrice = 100_000m,
                SellPrice = 150_000m,
                ListPrice = 200_000m,
                CreatedAt = DateTime.UtcNow
            };

            var supplier = new Supplier
            {
                SupplierId = supplierIdOverride,
                Name = "Test Supplier",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                ContactEmail = "supplier@test.com",
                ContactPhone = "0123456789"
            };

            await ctx.Products.AddAsync(product);
            await ctx.ProductVariants.AddAsync(variant);
            await ctx.Suppliers.AddAsync(supplier);
            await ctx.SaveChangesAsync();

            return (variant, supplier);
        }

        private static CreateProductKeyDto BuildValidDto(
            Guid variantId,
            int supplierId,
            decimal? cogsPrice = null,
            DateTime? expiry = null,
            string keyString = "TEST-KEY-123",
            string type = "INDIVIDUAL",
            string? notes = "test note")
        {
            return new CreateProductKeyDto
            {
                VariantId = variantId,
                SupplierId = supplierId,
                KeyString = keyString,
                Type = type,
                CogsPrice = cogsPrice,
                ExpiryDate = expiry,
                Notes = notes
            };
        }

        // ---------------------------------------------------------
        // CPK01 – Normal: tạo key tối thiểu, không COGS, không expiry
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK01_CreateProductKey_Succeeds_WithMinimalRequiredData()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx);

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId,
                cogsPrice: null,
                expiry: null,
                notes: null
            );

            var actorId = Guid.NewGuid();

            var result = await service.CreateProductKeyAsync(dto, actorId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(dto.KeyString, result.KeyString);
            Assert.Equal(dto.VariantId, result.VariantId);
            Assert.Equal(dto.SupplierId, result.SupplierId);
            Assert.Null(result.ExpiryDate);
        }

        // ---------------------------------------------------------
        // CPK02 – Normal: có COGS + expiry tương lai
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK02_CreateProductKey_Succeeds_WithCogsPrice_AndFutureExpiry()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx, supplierIdOverride: 2);

            var newCogs = 200_000m;
            var futureExpiry = DateTime.UtcNow.AddDays(30);

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId,
                cogsPrice: newCogs,
                expiry: futureExpiry
            );

            var actorId = Guid.NewGuid();

            var result = await service.CreateProductKeyAsync(dto, actorId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(futureExpiry.Date, result.ExpiryDate?.Date);

            var updatedVariant = await ctx.ProductVariants.FindAsync(variant.VariantId);
            Assert.Equal(newCogs, updatedVariant!.CogsPrice);
        }

        // ---------------------------------------------------------
        // CPK03 – Abnormal: Variant không tồn tại
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK03_CreateProductKey_Throws_WhenVariantDoesNotExist()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (_, supplier) = await SeedProductVariantAndSupplierAsync(ctx);

            var missingVariantId = Guid.NewGuid();

            var dto = BuildValidDto(
                variantId: missingVariantId,
                supplierId: supplier.SupplierId
            );

            var actorId = Guid.NewGuid();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateProductKeyAsync(dto, actorId, CancellationToken.None));

            Assert.Contains("Biến thể sản phẩm không tồn tại", ex.Message);
        }

        // ---------------------------------------------------------
        // CPK04 – Abnormal: Supplier không tồn tại
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK04_CreateProductKey_Throws_WhenSupplierDoesNotExist()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx);

            ctx.Suppliers.RemoveRange(ctx.Suppliers);
            await ctx.SaveChangesAsync();

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId
            );

            var actorId = Guid.NewGuid();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateProductKeyAsync(dto, actorId, CancellationToken.None));

            Assert.Contains("Nhà cung cấp không tồn tại", ex.Message);
        }

        // ---------------------------------------------------------
        // CPK05 – Abnormal: License key trùng
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK05_CreateProductKey_Throws_WhenKeyAlreadyExists()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx);

            const string duplicatedKey = "DUPLICATE-KEY";

            await ctx.ProductKeys.AddAsync(new ProductKey
            {
                KeyId = Guid.NewGuid(),
                VariantId = variant.VariantId,
                SupplierId = supplier.SupplierId,
                KeyString = duplicatedKey,
                Status = nameof(ProductKeyStatus.Available),
                Type = "INDIVIDUAL",
                ImportedAt = DateTime.UtcNow,
                ImportedBy = Guid.NewGuid()
            });
            await ctx.SaveChangesAsync();

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId,
                keyString: duplicatedKey
            );

            var actorId = Guid.NewGuid();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateProductKeyAsync(dto, actorId, CancellationToken.None));

            Assert.Contains("License key đã tồn tại trong hệ thống", ex.Message);
        }

        // ---------------------------------------------------------
        // CPK06 – Abnormal: Expiry trong quá khứ
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK06_CreateProductKey_Throws_WhenExpiryDateInPast()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx);

            var pastDate = DateTime.UtcNow.AddDays(-1);

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId,
                expiry: pastDate
            );

            var actorId = Guid.NewGuid();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateProductKeyAsync(dto, actorId, CancellationToken.None));

            Assert.Contains("Ngày hết hạn không được trong quá khứ", ex.Message);
        }

        // ---------------------------------------------------------
        // CPK07 – Boundary: Expiry = hôm nay (hợp lệ)
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK07_CreateProductKey_Allows_ExpiryDateEqualsToday()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx, supplierIdOverride: 3);

            var today = DateTime.UtcNow.Date;

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId,
                expiry: today
            );

            var actorId = Guid.NewGuid();

            var result = await service.CreateProductKeyAsync(dto, actorId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(today, result.ExpiryDate?.Date);
        }

        // ---------------------------------------------------------
        // CPK08 – Boundary: CogsPrice = 0
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK08_CreateProductKey_Allows_CogsPriceZero_AndUpdatesVariant()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx, supplierIdOverride: 4);

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId,
                cogsPrice: 0m
            );

            var actorId = Guid.NewGuid();
            await service.CreateProductKeyAsync(dto, actorId, CancellationToken.None);

            var updatedVariant = await ctx.ProductVariants.FindAsync(variant.VariantId);
            Assert.Equal(0m, updatedVariant!.CogsPrice);
        }

        // ---------------------------------------------------------
        // CPK09 – Normal: không truyền COGS => giữ nguyên COGS cũ
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK09_CreateProductKey_DoesNotChangeCogsPrice_WhenCogsPriceIsNull()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx, supplierIdOverride: 5);

            var originalCogs = variant.CogsPrice;

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId,
                cogsPrice: null
            );

            var actorId = Guid.NewGuid();
            await service.CreateProductKeyAsync(dto, actorId, CancellationToken.None);

            var updatedVariant = await ctx.ProductVariants.FindAsync(variant.VariantId);
            Assert.Equal(originalCogs, updatedVariant!.CogsPrice);
        }

        // ---------------------------------------------------------
        // CPK10 – Confirm: StockQty tăng 1
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK10_CreateProductKey_Increments_VariantStock_ByOne()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx, supplierIdOverride: 6);

            var initialStock = variant.StockQty;

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId
            );

            var actorId = Guid.NewGuid();
            await service.CreateProductKeyAsync(dto, actorId, CancellationToken.None);

            var updatedVariant = await ctx.ProductVariants.FindAsync(variant.VariantId);
            Assert.Equal(initialStock + 1, updatedVariant!.StockQty);
        }

        // ---------------------------------------------------------
        // CPK11 – Confirm: tạo LicensePackage quantity = 1
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK11_CreateProductKey_Creates_LicensePackage_WithQuantityOne()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx, supplierIdOverride: 7);

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId
            );

            var actorId = Guid.NewGuid();
            await service.CreateProductKeyAsync(dto, actorId, CancellationToken.None);

            var pkg = await ctx.LicensePackages.SingleAsync();
            Assert.Equal(variant.VariantId, pkg.VariantId);
            Assert.Equal(supplier.SupplierId, pkg.SupplierId);
            Assert.Equal(1, pkg.Quantity);
            Assert.Equal(1, pkg.ImportedToStock);
        }

        // ---------------------------------------------------------
        // CPK12 – Confirm: status key mới = Available
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK12_CreateProductKey_Sets_NewKeyStatus_ToAvailable()
        {
            var (service, ctx, _) = CreateServiceWithContext();
            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx, supplierIdOverride: 8);

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId,
                type: "SHARED"
            );

            var actorId = Guid.NewGuid();
            await service.CreateProductKeyAsync(dto, actorId, CancellationToken.None);

            var key = await ctx.ProductKeys.SingleAsync();
            Assert.Equal(nameof(ProductKeyStatus.Available), key.Status);
            Assert.Equal("SHARED", key.Type);
        }

        // ---------------------------------------------------------
        // CPK13 – Abnormal: backend error (repo ném Exception bất ngờ)
        // ---------------------------------------------------------

        [Fact]
        public async Task CPK13_CreateProductKey_PropagatesUnexpectedException_AsBackendError()
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var ctx = new KeytietkiemDbContext(options);

            var repoMock = new Mock<IGenericRepository<ProductKey>>();

            repoMock
                .Setup(r => r.FirstOrDefaultAsync(
                    It.IsAny<System.Linq.Expressions.Expression<Func<ProductKey, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProductKey?)null);

            repoMock
                .Setup(r => r.AddAsync(It.IsAny<ProductKey>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated DB failure"));

            var service = new ProductKeyService(ctx, repoMock.Object);

            var (variant, supplier) = await SeedProductVariantAndSupplierAsync(ctx, supplierIdOverride: 9);

            var dto = BuildValidDto(
                variantId: variant.VariantId,
                supplierId: supplier.SupplierId
            );

            var actorId = Guid.NewGuid();

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                service.CreateProductKeyAsync(dto, actorId, CancellationToken.None));

            Assert.Contains("Simulated DB failure", ex.Message);
        }
    }
}
