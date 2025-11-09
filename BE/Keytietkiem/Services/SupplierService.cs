using System.Text.Json;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Services;

public class SupplierService : ISupplierService
{
    private readonly KeytietkiemDbContext _context;
    private readonly IGenericRepository<Supplier> _supplierRepository;
    private readonly IGenericRepository<AuditLog> _auditRepository;
    private readonly IClock _clock;
    private readonly ILogger<SupplierService> _logger;

    public SupplierService(
        KeytietkiemDbContext context,
        IGenericRepository<Supplier> supplierRepository,
        IGenericRepository<AuditLog> auditRepository,
        IClock clock,
        ILogger<SupplierService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SupplierResponseDto> CreateSupplierAsync(
        CreateSupplierDto createDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        // Validate duplicate name
        if (await IsSupplierNameExistsAsync(createDto.Name, null, cancellationToken))
            throw new InvalidOperationException($"Nhà cung cấp với tên '{createDto.Name}' đã tồn tại");

        // Validate mandatory license details if provided
        if (!string.IsNullOrWhiteSpace(createDto.LicenseTerms) && createDto.LicenseTerms.Length < 10)
            throw new InvalidOperationException("Điều khoản giấy phép phải có ít nhất 10 ký tự");

        var supplier = new Supplier
        {
            Name = createDto.Name,
            ContactEmail = createDto.ContactEmail,
            ContactPhone = createDto.ContactPhone,
            CreatedAt = _clock.UtcNow,
            Status = nameof(SupplierStatus.Active)
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _supplierRepository.AddAsync(supplier, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Create audit log
            await CreateAuditLogAsync(
                actorId,
                actorEmail,
                "CREATE",
                "Supplier",
                supplier.SupplierId.ToString(),
                new
                {
                    supplier.Name,
                    supplier.ContactEmail,
                    supplier.ContactPhone,
                    createDto.LicenseTerms,
                    createDto.Notes
                },
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Supplier {SupplierId} created by {ActorEmail}", supplier.SupplierId, actorEmail);

            return await GetSupplierByIdAsync(supplier.SupplierId, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SupplierResponseDto> UpdateSupplierAsync(
        UpdateSupplierDto updateDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _supplierRepository.GetByIdAsync(updateDto.SupplierId, cancellationToken);
        if (supplier == null)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại");

        // Validate duplicate name
        if (await IsSupplierNameExistsAsync(updateDto.Name, updateDto.SupplierId, cancellationToken))
            throw new InvalidOperationException($"Nhà cung cấp với tên '{updateDto.Name}' đã tồn tại");

        // Validate mandatory license details if provided
        if (!string.IsNullOrWhiteSpace(updateDto.LicenseTerms) && updateDto.LicenseTerms.Length < 10)
            throw new InvalidOperationException("Điều khoản giấy phép phải có ít nhất 10 ký tự");

        var oldValues = new
        {
            supplier.Name,
            supplier.ContactEmail,
            supplier.ContactPhone
        };

        supplier.Name = updateDto.Name;
        supplier.LicenseTerms = updateDto.LicenseTerms;
        supplier.ContactEmail = updateDto.ContactEmail;
        supplier.ContactPhone = updateDto.ContactPhone;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _supplierRepository.Update(supplier);
            await _context.SaveChangesAsync(cancellationToken);

            // Create audit log
            await CreateAuditLogAsync(
                actorId,
                actorEmail,
                "UPDATE",
                "Supplier",
                supplier.SupplierId.ToString(),
                new
                {
                    OldValues = oldValues,
                    NewValues = new
                    {
                        supplier.Name,
                        supplier.ContactEmail,
                        supplier.ContactPhone,
                        updateDto.LicenseTerms,
                        updateDto.Notes
                    }
                },
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Supplier {SupplierId} updated by {ActorEmail}", supplier.SupplierId, actorEmail);

            return await GetSupplierByIdAsync(supplier.SupplierId, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SupplierResponseDto> GetSupplierByIdAsync(
        int supplierId,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.ProductKeys)
            .FirstOrDefaultAsync(s => s.SupplierId == supplierId, cancellationToken);

        if (supplier == null)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại");

        var activeProductCount = supplier.ProductKeys
            .Select(pk => pk.ProductId)
            .Distinct()
            .Count();

        return new SupplierResponseDto
        {
            SupplierId = supplier.SupplierId,
            Name = supplier.Name,
            ContactEmail = supplier.ContactEmail,
            ContactPhone = supplier.ContactPhone,
            CreatedAt = supplier.CreatedAt,
            Status = supplier.Status,
            LicenseTerms = supplier.LicenseTerms,
            ActiveProductCount = activeProductCount,
            TotalProductKeyCount = supplier.ProductKeys.Count
        };
    }

    public async Task<PagedResult<SupplierListDto>> GetAllSuppliersAsync(
        int pageNumber,
        int pageSize,
        string? status,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Suppliers
            .Include(s => s.ProductKeys)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(lowerSearchTerm) ||
                (s.ContactEmail != null && s.ContactEmail.ToLower().Contains(lowerSearchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(s => s.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);

        var suppliers = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SupplierListDto
            {
                SupplierId = s.SupplierId,
                Name = s.Name,
                ContactEmail = s.ContactEmail,
                ContactPhone = s.ContactPhone,
                CreatedAt = s.CreatedAt,
                Status = s.Status,
                ActiveProductCount = s.ProductKeys.Select(pk => pk.ProductId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierListDto>(suppliers, total, pageNumber, pageSize);
    }

    public async Task<DeactivateSupplierValidationDto> ValidateDeactivationAsync(
        int supplierId,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.ProductKeys)
            .ThenInclude(pk => pk.Product)
            .FirstOrDefaultAsync(s => s.SupplierId == supplierId, cancellationToken);

        if (supplier == null)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại");

        var activeProducts = supplier.ProductKeys
            .Where(pk => pk.Status == nameof(ProductKeyStatus.Available))
            .Select(pk => pk.Product.ProductName)
            .Distinct()
            .ToList();

        var canDeactivate = activeProducts.Count == 0;

        return new DeactivateSupplierValidationDto
        {
            CanDeactivate = canDeactivate,
            ActiveProductCount = activeProducts.Count,
            Message = canDeactivate
                ? "Nhà cung cấp có thể được vô hiệu hóa"
                : "Nhà cung cấp có sản phẩm đang hoạt động. Vui lòng xác nhận để tiếp tục.",
            AffectedProducts = activeProducts
        };
    }

    public async Task DeactivateSupplierAsync(
        DeactivateSupplierDto deactivateDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.ProductKeys)
            .FirstOrDefaultAsync(s => s.SupplierId == deactivateDto.SupplierId, cancellationToken);

        if (supplier == null)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại");

        if (supplier.Status == nameof(SupplierStatus.Deactive))
            throw new InvalidOperationException("Nhà cung cấp đã bị vô hiệu hóa");

        var validation = await ValidateDeactivationAsync(deactivateDto.SupplierId, cancellationToken);

        // If supplier has active products and no confirmation provided
        if (!validation.CanDeactivate && !deactivateDto.ConfirmReassignment)
        {
            throw new InvalidOperationException(
                $"Nhà cung cấp có {validation.ActiveProductCount} sản phẩm đang hoạt động. " +
                "Vui lòng xác nhận hoặc chỉ định nhà cung cấp khác.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Handle reassignment if specified
            if (deactivateDto.ReassignToSupplierId.HasValue)
            {
                var newSupplier = await _supplierRepository.GetByIdAsync(
                    deactivateDto.ReassignToSupplierId.Value,
                    cancellationToken);

                if (newSupplier == null)
                    throw new InvalidOperationException("Nhà cung cấp mới không tồn tại");

                if (newSupplier.Status == nameof(SupplierStatus.Deactive))
                    throw new InvalidOperationException("Không thể chuyển sản phẩm sang nhà cung cấp đã bị vô hiệu hóa");

                // Reassign all product keys to new supplier
                foreach (var productKey in supplier.ProductKeys)
                {
                    productKey.SupplierId = deactivateDto.ReassignToSupplierId.Value;
                }
            }

            // Set supplier status to Deactive instead of deleting
            supplier.Status = nameof(SupplierStatus.Deactive);
            _supplierRepository.Update(supplier);
            await _context.SaveChangesAsync(cancellationToken);

            // Create audit log
            await CreateAuditLogAsync(
                actorId,
                actorEmail,
                "DEACTIVATE",
                "Supplier",
                supplier.SupplierId.ToString(),
                new
                {
                    supplier.Name,
                    deactivateDto.Reason,
                    deactivateDto.ReassignToSupplierId,
                    ProductKeyCount = supplier.ProductKeys.Count
                },
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Supplier {SupplierId} deactivated by {ActorEmail}",
                supplier.SupplierId,
                actorEmail);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SupplierResponseDto> ToggleSupplierStatusAsync(
        int supplierId,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _supplierRepository.GetByIdAsync(supplierId, cancellationToken);
        if (supplier == null)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var oldStatus = supplier.Status;
            var newStatus = supplier.Status == nameof(SupplierStatus.Active)
                ? nameof(SupplierStatus.Deactive)
                : nameof(SupplierStatus.Active);

            supplier.Status = newStatus;
            _supplierRepository.Update(supplier);
            await _context.SaveChangesAsync(cancellationToken);

            // Create audit log
            await CreateAuditLogAsync(
                actorId,
                actorEmail,
                "TOGGLE_STATUS",
                "Supplier",
                supplier.SupplierId.ToString(),
                new
                {
                    supplier.Name,
                    OldStatus = oldStatus,
                    NewStatus = newStatus
                },
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Supplier {SupplierId} status toggled from {OldStatus} to {NewStatus} by {ActorEmail}",
                supplier.SupplierId,
                oldStatus,
                newStatus,
                actorEmail);

            return await GetSupplierByIdAsync(supplier.SupplierId, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> IsSupplierNameExistsAsync(
        string name,
        int? excludeSupplierId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Suppliers
            .Where(s => s.Name.ToLower() == name.ToLower() && s.Status == nameof(SupplierStatus.Active));

        if (excludeSupplierId.HasValue)
            query = query.Where(s => s.SupplierId != excludeSupplierId.Value);

        return await query.AnyAsync(cancellationToken);
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
