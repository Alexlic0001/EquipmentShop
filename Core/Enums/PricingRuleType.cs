// EquipmentShop.Core/Enums/PricingRuleType.cs
namespace EquipmentShop.Core.Enums
{
    public enum PricingRuleType
    {
        Global,        // Применяется ко всем товарам
        ByCategory,    // По категории
        ByBrand,       // По бренду
        ByProduct      // Индивидуально
    }
}