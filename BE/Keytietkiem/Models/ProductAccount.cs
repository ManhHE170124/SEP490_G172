namespace Keytietkiem.Models;

/// <summary>
/// Represents a product account that can be shared among multiple customers
/// (e.g., Netflix account, Microsoft 365 account, etc.)
/// </summary>
public partial class ProductAccount
{
    public Guid ProductAccountId { get; set; }

    public Guid ProductId { get; set; }

    public string AccountEmail { get; set; } = null!;

    public string? AccountUsername { get; set; }

    public string AccountPassword { get; set; } = null!;

    public int MaxUsers { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? ExpiryDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductAccountCustomer> ProductAccountCustomers { get; set; } = new List<ProductAccountCustomer>();

    public virtual ICollection<ProductAccountHistory> ProductAccountHistories { get; set; } = new List<ProductAccountHistory>();
}
