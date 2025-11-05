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
