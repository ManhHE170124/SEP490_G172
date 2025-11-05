using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Keytietkiem.Services;

public class ProductAccountService : IProductAccountService
{
    private readonly KeytietkiemDbContext _context;
    private readonly IGenericRepository<ProductAccount> _productAccountRepository;
    private readonly IGenericRepository<ProductAccountCustomer> _productAccountCustomerRepository;
    private readonly IGenericRepository<ProductAccountHistory> _productAccountHistoryRepository;
    private readonly IGenericRepository<User> _userRepository;
    private readonly IGenericRepository<Product> _productRepository;
    private readonly string _encryptionKey;
    private readonly IClock _clock;

    public ProductAccountService(
        KeytietkiemDbContext context,
        IGenericRepository<ProductAccount> productAccountRepository,
        IGenericRepository<ProductAccountCustomer> productAccountCustomerRepository,
        IGenericRepository<ProductAccountHistory> productAccountHistoryRepository,
        IGenericRepository<User> userRepository,
        IGenericRepository<Product> productRepository,
        IConfiguration configuration,
        IClock clock)
    {
        _context = context;
        _productAccountRepository = productAccountRepository;
        _productAccountCustomerRepository = productAccountCustomerRepository;
        _productAccountHistoryRepository = productAccountHistoryRepository;
        _userRepository = userRepository;
        _productRepository = productRepository;
        _encryptionKey = configuration["EncryptionConfig:Key"]
            ?? throw new InvalidOperationException("Encryption key not configured");
        _clock = clock;
    }

    public async Task<ProductAccountListResponseDto> GetListAsync(
        ProductAccountFilterDto filterDto,
        CancellationToken cancellationToken = default)
    {
        var query = _productAccountRepository.Query()
            .Include(pa => pa.Product)
            .Include(pa => pa.ProductAccountCustomers)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(filterDto.SearchTerm))
        {
            var searchLower = filterDto.SearchTerm.ToLower();
            query = query.Where(pa =>
                pa.AccountEmail.ToLower().Contains(searchLower) ||
                (pa.AccountUsername != null && pa.AccountUsername.ToLower().Contains(searchLower)) ||
                pa.Product.ProductName.ToLower().Contains(searchLower));
        }

        if (filterDto.ProductId.HasValue)
        {
            query = query.Where(pa => pa.ProductId == filterDto.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filterDto.Status))
        {
            query = query.Where(pa => pa.Status == filterDto.Status);
        }

        // Filter by product type if provided
        if (!string.IsNullOrWhiteSpace(filterDto.ProductType))
        {
            query = query.Where(pa => pa.Product.ProductType == filterDto.ProductType);
        }
        if (filterDto.ProductTypes != null && filterDto.ProductTypes.Any())
        {
            query = query.Where(pa => filterDto.ProductTypes.Contains(pa.Product.ProductType));
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var items = await query
            .OrderByDescending(pa => pa.CreatedAt)
            .Skip((filterDto.PageNumber - 1) * filterDto.PageSize)
            .Take(filterDto.PageSize)
            .Select(pa => new ProductAccountListDto
            {
                ProductAccountId = pa.ProductAccountId,
                ProductName = pa.Product.ProductName,
                AccountEmail = pa.AccountEmail,
                AccountUsername = pa.AccountUsername,
                MaxUsers = pa.MaxUsers,
                CurrentUsers = pa.ProductAccountCustomers.Count(pac => pac.IsActive),
                Status = pa.Status,
                ExpiryDate = pa.ExpiryDate,
                CreatedAt = pa.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new ProductAccountListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filterDto.PageSize),
            CurrentPage = filterDto.PageNumber
        };
    }

