using EquipmentShop.Core.Entities;

namespace EquipmentShop.Core.Interfaces
{
    public interface IPricingService
    {
        /// Рассчитывает финальную цену для одного товара
        Task<decimal> CalculateFinalPriceAsync(int productId);
        /// Применяет правила ценообразования к коллекции товаров
        Task<List<Product>> ApplyFinalPricesToProductsAsync(IEnumerable<Product> products);
        /// Применяет все правила ценообразования (опционально, для кэширования)
        Task ApplyAllPricingRulesAsync();
    }
}