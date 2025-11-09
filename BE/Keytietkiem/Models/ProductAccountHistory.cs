namespace Keytietkiem.Models;

/// <summary>
/// Tracks the history of customer additions and removals from product accounts
/// Provides audit trail for account sharing management
/// </summary>
public partial class ProductAccountHistory
{
    public long HistoryId { get; set; }

    public Guid ProductAccountId { get; set; }

    public Guid UserId { get; set; }

    public string Action { get; set; } = null!;

    public Guid ActionBy { get; set; }

    public DateTime ActionAt { get; set; }

    public string? Notes { get; set; }

    // Navigation properties
    public virtual ProductAccount ProductAccount { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
