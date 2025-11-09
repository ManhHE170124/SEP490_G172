namespace Keytietkiem.DTOs.Enums;

/// <summary>
/// Status of a product account (shared account)
/// </summary>
public enum ProductAccountStatus
{
    /// <summary>
    /// Account is active and available for use
    /// </summary>
    Active,

    /// <summary>
    /// Account is full (reached max users)
    /// </summary>
    Full,

    /// <summary>
    /// Account has expired
    /// </summary>
    Expired,

    /// <summary>
    /// Account has an error or is not working
    /// </summary>
    Error,

    /// <summary>
    /// Account is inactive/disabled
    /// </summary>
    Inactive
}
