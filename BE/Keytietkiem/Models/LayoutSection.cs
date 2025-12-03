using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class LayoutSection
{
    public int Id { get; set; }

    public string SectionKey { get; set; } = null!;

    public string SectionName { get; set; } = null!;

    public int? DisplayOrder { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
