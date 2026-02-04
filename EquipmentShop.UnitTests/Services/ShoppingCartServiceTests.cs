using EquipmentShop.UnitTests.Helpers;
using Microsoft.Extensions.Logging;

namespace EquipmentShop.UnitTests.Services
{
    public class ShoppingCartServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IProductRepository _productRepository;
        private readonly IShoppingCartService _cartService;
        private readonly int _categoryId;

        public ShoppingCartServiceTests()
        {
            _context = TestDbContextFactory.Create();
            var c = new Category { Name = "T", Slug = $"t-{Guid.NewGuid():N}".Substring(0, 12), IsActive = true };
            _context.Categories.Add(c);
            _context.SaveChanges();
            _categoryId = c.Id;
            _productRepository = new ProductRepository(_context, Mock.Of<ILogger<ProductRepository>>());
            _cartService = new ShoppingCartService(_context, _productRepository, Mock.Of<ILogger<ShoppingCartService>>());
        }

        public void Dispose() => TestDbContextFactory.Destroy(_context);

        [Fact]
        public async Task AddItemAsync_ValidProduct_AddsToCart()
        {
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p); await _context.SaveChangesAsync();
            var id = "c1"; await _cartService.AddItemAsync(id, p.Id, 2);
            var c = await _context.ShoppingCarts.Include(x => x.Items).FirstAsync(x => x.Id == id);
            c.Items.Count.Should().Be(1); c.Items.First().Quantity.Should().Be(2);
        }

        [Fact]
        public async Task UpdateItemQuantityAsync_ValidQuantity_UpdatesCart()
        {
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p); await _context.SaveChangesAsync();
            var id = "c1"; await _cartService.AddItemAsync(id, p.Id, 2);
            await _cartService.UpdateItemQuantityAsync(id, p.Id, 5);
            var c = await _context.ShoppingCarts.Include(x => x.Items).FirstAsync(x => x.Id == id);
            c.Items.First().Quantity.Should().Be(5);
        }

        [Fact]
        public async Task UpdateItemQuantityAsync_InvalidQuantity_Throws()
        {
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p); await _context.SaveChangesAsync();
            var id = "c1"; await _cartService.AddItemAsync(id, p.Id, 2);
            await Assert.ThrowsAsync<ArgumentException>(() => _cartService.UpdateItemQuantityAsync(id, p.Id, -1));
        }

        [Fact]
        public async Task UpdateItemQuantityAsync_ZeroQuantity_RemovesItem()
        {
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p); await _context.SaveChangesAsync();
            var id = "c1"; await _cartService.AddItemAsync(id, p.Id, 2);
            await _cartService.UpdateItemQuantityAsync(id, p.Id, 0);
            var c = await _context.ShoppingCarts.Include(x => x.Items).FirstAsync(x => x.Id == id);
            c.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task RemoveItemAsync_ExistingItem_RemovesFromCart()
        {
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p); await _context.SaveChangesAsync();
            var id = "c1"; await _cartService.AddItemAsync(id, p.Id, 2);
            await _cartService.RemoveItemAsync(id, p.Id);
            var c = await _context.ShoppingCarts.Include(x => x.Items).FirstAsync(x => x.Id == id);
            c.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task ClearCart_ExistingCart_EmptiesCart()
        {
            var p1 = new Product { Name = "P1", Slug = $"p1-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            var p2 = new Product { Name = "P2", Slug = $"p2-{Guid.NewGuid():N}".Substring(0, 12), Price = 200m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddRangeAsync(new[] { p1, p2 }); await _context.SaveChangesAsync();
            var id = "c1"; await _cartService.AddItemAsync(id, p1.Id, 1); await _cartService.AddItemAsync(id, p2.Id, 2);
            await _cartService.ClearCartAsync(id);
            var c = await _context.ShoppingCarts.Include(x => x.Items).FirstAsync(x => x.Id == id);
            c.Items.Should().BeEmpty();
        }
    }
}