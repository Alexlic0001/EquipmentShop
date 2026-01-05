
namespace EquipmentShop.Core.Constants
{
    public static class AppConstants
    {
        // Настройки магазина
        public const string StoreName = "DataStream Shop";
        public const string StoreEmail = "info@datastream.by";
        public const string StorePhone = "+375 (29) 123-45-67";
        public const string StoreAddress = "г. Минск, пр. Независимости, 58";

        // Валюты
        public const string DefaultCurrency = "BYN";
        public const string CurrencySymbol = " р.";

        // Налоги
        public const decimal DefaultTaxRate = 0.20m; // 20% НДС//

        // Доставка
        public const decimal FreeShippingThreshold = 50000m;
        public const decimal DefaultShippingCostMinsk = 10m;
        public const decimal DefaultShippingCostOther = 20m;

        // Пагинация
        public const int DefaultPageSize = 12;
        public const int MaxPageSize = 100;

        // Корзина
        public const int CartExpirationDays = 30;
        public const int MaxCartItemQuantity = 10;

        // Заказы
        public const string OrderNumberPrefix = "DS";
        public const int OrderAutoCancelHours = 24; // Автоотмена неоплаченных заказов

        // Изображения
        public const string DefaultProductImage = "/images/products/default.jpg";
        public const string DefaultCategoryImage = "/images/categories/default.jpg";
        public const string DefaultUserAvatar = "/images/avatars/default.png";

        public const int MaxImageSizeMB = 5;
        public static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        // Файлы
        public const string ProductImagesFolder = "wwwroot/images/products";
        public const string CategoryImagesFolder = "wwwroot/images/categories";
        public const string UserAvatarsFolder = "wwwroot/images/avatars";

        // Кэширование
        public const int CacheDurationMinutes = 30;
        public const string ProductsCacheKey = "FeaturedProducts";
        public const string CategoriesCacheKey = "MenuCategories";

        // Роли
        public const string AdminRole = "Admin";
        public const string ManagerRole = "Manager";
        public const string CustomerRole = "Customer";

        // Поиск
        public const int SearchMinLength = 2;
        public const int SearchMaxResults = 50;

        // Отзывы
        public const int MinReviewLength = 10;
        public const int MaxReviewLength = 1000;
        public const int ReviewEditDays = 7;

        // Безопасность
        public const int MaxLoginAttempts = 5;
        public const int LockoutMinutes = 15;
    }
}
