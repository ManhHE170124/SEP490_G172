using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Keytietkiem.Models;

[Table("PostComments")]
public partial class PostComment
{
    public Guid CommentId { get; set; }

    public Guid? PostId { get; set; }

    public Guid? UserId { get; set; }

    public Guid? ParentCommentId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public bool? IsApproved { get; set; }

    public virtual Post? Post { get; set; }

    public virtual User? User { get; set; }

    public virtual PostComment? ParentComment { get; set; }

    public virtual ICollection<PostComment> Replies { get; set; } = new List<PostComment>();
}

