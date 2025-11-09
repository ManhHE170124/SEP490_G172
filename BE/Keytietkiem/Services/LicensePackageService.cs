using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Services;

public class LicensePackageService : ILicensePackageService
{
    private readonly KeytietkiemDbContext _context;
    private readonly IGenericRepository<LicensePackage> _packageRepository;
    private readonly IGenericRepository<AuditLog> _auditRepository;
    private readonly IClock _clock;
    private readonly ILogger<LicensePackageService> _logger;

    public LicensePackageService(
        KeytietkiemDbContext context,
        IGenericRepository<LicensePackage> packageRepository,
        IGenericRepository<AuditLog> auditRepository,
        IClock clock,
        ILogger<LicensePackageService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _packageRepository = packageRepository ?? throw new ArgumentNullException(nameof(packageRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LicensePackageResponseDto> CreateLicensePackageAsync(
        CreateLicensePackageDto createDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        // Validate supplier exists
        var supplierExists = await _context.Suppliers
            .AnyAsync(s => s.SupplierId == createDto.SupplierId, cancellationToken);
        if (!supplierExists)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại");

        // Validate product exists
        var productExists = await _context.Products
            .AnyAsync(p => p.ProductId == createDto.ProductId, cancellationToken);
        if (!productExists)
            throw new InvalidOperationException("Sản phẩm không tồn tại");

        var package = new LicensePackage
        {
            SupplierId = createDto.SupplierId,
            ProductId = createDto.ProductId,
            Quantity = createDto.Quantity,
            PricePerUnit = createDto.PricePerUnit,
            ImportedToStock = 0,
            EffectiveDate = createDto.EffectiveDate,
            CreatedAt = _clock.UtcNow,
            Notes = createDto.Notes
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _packageRepository.AddAsync(package, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Create audit log
            await CreateAuditLogAsync(
                actorId,
                actorEmail,
                "CREATE",
                "LicensePackage",
                package.PackageId.ToString(),
                new
                {
                    package.SupplierId,
                    package.ProductId,
                    package.Quantity,
                    package.PricePerUnit,
                    package.EffectiveDate,
                    package.Notes
                },
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "License package {PackageId} created by {ActorEmail}",
                package.PackageId,
                actorEmail);

            return await GetLicensePackageByIdAsync(package.PackageId, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<LicensePackageResponseDto> UpdateLicensePackageAsync(
        UpdateLicensePackageDto updateDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        var package = await _packageRepository.GetByIdAsync(updateDto.PackageId, cancellationToken);
        if (package == null)
            throw new InvalidOperationException("Gói license không tồn tại");

        var oldValues = new
        {
            package.Quantity,
            package.PricePerUnit,
            package.EffectiveDate,
            package.Notes
        };

        if (updateDto.Quantity.HasValue)
        {
            if (updateDto.Quantity.Value < package.ImportedToStock)
                throw new InvalidOperationException(
                    $"Số lượng mới ({updateDto.Quantity.Value}) không thể nhỏ hơn số lượng đã nhập kho ({package.ImportedToStock})");
            package.Quantity = updateDto.Quantity.Value;
        }

        if (updateDto.PricePerUnit.HasValue)
            package.PricePerUnit = updateDto.PricePerUnit.Value;

        if (updateDto.EffectiveDate.HasValue)
            package.EffectiveDate = updateDto.EffectiveDate.Value;

        if (updateDto.Notes != null)
            package.Notes = updateDto.Notes;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _packageRepository.Update(package);
            await _context.SaveChangesAsync(cancellationToken);

            // Create audit log
            await CreateAuditLogAsync(
                actorId,
                actorEmail,
                "UPDATE",
                "LicensePackage",
                package.PackageId.ToString(),
                new
                {
                    OldValues = oldValues,
                    NewValues = new
                    {
                        package.Quantity,
                        package.PricePerUnit,
                        package.EffectiveDate,
                        package.Notes
                    }
                },
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "License package {PackageId} updated by {ActorEmail}",
                package.PackageId,
                actorEmail);

            return await GetLicensePackageByIdAsync(package.PackageId, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<LicensePackageResponseDto> GetLicensePackageByIdAsync(
        Guid packageId,
        CancellationToken cancellationToken = default)
    {
        var package = await _context.LicensePackages
            .Include(lp => lp.Supplier)
            .Include(lp => lp.Product)
            .FirstOrDefaultAsync(lp => lp.PackageId == packageId, cancellationToken);

        if (package == null)
            throw new InvalidOperationException("Gói license không tồn tại");

        return new LicensePackageResponseDto
        {
            PackageId = package.PackageId,
            SupplierId = package.SupplierId,
            SupplierName = package.Supplier.Name,
            ProductId = package.ProductId,
            ProductName = package.Product.ProductName,
            Quantity = package.Quantity,
            PricePerUnit = package.PricePerUnit,
            ImportedToStock = package.ImportedToStock,
            RemainingQuantity = package.Quantity - package.ImportedToStock,
            EffectiveDate = package.EffectiveDate,
            CreatedAt = package.CreatedAt,
            Notes = package.Notes
        };
    }

    public async Task<PagedResult<LicensePackageListDto>> GetAllLicensePackagesAsync(
        int pageNumber,
        int pageSize,
        int? supplierId = null,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LicensePackages
            .Include(lp => lp.Supplier)
            .Include(lp => lp.Product)
            .AsQueryable();

        // Apply filters
        if (supplierId.HasValue)
            query = query.Where(lp => lp.SupplierId == supplierId.Value);

        if (productId.HasValue)
            query = query.Where(lp => lp.ProductId == productId.Value);

        var total = await query.CountAsync(cancellationToken);

        var packages = await query
            .OrderByDescending(lp => lp.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(lp => new LicensePackageListDto
            {
                PackageId = lp.PackageId,
                SupplierName = lp.Supplier.Name,
                ProductName = lp.Product.ProductName,
                Quantity = lp.Quantity,
                PricePerUnit = lp.PricePerUnit,
                ImportedToStock = lp.ImportedToStock,
                RemainingQuantity = lp.Quantity - lp.ImportedToStock,
                EffectiveDate = lp.EffectiveDate
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<LicensePackageListDto>(packages, total, pageNumber, pageSize);
    }

    public async Task ImportLicenseToStockAsync(
        ImportLicenseToStockDto importDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        var package = await _context.LicensePackages
            .FirstOrDefaultAsync(lp => lp.PackageId == importDto.PackageId, cancellationToken);

        if (package == null)
            throw new InvalidOperationException("Gói license không tồn tại");

        var remainingQuantity = package.Quantity - package.ImportedToStock;
        if (importDto.QuantityToImport > remainingQuantity)
            throw new InvalidOperationException(
                $"Số lượng nhập vượt quá số lượng còn lại trong gói ({remainingQuantity})");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Update package imported count
            package.ImportedToStock += importDto.QuantityToImport;
            _packageRepository.Update(package);

            // Create audit log
            await CreateAuditLogAsync(
                actorId,
                actorEmail,
                "IMPORT_TO_STOCK",
                "LicensePackage",
                package.PackageId.ToString(),
                new
                {
                    importDto.QuantityToImport,
                    NewImportedTotal = package.ImportedToStock,
                    RemainingQuantity = package.Quantity - package.ImportedToStock,
                    importDto.Notes
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Imported {Quantity} licenses from package {PackageId} to stock by {ActorEmail}",
                importDto.QuantityToImport,
                package.PackageId,
                actorEmail);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteLicensePackageAsync(
        Guid packageId,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        var package = await _packageRepository.GetByIdAsync(packageId, cancellationToken);
        if (package == null)
            throw new InvalidOperationException("Gói license không tồn tại");

        if (package.ImportedToStock > 0)
            throw new InvalidOperationException(
                $"Không thể xóa gói license đã nhập {package.ImportedToStock} key vào kho");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _packageRepository.Remove(package);

            // Create audit log
            await CreateAuditLogAsync(
                actorId,
                actorEmail,
                "DELETE",
                "LicensePackage",
                package.PackageId.ToString(),
                new
                {
                    package.SupplierId,
                    package.ProductId,
                    package.Quantity,
                    package.PricePerUnit
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "License package {PackageId} deleted by {ActorEmail}",
                packageId,
                actorEmail);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CsvUploadResultDto> UploadLicenseCsvAsync(
        Guid packageId,
        int supplierId,
        IFormFile file,
        Guid actorId,
        string actorEmail,
        string keyType,
        DateTime? expiryDate = null,
        CancellationToken cancellationToken = default)
    {
        // Validate package exists and belongs to the supplier
        var package = await _context.LicensePackages
            .Include(p => p.Product)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.PackageId == packageId, cancellationToken);

        if (package == null)
            throw new InvalidOperationException("Gói license không tồn tại");

        if (package.SupplierId != supplierId)
            throw new InvalidOperationException("Gói license không thuộc về nhà cung cấp này");

        var result = new CsvUploadResultDto
        {
            PackageId = packageId,
            Errors = new List<string>()
        };

        var licenseKeys = new List<string>();
        var duplicateKeys = new HashSet<string>();
        var invalidKeys = new List<string>();

        // Read and parse CSV file
        try
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null,
                TrimOptions = TrimOptions.Trim
            });

            await csv.ReadAsync();
            csv.ReadHeader();

            // Check if "key" column exists
            if (!csv.HeaderRecord.Any(h => h.Equals("key", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("File CSV phải có cột 'key' chứa license key");
            }

            var keyIndex = Array.FindIndex(csv.HeaderRecord, h => h.Equals("key", StringComparison.OrdinalIgnoreCase));

            while (await csv.ReadAsync())
            {
                var key = csv.GetField(keyIndex)?.Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    invalidKeys.Add($"Dòng {csv.Parser.Row}: Key trống");
                    continue;
                }

                // Check for duplicates in the file
                if (licenseKeys.Contains(key))
                {
                    duplicateKeys.Add(key);
                    continue;
                }

                licenseKeys.Add(key);
            }

            result.TotalKeysInFile = licenseKeys.Count + duplicateKeys.Count + invalidKeys.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing CSV file for package {PackageId}", packageId);
            throw new InvalidOperationException($"Lỗi đọc file CSV: {ex.Message}");
        }

        // Check remaining quantity
        var remaining = package.Quantity - package.ImportedToStock;
        if (licenseKeys.Count > remaining)
        {
            throw new InvalidOperationException(
                $"Số lượng key trong file ({licenseKeys.Count}) vượt quá số lượng còn lại ({remaining})");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var successCount = 0;
            var existingKeysCount = 0;

            // Check for existing keys in database
            var existingKeys = await _context.ProductKeys
                .Where(pk => licenseKeys.Contains(pk.KeyString))
                .Select(pk => pk.KeyString)
                .ToListAsync(cancellationToken);

            foreach (var key in licenseKeys)
            {
                if (existingKeys.Contains(key))
                {
                    duplicateKeys.Add(key);
                    existingKeysCount++;
                    continue;
                }

                // Create new ProductKey
                var productKey = new ProductKey
                {
                    ProductId = package.ProductId,
                    KeyString = key,
                    Type = keyType,
                    Status = nameof(ProductKeyStatus.Available),
                    ImportedAt = _clock.UtcNow,
                    SupplierId = package.SupplierId,
                    ImportedBy = actorId,
                    ExpiryDate = expiryDate
                };

                _context.ProductKeys.Add(productKey);
                successCount++;
            }

            // Update package's ImportedToStock
            package.ImportedToStock += successCount;
            _context.LicensePackages.Update(package);

            // Update Product stock quantity and cost price
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == package.ProductId, cancellationToken);

            if (product != null)
            {
                // Increase stock quantity
                product.StockQty += successCount;
                product.UpdatedAt = DateTime.UtcNow;
                product.Status = "ACTIVE";
                _context.Products.Update(product);
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Create audit log
            await CreateAuditLogAsync(
                actorId,
                actorEmail,
                "UPLOAD_CSV",
                "LicensePackage",
                package.PackageId.ToString(),
                new
                {
                    PackageId = package.PackageId,
                    ProductName = package.Product.ProductName,
                    SupplierName = package.Supplier.Name,
                    TotalKeysInFile = result.TotalKeysInFile,
                    SuccessfullyImported = successCount,
                    DuplicateKeys = duplicateKeys.Count,
                    InvalidKeys = invalidKeys.Count,
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            result.SuccessfullyImported = successCount;
            result.DuplicateKeys = duplicateKeys.Count;
            result.InvalidKeys = invalidKeys.Count;

            if (duplicateKeys.Count > 0)
            {
                result.Errors.Add($"{duplicateKeys.Count} key bị trùng lặp");
            }
            if (invalidKeys.Count > 0)
            {
                result.Errors.AddRange(invalidKeys);
            }

            result.Message = $"Đã nhập thành công {successCount} license key vào kho";

            _logger.LogInformation(
                "CSV uploaded for package {PackageId}: {Success} successful, {Duplicates} duplicates, {Invalid} invalid",
                packageId, successCount, duplicateKeys.Count, invalidKeys.Count);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<LicenseKeysListResponseDto> GetLicenseKeysByPackageAsync(
        Guid packageId,
        int supplierId,
        CancellationToken cancellationToken = default)
    {
        // Validate package exists and belongs to the supplier
        var package = await _context.LicensePackages
            .Include(p => p.Product)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.PackageId == packageId, cancellationToken);

        if (package == null)
            throw new InvalidOperationException("Gói license không tồn tại");

        if (package.SupplierId != supplierId)
            throw new InvalidOperationException("Gói license không thuộc về nhà cung cấp này");

        // Get all keys imported for this package's product and supplier
        var keys = await _context.ProductKeys
            .Where(pk => pk.ProductId == package.ProductId && pk.SupplierId == supplierId)
            .OrderByDescending(pk => pk.ImportedAt)
            .Select(pk => new LicenseKeyDetailDto
            {
                KeyId = pk.KeyId,
                KeyString = pk.KeyString,
                Status = pk.Status,
                ImportedAt = pk.ImportedAt,
                ImportedByEmail = pk.ImportedBy.HasValue
                    ? _context.Users
                        .Where(u => u.UserId == pk.ImportedBy.Value)
                        .Select(u => u.Email)
                        .FirstOrDefault()
                    : null
            })
            .ToListAsync(cancellationToken);

        return new LicenseKeysListResponseDto
        {
            PackageId = package.PackageId,
            ProductName = package.Product.ProductName,
            SupplierName = package.Supplier.Name,
            TotalKeys = keys.Count,
            Keys = keys
        };
    }

    private async Task CreateAuditLogAsync(
        Guid actorId,
        string actorEmail,
        string action,
        string resource,
        string entityId,
        object details,
        CancellationToken cancellationToken)
    {
        var auditLog = new AuditLog
        {
            OccurredAt = _clock.UtcNow,
            ActorId = actorId,
            ActorEmail = actorEmail,
            Action = action,
            Resource = resource,
            EntityId = entityId,
            DetailJson = JsonSerializer.Serialize(details)
        };

        await _auditRepository.AddAsync(auditLog, cancellationToken);
    }
}