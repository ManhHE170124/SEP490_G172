// File: Services/ProductReportService.cs
using System.Text.Json;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Services
{
    public class ProductReportService : IProductReportService
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IGenericRepository<ProductReport> _reportRepository;
        private readonly IClock _clock;
        private readonly ILogger<ProductReportService> _logger;

        public ProductReportService(
            KeytietkiemDbContext context,
            IGenericRepository<ProductReport> reportRepository,
            IClock clock,
            ILogger<ProductReportService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ProductReportResponseDto> CreateProductReportAsync(
            CreateProductReportDto createDto,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // Validate Variant exists
            var variantExists = await _context.ProductVariants
                .AnyAsync(v => v.VariantId == createDto.ProductVariantId, cancellationToken);

            if (!variantExists)
                throw new InvalidOperationException("Biến thể sản phẩm không tồn tại");

            // At least one of ProductKey or ProductAccount must be provided
            if (!createDto.ProductKeyId.HasValue && !createDto.ProductAccountId.HasValue)
                throw new InvalidOperationException("Phải cung cấp ít nhất một trong ProductKeyId hoặc ProductAccountId");

            ProductKey? productKey = null;
            ProductAccount? productAccount = null;

            // Validate and fetch ProductKey if provided
            if (createDto.ProductKeyId.HasValue)
            {
                productKey = await _context.ProductKeys
                    .FirstOrDefaultAsync(k => k.KeyId == createDto.ProductKeyId.Value, cancellationToken);

                if (productKey == null)
                    throw new InvalidOperationException("Product key không tồn tại");
            }

            // Validate and fetch ProductAccount if provided
            if (createDto.ProductAccountId.HasValue)
            {
                productAccount = await _context.ProductAccounts
                    .FirstOrDefaultAsync(a => a.ProductAccountId == createDto.ProductAccountId.Value, cancellationToken);

                if (productAccount == null)
                    throw new InvalidOperationException("Tài khoản sản phẩm không tồn tại");
            }

            var report = new ProductReport
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Description = createDto.Description,
                ProductVariantId = createDto.ProductVariantId,
                ProductKeyId = createDto.ProductKeyId,
                ProductAccountId = createDto.ProductAccountId,
                UserId = userId,
                Status = nameof(ProductReportStatus.Pending),
                CreatedAt = _clock.UtcNow
            };

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _reportRepository.AddAsync(report, cancellationToken);

                // Set ProductKey status to Error if provided
                if (productKey != null)
                {
                    productKey.Status = "Error";
                    productKey.UpdatedAt = _clock.UtcNow;
                    _context.ProductKeys.Update(productKey);
                }

                // Set ProductAccount status to Error if provided
                if (productAccount != null)
                {
                    productAccount.Status = "Error";
                    productAccount.UpdatedAt = _clock.UtcNow;
                    _context.ProductAccounts.Update(productAccount);
                }

                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("ProductReport {ReportId} created by user {UserId}", report.Id, userId);

                return await GetProductReportByIdAsync(report.Id, cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<ProductReportResponseDto> UpdateProductReportStatusAsync(
            UpdateProductReportDto updateDto,
            Guid actorId,
            string actorEmail,
            CancellationToken cancellationToken = default)
        {
            var report = await _context.ProductReports
                .Include(r => r.ProductKey)
                .Include(r => r.ProductAccount)
                .FirstOrDefaultAsync(r => r.Id == updateDto.Id, cancellationToken);

            if (report == null)
                throw new InvalidOperationException("Báo cáo không tồn tại");

            var oldStatus = report.Status;
            report.Status = updateDto.Status.ToString();
            report.UpdatedAt = _clock.UtcNow;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _reportRepository.Update(report);

                // Set ProductKey status to Error if exists and not already Error
                if (report.ProductKey != null && report.ProductKey.Status != "Error")
                {
                    report.ProductKey.Status = "Error";
                    report.ProductKey.UpdatedAt = _clock.UtcNow;
                    _context.ProductKeys.Update(report.ProductKey);
                }

                // Set ProductAccount status to Error if exists and not already Error
                if (report.ProductAccount != null && report.ProductAccount.Status != "Error")
                {
                    report.ProductAccount.Status = "Error";
                    report.ProductAccount.UpdatedAt = _clock.UtcNow;
                    _context.ProductAccounts.Update(report.ProductAccount);
                }

                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "ProductReport {ReportId} status updated from {OldStatus} to {NewStatus} by {ActorEmail}",
                    report.Id,
                    oldStatus,
                    report.Status,
                    actorEmail);

                return await GetProductReportByIdAsync(report.Id, cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<ProductReportResponseDto> GetProductReportByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var report = await _context.ProductReports
                .Include(r => r.ProductVariant)
                    .ThenInclude(v => v.Product)
                .Include(r => r.ProductKey)
                    .ThenInclude(k => k.Supplier)
                .Include(r => r.ProductAccount)
                    .ThenInclude(a => a.Supplier)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (report == null)
                throw new InvalidOperationException("Báo cáo không tồn tại");

            return new ProductReportResponseDto
            {
                Id = report.Id,
                Status = report.Status,
                Name = report.Name,
                Description = report.Description,
                ProductKeyId = report.ProductKeyId,
                ProductKeyString = report.ProductKey?.KeyString,
                ProductAccountId = report.ProductAccountId,
                ProductAccountUsername = report.ProductAccount?.AccountUsername,
                ProductVariantId = report.ProductVariantId,
                ProductVariantTitle = report.ProductVariant.Title,
                ProductName = report.ProductVariant.Product.ProductName,
                SupplierName = report.ProductKey?.Supplier?.Name ?? report.ProductAccount?.Supplier?.Name,
                UserId = report.UserId,
                UserEmail = report.User.Email,
                UserFullName = report.User.FullName,
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt
            };
        }

        public async Task<PagedResult<ProductReportListDto>> GetAllProductReportsAsync(
            int pageNumber,
            int pageSize,
            string? status,
            Guid? userId,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.ProductReports
                .Include(r => r.ProductVariant)
                    .ThenInclude(v => v.Product)
                .Include(r => r.User)
                .AsQueryable();

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            // Apply user filter
            if (userId.HasValue)
            {
                query = query.Where(r => r.UserId == userId.Value);
            }

            // Apply search filter (search in title and email)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(r =>
                    r.Name.ToLower().Contains(lowerSearchTerm) ||
                    r.User.Email.ToLower().Contains(lowerSearchTerm));
            }

            var total = await query.CountAsync(cancellationToken);

            var reports = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ProductReportListDto
                {
                    Id = r.Id,
                    Status = r.Status,
                    Name = r.Name,
                    ProductVariantTitle = r.ProductVariant.Title,
                    ProductName = r.ProductVariant.Product.ProductName,
                    UserEmail = r.User.Email,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<ProductReportListDto>(reports, total, pageNumber, pageSize);
        }

        public async Task<PagedResult<ProductReportResponseDto>> GetKeyErrorsAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.ProductReports
                .Include(r => r.ProductVariant)
                    .ThenInclude(v => v.Product)
                .Include(r => r.ProductKey)
                    .ThenInclude(k => k.Supplier)
                .Include(r => r.User)
                .Where(r => r.ProductKeyId != null)
                .AsQueryable();

            // Apply search filter (search in title, email, and order ID)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(r =>
                    r.Name.ToLower().Contains(lowerSearchTerm) ||
                    r.User.Email.ToLower().Contains(lowerSearchTerm) ||
                    (r.ProductKey != null && r.ProductKey.AssignedToOrderId != null &&
                     r.ProductKey.AssignedToOrderId.ToString().ToLower().Contains(lowerSearchTerm)));
            }

            var total = await query.CountAsync(cancellationToken);

            var reports = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ProductReportResponseDto
                {
                    Id = r.Id,
                    Status = r.Status,
                    Name = r.Name,
                    Description = r.Description,
                    ProductKeyId = r.ProductKeyId,
                    ProductKeyString = r.ProductKey!.KeyString,
                    ProductAccountId = r.ProductAccountId,
                    ProductAccountUsername = null,
                    ProductVariantId = r.ProductVariantId,
                    ProductVariantTitle = r.ProductVariant.Title,
                    ProductName = r.ProductVariant.Product.ProductName,
                    SupplierName = r.ProductKey!.Supplier.Name,
                    UserId = r.UserId,
                    UserEmail = r.User.Email,
                    UserFullName = r.User.FullName,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<ProductReportResponseDto>(reports, total, pageNumber, pageSize);
        }

        public async Task<PagedResult<ProductReportResponseDto>> GetAccountErrorsAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.ProductReports
                .Include(r => r.ProductVariant)
                    .ThenInclude(v => v.Product)
                .Include(r => r.ProductAccount)
                    .ThenInclude(a => a.Supplier)
                .Include(r => r.User)
                .Where(r => r.ProductAccountId != null)
                .AsQueryable();

            // Apply search filter (search in title and email)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(r =>
                    r.Name.ToLower().Contains(lowerSearchTerm) ||
                    r.User.Email.ToLower().Contains(lowerSearchTerm));
            }

            var total = await query.CountAsync(cancellationToken);

            var reports = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ProductReportResponseDto
                {
                    Id = r.Id,
                    Status = r.Status,
                    Name = r.Name,
                    Description = r.Description,
                    ProductKeyId = r.ProductKeyId,
                    ProductKeyString = null,
                    ProductAccountId = r.ProductAccountId,
                    ProductAccountUsername = r.ProductAccount!.AccountUsername,
                    ProductVariantId = r.ProductVariantId,
                    ProductVariantTitle = r.ProductVariant.Title,
                    ProductName = r.ProductVariant.Product.ProductName,
                    SupplierName = r.ProductAccount!.Supplier.Name,
                    UserId = r.UserId,
                    UserEmail = r.User.Email,
                    UserFullName = r.User.FullName,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<ProductReportResponseDto>(reports, total, pageNumber, pageSize);
        }

        public async Task<int> CountKeyErrorsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ProductReports
                .Where(r => r.ProductKeyId != null)
                .CountAsync(cancellationToken);
        }

        public async Task<int> CountAccountErrorsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ProductReports
                .Where(r => r.ProductAccountId != null)
                .CountAsync(cancellationToken);
        }
    }
}
