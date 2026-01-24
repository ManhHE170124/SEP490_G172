using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Services;

/// <summary>
/// Service implementation for role operations.
/// Handles role seeding and retrieval.
/// </summary>
 public class RoleService : IRoleService
{
    private readonly KeytietkiemDbContext _context;
    private readonly IGenericRepository<Role> _roleRepository;
    private readonly IClock _clock;
    private readonly ILogger<RoleService> _logger;

    private static readonly string[] DefaultRoles = { "Admin", "Storage Staff", "Customer", "Content Creator", "Customer Care Staff" };

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleService"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="roleRepository">The role repository.</param>
    /// <param name="clock">The clock for timestamps.</param>
    /// <param name="logger">The logger.</param>
    public RoleService(
        KeytietkiemDbContext context,
        IGenericRepository<Role> roleRepository,
        IClock clock,
        ILogger<RoleService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Seeds default roles if they don't exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SeedDefaultRolesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking default roles (Admin, Staff, Customer)...");

        var existingRoleIds = await _context.Roles
            .Where(r => DefaultRoles.Contains(r.RoleId))
            .Select(r => r.RoleId)
            .ToListAsync(cancellationToken);

        var missingRoles = DefaultRoles
            .Where(roleId => !existingRoleIds.Contains(roleId))
            .ToList();

        if (missingRoles.Count == 0)
        {
            _logger.LogInformation("All default roles already exist");
            return;
        }

        _logger.LogInformation("Seeding {Count} missing roles: {Roles}", missingRoles.Count, string.Join(", ", missingRoles));

        var rolesToAdd = missingRoles.Select(roleId => new Role
        {
            RoleId = roleId,
            Name = roleId,
            Code = roleId.ToUpper().Replace(" ", "_"), // Set Code for permission checks
            IsSystem = true,
            IsActive = true,
            CreatedAt = _clock.UtcNow
        }).ToList();

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.Roles.AddRangeAsync(rolesToAdd, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully seeded {Count} default roles", rolesToAdd.Count);
            
            // Update existing roles that might be missing Code
            var existingRolesWithoutCode = await _context.Roles
                .Where(r => string.IsNullOrWhiteSpace(r.Code) && DefaultRoles.Contains(r.RoleId))
                .ToListAsync(cancellationToken);

            if (existingRolesWithoutCode.Any())
            {
                _logger.LogInformation("Updating Code for {Count} existing roles...", existingRolesWithoutCode.Count);
                foreach (var role in existingRolesWithoutCode)
                {
                    role.Code = role.RoleId.ToUpper().Replace(" ", "_");
                    role.UpdatedAt = _clock.UtcNow;
                }
                _context.Roles.UpdateRange(existingRolesWithoutCode);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully updated Code for existing roles.");
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to seed default roles");
            throw;
        }
    }

    /// <summary>
    /// Retrieves all active roles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active roles.</returns>
    public async Task<List<Role>> GetAllActiveRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a role by its identifier.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role if found, otherwise null.</returns>
    public async Task<Role?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        return await _roleRepository.GetByIdAsync(roleId, cancellationToken);
    }

    /// <summary>
    /// Checks if a role exists.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the role exists, otherwise false.</returns>
    public async Task<bool> RoleExistsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        return await _roleRepository.AnyAsync(r => r.RoleId == roleId, cancellationToken);
    }
}
