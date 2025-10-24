/**
 * File: TagDTO.cs
 * Author: HieuNDHE173169
 * Created: 21/10/2025
 * Last Updated: 24/10/2025
 * Version: 1.0.0
 * Purpose: Data Transfer Object for Tag operations. Provides a clean interface
 *          for API communication without exposing internal entity structure.
 *          Supports tag creation, updates, and responses.
 * Usage:
 *   - Input DTO for tag creation/updates
 *   - Output DTO for tag responses
 *   - Validation and data transfer
 */

using System;

namespace Keytietkiem.DTOs
{
    public class TagDTO
    {
        public int TagId { get; set; }
        public string TagName { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateTagDTO
    {
        public string TagName { get; set; } = null!;
        public string Slug { get; set; } = null!;
    }

    public class UpdateTagDTO
    {
        public string TagName { get; set; } = null!;
        public string Slug { get; set; } = null!;
    }
}
