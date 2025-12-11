using Keytietkiem.DTOs;

namespace Keytietkiem.Services.Interfaces;

public interface IProductReportService
{
    /// <summary>
    /// Creates a new product report
    /// </summary>
    /// <param name="createDto">Product report creation data</param>
    /// <param name="userId">User ID performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created product report information</returns>
    Task<ProductReportResponseDto> CreateProductReportAsync(
        CreateProductReportDto createDto,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a product report
    /// </summary>
    /// <param name="updateDto">Product report update data</param>
    /// <param name="actorId">User ID performing the action</param>
    /// <param name="actorEmail">User email performing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated product report information</returns>
    Task<ProductReportResponseDto> UpdateProductReportStatusAsync(
        UpdateProductReportDto updateDto,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product report by ID with full details
    /// </summary>
    /// <param name="id">Product report identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Product report details</returns>
    Task<ProductReportResponseDto> GetProductReportByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all product reports with pagination and filtering
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="userId">Optional user ID filter</param>
    /// <param name="searchTerm">Optional search term for title and email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of product reports</returns>
    Task<PagedResult<ProductReportListDto>> GetAllProductReportsAsync(
        int pageNumber,
        int pageSize,
        string? status,
        Guid? userId,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets key error reports with pagination (reports with ProductKeyId)
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="searchTerm">Optional search term for title and email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of key error reports</returns>
    Task<PagedResult<ProductReportResponseDto>> GetKeyErrorsAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets account error reports with pagination (reports with ProductAccountId)
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="searchTerm">Optional search term for title and email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of account error reports</returns>
    Task<PagedResult<ProductReportResponseDto>> GetAccountErrorsAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of key error reports
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total count of key errors</returns>
    Task<int> CountKeyErrorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of account error reports
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total count of account errors</returns>
    Task<int> CountAccountErrorsAsync(CancellationToken cancellationToken = default);
}
