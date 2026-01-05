using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

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

        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public string Brand { get; set; } = string.Empty;

        public int StockQuantity { get; set; }
        public int MinStockThreshold { get; set; } = 5;
        public bool IsAvailable { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsNew { get; set; }
        public bool IsOnSale => OldPrice.HasValue;

        public double Rating { get; set; }
        public int ReviewsCount { get; set; }
        public int SoldCount { get; set; }

        public Dictionary<string, string> Specifications { get; set; } = new();
        public List<string> Tags { get; set; } = new();

        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public string MetaKeywords { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }

        // ДОБАВЬТЕ ЭТОТ АТРИБУТ:
        [JsonIgnore]
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        [JsonIgnore]
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        [NotMapped]
        [JsonIgnore]
        public string TagsString { get; set; } = string.Empty;

        [NotMapped]
        [JsonIgnore]
        public string SpecificationsString { get; set; } = string.Empty;

        public decimal GetDiscountPercentage()
        {
            if (!OldPrice.HasValue || OldPrice.Value <= 0) return 0;
            return 100 - (Price / OldPrice.Value * 100);
        }

        public bool IsLowStock => StockQuantity <= MinStockThreshold && StockQuantity > 0;
        public bool IsOutOfStock => StockQuantity <= 0;
    }
}