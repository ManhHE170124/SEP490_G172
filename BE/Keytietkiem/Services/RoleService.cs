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
