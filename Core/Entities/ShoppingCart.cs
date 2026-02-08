using EquipmentShop.Core.Entities;

namespace EquipmentShop.Core.Entities
{
    public class ShoppingCart
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? UserId { get; set; }

        // Даты
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);

        // Навигационные свойства
        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();

        // Вычисляемые свойства
        public decimal Subtotal => Items.Sum(i => i.TotalPrice);
        public int TotalItems => Items.Sum(i => i.Quantity);

        public bool IsEmpty => !Items.Any();
    }
}
