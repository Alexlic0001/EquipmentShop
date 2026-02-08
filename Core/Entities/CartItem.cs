namespace EquipmentShop.Core.Entities
{
    public class CartItem
    {
        public int Id { get; set; }

        // Связи
        public string CartId { get; set; } = string.Empty;
        public ShoppingCart Cart { get; set; } = null!;
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        // Количество и цена
        public int Quantity { get; set; } = 1;
        public decimal Price { get; set; }

        // Дополнительная информация
        public string? SelectedAttributes { get; set; } // JSON с выбранными атрибутами
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Вычисляемые свойства
        public decimal TotalPrice => Price * Quantity;

        
    }
}
