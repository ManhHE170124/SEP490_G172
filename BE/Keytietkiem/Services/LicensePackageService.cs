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
                RemainingQuantity = lp.Quantity - lp.ImportedToStock
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
        Guid variantId,
        int supplierId,
        IFormFile file,
        Guid actorId,
        string actorEmail,
        string keyType,
        DateTime? expiryDate = null,
        CancellationToken cancellationToken = default)
    {
        ValidateExpiryDate(expiryDate);

        var package = await _context.LicensePackages
            .Include(p => p.Product)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.PackageId == packageId, cancellationToken);

        if (package == null)
            throw new InvalidOperationException("Gói license không tồn tại");

        if (package.SupplierId != supplierId)
            throw new InvalidOperationException("Gói license không thuộc về nhà cung cấp này");

        // Validate variant exists and belongs to the package's product
        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(v => v.VariantId == variantId && v.ProductId == package.ProductId, cancellationToken);

        if (variant == null)
            throw new InvalidOperationException("Biến thể sản phẩm không tồn tại hoặc không thuộc về sản phẩm của gói này");

        var parseResult = await ParseLicenseKeysFromCsvAsync(file, cancellationToken);

        return await PersistLicenseKeysAsync(
            package,
            variantId,
            parseResult,
            actorId,
            actorEmail,
            keyType,
            expiryDate,
            package.Product.ProductName,
            package.Supplier.Name,
            false,
            cancellationToken);
    }

    public async Task<CsvUploadResultDto> CreatePackageAndUploadCsvAsync(
        Guid variantId,
        int supplierId,
        IFormFile file,
        Guid actorId,
        string actorEmail,
        string keyType,
        decimal? cogsPrice = null,
        DateTime? expiryDate = null,
        CancellationToken cancellationToken = default)
    {
        ValidateExpiryDate(expiryDate);

        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.VariantId == variantId, cancellationToken);

        if (variant == null)
            throw new InvalidOperationException("Biến thể sản phẩm không tồn tại");

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.SupplierId == supplierId, cancellationToken);

        if (supplier == null)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại");

        // Update variant's CogsPrice if provided
        if (cogsPrice.HasValue)
        {
            variant.CogsPrice = cogsPrice.Value;
            variant.UpdatedAt = _clock.UtcNow;
            _context.ProductVariants.Update(variant);
        }

        var parseResult = await ParseLicenseKeysFromCsvAsync(file, cancellationToken);

        if (parseResult.LicenseKeys.Count == 0)
            throw new InvalidOperationException("File CSV không có license key hợp lệ");

        var package = new LicensePackage
        {
            SupplierId = supplierId,
            ProductId = variant.ProductId,
            Quantity = parseResult.LicenseKeys.Count,
            PricePerUnit = variant.CogsPrice,
            ImportedToStock = 0,
            CreatedAt = _clock.UtcNow
        };

        return await PersistLicenseKeysAsync(
            package,
            variantId,
            parseResult,
            actorId,
            actorEmail,
            keyType,
            expiryDate,
            variant.Product.ProductName,
            supplier.Name,
            true,
            cancellationToken);
    }

    private async Task<LicenseKeyCsvParseResult> ParseLicenseKeysFromCsvAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var result = new LicenseKeyCsvParseResult();

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

            if (!csv.HeaderRecord.Any(h => h.Equals("key", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("File CSV phải có cột 'key' chứa license key");

            var keyIndex = Array.FindIndex(csv.HeaderRecord, h => h.Equals("key", StringComparison.OrdinalIgnoreCase));
            var keysInFile = new HashSet<string>();

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var key = csv.GetField(keyIndex)?.Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    result.InvalidKeys.Add($"Dòng {csv.Parser.Row}: Key trống");
                    continue;
                }

                if (!keysInFile.Add(key))
                {
                    result.DuplicateKeys.Add(key);
                    continue;
                }

                result.LicenseKeys.Add(key);
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing CSV file");
            throw new InvalidOperationException($"Lỗi đọc file CSV: {ex.Message}");
        }

        return result;
    }

    private async Task<CsvUploadResultDto> PersistLicenseKeysAsync(
        LicensePackage package,
        Guid variantId,
        LicenseKeyCsvParseResult parseResult,
        Guid actorId,
        string actorEmail,
        string keyType,
        DateTime? expiryDate,
        string productName,
        string supplierName,
        bool isNewPackage,
        CancellationToken cancellationToken)
    {
        if (parseResult.LicenseKeys.Count == 0)
            throw new InvalidOperationException("File CSV không có license key hợp lệ");

        var result = new CsvUploadResultDto
        {
            PackageId = package.PackageId,
            Errors = new List<string>(),
            TotalKeysInFile = parseResult.TotalKeys
        };

        var remaining = package.Quantity - package.ImportedToStock;
        if (parseResult.LicenseKeys.Count > remaining)
        {
            throw new InvalidOperationException(
                $"Số lượng key trong file ({parseResult.LicenseKeys.Count}) vượt quá số lượng còn lại ({remaining})");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (isNewPackage)
            {
                await _context.LicensePackages.AddAsync(package, cancellationToken);
            }

            var successCount = 0;

            var existingKeys = await _context.ProductKeys
                .Where(pk => parseResult.LicenseKeys.Contains(pk.KeyString))
                .Select(pk => pk.KeyString)
                .ToListAsync(cancellationToken);

            var existingKeySet = new HashSet<string>(existingKeys);

            foreach (var key in parseResult.LicenseKeys)
            {
                if (existingKeySet.Contains(key))
                {
                    parseResult.DuplicateKeys.Add(key);
                    continue;
                }

                var productKey = new ProductKey
                {
                    VariantId = variantId,
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

            package.ImportedToStock += successCount;

            if (!isNewPackage)
            {
                _context.LicensePackages.Update(package);
            }

            var variant = await _context.ProductVariants
                .FirstOrDefaultAsync(v => v.VariantId == variantId, cancellationToken);

            if (variant != null)
            {
                variant.StockQty += successCount;
                variant.UpdatedAt = DateTime.UtcNow;
                _context.ProductVariants.Update(variant);
            }

            await _context.SaveChangesAsync(cancellationToken);

            result.PackageId = package.PackageId;
            result.SuccessfullyImported = successCount;
            result.DuplicateKeys = parseResult.DuplicateKeys.Count;
            result.InvalidKeys = parseResult.InvalidKeys.Count;

            if (parseResult.DuplicateKeys.Count > 0)
            {
                result.Errors.Add($"{parseResult.DuplicateKeys.Count} key bị trùng lặp");
            }

            if (parseResult.InvalidKeys.Count > 0)
            {
                result.Errors.AddRange(parseResult.InvalidKeys);
            }

            result.Message = $"Đã nhập thành công {successCount} license key vào kho";

            await CreateAuditLogAsync(
                actorId,
                actorEmail,
                isNewPackage ? "UPLOAD_CSV_NEW_PACKAGE" : "UPLOAD_CSV",
                "LicensePackage",
                package.PackageId.ToString(),
                new
                {
                    PackageId = package.PackageId,
                    ProductName = productName,
                    SupplierName = supplierName,
                    TotalKeysInFile = parseResult.TotalKeys,
                    SuccessfullyImported = successCount,
                    DuplicateKeys = parseResult.DuplicateKeys.Count,
                    InvalidKeys = parseResult.InvalidKeys.Count,
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "CSV uploaded for package {PackageId}: {Success} successful, {Duplicates} duplicates, {Invalid} invalid",
                package.PackageId,
                successCount,
                parseResult.DuplicateKeys.Count,
                parseResult.InvalidKeys.Count);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private void ValidateExpiryDate(DateTime? expiryDate)
    {
        if (expiryDate.HasValue && expiryDate.Value.Date < _clock.UtcNow.Date)
        {
            throw new InvalidOperationException("Ngày hết hạn không thể nhỏ hơn ngày hiện tại");
        }
    }

    private sealed class LicenseKeyCsvParseResult
    {
        public List<string> LicenseKeys { get; } = new();
        public HashSet<string> DuplicateKeys { get; } = new();
        public List<string> InvalidKeys { get; } = new();
        public int TotalKeys => LicenseKeys.Count + DuplicateKeys.Count + InvalidKeys.Count;
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
            .Include(pk => pk.Variant)
            .Where(pk => pk.Variant.ProductId == package.ProductId && pk.SupplierId == supplierId)
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