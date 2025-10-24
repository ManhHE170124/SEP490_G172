/**
 * File: PermissionDTO.cs
 * Author: HieuNDHE173169
 * Created: 20/10/2025
 * Last Updated: 20/10/2025
 * Version: 1.0.0
 * Purpose: Data Transfer Object for Permission operations. Provides a clean interface
 *          for API communication without exposing internal entity structure.
 *          Supports permission creation, updates, and responses.
 * Properties:
 *   - PermissionId (long)      : Unique permission identifier
 *   - PermissionName (string)  : Permission name (unique)
 *   - Description (string)    : Detailed permission description
 *   - CreatedAt (DateTime)    : Permission creation timestamp
 *   - UpdatedAt (DateTime?)    : Last update timestamp
 * Usage:
 *   - Input DTO for permission creation/updates
 *   - Output DTO for permission responses
 *   - Validation and data transfer
 */

using System;

namespace Keytietkiem.DTOs
{
    public class PermissionDTO
    {
        public long PermissionId { get; set; }
        public string PermissionName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreatePermissionDTO
    {
        public string PermissionName { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class UpdatePermissionDTO
    {
        public string PermissionName { get; set; } = null!;
        public string? Description { get; set; }
    }
}
