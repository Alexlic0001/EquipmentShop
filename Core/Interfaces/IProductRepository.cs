using EquipmentShop.Core.Entities;

namespace EquipmentShop.Core.Interfaces
{
    public interface IProductRepository : IRepository<Product>
    {
        Task<Product?> GetBySlugAsync(string slug);
        Task<IEnumerable<Product>> GetFeaturedAsync(int count = 8);
        Task<IEnumerable<Product>> GetNewArrivalsAsync(int count = 8);
        Task<IEnumerable<Product>> GetOnSaleAsync(int count = 8);
        Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId, int page = 1, int pageSize = 12);
        Task<IEnumerable<Product>> SearchAsync(string query, int page = 1, int pageSize = 12);
        Task<IEnumerable<Product>> GetRelatedAsync(int productId, int count = 4);
        Task<IEnumerable<Product>> GetBestsellersAsync(int count = 10);
        Task<IEnumerable<Product>> GetLowStockAsync();
        Task UpdateStockAsync(int productId, int quantityChange);
        Task<IEnumerable<string>> GetProductTagsAsync();
        Task<int> GetTotalCountByCategoryAsync(int categoryId);
        Task<int> GetTotalSearchCountAsync(string query);
        Task<IEnumerable<Product>> FilterAsync(ProductFilter filter);
        Task<IEnumerable<Product>> GetRecommendedForUserAsync(string userId, int count = 8);

        Task<string> GenerateUniqueSlugAsync(string baseName, int? excludeId = null);
    }

    public class ProductFilter
    {
        public int? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Brand { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool? InStock { get; set; }
        public bool? OnSale { get; set; }
        public bool? IsFeatured { get; set; }
        public string? SortBy { get; set; } // "price_asc", "price_desc", "newest", "popular"
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }
}
