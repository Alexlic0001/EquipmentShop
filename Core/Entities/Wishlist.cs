namespace EquipmentShop.Core.Entities
{
    public class Wishlist
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    }

    public class WishlistItem
    {
        public int Id { get; set; }
        public int WishlistId { get; set; }
        public Wishlist Wishlist { get; set; } = null!;
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public string? Notes { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow; // ← Добавлено
    }
}