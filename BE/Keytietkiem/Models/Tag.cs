using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Tag
{
    public Guid TagId { get; set; }

    public string TagName { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}
