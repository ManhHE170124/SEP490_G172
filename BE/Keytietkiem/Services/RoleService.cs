using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Services;

public class RoleService : IRoleService
{
    private readonly KeytietkiemDbContext _context;
    private readonly IGenericRepository<Role> _roleRepository;
    private readonly IClock _clock;
    private readonly ILogger<RoleService> _logger;

    private static readonly string[] DefaultRoles = { "Admin", "Storage Staff", "Customer", "Content Creator", "Customer Care Staff" };

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

    public async Task SeedDefaultRolesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking default roles (Admin, Staff, Customer)...");

        // Map RoleId to Code
        var roleCodeMap = new Dictionary<string, string>
        {
            { "Admin", "ADMIN" },
            { "Storage Staff", "STORAGE_STAFF" },
            { "Customer", "CUSTOMER" },
            { "Content Creator", "CONTENT_CREATOR" },
            { "Customer Care Staff", "CUSTOMER_CARE" }
        };

        var existingRoles = await _context.Roles
            .Where(r => DefaultRoles.Contains(r.RoleId))
            .ToListAsync(cancellationToken);

        var existingRoleIds = existingRoles.Select(r => r.RoleId).ToList();

        // Update Code for existing roles that don't have Code set
        var rolesToUpdate = existingRoles
            .Where(r => string.IsNullOrWhiteSpace(r.Code) && roleCodeMap.ContainsKey(r.RoleId))
            .ToList();

        if (rolesToUpdate.Any())
        {
            _logger.LogInformation("Updating Code for {Count} existing roles", rolesToUpdate.Count);
            foreach (var role in rolesToUpdate)
            {
                role.Code = roleCodeMap[role.RoleId];
                role.UpdatedAt = _clock.UtcNow;
            }
            _context.Roles.UpdateRange(rolesToUpdate);
            await _context.SaveChangesAsync(cancellationToken);
        }

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
            Code = roleCodeMap.TryGetValue(roleId, out var code) ? code : roleId.ToUpper().Replace(" ", "_"),
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
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to seed default roles");
            throw;
        }
    }

    public async Task<List<Role>> GetAllActiveRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Role?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        return await _roleRepository.GetByIdAsync(roleId, cancellationToken);
    }

    public async Task<bool> RoleExistsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        return await _roleRepository.AnyAsync(r => r.RoleId == roleId, cancellationToken);
    }
}
