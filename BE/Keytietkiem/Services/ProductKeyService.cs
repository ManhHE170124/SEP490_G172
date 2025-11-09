
using Keytietkiem.DTOs;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.Repositories;

namespace Keytietkiem.Services
{
    public class ProductKeyService : IProductKeyService
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IGenericRepository<ProductKey> _productKeyRepository;
        private readonly IGenericRepository<AuditLog> _auditLogRepository;

        public ProductKeyService(KeytietkiemDbContext context,
            IGenericRepository<ProductKey> productKeyRepository,
            IGenericRepository<AuditLog> auditLogRepository)
        {
            _context = context;
            _productKeyRepository = productKeyRepository;
            _auditLogRepository = auditLogRepository;
        }

        public async Task<ProductKeyListResponseDto> GetProductKeysAsync(
            ProductKeyFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var query = _productKeyRepository.Query()
                .Include(pk => pk.Product)
                .Include(pk => pk.Supplier)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchLower = filter.SearchTerm.ToLower();
                query = query.Where(pk =>
                    pk.Product.ProductName.ToLower().Contains(searchLower) ||
                    pk.Product.ProductCode.ToLower().Contains(searchLower) ||
                    pk.KeyString.ToLower().Contains(searchLower));
            }

            if (filter.ProductId.HasValue)
            {
                query = query.Where(pk => pk.ProductId == filter.ProductId.Value);
            }

            if (filter.SupplierId.HasValue)
            {
                query = query.Where(pk => pk.SupplierId == filter.SupplierId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(pk => pk.Status == filter.Status);
            }

            if (!string.IsNullOrWhiteSpace(filter.Type))
            {
                query = query.Where(pk => pk.Type == filter.Type);
            }

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
                    ProductName = pk.Product.ProductName,
                    ProductSku = pk.Product.ProductCode,
                    KeyString = pk.KeyString,
                    Status = pk.Status,
                    Type = pk.Type,
                    UpdatedAt = pk.UpdatedAt,

                    ImportedAt = pk.ImportedAt
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
                .Include(pk => pk.Product)
                .Include(pk => pk.Supplier)
                .FirstOrDefaultAsync(pk => pk.KeyId == keyId, cancellationToken);

            if (key == null)
            {
                throw new InvalidOperationException("Product key không tồn tại");
            }

            var importedByEmail = key.ImportedBy.HasValue
                ? await _context.Users
                    .Where(u => u.UserId == key.ImportedBy.Value)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            return new ProductKeyDetailDto
            {
                KeyId = key.KeyId,
                ProductId = key.ProductId,
                ProductName = key.Product.ProductName,
                ProductCode = key.Product.ProductCode,
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
            // Validate product exists
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == dto.ProductId, cancellationToken);

            if (product == null)
            {
                throw new InvalidOperationException("Sản phẩm không tồn tại");
            }

            // Validate supplier exists
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == dto.SupplierId, cancellationToken);

            if (supplier == null)
            {
                throw new InvalidOperationException("Nhà cung cấp không tồn tại");
            }

            // Check for duplicate key
            var existingKey = await _productKeyRepository.FirstOrDefaultAsync(
                pk => pk.KeyString == dto.KeyString,
                cancellationToken);

            if (existingKey != null)
            {
                throw new InvalidOperationException("License key đã tồn tại trong hệ thống");
            }

            var productKey = new ProductKey
            {
                KeyId = Guid.NewGuid(),
                ProductId = dto.ProductId,
                SupplierId = dto.SupplierId,
                KeyString = dto.KeyString,
                Type = dto.Type,
                Status = nameof(ProductKeyStatus.Available),
                ExpiryDate = dto.ExpiryDate,
                Notes = dto.Notes,
                ImportedAt = DateTime.UtcNow,
                ImportedBy = actorId
            };

            await _productKeyRepository.AddAsync(productKey, cancellationToken);

            // Update Product stock quantity
            product.StockQty += 1;
            product.UpdatedAt = DateTime.UtcNow;
            _context.Products.Update(product);

            // Create audit log
            var auditLog = new AuditLog
            {
                Action = "CREATE_PRODUCT_KEY",
                Resource = "ProductKey",
                EntityId = productKey.KeyId.ToString(),
                DetailJson = $"Tạo product key mới cho sản phẩm {product.ProductName}",
                ActorId = actorId,
                OccurredAt = DateTime.UtcNow
            };

            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
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
            {
                throw new InvalidOperationException("Product key không tồn tại");
            }

            var oldStatus = key.Status;
            key.ExpiryDate = dto.ExpiryDate;
            key.Status = dto.Status;
            if (key.ExpiryDate.HasValue && key.ExpiryDate < DateTime.UtcNow)
            {
                key.Status = nameof(ProductKeyStatus.Expired);
            }
            key.Notes = dto.Notes;
            key.UpdatedAt = DateTime.UtcNow;

            _productKeyRepository.Update(key);

