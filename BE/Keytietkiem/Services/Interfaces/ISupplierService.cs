using Keytietkiem.DTOs;

namespace Keytietkiem.Services.Interfaces;

public interface ISupplierService
{
    /// <summary>
    /// Creates a new supplier with validation
    /// </summary>
    /// <param name="createDto">Supplier creation data</param>
    /// <param name="actorId">User ID performing the action</param>
    /// <param name="actorEmail">User email performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created supplier information</returns>
    Task<SupplierResponseDto> CreateSupplierAsync(
        CreateSupplierDto createDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing supplier
    /// </summary>
    /// <param name="updateDto">Supplier update data</param>
    /// <param name="actorId">User ID performing the action</param>
    /// <param name="actorEmail">User email performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated supplier information</returns>
    Task<SupplierResponseDto> UpdateSupplierAsync(
        UpdateSupplierDto updateDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets supplier by ID with full details
    /// </summary>
    /// <param name="supplierId">Supplier identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Supplier details</returns>
    Task<SupplierResponseDto> GetSupplierByIdAsync(int supplierId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all suppliers with pagination
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="searchTerm">Optional search term for name/email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of suppliers</returns>
    Task<PagedResult<SupplierListDto>> GetAllSuppliersAsync(
        int pageNumber,
        int pageSize,
        string? status,
        string? searchTerm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if supplier can be deactivated
    /// </summary>
    /// <param name="supplierId">Supplier identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with affected products</returns>
    Task<DeactivateSupplierValidationDto> ValidateDeactivationAsync(
        int supplierId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a supplier (soft delete by removing all product key relationships)
    /// </summary>
    /// <param name="deactivateDto">Deactivation request with confirmation</param>
    /// <param name="actorId">User ID performing the action</param>
    /// <param name="actorEmail">User email performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeactivateSupplierAsync(
        DeactivateSupplierDto deactivateDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles supplier status (Active/Deactive)
    /// </summary>
    /// <param name="supplierId">Supplier identifier</param>
    /// <param name="actorId">User ID performing the action</param>
    /// <param name="actorEmail">User email performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated supplier information</returns>
    Task<SupplierResponseDto> ToggleSupplierStatusAsync(
        int supplierId,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if supplier name already exists
    /// </summary>
    /// <param name="name">Supplier name</param>
    /// <param name="excludeSupplierId">Supplier ID to exclude (for updates)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if name exists, false otherwise</returns>
    Task<bool> IsSupplierNameExistsAsync(
        string name,
        int? excludeSupplierId,
        CancellationToken cancellationToken = default);
}
