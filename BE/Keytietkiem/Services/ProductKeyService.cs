// File: Services/ProductKeyService.cs
using System.Globalization;
using System.Text;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Services
{
    public class ProductKeyService : IProductKeyService
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IGenericRepository<ProductKey> _productKeyRepository;

        public ProductKeyService(
            KeytietkiemDbContext context,
            IGenericRepository<ProductKey> productKeyRepository)
        {
            _context = context;
            _productKeyRepository = productKeyRepository;
        }

        public async Task<ProductKeyListResponseDto> GetProductKeysAsync(
            ProductKeyFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var query = _productKeyRepository.Query()
                .Include(pk => pk.Variant)
                    .ThenInclude(v => v.Product)
                .Include(pk => pk.Supplier)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchLower = filter.SearchTerm.ToLower();
                query = query.Where(pk =>
                    pk.Variant.Product.ProductName.ToLower().Contains(searchLower) ||
                    pk.Variant.Product.ProductCode.ToLower().Contains(searchLower) ||
                    pk.Variant.Title.ToLower().Contains(searchLower) ||
                    pk.KeyString.ToLower().Contains(searchLower));
            }

            if (filter.VariantId.HasValue)
                query = query.Where(pk => pk.VariantId == filter.VariantId.Value);

            if (filter.ProductId.HasValue)
                query = query.Where(pk => pk.Variant.ProductId == filter.ProductId.Value);

            if (filter.SupplierId.HasValue)
                query = query.Where(pk => pk.SupplierId == filter.SupplierId.Value);

            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(pk => pk.Status == filter.Status);

            if (!string.IsNullOrWhiteSpace(filter.Type))
                query = query.Where(pk => pk.Type == filter.Type);

            // Get total count
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination
            var items = await query
                .OrderByDescending(pk => pk.ImportedAt)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(pk => new ProductKeyListDto
                {
                    KeyId = pk.KeyId,
                    ProductName = pk.Variant.Product.ProductName,
                    ProductSku = pk.Variant.Product.ProductCode,
                    KeyString = pk.KeyString,
                    Status = pk.Status,
                    Type = pk.Type,
                    UpdatedAt = pk.UpdatedAt,
                    ImportedAt = pk.ImportedAt,
                    AssignToOrder = pk.AssignedToOrderId
                })
                .ToListAsync(cancellationToken);

            return new ProductKeyListResponseDto
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<ProductKeyDetailDto> GetProductKeyByIdAsync(
            Guid keyId,
            CancellationToken cancellationToken = default)
        {
            var key = await _productKeyRepository.Query()
                .Include(pk => pk.Variant)
                    .ThenInclude(v => v.Product)
                .Include(pk => pk.Supplier)
                .FirstOrDefaultAsync(pk => pk.KeyId == keyId, cancellationToken);

            if (key == null)
                throw new InvalidOperationException("Product key không tồn tại");

            var importedByEmail = key.ImportedBy.HasValue
                ? await _context.Users
                    .Where(u => u.UserId == key.ImportedBy.Value)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            return new ProductKeyDetailDto
            {
                KeyId = key.KeyId,
                ProductId = key.Variant.ProductId,
                VariantId = key.VariantId,
                VariantTitle = key.Variant.Title,
                ProductName = key.Variant.Product.ProductName,
                ProductCode = key.Variant.Product.ProductCode,
                CogsPrice = key.Variant.CogsPrice,
                SellPrice = key.Variant.SellPrice,
                SupplierId = key.SupplierId,
                SupplierName = key.Supplier.Name,
                KeyString = key.KeyString,
                Type = key.Type,
                Status = key.Status,
                ExpiryDate = key.ExpiryDate,
                Notes = key.Notes,
                AssignedToOrderId = key.AssignedToOrderId,
                ImportedAt = key.ImportedAt,
                ImportedBy = key.ImportedBy,
                ImportedByEmail = importedByEmail,
                UpdatedAt = key.UpdatedAt
            };
        }

        public async Task<ProductKeyDetailDto> CreateProductKeyAsync(
            CreateProductKeyDto dto,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            // Validate variant exists
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.VariantId == dto.VariantId, cancellationToken);

            if (variant == null)
                throw new InvalidOperationException("Biến thể sản phẩm không tồn tại");

            // Validate supplier exists
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == dto.SupplierId, cancellationToken);

            if (supplier == null)
                throw new InvalidOperationException("Nhà cung cấp không tồn tại");

            // Check for duplicate key
            var existingKey = await _productKeyRepository.FirstOrDefaultAsync(
                pk => pk.KeyString == dto.KeyString,
                cancellationToken);

            if (existingKey != null)
                throw new InvalidOperationException("License key đã tồn tại trong hệ thống");

            if (dto.ExpiryDate.HasValue && dto.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
                throw new InvalidOperationException("Ngày hết hạn không được trong quá khứ");

            // Update variant's CogsPrice if provided
            if (dto.CogsPrice.HasValue)
            {
                variant.CogsPrice = dto.CogsPrice.Value;
            }

            var productKey = new ProductKey
            {
                KeyId = Guid.NewGuid(),
                VariantId = dto.VariantId,
                SupplierId = dto.SupplierId,
                KeyString = dto.KeyString,
                Type = dto.Type,
                Status = nameof(ProductKeyStatus.Available),
                ExpiryDate = dto.ExpiryDate,
                Notes = dto.Notes,
                ImportedAt = DateTime.UtcNow,
                ImportedBy = actorId
            };

            var licensePackage = new LicensePackage
            {
                SupplierId = dto.SupplierId,
                VariantId = variant.VariantId,
                Quantity = 1,
                ImportedToStock = 1,
                CreatedAt = DateTime.UtcNow,
                Notes = "Auto-generated for manual key import"
            };

            _context.LicensePackages.Add(licensePackage);
            await _productKeyRepository.AddAsync(productKey, cancellationToken);

            // Update Variant stock quantity
            variant.StockQty += 1;
            variant.UpdatedAt = DateTime.UtcNow;
            _context.ProductVariants.Update(variant);

            await _context.SaveChangesAsync(cancellationToken);

            return await GetProductKeyByIdAsync(productKey.KeyId, cancellationToken);
        }

        public async Task<ProductKeyDetailDto> UpdateProductKeyAsync(
            UpdateProductKeyDto dto,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            var key = await _productKeyRepository.FirstOrDefaultAsync(
                pk => pk.KeyId == dto.KeyId,
                cancellationToken);

            if (key == null)
                throw new InvalidOperationException("Product key không tồn tại");

            var oldStatus = key.Status; // giữ lại để tránh thay đổi ngoài phạm vi audit

            key.ExpiryDate = dto.ExpiryDate;
            key.Status = dto.Status;
            if (key.ExpiryDate.HasValue && key.ExpiryDate < DateTime.UtcNow)
                key.Status = nameof(ProductKeyStatus.Expired);
            key.Notes = dto.Notes;
            key.UpdatedAt = DateTime.UtcNow;

            _productKeyRepository.Update(key);

            await _context.SaveChangesAsync(cancellationToken);

            return await GetProductKeyByIdAsync(key.KeyId, cancellationToken);
        }

        public async Task DeleteProductKeyAsync(
            Guid keyId,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            var key = await _productKeyRepository.FirstOrDefaultAsync(
                pk => pk.KeyId == keyId,
                cancellationToken);

            if (key == null)
                throw new InvalidOperationException("Product key không tồn tại");

            if (key.AssignedToOrderId.HasValue)
                throw new InvalidOperationException("Không thể xóa key đã được gán cho đơn hàng");

            _productKeyRepository.Remove(key);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task AssignKeyToOrderAsync(
            AssignKeyToOrderDto dto,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            var key = await _productKeyRepository.FirstOrDefaultAsync(
                pk => pk.KeyId == dto.KeyId,
                cancellationToken);

            if (key == null)
                throw new InvalidOperationException("Product key không tồn tại");

            if (key.Status != nameof(ProductKeyStatus.Available))
                throw new InvalidOperationException("Product key không ở trạng thái Available");

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, cancellationToken);

            if (order == null)
                throw new InvalidOperationException("Đơn hàng không tồn tại");

            key.AssignedToOrderId = dto.OrderId;
            key.Status = nameof(ProductKeyStatus.Sold);
            key.UpdatedAt = DateTime.UtcNow;

            _productKeyRepository.Update(key);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UnassignKeyFromOrderAsync(
            Guid keyId,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            var key = await _productKeyRepository.FirstOrDefaultAsync(
                pk => pk.KeyId == keyId,
                cancellationToken);

            if (key == null)
                throw new InvalidOperationException("Product key không tồn tại");

            if (!key.AssignedToOrderId.HasValue)
                throw new InvalidOperationException("Product key chưa được gán cho đơn hàng nào");

            var orderId = key.AssignedToOrderId.Value; // giữ để không thay đổi code khác
            key.AssignedToOrderId = null;
            key.Status = nameof(ProductKeyStatus.Available);
            key.UpdatedAt = DateTime.UtcNow;

            _productKeyRepository.Update(key);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> BulkUpdateKeyStatusAsync(
            BulkUpdateKeyStatusDto dto,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            var keys = await _productKeyRepository.Query()
                .Where(pk => dto.KeyIds.Contains(pk.KeyId))
                .ToListAsync(cancellationToken);

            if (keys.Count == 0)
                throw new InvalidOperationException("Không tìm thấy product key nào");

            var now = DateTime.UtcNow;
            foreach (var key in keys)
            {
                key.Status = dto.Status;
                key.UpdatedAt = now;
            }

            _productKeyRepository.UpdateRange(keys);

            await _context.SaveChangesAsync(cancellationToken);

            return keys.Count;
        }

        public async Task<byte[]> ExportKeysToCSVAsync(
            ProductKeyFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var query = _productKeyRepository.Query()
                .Include(pk => pk.Variant)
                    .ThenInclude(v => v.Product)
                .Include(pk => pk.Supplier)
                .AsQueryable();

            // Apply same filters as GetProductKeysAsync
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchLower = filter.SearchTerm.ToLower();
                query = query.Where(pk =>
                    pk.Variant.Product.ProductName.ToLower().Contains(searchLower) ||
                    pk.Variant.Product.ProductCode.ToLower().Contains(searchLower) ||
                    pk.Variant.Title.ToLower().Contains(searchLower) ||
                    pk.KeyString.ToLower().Contains(searchLower));
            }

            if (filter.VariantId.HasValue)
                query = query.Where(pk => pk.VariantId == filter.VariantId.Value);

            if (filter.ProductId.HasValue)
                query = query.Where(pk => pk.Variant.ProductId == filter.ProductId.Value);

            if (filter.SupplierId.HasValue)
                query = query.Where(pk => pk.SupplierId == filter.SupplierId.Value);

            if (!string.IsNullOrWhiteSpace(filter.Type))
                query = query.Where(pk => pk.Type == filter.Type);

            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(pk => pk.Status == filter.Status);

            var keys = await query
                .OrderByDescending(pk => pk.UpdatedAt ?? pk.ImportedAt)
                .ToListAsync(cancellationToken);

            // Build CSV
            var csv = new StringBuilder();
            csv.AppendLine("Product Code,Product Name,Key Value,Type,Status,Expiry Date,Imported At,Notes");

            foreach (var key in keys)
            {
                var expiryDate = key.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
                var importedAt = key.ImportedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                var notes = key.Notes?.Replace("\"", "\"\"") ?? "";
                var type = key.Type;

                csv.AppendLine(
                    $"\"{key.Variant.Product.ProductCode}\",\"{key.Variant.Product.ProductName}\",\"{key.KeyString}\",\"{type}\",\"{key.Status}\",\"{expiryDate}\",\"{importedAt}\",\"{notes}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<ProductKeyListResponseDto> GetExpiredKeysAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var query = _productKeyRepository.Query()
                .Include(pk => pk.Variant)
                    .ThenInclude(v => v.Product)
                .Include(pk => pk.Supplier)
                .Where(pk => pk.Status == nameof(ProductKeyStatus.Expired))
                .AsQueryable();

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(pk => pk.ExpiryDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(pk => new ProductKeyListDto
                {
                    KeyId = pk.KeyId,
                    ProductName = pk.Variant.Product.ProductName,
                    ProductSku = pk.Variant.Product.ProductCode,
                    KeyString = pk.KeyString,
                    Status = pk.Status,
                    Type = pk.Type,
                    UpdatedAt = pk.UpdatedAt,
                    ImportedAt = pk.ImportedAt,
                    AssignToOrder = pk.AssignedToOrderId
                })
                .ToListAsync(cancellationToken);

            return new ProductKeyListResponseDto
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<ProductKeyListResponseDto> GetKeysExpiringSoonAsync(
            int days,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var futureDate = now.AddDays(days);

            var query = _productKeyRepository.Query()
                .Include(pk => pk.Variant)
                    .ThenInclude(v => v.Product)
                .Include(pk => pk.Supplier)
                .Where(pk => pk.Status == nameof(ProductKeyStatus.Available) &&
                             pk.ExpiryDate.HasValue &&
                             pk.ExpiryDate.Value >= now &&
                             pk.ExpiryDate.Value <= futureDate)
                .AsQueryable();

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderBy(pk => pk.ExpiryDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(pk => new ProductKeyListDto
                {
                    KeyId = pk.KeyId,
                    ProductName = pk.Variant.Product.ProductName,
                    ProductSku = pk.Variant.Product.ProductCode,
                    KeyString = pk.KeyString,
                    Status = pk.Status,
                    Type = pk.Type,
                    UpdatedAt = pk.UpdatedAt,
                    ImportedAt = pk.ImportedAt,
                    AssignToOrder = pk.AssignedToOrderId,
                    ExpiryDate = pk.ExpiryDate
                })
                .ToListAsync(cancellationToken);

            return new ProductKeyListResponseDto
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        private static ProductKeyType ParseProductKeyType(string type)
        {
            if (Enum.TryParse<ProductKeyType>(type, true, out var value))
                return value;

            throw new InvalidOperationException($"Loại key không hợp lệ: {type}");
        }
    }
}
