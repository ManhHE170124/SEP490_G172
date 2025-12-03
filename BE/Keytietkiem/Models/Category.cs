using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Category
{
    public int CategoryId { get; set; }

    public string CategoryCode { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<Faq> Faqs { get; set; } = new List<Faq>();

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
