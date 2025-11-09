namespace Keytietkiem.DTOs.Enums;

/// <summary>
/// Actions that can be performed on product account customers
/// </summary>
public enum ProductAccountAction
{
    /// <summary>
    /// Customer was added to the account
    /// </summary>
    Added,

    /// <summary>
    /// Customer was removed from the account
    /// </summary>
    Removed,

    /// <summary>
    /// Account credentials were updated
    /// </summary>
    CredentialsUpdated,

    /// <summary>
    /// Account status was changed
    /// </summary>
    StatusChanged
}
