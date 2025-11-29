using System.ComponentModel.DataAnnotations.Schema;

namespace Keytietkiem.Models;

public class ProductReport
{
    public Guid Id { get; set; }
    public string Status { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;

    public Guid? ProductKeyId { get; set; }
    
    public ProductKey? ProductKey { get; set; }

    public Guid? ProductAccountId { get; set; }
    
    public ProductAccount? ProductAccount { get; set; }

    public Guid ProductVariantId { get; set; }
    
    public ProductVariant ProductVariant { get; set; } = null!;

    public Guid UserId { get; set; }
    
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}