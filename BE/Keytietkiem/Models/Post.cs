using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Post
{
    public Guid PostId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? ShortDescription { get; set; }

    public string? Content { get; set; }

    public string? Thumbnail { get; set; }

    public int? PostTypeId { get; set; }

    public Guid? AuthorId { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public string? Status { get; set; }

    public int? ViewCount { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? Author { get; set; }

    public virtual PostType? PostType { get; set; }

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
