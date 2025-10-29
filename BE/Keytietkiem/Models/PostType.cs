using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class PostType
{
    public int PostTypeId { get; set; }

    public string PostTypeName { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}
