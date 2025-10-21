/**
  File: Category.cs
  Author: Keytietkiem Team
  Created: 16/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Represents product categories in the e-commerce system. Organizes
           products into hierarchical categories for better navigation and
           management. Supports category-based product filtering and display.
  Properties:
    - CategoryId (int)         : Unique category identifier
    - CategoryCode (string)    : Category code/SKU (unique)
    - CategoryName (string)   : Category display name
    - Description (string)    : Category description
    - DisplayOrder (int)       : Sort order for display
    - IsActive (bool)         : Category activation status
    - CreatedAt (DateTime)    : Category creation timestamp
    - CreatedBy (Guid?)       : Creator user ID
    - UpdatedAt (DateTime?)   : Last update timestamp
    - UpdatedBy (Guid?)       : Last updater user ID
  Relationships:
    - Many Products (M:N via ProductCategories)
  E-commerce Features:
    - Product categorization
    - Category hierarchy support
    - Display ordering
    - Category activation/deactivation
    - Product filtering by category
*/

using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class Category
{
    public int CategoryId { get; set; }

    public string CategoryCode { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
