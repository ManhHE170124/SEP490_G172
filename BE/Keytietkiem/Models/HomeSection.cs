using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class HomeSection
{
    public long SectionId { get; set; }

    public string SectionKey { get; set; } = null!;

    public string Title { get; set; } = null!;

    public int Order { get; set; }

    public bool IsVisible { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
