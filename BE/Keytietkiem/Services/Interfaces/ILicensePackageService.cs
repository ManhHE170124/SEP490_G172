using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Microsoft.AspNetCore.Http;

namespace Keytietkiem.Services.Interfaces;

public interface ILicensePackageService
{
    /// <summary>
    /// Creates a new license package
    /// </summary>
    /// <param name="createDto">License package creation data</param>
    /// <param name="actorId">User ID performing the action</param>
    /// <param name="actorEmail">User email performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created license package information</returns>
    Task<LicensePackageResponseDto> CreateLicensePackageAsync(
        CreateLicensePackageDto createDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing license package
    /// </summary>
    /// <param name="updateDto">License package update data</param>
    /// <param name="actorId">User ID performing the action</param>
    /// <param name="actorEmail">User email performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated license package information</returns>
    Task<LicensePackageResponseDto> UpdateLicensePackageAsync(
        UpdateLicensePackageDto updateDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets license package by ID with full details
    /// </summary>
    /// <param name="packageId">Package identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>License package details</returns>
    Task<LicensePackageResponseDto> GetLicensePackageByIdAsync(
        Guid packageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all license packages with pagination and filtering
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="supplierId">Optional supplier ID filter</param>
    /// <param name="productId">Optional product ID filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of license packages</returns>
    Task<PagedResult<LicensePackageListDto>> GetAllLicensePackagesAsync(
        int pageNumber,
        int pageSize,
        int? supplierId = null,
        Guid? productId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports licenses from a package to stock
    /// </summary>
    /// <param name="importDto">Import request with quantity</param>
    /// <param name="actorId">User ID performing the action</param>
    /// <param name="actorEmail">User email performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ImportLicenseToStockAsync(
        ImportLicenseToStockDto importDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a license package
    /// </summary>
    /// <param name="packageId">Package identifier</param>
    /// <param name="actorId">User ID performing the action</param>
    /// <param name="actorEmail">User email performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteLicensePackageAsync(
        Guid packageId,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads CSV file containing license keys and imports them to stock
    /// </summary>
    /// <param name="packageId">Package identifier</param>
    /// <param name="supplierId">Supplier identifier</param>
    /// <param name="file">CSV file containing license keys</param>
    /// <param name="actorId">User ID performing the action</param>
    /// <param name="actorEmail">User email performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload and import result</returns>
    Task<CsvUploadResultDto> UploadLicenseCsvAsync(
        Guid packageId,
        int supplierId,
        IFormFile file,
        Guid actorId,
        string actorEmail,
        string keyType,
        DateTime? expiryDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all license keys imported from a specific package
    /// </summary>
    /// <param name="packageId">Package identifier</param>
    /// <param name="supplierId">Supplier identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of license keys with details</returns>
    Task<LicenseKeysListResponseDto> GetLicenseKeysByPackageAsync(
        Guid packageId,
        int supplierId,
        CancellationToken cancellationToken = default);
}
