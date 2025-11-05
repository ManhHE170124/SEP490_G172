namespace Keytietkiem.Models;

/// <summary>
/// Junction table managing the many-to-many relationship between ProductAccounts and Users/Customers
/// Tracks which customers have access to which product accounts
/// </summary>
public partial class ProductAccountCustomer
{
    public long ProductAccountCustomerId { get; set; }

    public Guid ProductAccountId { get; set; }

    public Guid UserId { get; set; }

    public DateTime AddedAt { get; set; }

    public Guid AddedBy { get; set; }

    public DateTime? RemovedAt { get; set; }

    public Guid? RemovedBy { get; set; }

    public bool IsActive { get; set; }

    public string? Notes { get; set; }

    // Navigation properties
    public virtual ProductAccount ProductAccount { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
