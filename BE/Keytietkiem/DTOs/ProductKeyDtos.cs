using System;
using System.ComponentModel.DataAnnotations;
using Keytietkiem.DTOs.Enums;

namespace Keytietkiem.DTOs
{
    /// <summary>
    /// DTO for creating a new product key
    /// </summary>
    public class CreateProductKeyDto
    {
        [Required(ErrorMessage = "ID sản phẩm là bắt buộc")]
        public Guid ProductId { get; set; }

        [Required(ErrorMessage = "ID nhà cung cấp là bắt buộc")]
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "License key là bắt buộc")]
        [StringLength(500, ErrorMessage = "License key không được vượt quá 500 ký tự")]
        public string KeyString { get; set; } = string.Empty;

        public string Type { get; set; } = nameof(ProductKeyType.Individual);

        public DateTime? ExpiryDate { get; set; }

        [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing product key
    /// </summary>
    public class UpdateProductKeyDto
    {
        [Required(ErrorMessage = "ID key là bắt buộc")]
        public Guid KeyId { get; set; }

        [Required(ErrorMessage = "Trạng thái là bắt buộc")]
        [StringLength(50, ErrorMessage = "Trạng thái không được vượt quá 50 ký tự")]
        public string Status { get; set; } = string.Empty; // Available, Sold, Error, Recalled

        public DateTime? ExpiryDate { get; set; }

        [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO for product key detail information
    /// </summary>
    public class ProductKeyDetailDto
    {
        public Guid KeyId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string KeyString { get; set; } = string.Empty;
        public string Type { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public string? Notes { get; set; }
        public Guid? AssignedToOrderId { get; set; }
        public string? OrderCode { get; set; }
        public DateTime ImportedAt { get; set; }
        public Guid? ImportedBy { get; set; }
        public string? ImportedByEmail { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO for product key list item
    /// </summary>
    public class ProductKeyListDto
    {
        public Guid KeyId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSku { get; set; } = string.Empty;
        public string KeyString { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; }
        public string? OrderCode { get; set; }
        public DateTime? ImportedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO for filtering product keys
    /// </summary>
    public class ProductKeyFilterDto
    {
        public string? SearchTerm { get; set; } // Search by product name, SKU, key string
        public Guid? ProductId { get; set; }
        public int? SupplierId { get; set; }
        public string? Status { get; set; } // Available, Sold, Error, Recalled
        public string? Type { get; set; } // Individual or Pool
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// DTO for paginated product key list response
    /// </summary>
    public class ProductKeyListResponseDto
    {
        public List<ProductKeyListDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    /// <summary>
    /// DTO for assigning key to order
    /// </summary>
    public class AssignKeyToOrderDto
    {
        [Required(ErrorMessage = "ID key là bắt buộc")]
        public Guid KeyId { get; set; }

        [Required(ErrorMessage = "ID đơn hàng là bắt buộc")]
        public Guid OrderId { get; set; }
    }

    /// <summary>
    /// DTO for bulk update key status
    /// </summary>
    public class BulkUpdateKeyStatusDto
    {
        [Required(ErrorMessage = "Danh sách ID key là bắt buộc")]
        [MinLength(1, ErrorMessage = "Phải có ít nhất 1 key")]
        public List<Guid> KeyIds { get; set; } = new();

        [Required(ErrorMessage = "Trạng thái là bắt buộc")]
        [StringLength(50, ErrorMessage = "Trạng thái không được vượt quá 50 ký tự")]
        public string Status { get; set; } = string.Empty;
    }
}