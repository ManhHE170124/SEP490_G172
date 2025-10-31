using Keytietkiem.Models;

namespace Keytietkiem.Services.Interfaces;

public interface IRoleService
{
    /// <summary>
    /// Seeds default system roles if they do not exist (Admin, Staff, Customer)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SeedDefaultRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active roles
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active roles</returns>
    Task<List<Role>> GetAllActiveRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets role by ID
    /// </summary>
    /// <param name="roleId">Role identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Role if found, null otherwise</returns>
    Task<Role?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a role exists by ID
    /// </summary>
    /// <param name="roleId">Role identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if role exists, false otherwise</returns>
    Task<bool> RoleExistsAsync(string roleId, CancellationToken cancellationToken = default);
}
