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

        public int SoldCount { get; set; }

        public Dictionary<string, string> Specifications { get; set; } = new();
        public List<string> Tags { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        [NotMapped]
        [JsonIgnore]
        public string TagsString { get; set; } = string.Empty;

        public bool IsLowStock => StockQuantity <= MinStockThreshold && StockQuantity > 0;
        public bool IsOutOfStock => StockQuantity <= 0;
    }
}