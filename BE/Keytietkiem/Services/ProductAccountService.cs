using System.ComponentModel.DataAnnotations;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Services;

public class ProductAccountService : IProductAccountService
{
    private readonly IClock _clock;
    private readonly KeytietkiemDbContext _context;
    private readonly string _encryptionKey;
    private readonly IGenericRepository<ProductAccountCustomer> _productAccountCustomerRepository;
    private readonly IGenericRepository<ProductAccountHistory> _productAccountHistoryRepository;
    private readonly IGenericRepository<ProductAccount> _productAccountRepository;
    private readonly IGenericRepository<ProductVariant> _productVariantRepository;
    private readonly IGenericRepository<User> _userRepository;

    public ProductAccountService(
        KeytietkiemDbContext context,
        IGenericRepository<ProductAccount> productAccountRepository,
        IGenericRepository<ProductAccountCustomer> productAccountCustomerRepository,
        IGenericRepository<ProductAccountHistory> productAccountHistoryRepository,
        IGenericRepository<User> userRepository,
        IGenericRepository<ProductVariant> productVariantRepository,
        IConfiguration configuration,
        IClock clock)
    {
        _context = context;
        _productAccountRepository = productAccountRepository;
        _productAccountCustomerRepository = productAccountCustomerRepository;
        _productAccountHistoryRepository = productAccountHistoryRepository;
        _userRepository = userRepository;
        _productVariantRepository = productVariantRepository;
        _encryptionKey = configuration["EncryptionConfig:Key"]
                         ?? throw new InvalidOperationException("Encryption key not configured");
        _clock = clock;
    }

    public async Task<ProductAccountListResponseDto> GetListAsync(
        ProductAccountFilterDto filterDto,
        CancellationToken cancellationToken = default)
    {
        var query = _productAccountRepository.Query()
            .Include(pa => pa.Variant)
                .ThenInclude(v => v.Product)
            .Include(pa => pa.Supplier)
            .Include(pa => pa.ProductAccountCustomers)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(filterDto.SearchTerm))
        {
            var searchLower = filterDto.SearchTerm.ToLower();
            query = query.Where(pa =>
                pa.AccountEmail.ToLower().Contains(searchLower) ||
                (pa.AccountUsername != null && pa.AccountUsername.ToLower().Contains(searchLower)) ||
                pa.Variant.Product.ProductName.ToLower().Contains(searchLower) ||
                pa.Variant.Title.ToLower().Contains(searchLower));
        }

        if (filterDto.VariantId.HasValue) query = query.Where(pa => pa.VariantId == filterDto.VariantId.Value);

        if (filterDto.ProductId.HasValue) query = query.Where(pa => pa.Variant.ProductId == filterDto.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(filterDto.Status)) query = query.Where(pa => pa.Status == filterDto.Status);

        // Filter by product type if provided
        if (!string.IsNullOrWhiteSpace(filterDto.ProductType))
            query = query.Where(pa => pa.Variant.Product.ProductType == filterDto.ProductType);
        if (filterDto.ProductTypes != null && filterDto.ProductTypes.Any())
            query = query.Where(pa => filterDto.ProductTypes.Contains(pa.Variant.Product.ProductType));

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
                VariantId = pa.VariantId,
                VariantTitle = pa.Variant.Title,
                ProductName = pa.Variant.Product.ProductName,
                SupplierId = pa.SupplierId,
                SupplierName = pa.Supplier.Name,
                AccountEmail = pa.AccountEmail,
                AccountUsername = pa.AccountUsername,
                MaxUsers = pa.MaxUsers,
                CurrentUsers = pa.ProductAccountCustomers.Count(pac => pac.IsActive),
                Status = pa.Status,
                CogsPrice = pa.Variant.CogsPrice,
                SellPrice = pa.Variant.SellPrice,
                ExpiryDate = pa.ExpiryDate,
                CreatedAt = pa.CreatedAt,
                OrderId = pa.ProductAccountCustomers.FirstOrDefault(x=> x.OrderId.HasValue).OrderId
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

