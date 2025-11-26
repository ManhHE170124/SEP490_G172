// Keytietkiem/DTOs/Cart/StorefrontCartDtos.cs (file anh đang để các DTO cart)

using System;
using System.Collections.Generic;
using System.Linq;

namespace Keytietkiem.DTOs.Cart;

public sealed class AddToCartRequestDto
{
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
}

public sealed class UpdateCartItemRequestDto
{
    public int Quantity { get; set; }
}

public sealed class SetCartReceiverEmailRequestDto
{
    public string ReceiverEmail { get; set; } = string.Empty;
}

public sealed class StorefrontCartItemDto
{
    public Guid VariantId { get; set; }
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;

    public string VariantTitle { get; set; } = string.Empty;
    public string? Thumbnail { get; set; }

    public int Quantity { get; set; }

    /// <summary>Giá niêm yết (ListPrice) tại thời điểm cho vào giỏ</summary>
    public decimal ListPrice { get; set; }

    /// <summary>Giá bán thực tế (SellPrice) tại thời điểm cho vào giỏ</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;

    public decimal ListLineTotal => ListPrice * Quantity;
}

public sealed class StorefrontCartDto
{
    public string? ReceiverEmail { get; init; }

    /// <summary>Thông tin tài khoản đang đăng nhập (nếu có)</summary>
    public string? AccountUserName { get; init; }
    public string? AccountEmail { get; init; }

    public IReadOnlyList<StorefrontCartItemDto> Items { get; init; }
        = Array.Empty<StorefrontCartItemDto>();

    public int TotalQuantity => Items.Sum(i => i.Quantity);

    /// <summary>Tổng tiền sau giảm (dùng giá bán)</summary>
    public decimal TotalAmount => Items.Sum(i => i.LineTotal);

    /// <summary>Tổng tiền theo giá niêm yết</summary>
    public decimal TotalListAmount => Items.Sum(i => i.ListLineTotal);

    /// <summary>Tổng số tiền giảm được (clamp >= 0)</summary>
    public decimal TotalDiscount
    {
        get
        {
            var discount = TotalListAmount - TotalAmount;
            return discount < 0 ? 0 : discount;
        }
    }
    public class CartCheckoutResultDto
    {
        public Guid OrderId { get; set; }
        public string OrderStatus { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string Email { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