    public async Task<ProductAccountResponseDto> GetByIdAsync(
        Guid productAccountId,
        bool includePassword = false,
        CancellationToken cancellationToken = default)
    {
        var account = await _productAccountRepository.Query()
            .Include(pa => pa.Product)
            .Include(pa => pa.ProductAccountCustomers)
                .ThenInclude(pac => pac.User)
            .FirstOrDefaultAsync(pa => pa.ProductAccountId == productAccountId, cancellationToken);

        if (account == null)
        {
            throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");
        }

        var createdByUser = await _userRepository.GetByIdAsync(account.CreatedBy, cancellationToken);
        User? updatedByUser = null;
        if (account.UpdatedBy.HasValue)
        {
            updatedByUser = await _userRepository.GetByIdAsync(account.UpdatedBy.Value, cancellationToken);
        }

        var password = includePassword
            ? EncryptionHelper.Decrypt(account.AccountPassword, _encryptionKey)
            : "********";

        return new ProductAccountResponseDto
        {
            ProductAccountId = account.ProductAccountId,
            ProductId = account.ProductId,
            ProductName = account.Product.ProductName,
            AccountEmail = account.AccountEmail,
            AccountUsername = account.AccountUsername,
            AccountPassword = password,
            MaxUsers = account.MaxUsers,
            CurrentUsers = account.ProductAccountCustomers.Count(pac => pac.IsActive),
            Status = account.Status,
            ExpiryDate = account.ExpiryDate,
            Notes = account.Notes,
            CreatedAt = account.CreatedAt,
            CreatedBy = account.CreatedBy,
            UpdatedAt = account.UpdatedAt,
            UpdatedBy = account.UpdatedBy,
            Customers = account.ProductAccountCustomers
                .Select(pac => new ProductAccountCustomerDto
                {
                    ProductAccountCustomerId = pac.ProductAccountCustomerId,
                    UserId = pac.UserId,
                    UserEmail = pac.User.Email,
                    UserFullName = pac.User.FullName,
                    AddedAt = pac.AddedAt,
                    AddedBy = pac.AddedBy,
                    RemovedAt = pac.RemovedAt,
                    RemovedBy = pac.RemovedBy,
                    IsActive = pac.IsActive,
                    Notes = pac.Notes
                })
                .ToList()
        };
    }

    public async Task<ProductAccountResponseDto> CreateAsync(
        CreateProductAccountDto createDto,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        // Validate product exists
        var product = await _productRepository.GetByIdAsync(createDto.ProductId, cancellationToken);
        if (product == null)
        {
            throw new KeyNotFoundException("Không tìm thấy sản phẩm");
        }

        // Validate product type supports shared accounts
        if (!(product.ProductType == nameof(ProductEnums.SHARED_ACCOUNT) || product.ProductType == nameof(ProductEnums.PERSONAL_ACCOUNT)))
        {
            throw new InvalidOperationException("Sản phẩm này không hỗ trợ tài khoản chia sẻ");
        }

        // Encrypt password
        var encryptedPassword = EncryptionHelper.Encrypt(createDto.AccountPassword, _encryptionKey);

        var now = _clock.UtcNow;

        var account = new ProductAccount
        {
            ProductAccountId = Guid.NewGuid(),
            ProductId = createDto.ProductId,
            AccountEmail = createDto.AccountEmail,
            AccountUsername = createDto.AccountUsername,
            AccountPassword = encryptedPassword,
            MaxUsers = (product.ProductType == nameof(ProductEnums.PERSONAL_ACCOUNT)) ? 1 : createDto.MaxUsers,
            Status = nameof(ProductAccountStatus.Active),
            ExpiryDate = createDto.ExpiryDate,
            Notes = createDto.Notes,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now
        };

        await _productAccountRepository.AddAsync(account, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(account.ProductAccountId, false, cancellationToken);
    }

    public async Task<ProductAccountResponseDto> UpdateAsync(
        UpdateProductAccountDto updateDto,
        Guid updatedBy,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var account = await _productAccountRepository.GetByIdAsync(
            updateDto.ProductAccountId,
            cancellationToken);

        if (account == null)
        {
            throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");
        }

        var now = _clock.UtcNow;
        var needsHistoryLog = false;
        var action = nameof(ProductAccountAction.CredentialsUpdated);

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(updateDto.AccountEmail))
        {
            account.AccountEmail = updateDto.AccountEmail;
            needsHistoryLog = true;
        }

        if (updateDto.AccountUsername != null)
        {
            account.AccountUsername = updateDto.AccountUsername;
            needsHistoryLog = true;
        }

        if (!string.IsNullOrWhiteSpace(updateDto.AccountPassword))
        {
            account.AccountPassword = EncryptionHelper.Encrypt(updateDto.AccountPassword, _encryptionKey);
            needsHistoryLog = true;
        }

        // Enforce MaxUsers based on product type
        var updProduct = await _productRepository.GetByIdAsync(account.ProductId, cancellationToken);
        var updIsPersonal = (updProduct.ProductType == nameof(ProductEnums.PERSONAL_ACCOUNT));
        if (updIsPersonal)
        {
            account.MaxUsers = 1;
        }
        else if (updateDto.MaxUsers.HasValue)
        {
            account.MaxUsers = updateDto.MaxUsers.Value;
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Status))
        {
            account.Status = updateDto.Status;
            action = nameof(ProductAccountAction.StatusChanged);
            needsHistoryLog = true;
        }

