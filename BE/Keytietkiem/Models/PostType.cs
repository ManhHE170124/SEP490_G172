using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class PostType
{
    public Guid PostTypeId { get; set; }

    public string PostTypeName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}
