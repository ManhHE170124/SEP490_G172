using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Comment
{
    public Guid CommentId { get; set; }

    public Guid? PostId { get; set; }

    public Guid? UserId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public bool? IsApproved { get; set; }

    public virtual Post? Post { get; set; }

    public virtual User? User { get; set; }
}
