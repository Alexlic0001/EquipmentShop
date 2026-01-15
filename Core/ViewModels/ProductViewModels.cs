
using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Interfaces;

namespace EquipmentShop.Core.ViewModels
{
    public class ProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public List<string> GalleryImages { get; set; } = new();
        public string CategoryName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string Brand { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsNew { get; set; }
        public double Rating { get; set; }
        public int ReviewsCount { get; set; }
        public int SoldCount { get; set; }
        public Dictionary<string, string> Specifications { get; set; } = new();
        public List<string> Tags { get; set; } = new();

        // Вычисляемые свойства
        public decimal DiscountPercentage => GetDiscountPercentage();
        public string FormattedPrice => $"{Price:N0}{AppConstants.CurrencySymbol}";
        public string FormattedOldPrice => OldPrice.HasValue ? $"{OldPrice.Value:N0}{AppConstants.CurrencySymbol}" : string.Empty;
        //public string FormattedOldPrice => OldPrice?.ToString("C0") ?? string.Empty;
        public bool IsOnSale => OldPrice.HasValue;
        public string StockStatus => GetStockStatus();
        public string RatingStars => new string('★', (int)Math.Round(Rating)) + new string('☆', 5 - (int)Math.Round(Rating));

        private decimal GetDiscountPercentage()
        {
            if (!OldPrice.HasValue || OldPrice.Value <= 0) return 0;
            return 100 - (Price / OldPrice.Value * 100);
        }

        private string GetStockStatus()
        {
            if (StockQuantity <= 0) return "Нет в наличии";
            if (StockQuantity <= 5) return "Мало осталось";
            return "В наличии";
        }
    }

    public class ProductListViewModel
    {
        public IEnumerable<ProductViewModel> Products { get; set; } = new List<ProductViewModel>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalItems { get; set; } = 0;
        public string? Category { get; set; }
        public int? CategoryId { get; set; }
        public string? SearchQuery { get; set; }
       // public ProductFilter? Filter { get; set; }
        public string SortBy { get; set; } = "newest";
        public IEnumerable<CategoryViewModel> Categories { get; set; } = new List<CategoryViewModel>();
        public IEnumerable<string> Brands { get; set; } = new List<string>();

        // Пагинация
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        public int StartPage => Math.Max(1, CurrentPage - 2);
        public int EndPage => Math.Min(TotalPages, CurrentPage + 2);
    }

    public class ProductDetailsViewModel
    {
        public ProductViewModel Product { get; set; } = null!;
        public IEnumerable<ProductViewModel> RelatedProducts { get; set; } = new List<ProductViewModel>();
        public IEnumerable<ReviewViewModel> Reviews { get; set; } = new List<ReviewViewModel>();
        public ReviewStats ReviewStats { get; set; } = null!;
        public bool CanAddReview { get; set; }
        public bool HasPurchasedProduct { get; set; }
        public ReviewFilter ReviewFilter { get; set; } = new ReviewFilter();

        // Для формы добавления отзыва
        public CreateReviewViewModel? NewReview { get; set; }
    }

}
