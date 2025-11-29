namespace Keytietkiem.DTOs.Enums;

/// <summary>
/// Status of a product report
/// </summary>
public enum ProductReportStatus
{
    /// <summary>
    /// Report has been submitted and is awaiting review
    /// </summary>
    Pending,

    /// <summary>
    /// Report is currently being processed/investigated
    /// </summary>
    Processing,

    /// <summary>
    /// Report has been resolved
    /// </summary>
    Resolved
}
