// Keytietkiem/DTOs/Cart/StorefrontCartDtos.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Keytietkiem.DTOs.Cart
{
    public sealed class AddToCartRequestDto
    {
        public Guid VariantId { get; set; }
        public int Quantity { get; set; }
    }

    // FE gọi PUT /items/{variantId} với body { quantity }
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
        public long CartItemId { get; set; }
        public Guid VariantId { get; set; }
        public Guid ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;

        public string VariantTitle { get; set; } = string.Empty;
        public string? Thumbnail { get; set; }
        public string Slug { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal ListPrice { get; set; }
        public decimal UnitPrice { get; set; }

        public decimal LineTotal => UnitPrice * Quantity;
        public decimal ListLineTotal => ListPrice * Quantity;
    }

    public sealed class StorefrontCartDto
    {
        public Guid CartId { get; init; }
        public string Status { get; init; } = "Active";
        public DateTime UpdatedAt { get; init; }

        // Guest nhập mail, logged-in có thể prefill
        public string? ReceiverEmail { get; init; }

        public string? AccountUserName { get; init; }
        public string? AccountEmail { get; init; }

        public IReadOnlyList<StorefrontCartItemDto> Items { get; init; } = Array.Empty<StorefrontCartItemDto>();

        public int TotalQuantity => Items.Sum(i => i.Quantity);
        public decimal TotalAmount => Items.Sum(i => i.LineTotal);
        public decimal TotalListAmount => Items.Sum(i => i.ListLineTotal);

        public decimal TotalDiscount
        {
            get
            {
                var discount = TotalListAmount - TotalAmount;
                return discount < 0 ? 0 : discount;
            }
        }
    }
}
