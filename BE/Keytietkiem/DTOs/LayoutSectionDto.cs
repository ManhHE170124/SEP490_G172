/**
 * File: LayoutSectionDto.cs
 * Author: TungNVHE170677
 * Created: 26/10/2025
 * Last Updated:
 * Version: 1.0.0
 * Purpose:
 *   Data Transfer Object (DTO) representing customizable layout sections of the website.
 *   Each layout section defines a specific region (e.g., header, footer, sidebar, featured posts area)
 *   which can be toggled, ordered, and managed dynamically.
 */
namespace Keytietkiem.DTOs
{
    public class LayoutSectionDto
    {
        public int Id { get; set; }
        public string SectionKey { get; set; }
        public string SectionName { get; set; }
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