            // Create audit log
            var auditLog = new AuditLog
            {
                Action = "UPDATE_PRODUCT_KEY",
                Resource = "ProductKey",
                EntityId = key.KeyId.ToString(),
                DetailJson = $"Cập nhật product key: Status từ {oldStatus} sang {dto.Status}",
                ActorId = actorId,
                OccurredAt = DateTime.UtcNow
            };

            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
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
            {
                throw new InvalidOperationException("Product key không tồn tại");
            }

            if (key.AssignedToOrderId.HasValue)
            {
                throw new InvalidOperationException("Không thể xóa key đã được gán cho đơn hàng");
            }

            _productKeyRepository.Remove(key);

            // Create audit log
            var auditLog = new AuditLog
            {
                Action = "DELETE_PRODUCT_KEY",
                Resource = "ProductKey",
                EntityId = keyId.ToString(),
                DetailJson = $"Xóa product key: {key.KeyString}",
                ActorId = actorId,
                OccurredAt = DateTime.UtcNow
            };

            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
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
            {
                throw new InvalidOperationException("Product key không tồn tại");
            }

            if (key.Status != nameof(ProductKeyStatus.Available))
            {
                throw new InvalidOperationException("Product key không ở trạng thái Available");
            }

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, cancellationToken);

            if (order == null)
            {
                throw new InvalidOperationException("Đơn hàng không tồn tại");
            }

            key.AssignedToOrderId = dto.OrderId;
            key.Status = nameof(ProductKeyStatus.Sold);
            key.UpdatedAt = DateTime.UtcNow;

            _productKeyRepository.Update(key);

            // Create audit log
            var auditLog = new AuditLog
            {
                Action = "ASSIGN_KEY_TO_ORDER",
                Resource = "ProductKey",
                EntityId = key.KeyId.ToString(),
                DetailJson = $"Gán product key cho đơn hàng {order.OrderId}",
                ActorId = actorId,
                OccurredAt = DateTime.UtcNow
            };

            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
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
            {
                throw new InvalidOperationException("Product key không tồn tại");
            }

            if (!key.AssignedToOrderId.HasValue)
            {
                throw new InvalidOperationException("Product key chưa được gán cho đơn hàng nào");
            }

            var orderId = key.AssignedToOrderId.Value;
            key.AssignedToOrderId = null;
            key.Status = nameof(ProductKeyStatus.Available);
            key.UpdatedAt = DateTime.UtcNow;

            _productKeyRepository.Update(key);

            // Create audit log
            var auditLog = new AuditLog
            {
                Action = "UNASSIGN_KEY_FROM_ORDER",
                Resource = "ProductKey",
                EntityId = key.KeyId.ToString(),
                DetailJson = $"Gỡ product key khỏi đơn hàng {orderId}",
                ActorId = actorId,
                OccurredAt = DateTime.UtcNow
            };

            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
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
            {
                throw new InvalidOperationException("Không tìm thấy product key nào");
            }

            var now = DateTime.UtcNow;
            foreach (var key in keys)
            {
                key.Status = dto.Status;
                key.UpdatedAt = now;
            }

            _productKeyRepository.UpdateRange(keys);

            // Create audit log
            var auditLog = new AuditLog
            {
                Action = "BULK_UPDATE_KEY_STATUS",
                Resource = "ProductKey",
                EntityId = string.Join(",", dto.KeyIds.Take(5)),
                DetailJson = $"Cập nhật trạng thái {keys.Count} product keys sang {dto.Status}",
                ActorId = actorId,
                OccurredAt = now
            };

            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return keys.Count;
        }

        public async Task<byte[]> ExportKeysToCSVAsync(
            ProductKeyFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var query = _productKeyRepository.Query()
                .Include(pk => pk.Product)
                .Include(pk => pk.Supplier)
                .AsQueryable();

            // Apply same filters as GetProductKeysAsync
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchLower = filter.SearchTerm.ToLower();
                query = query.Where(pk =>
                    pk.Product.ProductName.ToLower().Contains(searchLower) ||
                    pk.Product.ProductCode.ToLower().Contains(searchLower) ||
                    pk.KeyString.ToLower().Contains(searchLower));
            }

            if (filter.ProductId.HasValue)
            {
                query = query.Where(pk => pk.ProductId == filter.ProductId.Value);
            }

            if (filter.SupplierId.HasValue)
            {
                query = query.Where(pk => pk.SupplierId == filter.SupplierId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Type))
            {
                query = query.Where(pk => pk.Type == filter.Type);
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(pk => pk.Status == filter.Status);
            }

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
                var type = key.Type.ToString();

                csv.AppendLine($"\"{key.Product.ProductCode}\",\"{key.Product.ProductName}\",\"{key.KeyString}\",\"{type}\",\"{key.Status}\",\"{expiryDate}\",\"{importedAt}\",\"{notes}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }
        private static ProductKeyType ParseProductKeyType(string type)
        {
            if (Enum.TryParse<ProductKeyType>(type, ignoreCase: true, out var value))
                return value;

            throw new InvalidOperationException($"Loại key không hợp lệ: {type}");
        }

    }
}
