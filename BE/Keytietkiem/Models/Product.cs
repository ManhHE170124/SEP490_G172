using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Product
{
    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public int SupplierId { get; set; }

    public string ProductType { get; set; } = null!;

    public decimal? CostPrice { get; set; }

    public decimal? SalePrice { get; set; }

    public int StockQty { get; set; }

    public int WarrantyDays { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public bool AutoDelivery { get; set; }

    public string Status { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<ProductKey> ProductKeys { get; set; } = new List<ProductKey>();

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
}
