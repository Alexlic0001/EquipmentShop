// Core/Interfaces/IPricingService.cs
namespace EquipmentShop.Core.Interfaces
{
    public interface IPricingService
    {
        Task<decimal> CalculateFinalPriceAsync(int productId);
        Task ApplyAllPricingRulesAsync();
    }
}