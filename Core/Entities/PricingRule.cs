// EquipmentShop.Core/Entities/PricingRule.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EquipmentShop.Core.Enums;

namespace EquipmentShop.Core.Entities
{
    public class PricingRule
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "Новое правило";

        [Required]
        public PricingRuleType RuleType { get; set; } = PricingRuleType.Global;

        // Область применения
        public int? CategoryId { get; set; }
        [MaxLength(100)]
        public string? Brand { get; set; }
        public int? ProductId { get; set; }

        [Required]
        public PricingAdjustmentType AdjustmentType { get; set; } = PricingAdjustmentType.Percentage;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AdjustmentValue { get; set; }

        [Required]
        public int Priority { get; set; } = 0;

        [Required]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public Category? Category { get; set; }
        public Product? Product { get; set; }
    }
}