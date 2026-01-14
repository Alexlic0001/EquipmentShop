namespace EquipmentShop.Core.Entities
{
    public class OrderItem
    {
        public int Id { get; set; }

        // Связи
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;
        public int? ProductId { get; set; }
        public Product Product { get; set; } = null!;

        // Информация о товаре (snapshot на момент заказа)
        public string ProductName { get; set; } = string.Empty;
        public string? ProductSku { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int Quantity { get; set; }

        // Дополнительная информация
        public string? ProductAttributes { get; set; } // JSON с выбранными атрибутами

        // Вычисляемые свойства
        public decimal TotalPrice => UnitPrice * Quantity;
        public decimal? DiscountAmount => OriginalPrice.HasValue
            ? (OriginalPrice.Value - UnitPrice) * Quantity
            : 0;

        // Методы
        public void UpdateFromProduct(Product product)
        {
            ProductName = product.Name;
            UnitPrice = product.Price;
            OriginalPrice = product.OldPrice;
        }
    }
}
