/**
 * File: ModuleDTO.cs
 * Author: HieuNDHE173169
 * Created: 20/10/2025
 * Last Updated: 20/10/2025
 * Version: 1.0.0
 * Purpose: Data Transfer Object for Module operations. Provides a clean interface
 *          for API communication without exposing internal entity structure.
 *          Supports module creation, updates, and responses.
 * Properties:
 *   - ModuleId (long)          : Unique module identifier
 *   - ModuleName (string)      : Module name (unique)
 *   - Description (string)    : Module description
 *   - CreatedAt (DateTime)     : Module creation timestamp
 *   - UpdatedAt (DateTime?)    : Last update timestamp
 * Usage:
 *   - Input DTO for module creation/updates
 *   - Output DTO for module responses
 *   - Validation and data transfer
 */

using System;

namespace Keytietkiem.DTOs
{
    public class ModuleDTO
    {
        public long ModuleId { get; set; }
        public string ModuleName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateModuleDTO
    {
        public string ModuleName { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class UpdateModuleDTO
    {
        public string ModuleName { get; set; } = null!;
        public string? Description { get; set; }
    }
}
