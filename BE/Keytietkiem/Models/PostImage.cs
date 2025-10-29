using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class PostImage
{
    public Guid ImageId { get; set; }

    public Guid PostId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? Caption { get; set; }

    public int? DisplayOrder { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Post Post { get; set; } = null!;
}