    public async Task<(Guid?, bool)> CheckAccountEmailOrUsernameExists(Guid variantId, string email, string? username, CancellationToken cancellationToken = default)
    {
        var existAccount = await _productAccountRepository.FirstOrDefaultAsync(x=> x.VariantId == variantId
            && (x.AccountEmail.ToLower().Equals(email.ToLower())
            || (!string.IsNullOrWhiteSpace(username)
                && x.AccountUsername != null
                && x.AccountUsername.ToLower().Equals(username.ToLower()))), cancellationToken);

        return (existAccount?.ProductAccountId, existAccount != null);
    }

    public async Task<ProductAccountResponseDto> GetByIdAsync(
        Guid productAccountId,
        bool includePassword = false,
        CancellationToken cancellationToken = default)
    {
        var account = await _productAccountRepository.Query()
            .Include(pa => pa.Variant)
                .ThenInclude(v => v.Product)
            .Include(pa => pa.Supplier)
            .Include(pa => pa.ProductAccountCustomers)
            .ThenInclude(pac => pac.User)
            .FirstOrDefaultAsync(pa => pa.ProductAccountId == productAccountId, cancellationToken);

        if (account == null) throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");

        var password = includePassword
            ? EncryptionHelper.Decrypt(account.AccountPassword, _encryptionKey)
            : "********";

        return new ProductAccountResponseDto
        {
            ProductAccountId = account.ProductAccountId,
            ProductId = account.Variant.ProductId,
            VariantId = account.VariantId,
            VariantTitle = account.Variant.Title,
            ProductName = account.Variant.Product.ProductName,
            SupplierId = account.SupplierId,
            SupplierName = account.Supplier?.Name ?? string.Empty,
            AccountEmail = account.AccountEmail,
            AccountUsername = account.AccountUsername,
            AccountPassword = password,
            MaxUsers = account.MaxUsers,
            CurrentUsers = account.ProductAccountCustomers.Count(pac => pac.IsActive),
            Status = account.Status,
            CogsPrice = account.Variant.CogsPrice,
            SellPrice = account.Variant.SellPrice,
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
        // Validate variant exists
        var variant = await _productVariantRepository.Query()
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.VariantId == createDto.VariantId, cancellationToken);

        if (variant == null) throw new KeyNotFoundException("Không tìm thấy biến thể sản phẩm");

        // Validate supplier exists
        var supplierExists = await _context.Suppliers
            .AnyAsync(s => s.SupplierId == createDto.SupplierId, cancellationToken);

        if (!supplierExists) throw new KeyNotFoundException($"Không tìm thấy nhà cung cấp với ID: {createDto.SupplierId}");

        // Validate product type supports shared accounts
        if (!(variant.Product.ProductType == nameof(ProductEnums.SHARED_ACCOUNT) ||
              variant.Product.ProductType == nameof(ProductEnums.PERSONAL_ACCOUNT)))
            throw new InvalidOperationException("Sản phẩm này không hỗ trợ tài khoản chia sẻ");

        // Update variant's CogsPrice if provided
        if (createDto.CogsPrice.HasValue)
        {
            variant.CogsPrice = createDto.CogsPrice.Value;
        }

        // Increment stock quantity for the variant
        variant.StockQty += 1;
        variant.UpdatedAt = _clock.UtcNow;
        _productVariantRepository.Update(variant);

        // Encrypt password
        var encryptedPassword = EncryptionHelper.Encrypt(createDto.AccountPassword, _encryptionKey);

        var now = _clock.UtcNow;

        var account = new ProductAccount
        {
            ProductAccountId = Guid.NewGuid(),
            VariantId = createDto.VariantId,
            SupplierId = createDto.SupplierId,
            AccountEmail = (createDto.AccountEmail ?? string.Empty).Trim(),
            AccountUsername = string.IsNullOrWhiteSpace(createDto.AccountUsername)
                ? null
                : createDto.AccountUsername.Trim(),
            AccountPassword = encryptedPassword,
            MaxUsers = variant.Product.ProductType == nameof(ProductEnums.PERSONAL_ACCOUNT) ? 1 : createDto.MaxUsers,
            Status = nameof(ProductAccountStatus.Active),
            ExpiryDate = createDto.StartDate.AddDays(variant.DurationDays ?? 30),
            Notes = createDto.Notes,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now
        };

        ValidateProductAccountEntity(account, true);

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

        if (account == null) throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");

        var now = _clock.UtcNow;
        var needsHistoryLog = false;
        var action = nameof(ProductAccountAction.CredentialsUpdated);

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(updateDto.AccountEmail) && !string.Equals(account.AccountEmail,
                updateDto.AccountEmail, StringComparison.CurrentCultureIgnoreCase))
        {
            account.AccountEmail = updateDto.AccountEmail;
            needsHistoryLog = true;
        }

