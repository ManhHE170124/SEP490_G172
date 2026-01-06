/**
 * File: BaseRepository.cs
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 1.0.0
 * Purpose: Base repository class to manage DbContext connection once.
 *          All repositories should inherit from this class to share the same DbContext instance.
 */
using Keytietkiem.Models;

namespace Keytietkiem.Repositories;

/// <summary>
/// Base repository class that provides shared DbContext instance for all repositories.
/// This ensures that all repositories use the same DbContext connection within a scope.
/// </summary>
public abstract class BaseRepository
{
    /// <summary>
    /// Protected DbContext instance shared by all repositories inheriting from this class.
    /// </summary>
    protected readonly KeytietkiemDbContext _context;

    /// <summary>
    /// Initializes a new instance of the BaseRepository class.
    /// </summary>
    /// <param name="context">The database context instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    protected BaseRepository(KeytietkiemDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }
}

