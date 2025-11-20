using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.Models;

/// <summary>
/// Represents a product account that can be shared among multiple customers
/// (e.g., Netflix account, Microsoft 365 account, etc.)
/// </summary>
public partial class ProductAccount : IValidatableObject
{
    public Guid ProductAccountId { get; set; }

    public Guid ProductId { get; set; }

    [EmailAddress]
    [StringLength(254)]
    public string AccountEmail { get; set; } = null!;

    [StringLength(100)]
    public string? AccountUsername { get; set; }

    [Required]
    [StringLength(512)]
    public string AccountPassword { get; set; } = null!;

    [Range(1, 100)]
    public int MaxUsers { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    public DateTime? ExpiryDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CogsPrice { get; set; }

    public decimal? CogsPrice { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductAccountCustomer> ProductAccountCustomers { get; set; } = new List<ProductAccountCustomer>();

    public virtual ICollection<ProductAccountHistory> ProductAccountHistories { get; set; } = new List<ProductAccountHistory>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var errors = new List<ValidationResult>();

        var hasEmail = !string.IsNullOrWhiteSpace(AccountEmail);
        var hasUsername = !string.IsNullOrWhiteSpace(AccountUsername);

        if (!hasEmail && !hasUsername)
        {
            errors.Add(new ValidationResult(
                "Phải cung cấp ít nhất Email hoặc Username.",
                new[] { nameof(AccountEmail), nameof(AccountUsername) }));
        }

        if (ExpiryDate.HasValue)
        {
            if (ExpiryDate.Value.Date < DateTime.UtcNow.Date)
            {
                errors.Add(new ValidationResult(
                    "Ngày hết hạn không được nhỏ hơn ngày hiện tại.",
                    new[] { nameof(ExpiryDate) }));
            }
        }
        else if (validationContext.Items.TryGetValue("RequireExpiryDate", out var requireExpiryObj)
                 && requireExpiryObj is bool requireExpiry
                 && requireExpiry)
        {
            errors.Add(new ValidationResult(
                "Ngày hết hạn là bắt buộc.",
                new[] { nameof(ExpiryDate) }));
        }

        return errors;
    }
}
