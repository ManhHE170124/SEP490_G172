/**
  File: Product.cs
  Author: Keytietkiem Team
  Created: 16/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Represents products in the e-commerce system. Contains product information,
           pricing, inventory, warranty details, and supplier relationships.
           Supports product management, inventory tracking, and order processing.
  Properties:
    - ProductId (Guid)         : Unique product identifier
    - ProductCode (string)     : Product SKU/code (unique)
    - ProductName (string)     : Product display name
    - SupplierId (int)        : Foreign key to Supplier
    - ProductType (string)     : Type of product (Software/Hardware/etc.)
    - CostPrice (decimal?)     : Product cost price
    - SalePrice (decimal?)     : Product selling price
    - StockQty (int)          : Available stock quantity
    - WarrantyDays (int)      : Warranty period in days
    - ExpiryDate (DateOnly?)   : Product expiration date
    - AutoDelivery (bool)     : Auto-delivery enabled flag
    - Status (string)         : Product status (Active/Inactive/etc.)
    - Description (string)    : Product description
    - CreatedAt (DateTime)    : Product creation timestamp
    - CreatedBy (Guid?)       : Creator user ID
    - UpdatedAt (DateTime?)   : Last update timestamp
    - UpdatedBy (Guid?)       : Last updater user ID
  Relationships:
    - One Supplier (N:1)
    - Many ProductKeys (1:N)
    - Many OrderDetails (1:N)
    - Many Categories (M:N via ProductCategories)
  E-commerce Features:
    - Inventory management
    - Pricing control
    - Warranty tracking
    - Auto-delivery support
    - Product categorization
*/

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
