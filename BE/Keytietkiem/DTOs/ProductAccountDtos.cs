using System.ComponentModel.DataAnnotations;

namespace Keytietkiem.DTOs;

/// <summary>
/// Request DTO for creating a new shareable product account
/// </summary>
public class CreateProductAccountDto
{
    [Required(ErrorMessage = "ID biến thể sản phẩm là bắt buộc")]
    public Guid VariantId { get; set; }

    [Required(ErrorMessage = "Email tài khoản là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự")]
    public string AccountEmail { get; set; } = null!;

    [StringLength(100, ErrorMessage = "Tên tài khoản không được vượt quá 100 ký tự")]
    public string? AccountUsername { get; set; }

    [Required(ErrorMessage = "Mật khẩu tài khoản là bắt buộc")]
    [StringLength(512, ErrorMessage = "Mật khẩu không được vượt quá 512 ký tự")]
    public string AccountPassword { get; set; } = null!;

    [Required(ErrorMessage = "Số lượng người dùng tối đa là bắt buộc")]
    [Range(1, 100, ErrorMessage = "Số lượng người dùng phải từ 1 đến 100")]
    public int MaxUsers { get; set; }

    /// <summary>
    /// COGS price - will update the Variant's CogsPrice
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Giá vốn phải lớn hơn hoặc bằng 0")]
    public decimal? CogsPrice { get; set; }

    public DateTime StartDate { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự")]
    public string? Notes { get; set; }
}

/// <summary>
/// Request DTO for updating an existing product account
/// </summary>
public class UpdateProductAccountDto
{
    [Required(ErrorMessage = "ID tài khoản là bắt buộc")]
    public Guid ProductAccountId { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự")]
    public string? AccountEmail { get; set; }

    [StringLength(100, ErrorMessage = "Tên tài khoản không được vượt quá 100 ký tự")]
    public string? AccountUsername { get; set; }

    [StringLength(512, ErrorMessage = "Mật khẩu không được vượt quá 512 ký tự")]
    public string? AccountPassword { get; set; }

    [Range(1, 100, ErrorMessage = "Số lượng người dùng phải từ 1 đến 100")]
    public int? MaxUsers { get; set; }

    [StringLength(20, ErrorMessage = "Trạng thái không được vượt quá 20 ký tự")]
    public string? Status { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự")]
    public string? Notes { get; set; }
}

/// <summary>
/// Response DTO for product account details
/// </summary>
public class ProductAccountResponseDto
{
    public Guid ProductAccountId { get; set; }
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }
    public string VariantTitle { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
    public string? AccountUsername { get; set; }
    public string AccountPassword { get; set; } = string.Empty;
    public int MaxUsers { get; set; }
    public int CurrentUsers { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal CogsPrice { get; set; }
    public decimal SellPrice { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public List<ProductAccountCustomerDto> Customers { get; set; } = new();
}

/// <summary>
/// Simplified product account info for list views
/// </summary>
public class ProductAccountListDto
{
    public Guid ProductAccountId { get; set; }
    public Guid VariantId { get; set; }
    public string VariantTitle { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
    public string? AccountUsername { get; set; }
    public int MaxUsers { get; set; }
    public int CurrentUsers { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal CogsPrice { get; set; }
    public decimal SellPrice { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? OrderId { get; set; }
}

/// <summary>
/// DTO for customer information in product account
/// </summary>
public class ProductAccountCustomerDto
{
    public long ProductAccountCustomerId { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string? UserFullName { get; set; }
    public DateTime AddedAt { get; set; }
    public Guid AddedBy { get; set; }
    public string? AddedByEmail { get; set; }
    public DateTime? RemovedAt { get; set; }
    public Guid? RemovedBy { get; set; }
    public string? RemovedByEmail { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request DTO for adding a customer to a product account
/// </summary>
public class AddCustomerToAccountDto
{
    [Required(ErrorMessage = "ID tài khoản sản phẩm là bắt buộc")]
    public Guid ProductAccountId { get; set; }

    [Required(ErrorMessage = "ID người dùng là bắt buộc")]
    public Guid UserId { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
    public string? Notes { get; set; }
}

/// <summary>
/// Request DTO for removing a customer from a product account
/// </summary>
public class RemoveCustomerFromAccountDto
{
    [Required(ErrorMessage = "ID tài khoản sản phẩm là bắt buộc")]
    public Guid ProductAccountId { get; set; }

    [Required(ErrorMessage = "ID người dùng là bắt buộc")]
    public Guid UserId { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for product account history record
/// </summary>
public class ProductAccountHistoryDto
{
    public long HistoryId { get; set; }
    public Guid ProductAccountId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid ActionBy { get; set; }
    public string? ActionByEmail { get; set; }
    public DateTime ActionAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Response DTO for product account history list
/// </summary>
public class ProductAccountHistoryResponseDto
{
    public Guid ProductAccountId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
    public int TotalHistoryRecords { get; set; }
    public List<ProductAccountHistoryDto> History { get; set; } = new();
}

/// <summary>
/// DTO for product account list filters
/// </summary>
public class ProductAccountFilterDto
{
    public string? SearchTerm { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? ProductId { get; set; }
    public string? Status { get; set; }
    // Filter by product type of the linked Product (e.g., SHARED_ACCOUNT, PERSONAL_ACCOUNT)
    public string? ProductType { get; set; }
    public IEnumerable<string>? ProductTypes { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// DTO for paginated product account list response
/// </summary>
public class ProductAccountListResponseDto
{
    public List<ProductAccountListDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
}

/// <summary>
/// DTO for assigning product account to order
/// </summary>
public class AssignAccountToOrderDto
{
    [Required(ErrorMessage = "ID tài khoản sản phẩm là bắt buộc")]
    public Guid ProductAccountId { get; set; }

    [Required(ErrorMessage = "ID đơn hàng là bắt buộc")]
    public Guid OrderId { get; set; }

    [Required(ErrorMessage = "ID người dùng là bắt buộc")]
    public Guid UserId { get; set; }
}

/// <summary>
/// DTO for extending expiry date of product account
/// </summary>
public class ExtendExpiryDateDto
{
    [Required(ErrorMessage = "ID tài khoản sản phẩm là bắt buộc")]
    public Guid ProductAccountId { get; set; }

    /// <summary>
    /// Number of days to extend (optional). If not provided, will use DurationDays from ProductVariant.
    /// </summary>
    [Range(1, 3650, ErrorMessage = "Số ngày gia hạn phải từ 1 đến 3650 (10 năm)")]
    public int? DaysToExtend { get; set; }

    public string? Notes { get; set; }
}