        if (updateDto.ExpiryDate.HasValue)
        {
            account.ExpiryDate = updateDto.ExpiryDate;
        }

        if (updateDto.Notes != null)
        {
            account.Notes = updateDto.Notes;
        }

        account.UpdatedAt = now;
        account.UpdatedBy = updatedBy;

        _productAccountRepository.Update(account);
        await _context.SaveChangesAsync(cancellationToken);

        // Log history if credentials or status changed
        if (needsHistoryLog)
        {
            var history = new ProductAccountHistory
            {
                ProductAccountId = account.ProductAccountId,
                UserId = userId,
                Action = action,
                ActionBy = updatedBy,
                ActionAt = now,
                Notes = $"Cập nhật thông tin tài khoản"
            };

            await _productAccountHistoryRepository.AddAsync(history, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(account.ProductAccountId, false, cancellationToken);
    }

    public async Task DeleteAsync(Guid productAccountId, CancellationToken cancellationToken = default)
    {
        var account = await _productAccountRepository.GetByIdAsync(productAccountId, cancellationToken);

        if (account == null)
        {
            throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");
        }

        // Check if account has active customers
        var hasActiveCustomers = await _productAccountCustomerRepository.Query()
            .AnyAsync(pac => pac.ProductAccountId == productAccountId && pac.IsActive, cancellationToken);

        if (hasActiveCustomers)
        {
            throw new InvalidOperationException("Không thể xóa tài khoản đang có khách hàng sử dụng");
        }

        _productAccountRepository.Remove(account);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductAccountCustomerDto> AddCustomerAsync(
        AddCustomerToAccountDto addDto,
        Guid addedBy,
        CancellationToken cancellationToken = default)
    {
        var account = await _productAccountRepository.Query()
            .Include(pa => pa.ProductAccountCustomers)
            .FirstOrDefaultAsync(pa => pa.ProductAccountId == addDto.ProductAccountId, cancellationToken);

        if (account == null)
        {
            throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");
        }

        // Check if account is active
        if (account.Status != nameof(ProductAccountStatus.Active))
        {
            throw new InvalidOperationException("Tài khoản không ở trạng thái hoạt động");
        }

        // Check if account is expired
        if (account.ExpiryDate.HasValue && account.ExpiryDate.Value < _clock.UtcNow)
        {
            throw new InvalidOperationException("Tài khoản đã hết hạn");
        }

        // Check if account is full
        var currentActiveUsers = account.ProductAccountCustomers.Count(pac => pac.IsActive);
        if (currentActiveUsers >= account.MaxUsers)
        {
            throw new InvalidOperationException("Tài khoản đã đạt số lượng người dùng tối đa");
        }

        // Check if user exists
        var user = await _userRepository.GetByIdAsync(addDto.UserId, cancellationToken);
        if (user == null)
        {
            throw new KeyNotFoundException("Không tìm thấy người dùng");
        }

        // Check if customer already exists
        var existingCustomer = await _productAccountCustomerRepository.Query()
            .FirstOrDefaultAsync(pac =>
                pac.ProductAccountId == addDto.ProductAccountId &&
                pac.UserId == addDto.UserId &&
                pac.IsActive,
                cancellationToken);

        if (existingCustomer != null)
        {
            throw new InvalidOperationException("Người dùng đã được thêm vào tài khoản này");
        }

        var now = _clock.UtcNow;

        var customer = new ProductAccountCustomer
        {
            ProductAccountId = addDto.ProductAccountId,
            UserId = addDto.UserId,
            AddedAt = now,
            AddedBy = addedBy,
            IsActive = true,
            Notes = addDto.Notes
        };

        await _productAccountCustomerRepository.AddAsync(customer, cancellationToken);

        // Log history
        var history = new ProductAccountHistory
        {
            ProductAccountId = addDto.ProductAccountId,
            UserId = addDto.UserId,
            Action = nameof(ProductAccountAction.Added),
            ActionBy = addedBy,
            ActionAt = now,
            Notes = addDto.Notes
        };

        await _productAccountHistoryRepository.AddAsync(history, cancellationToken);

        // Update account status if now full
        if (currentActiveUsers + 1 >= account.MaxUsers)
        {
            account.Status = nameof(ProductAccountStatus.Full);
            account.UpdatedAt = now;
            _productAccountRepository.Update(account);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var addedByUser = await _userRepository.GetByIdAsync(addedBy, cancellationToken);

        return new ProductAccountCustomerDto
        {
            ProductAccountCustomerId = customer.ProductAccountCustomerId,
            UserId = customer.UserId,
            UserEmail = user.Email,
            UserFullName = user.FullName,
            AddedAt = customer.AddedAt,
            AddedBy = customer.AddedBy,
            AddedByEmail = addedByUser?.Email,
            IsActive = customer.IsActive,
            Notes = customer.Notes
        };
    }

    public async Task RemoveCustomerAsync(
        RemoveCustomerFromAccountDto removeDto,
        Guid removedBy,
        CancellationToken cancellationToken = default)
    {
        var customer = await _productAccountCustomerRepository.Query()
            .Include(pac => pac.ProductAccount)
            .FirstOrDefaultAsync(pac =>
                pac.ProductAccountId == removeDto.ProductAccountId &&
                pac.UserId == removeDto.UserId &&
                pac.IsActive,
                cancellationToken);

        if (customer == null)
        {
            throw new KeyNotFoundException("Không tìm thấy khách hàng trong tài khoản");
        }

        var now = _clock.UtcNow;

        customer.IsActive = false;
        customer.RemovedAt = now;
        customer.RemovedBy = removedBy;
        customer.Notes = removeDto.Notes;

        _productAccountCustomerRepository.Update(customer);

        // Log history
        var history = new ProductAccountHistory
        {
            ProductAccountId = removeDto.ProductAccountId,
            UserId = removeDto.UserId,
            Action = nameof(ProductAccountAction.Removed),
            ActionBy = removedBy,
            ActionAt = now,
            Notes = removeDto.Notes
        };

        await _productAccountHistoryRepository.AddAsync(history, cancellationToken);

        // Update account status if was full
        var account = customer.ProductAccount;
        if (account.Status == nameof(ProductAccountStatus.Full))
        {
            account.Status = nameof(ProductAccountStatus.Active);
            account.UpdatedAt = now;
            _productAccountRepository.Update(account);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductAccountHistoryResponseDto> GetHistoryAsync(
        Guid productAccountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _productAccountRepository.Query()
            .Include(pa => pa.Product)
            .FirstOrDefaultAsync(pa => pa.ProductAccountId == productAccountId, cancellationToken);

        if (account == null)
        {
            throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");
        }

        var history = await _productAccountHistoryRepository.Query()
            .Include(pah => pah.User)
            .Where(pah => pah.ProductAccountId == productAccountId)
            .OrderByDescending(pah => pah.ActionAt)
            .Select(pah => new ProductAccountHistoryDto
            {
                HistoryId = pah.HistoryId,
                ProductAccountId = pah.ProductAccountId,
                UserId = pah.UserId,
                UserEmail = pah.User.Email,
                UserFullName = pah.User.FullName,
                Action = pah.Action,
                ActionBy = pah.ActionBy,
                ActionAt = pah.ActionAt,
                Notes = pah.Notes
            })
            .ToListAsync(cancellationToken);

        return new ProductAccountHistoryResponseDto
        {
            ProductAccountId = account.ProductAccountId,
            ProductName = account.Product.ProductName,
            AccountEmail = account.AccountEmail,
            TotalHistoryRecords = history.Count,
            History = history
        };
    }

    public async Task<string> GetDecryptedPasswordAsync(
        Guid productAccountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _productAccountRepository.GetByIdAsync(productAccountId, cancellationToken);

        if (account == null)
        {
            throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");
        }

        return EncryptionHelper.Decrypt(account.AccountPassword, _encryptionKey);
    }
}