        if (updateDto.AccountUsername != null && !string.Equals(account.AccountUsername, updateDto.AccountUsername,
                StringComparison.CurrentCultureIgnoreCase))
        {
            account.AccountUsername = updateDto.AccountUsername;
            needsHistoryLog = true;
        }

        if (!string.IsNullOrWhiteSpace(updateDto.AccountPassword) && !string.Equals(account.AccountPassword,
                updateDto.AccountPassword, StringComparison.CurrentCultureIgnoreCase))
        {
            account.AccountPassword = EncryptionHelper.Encrypt(updateDto.AccountPassword, _encryptionKey);
            needsHistoryLog = true;
        }

        // Enforce MaxUsers based on product type
        var variant = await _productVariantRepository.Query()
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.VariantId == account.VariantId, cancellationToken);

        if (variant == null) throw new KeyNotFoundException("Không tìm thấy biến thể sản phẩm");

        // Validate and update supplier if provided
        if (updateDto.SupplierId.HasValue && account.SupplierId != updateDto.SupplierId.Value)
        {
            var supplierExists = await _context.Suppliers
                .AnyAsync(s => s.SupplierId == updateDto.SupplierId.Value, cancellationToken);

            if (!supplierExists) throw new KeyNotFoundException($"Không tìm thấy nhà cung cấp với ID: {updateDto.SupplierId.Value}");

            account.SupplierId = updateDto.SupplierId.Value;
        }

        var updIsPersonal = variant.Product.ProductType == nameof(ProductEnums.PERSONAL_ACCOUNT);
        if (updIsPersonal)
        {
            account.MaxUsers = 1;
        }
        else if (updateDto.MaxUsers.HasValue && account.MaxUsers != updateDto.MaxUsers)
        {
            account.MaxUsers = updateDto.MaxUsers.Value;
            action = nameof(ProductAccountAction.UpdateSlot);
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Status) && account.Status != updateDto.Status)
        {
            account.Status = updateDto.Status;
            action = nameof(ProductAccountAction.StatusChanged);
            needsHistoryLog = true;
        }

        if (updateDto.ExpiryDate.HasValue && account.ExpiryDate != updateDto.ExpiryDate)
            account.ExpiryDate = updateDto.ExpiryDate;

        if (updateDto.Notes != null) account.Notes = updateDto.Notes;

        account.UpdatedAt = now;
        account.UpdatedBy = updatedBy;

        ValidateProductAccountEntity(account, false);

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
                Notes = "Cập nhật thông tin tài khoản"
            };

            await _productAccountHistoryRepository.AddAsync(history, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(account.ProductAccountId, false, cancellationToken);
    }

    public async Task DeleteAsync(Guid productAccountId, CancellationToken cancellationToken = default)
    {
        var account = await _productAccountRepository.GetByIdAsync(productAccountId, cancellationToken);

        if (account == null) throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");

        // Check if account has active customers
        var hasActiveCustomers = await _productAccountCustomerRepository.Query()
            .AnyAsync(pac => pac.ProductAccountId == productAccountId && pac.IsActive, cancellationToken);

        if (hasActiveCustomers)
            throw new InvalidOperationException("Không thể xóa tài khoản đang có khách hàng sử dụng");

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

        if (account == null) throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");

        // Check if account is active
        if (account.Status != nameof(ProductAccountStatus.Active))
            throw new InvalidOperationException("Tài khoản không ở trạng thái hoạt động");

        // Check if account is expired
        if (account.ExpiryDate.HasValue && account.ExpiryDate.Value < _clock.UtcNow)
            throw new InvalidOperationException("Tài khoản đã hết hạn");

        // Check if account is full
        var currentActiveUsers = account.ProductAccountCustomers.Count(pac => pac.IsActive);
        if (currentActiveUsers >= account.MaxUsers)
            throw new InvalidOperationException("Tài khoản đã đạt số lượng người dùng tối đa");

        // Check if user exists
        var user = await _userRepository.GetByIdAsync(addDto.UserId, cancellationToken);
        if (user == null) throw new KeyNotFoundException("Không tìm thấy người dùng");

        // Check if customer already exists
        var existingCustomer = await _productAccountCustomerRepository.Query()
            .FirstOrDefaultAsync(pac =>
                    pac.ProductAccountId == addDto.ProductAccountId &&
                    pac.UserId == addDto.UserId &&
                    pac.IsActive,
                cancellationToken);

        if (existingCustomer != null) throw new InvalidOperationException("Người dùng đã được thêm vào tài khoản này");

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

        if (customer == null) throw new KeyNotFoundException("Không tìm thấy khách hàng trong tài khoản");

        var now = _clock.UtcNow;

        // Use provided notes or default message
        var notes = string.IsNullOrWhiteSpace(removeDto.Notes)
            ? "Xóa khách hàng khỏi tài khoản"
            : removeDto.Notes;

        customer.IsActive = false;
        customer.RemovedAt = now;
        customer.RemovedBy = removedBy;
        customer.Notes = notes;

        _productAccountCustomerRepository.Update(customer);

        // Log history
        var history = new ProductAccountHistory
        {
            ProductAccountId = removeDto.ProductAccountId,
            UserId = removeDto.UserId,
            Action = nameof(ProductAccountAction.Removed),
            ActionBy = removedBy,
            ActionAt = now,
            Notes = notes
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
            .Include(pa => pa.Variant)
                .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(pa => pa.ProductAccountId == productAccountId, cancellationToken);

        if (account == null) throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");

        var history = await _productAccountHistoryRepository.Query()
            .Include(pah => pah.User)
            .Where(pah => pah.ProductAccountId == productAccountId)
            .OrderByDescending(pah => pah.ActionAt)
            .Select(pah => new ProductAccountHistoryDto
            {
                HistoryId = pah.HistoryId,
                ProductAccountId = pah.ProductAccountId,
                UserId = pah.UserId,
                UserEmail = pah.User != null ? pah.User.Email : null,
                UserFullName = pah.User != null ? pah.User.FullName : null,
                Action = pah.Action,
                ActionBy = pah.ActionBy,
                ActionAt = pah.ActionAt,
                Notes = pah.Notes
            })
            .ToListAsync(cancellationToken);

        return new ProductAccountHistoryResponseDto
        {
            ProductAccountId = account.ProductAccountId,
            ProductName = account.Variant.Product.ProductName,
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

        if (account == null) throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");

        return EncryptionHelper.Decrypt(account.AccountPassword, _encryptionKey);
    }

    public async Task<ProductAccountCustomerDto> AssignAccountToOrderAsync(
        AssignAccountToOrderDto assignDto,
        Guid assignedBy,
        CancellationToken cancellationToken = default)
    {
        var account = await _productAccountRepository.Query()
            .Include(pa => pa.ProductAccountCustomers)
            .Include(pa => pa.Variant)
                .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(pa => pa.ProductAccountId == assignDto.ProductAccountId, cancellationToken);

        if (account == null) throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");

        // Check if account is active
        if (account.Status != nameof(ProductAccountStatus.Active))
            throw new InvalidOperationException("Tài khoản không ở trạng thái hoạt động");

        // Check if account is expired
        if (account.ExpiryDate.HasValue && account.ExpiryDate.Value < _clock.UtcNow)
            throw new InvalidOperationException("Tài khoản đã hết hạn");

        // Check if account is full
        var currentActiveUsers = account.ProductAccountCustomers.Count(pac => pac.IsActive);
        if (currentActiveUsers >= account.MaxUsers)
            throw new InvalidOperationException("Tài khoản đã đạt số lượng người dùng tối đa");

        // Verify order exists
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == assignDto.OrderId, cancellationToken);

        if (order == null) throw new KeyNotFoundException("Không tìm thấy đơn hàng");

        // Check if user exists
        var user = await _userRepository.GetByIdAsync(assignDto.UserId, cancellationToken);
        if (user == null) throw new KeyNotFoundException("Không tìm thấy người dùng");

        // Check if customer already exists
        var existingCustomer = await _productAccountCustomerRepository.Query()
            .FirstOrDefaultAsync(pac =>
                    pac.ProductAccountId == assignDto.ProductAccountId &&
                    pac.UserId == assignDto.UserId &&
                    pac.IsActive,
                cancellationToken);

        if (existingCustomer != null) throw new InvalidOperationException("Người dùng đã được thêm vào tài khoản này");

        var now = _clock.UtcNow;

        var customer = new ProductAccountCustomer
        {
            ProductAccountId = assignDto.ProductAccountId,
            UserId = assignDto.UserId,
            AddedAt = now,
            AddedBy = assignedBy,
            IsActive = true,
            OrderId =  assignDto.OrderId,
            Notes = $"Gán cho đơn hàng {assignDto.OrderId}"
        };

        await _productAccountCustomerRepository.AddAsync(customer, cancellationToken);

        // Log history
        var history = new ProductAccountHistory
        {
            ProductAccountId = assignDto.ProductAccountId,
            UserId = assignDto.UserId,
            Action = nameof(ProductAccountAction.Added),
            ActionBy = assignedBy,
            ActionAt = now,
            Notes = $"Gán cho đơn hàng {assignDto.OrderId}"
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

        var addedByUser = await _userRepository.GetByIdAsync(assignedBy, cancellationToken);

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

    public async Task<ProductAccountResponseDto> ExtendExpiryDateAsync(
        ExtendExpiryDateDto extendDto,
        Guid extendedBy,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.ProductAccounts
            .Include(pa => pa.Variant)
            .FirstOrDefaultAsync(pa => pa.ProductAccountId == extendDto.ProductAccountId, cancellationToken);

        if (account == null) throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");

        var now = _clock.UtcNow;
        var oldExpiryDate = account.ExpiryDate;
        
        // Base date for calculation: use current expiry if exists and active, otherwise use now.
        // If account is expired, we might want to extend from NOW, or from old expiry?
        // Usually if expired, you extend from NOW. If active, you extend from expiry.
        // Let's stick to extending from current expiry if it's in the future, else from now?
        // Or simple logic: base = expiry ?? now.
        // If expiry is in the past (expired), adding days to it might still leave it expired or not give full value.
        // Let's assume: if expired, extend from NOW. If active, extend from Expiry.
        var baseDate = (account.ExpiryDate.HasValue && account.ExpiryDate.Value > now) ? account.ExpiryDate.Value : now;

        int daysExtended = 0;
        string extensionSource;

        if (extendDto.NewExpiryDate.HasValue)
        {
            if (extendDto.NewExpiryDate.Value <= baseDate)
            {
               throw new InvalidOperationException(
                    "Ngày hết hạn mới phải lớn hơn ngày hiện tại.");
            }
            account.ExpiryDate = extendDto.NewExpiryDate.Value;
            daysExtended = (int)(account.ExpiryDate.Value - (oldExpiryDate ?? now)).TotalDays;
            extensionSource = "custom_date";
        }
        else
        {
            int daysToAdd = 0;
            
            if (extendDto.DaysToExtend.HasValue && extendDto.DaysToExtend.Value > 0)
            {
                daysToAdd = extendDto.DaysToExtend.Value;
                extensionSource = "custom_days";
            }
            else if (account.Variant?.DurationDays.HasValue == true && account.Variant.DurationDays.Value > 0)
            {
                daysToAdd = account.Variant.DurationDays.Value;
                extensionSource = "variant";
            }
            else
            {
                throw new InvalidOperationException(
                    "Không thể xác định số ngày gia hạn. Vui lòng cung cấp ngày hết hạn mới, số ngày gia hạn hoặc đảm bảo sản phẩm có thời hạn mặc định.");
            }
            
            account.ExpiryDate = baseDate.AddDays(daysToAdd);
            daysExtended = daysToAdd;
        }

        account.UpdatedAt = now;
        account.UpdatedBy = extendedBy;
        
        if (account.Status == nameof(ProductAccountStatus.Expired) && account.ExpiryDate > now)
        {
             var currentActiveUsers = account.ProductAccountCustomers.Count(pac => pac.IsActive);
             if (currentActiveUsers >= account.MaxUsers)
                account.Status = nameof(ProductAccountStatus.Full);
             else
                account.Status = nameof(ProductAccountStatus.Active);
        }

        _productAccountRepository.Update(account);

        // Log history
        string actionNote;
        if (extensionSource == "custom_date")
        {
             actionNote = $"Gia hạn đến {account.ExpiryDate:dd/MM/yyyy} (tùy chỉnh ngày)";
        }
        else if (extensionSource == "variant")
        {
             actionNote = $"Gia hạn thêm {daysExtended} ngày (theo gói)";
        }
        else
        {
             actionNote = $"Gia hạn thêm {daysExtended} ngày (tùy chỉnh)";
        }

        var notes = string.IsNullOrWhiteSpace(extendDto.Notes)
            ? $"{actionNote}. Cũ: {oldExpiryDate:dd/MM/yyyy}, Mới: {account.ExpiryDate:dd/MM/yyyy}"
            : $"{actionNote}. Cũ: {oldExpiryDate:dd/MM/yyyy}, Mới: {account.ExpiryDate:dd/MM/yyyy}. Ghi chú: {extendDto.Notes}";

        var history = new ProductAccountHistory
        {
            ProductAccountId = account.ProductAccountId,
            UserId = null, // System action, not tied to a specific customer
            Action = "ExtendExpiry",
            ActionBy = extendedBy,
            ActionAt = now,
            Notes = notes
        };

        await _productAccountHistoryRepository.AddAsync(history, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(account.ProductAccountId, false, cancellationToken);
    }

    public async Task<List<ProductAccountResponseDto>> GetAccountsExpiringSoonAsync(
        int days = 5,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var futureDate = now.AddDays(days);

        var accounts = await _productAccountRepository.Query()
            .Include(pa => pa.Variant)
                .ThenInclude(v => v.Product)
            .Include(pa => pa.ProductAccountCustomers)
                .ThenInclude(pac => pac.User)
            .Where(pa =>
                pa.ExpiryDate.HasValue &&
                pa.ExpiryDate.Value >= now &&
                pa.ExpiryDate.Value <= futureDate &&
                (pa.Status == nameof(ProductAccountStatus.Active) ||
                 pa.Status == nameof(ProductAccountStatus.Full)))
            .OrderBy(pa => pa.ExpiryDate)
            .ToListAsync(cancellationToken);

        return accounts.Select(account => new ProductAccountResponseDto
        {
            ProductAccountId = account.ProductAccountId,
            ProductId = account.Variant.ProductId,
            VariantId = account.VariantId,
            VariantTitle = account.Variant.Title,
            ProductName = account.Variant.Product.ProductName,
            AccountEmail = account.AccountEmail,
            AccountUsername = account.AccountUsername,
            AccountPassword = "********", // Masked
            MaxUsers = account.MaxUsers,
            CurrentUsers = account.ProductAccountCustomers.Count(pac => pac.IsActive),
            Status = account.Status,
            CogsPrice = account.Variant.CogsPrice,
            SellPrice = account.Variant.SellPrice,
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
        }).ToList();
    }

    public async Task<ProductAccountListResponseDto> GetExpiredAccountsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var query = _productAccountRepository.Query()
            .Include(pa => pa.Variant)
                .ThenInclude(v => v.Product)
            .Include(pa => pa.Supplier)
            .Include(pa => pa.ProductAccountCustomers)
            .Where(pa => pa.Status == nameof(ProductAccountStatus.Expired))
            .AsQueryable();

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var items = await query
            .OrderByDescending(pa => pa.ExpiryDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(pa => new ProductAccountListDto
            {
                ProductAccountId = pa.ProductAccountId,
                VariantId = pa.VariantId,
                VariantTitle = pa.Variant.Title,
                ProductName = pa.Variant.Product.ProductName,
                SupplierId = pa.SupplierId,
                SupplierName = pa.Supplier.Name,
                AccountEmail = pa.AccountEmail,
                AccountUsername = pa.AccountUsername,
                MaxUsers = pa.MaxUsers,
                CurrentUsers = pa.ProductAccountCustomers.Count(pac => pac.IsActive),
                Status = pa.Status,
                CogsPrice = pa.Variant.CogsPrice,
                SellPrice = pa.Variant.SellPrice,
                ExpiryDate = pa.ExpiryDate,
                CreatedAt = pa.CreatedAt,
                OrderId = pa.ProductAccountCustomers.FirstOrDefault(x => x.OrderId.HasValue).OrderId
            })
            .ToListAsync(cancellationToken);

        return new ProductAccountListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            CurrentPage = pageNumber
        };
    }
    


    public async Task<ProductAccountResponseDto> EditExpiryDateAsync(
        EditProductAccountExpiryDto editDto,
        Guid editedBy,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.ProductAccounts
            .Include(pa => pa.Variant)
            .FirstOrDefaultAsync(pa => pa.ProductAccountId == editDto.ProductAccountId, cancellationToken);

        if (account == null) throw new KeyNotFoundException("Không tìm thấy tài khoản sản phẩm");

        // Validate: new expiry date must be >= today + duration
        var now = _clock.UtcNow;
        var today = now.Date;
        
        // If variant has duration, use it. If not, maybe fallback to 0 or 30? Use 0 to be safe (min is just today)
        var durationDays = account.Variant?.DurationDays ?? 0;
        
        // Calculate min date: Today + Duration
        // Example: Today = 13/01. Duration = 30 days. Min = 12/02.
        var minDate = today.AddDays(durationDays);

        // Normalize input date to compare properly (ignore time part of input if needed, but DateTime usually has time)
        // If user sends 2026-02-28 (midnight), and minDate is 2026-02-28, it matches.
        if (editDto.NewExpiryDate.Date < minDate)
        {
            throw new InvalidOperationException(
                $"Ngày hết hạn mới phải từ {minDate:dd/MM/yyyy} trở đi (Hôm nay + {durationDays} ngày theo gói).");
        }

        var oldExpiryDate = account.ExpiryDate;
        
        account.ExpiryDate = editDto.NewExpiryDate;
        account.UpdatedAt = now;
        account.UpdatedBy = editedBy;

        // Auto-update status if expired account is now valid again
        if (account.Status == nameof(ProductAccountStatus.Expired) && account.ExpiryDate > now)
        {
            // Check if full
            var currentActiveUsers = await _productAccountCustomerRepository.Query()
                .CountAsync(pac => pac.ProductAccountId == account.ProductAccountId && pac.IsActive, cancellationToken);
                
            if (currentActiveUsers >= account.MaxUsers)
                account.Status = nameof(ProductAccountStatus.Full);
            else
                account.Status = nameof(ProductAccountStatus.Active);
        }

        _productAccountRepository.Update(account);

        // Log history
        var notes = string.IsNullOrWhiteSpace(editDto.Notes)
            ? $"Sửa ngày hết hạn. Cũ: {oldExpiryDate:dd/MM/yyyy}, Mới: {account.ExpiryDate:dd/MM/yyyy}"
            : $"Sửa ngày hết hạn. Cũ: {oldExpiryDate:dd/MM/yyyy}, Mới: {account.ExpiryDate:dd/MM/yyyy}. Ghi chú: {editDto.Notes}";

        var history = new ProductAccountHistory
        {
            ProductAccountId = account.ProductAccountId,
            UserId = null,
            Action = nameof(ProductAccountAction.UpdateSlot), // Reusing existing action enum for simplicity or "CredentialsUpdated"? 'UpdateSlot' works for admin override. Actually 'CredentialsUpdated' is better as generic update. Or just custom string "EditExpiry".
            ActionBy = editedBy,
            ActionAt = now,
            Notes = notes
        };
        // Override action to be specific
        history.Action = "EditExpiry";

        await _productAccountHistoryRepository.AddAsync(history, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Sync stock is handled by caller (Controller) using VariantStockRecalculator usually
        // but we return the DTO so controller can do it.

        return await GetByIdAsync(account.ProductAccountId, false, cancellationToken);
    }

    private static void ValidateProductAccountEntity(ProductAccount account, bool requireExpiryDate)
    {
        var validationContext = new ValidationContext(account);
        validationContext.Items["RequireExpiryDate"] = requireExpiryDate;

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            account,
            validationContext,
            validationResults,
            true);

        if (!isValid)
        {
            var errorMessage = string.Join(" ", validationResults.Select(r => r.ErrorMessage));
            throw new ValidationException(errorMessage);
        }
    }
}