using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentShop.Infrastructure.Services
{
    public class PricingService : IPricingService
    {
        private readonly AppDbContext _context;

        public PricingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> CalculateFinalPriceAsync(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null) return 0;

            var basePrice = product.Price; // ←  decimal!
            var currentPrice = basePrice;

            var rules = await _context.PricingRules
                .Where(r => r.IsActive)
                .OrderBy(r => r.Priority)
                .ToListAsync();

            foreach (var rule in rules)
            {
                if (!IsRuleApplicable(rule, product)) continue;
                currentPrice = ApplyRule(currentPrice, rule);
            }

            return Math.Max(0, Math.Round(currentPrice, 2));
        }

        private bool IsRuleApplicable(PricingRule rule, Product product)
        {
            return rule.RuleType switch
            {
                Core.Enums.PricingRuleType.Global => true,
                Core.Enums.PricingRuleType.ByCategory => rule.CategoryId == product.CategoryId,
                Core.Enums.PricingRuleType.ByBrand => rule.Brand == product.Brand,
                Core.Enums.PricingRuleType.ByProduct => rule.ProductId == product.Id,
                _ => false
            };
        }

        private decimal ApplyRule(decimal price, PricingRule rule)
        {
            return rule.AdjustmentType switch
            {
                Core.Enums.PricingAdjustmentType.Percentage =>
                    price * (1 + rule.AdjustmentValue / 100),
                Core.Enums.PricingAdjustmentType.FixedAmount =>
                    price + rule.AdjustmentValue,
                _ => price
            };
        }

        public async Task<List<Product>> ApplyFinalPricesToProductsAsync(IEnumerable<Product> products)
        {
            var result = new List<Product>();

            foreach (var product in products)
            {
                var finalPrice = await CalculateFinalPriceAsync(product.Id);
                product.Price = finalPrice;
                result.Add(product);
            }

            return result;
        }

        public async Task ApplyAllPricingRulesAsync()
        {
            await Task.CompletedTask;
        }
    }
}