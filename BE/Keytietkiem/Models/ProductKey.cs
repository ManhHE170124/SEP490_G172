/**
  File: ProductKey.cs
  Author: Keytietkiem Team
  Created: 16/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Represents product keys/licenses in the e-commerce system. Contains
           digital product keys, their status, and tracking information.
           Supports key management, delivery automation, and order fulfillment.
  Properties:
    - KeyId (Guid)            : Unique key identifier
    - ProductId (Guid)        : Foreign key to Product
    - KeyString (string)      : The actual product key/license (unique)
    - Status (string)         : Key status (Available/Used/Expired/etc.)
    - ImportedBy (Guid?)     : User who imported the key
    - ImportedAt (DateTime)   : Key import timestamp
  Relationships:
    - One Product (N:1)
    - Many OrderDetails (1:N)
  E-commerce Features:
    - Digital product delivery
    - Key status tracking
    - Import management
    - Order fulfillment
    - License management
*/

using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class ProductKey
{
    public Guid KeyId { get; set; }

    public Guid ProductId { get; set; }

    public string KeyString { get; set; } = null!;

    public string Status { get; set; } = null!;

    public Guid? ImportedBy { get; set; }

    public DateTime ImportedAt { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual Product Product { get; set; } = null!;
}
