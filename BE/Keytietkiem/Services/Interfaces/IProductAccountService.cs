using Keytietkiem.DTOs;

namespace Keytietkiem.Services.Interfaces;

/// <summary>
/// Service interface for managing product accounts (shared accounts)
/// </summary>
public interface IProductAccountService
{
    /// <summary>
    /// Get paginated list of product accounts with filters
    /// </summary>
    Task<ProductAccountListResponseDto> GetListAsync(ProductAccountFilterDto filterDto, CancellationToken cancellationToken = default);
    
    Task<(Guid?, bool)> CheckAccountEmailOrUsernameExists(Guid productId, string email, string? username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single product account by ID with full details
    /// </summary>
    Task<ProductAccountResponseDto> GetByIdAsync(Guid productAccountId, bool includePassword = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new product account
    /// </summary>
    Task<ProductAccountResponseDto> CreateAsync(CreateProductAccountDto createDto, Guid createdBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing product account
    /// </summary>
    Task<ProductAccountResponseDto> UpdateAsync(UpdateProductAccountDto updateDto, Guid updatedBy, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a product account
    /// </summary>
    Task DeleteAsync(Guid productAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a customer to a product account
    /// </summary>
    Task<ProductAccountCustomerDto> AddCustomerAsync(AddCustomerToAccountDto addDto, Guid addedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a customer from a product account
    /// </summary>
    Task RemoveCustomerAsync(RemoveCustomerFromAccountDto removeDto, Guid removedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get history of a product account
    /// </summary>
    Task<ProductAccountHistoryResponseDto> GetHistoryAsync(Guid productAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the decrypted password for a product account
    /// </summary>
    Task<string> GetDecryptedPasswordAsync(Guid productAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign a product account to an order by adding the user as a customer
    /// </summary>
    Task<ProductAccountCustomerDto> AssignAccountToOrderAsync(AssignAccountToOrderDto assignDto, Guid assignedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extend the expiry date of a product account
    /// </summary>
    Task<ProductAccountResponseDto> ExtendExpiryDateAsync(ExtendExpiryDateDto extendDto, Guid extendedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get product accounts expiring within specified days
    /// </summary>
    Task<List<ProductAccountResponseDto>> GetAccountsExpiringSoonAsync(int days = 5, CancellationToken cancellationToken = default);
}
