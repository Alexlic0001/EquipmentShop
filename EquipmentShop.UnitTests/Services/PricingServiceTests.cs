using EquipmentShop.UnitTests.Helpers;

namespace EquipmentShop.UnitTests.Services
{
    public class PricingServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IPricingService _pricingService;
        private readonly int _categoryId;

        public PricingServiceTests()
        {
            _context = TestDbContextFactory.Create();
            var c = new Category { Name = "T", Slug = $"t-{Guid.NewGuid():N}".Substring(0, 12), IsActive = true };
            _context.Categories.Add(c);
            _context.SaveChanges();
            _categoryId = c.Id;
            _pricingService = new PricingService(_context);
        }

        public void Dispose() => TestDbContextFactory.Destroy(_context);

        [Fact]
        public async Task CalculateFinalPriceAsync_NoRules_ReturnsOriginalPrice()
        {
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 1000m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p); await _context.SaveChangesAsync();
            (await _pricingService.CalculateFinalPriceAsync(p.Id)).Should().Be(1000m);
        }

        [Fact]
        public async Task CalculateFinalPriceAsync_PercentageDiscount_ReturnsDiscountedPrice()
        {
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 1000m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p); await _context.SaveChangesAsync();
            var r = new PricingRule { RuleType = PricingRuleType.ByProduct, ProductId = p.Id, AdjustmentType = PricingAdjustmentType.Percentage, AdjustmentValue = -10, IsActive = true, Priority = 1 };
            await _context.PricingRules.AddAsync(r); await _context.SaveChangesAsync();
            (await _pricingService.CalculateFinalPriceAsync(p.Id)).Should().Be(900m);
        }

        [Fact]
        public async Task CalculateFinalPriceAsync_FixedDiscount_ReturnsDiscountedPrice()
        {
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 1000m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p); await _context.SaveChangesAsync();
            var r = new PricingRule { RuleType = PricingRuleType.ByProduct, ProductId = p.Id, AdjustmentType = PricingAdjustmentType.FixedAmount, AdjustmentValue = -100, IsActive = true, Priority = 1 };
            await _context.PricingRules.AddAsync(r); await _context.SaveChangesAsync();
            (await _pricingService.CalculateFinalPriceAsync(p.Id)).Should().Be(900m);
        }

        [Fact]
        public async Task CalculateFinalPriceAsync_InactiveRule_IgnoresRule()
        {
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 1000m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p); await _context.SaveChangesAsync();
            var r = new PricingRule { RuleType = PricingRuleType.ByProduct, ProductId = p.Id, AdjustmentType = PricingAdjustmentType.Percentage, AdjustmentValue = -20, IsActive = false, Priority = 1 };
            await _context.PricingRules.AddAsync(r); await _context.SaveChangesAsync();
            (await _pricingService.CalculateFinalPriceAsync(p.Id)).Should().Be(1000m);
        }
    }
}