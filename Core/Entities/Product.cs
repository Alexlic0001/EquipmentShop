using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentShop.Core.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string ImageUrl { get; set; } = "/images/products/default.jpg";
        public List<string> GalleryImages { get; set; } = new();

        // Категория и бренд
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public string Brand { get; set; } = string.Empty;

        // Наличие
        public int StockQuantity { get; set; }
        public int MinStockThreshold { get; set; } = 5;
        public bool IsAvailable { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsNew { get; set; }
        public bool IsOnSale => OldPrice.HasValue;

        // Рейтинг и отзывы
        public double Rating { get; set; }
        public int ReviewsCount { get; set; }
        public int SoldCount { get; set; }

        // Технические характеристики
        public Dictionary<string, string> Specifications { get; set; } = new();
        public List<string> Tags { get; set; } = new();

        // Мета-данные
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public string MetaKeywords { get; set; } = string.Empty;

        // Даты
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }

        // Навигационные свойства
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        // Для работы в форме (НЕ сохраняется в БД)
        [NotMapped]
        [System.Text.Json.Serialization.JsonIgnore]
        public string TagsString { get; set; } = string.Empty;

        [NotMapped]
        [System.Text.Json.Serialization.JsonIgnore]
        public string SpecificationsString { get; set; } = string.Empty;

        // Методы
        public decimal GetDiscountPercentage()
        {
            if (!OldPrice.HasValue || OldPrice.Value <= 0) return 0;
            return 100 - (Price / OldPrice.Value * 100);
        }

        public bool IsLowStock => StockQuantity <= MinStockThreshold && StockQuantity > 0;
        public bool IsOutOfStock => StockQuantity <= 0;
    }
}