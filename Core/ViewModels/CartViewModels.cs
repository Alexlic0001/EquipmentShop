using System.ComponentModel.DataAnnotations;

namespace EquipmentShop.Core.ViewModels
{
    public class CartViewModel
    {
        public string CartId { get; set; } = string.Empty;
        public List<CartItemViewModel> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public bool IsEmpty => !Items.Any();
    }

    public class CartItemViewModel
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSlug { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int MaxQuantity { get; set; } = 10;
        public decimal TotalPrice => Price * Quantity;
        public bool IsAvailable { get; set; }
        public string? SelectedAttributes { get; set; }

        public bool CanIncrease => Quantity < MaxQuantity;
        public bool CanDecrease => Quantity > 1;
    }

    public class CartSummaryViewModel
    {
        public int ItemCount { get; set; }
        public decimal Total { get; set; }
        public bool IsEmpty => ItemCount == 0;
    }

    public class MiniCartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public decimal Subtotal { get; set; }
        public bool IsEmpty => !Items.Any();
    }

    public class AddToCartViewModel
    {
        [Required]
        public int ProductId { get; set; }

        [Range(1, 10, ErrorMessage = "Количество должно быть от 1 до 10")]
        public int Quantity { get; set; } = 1;

        public string? Attributes { get; set; }
    }
}