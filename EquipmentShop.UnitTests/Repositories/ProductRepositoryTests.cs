using EquipmentShop.UnitTests.Helpers;
using Microsoft.Extensions.Logging;

namespace EquipmentShop.UnitTests.Repositories
{
    public class ProductRepositoryTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IProductRepository _repository;

        public ProductRepositoryTests()
        {
            _context = TestDbContextFactory.Create();
            _repository = new ProductRepository(_context, Mock.Of<ILogger<ProductRepository>>());
        }

        public void Dispose() => TestDbContextFactory.Destroy(_context);

        [Fact]
        public async Task GetByIdAsync_ExistingProduct_ReturnsProduct()
        {
            var c = new Category { Name = "T", Slug = $"t-{Guid.NewGuid():N}".Substring(0, 12), IsActive = true };
            await _context.Categories.AddAsync(c);
            await _context.SaveChangesAsync();
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = c.Id, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p);
            await _context.SaveChangesAsync();
            var r = await _repository.GetByIdAsync(p.Id);
            r.Should().NotBeNull();
            r.Id.Should().Be(p.Id);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllProducts()
        {
            var c = new Category { Name = "T", Slug = $"t-{Guid.NewGuid():N}".Substring(0, 12), IsActive = true };
            await _context.Categories.AddAsync(c);
            await _context.SaveChangesAsync();
            await _context.Products.AddRangeAsync(new List<Product>
            {
                new Product { Name = "P1", Slug = $"p1-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 5, IsAvailable = true, CategoryId = c.Id, ImageUrl = "/img.jpg" },
                new Product { Name = "P2", Slug = $"p2-{Guid.NewGuid():N}".Substring(0, 12), Price = 200m, StockQuantity = 3, IsAvailable = true, CategoryId = c.Id, ImageUrl = "/img.jpg" }
            });
            await _context.SaveChangesAsync();
            var r = await _repository.GetAllAsync();
            r.Should().HaveCount(2);
        }

        [Fact]
        public async Task AddAsync_ValidProduct_AddsToDatabase()
        {
            var c = new Category { Name = "T", Slug = $"t-{Guid.NewGuid():N}".Substring(0, 12), IsActive = true };
            await _context.Categories.AddAsync(c);
            await _context.SaveChangesAsync();
            var p = new Product { Name = "N", Slug = $"n-{Guid.NewGuid():N}".Substring(0, 12), Price = 500m, StockQuantity = 10, IsAvailable = true, CategoryId = c.Id, ImageUrl = "/img.jpg" };
            var r = await _repository.AddAsync(p);
            await _context.SaveChangesAsync();
            r.Should().NotBeNull();
            r.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task UpdateAsync_ExistingProduct_UpdatesProperties()
        {
            var c = new Category { Name = "T", Slug = $"t-{Guid.NewGuid():N}".Substring(0, 12), IsActive = true };
            await _context.Categories.AddAsync(c);
            await _context.SaveChangesAsync();
            var p = new Product { Name = "O", Slug = $"o-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 5, IsAvailable = true, CategoryId = c.Id, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p);
            await _context.SaveChangesAsync();
            p.Name = "N"; p.Price = 200m;
            await _repository.UpdateAsync(p);
            await _context.SaveChangesAsync();
            var u = await _context.Products.FindAsync(p.Id);
            u.Name.Should().Be("N");
            u.Price.Should().Be(200m);
        }

        [Fact]
        public async Task DeleteAsync_ExistingProduct_RemovesFromDatabase()
        {
            var c = new Category { Name = "T", Slug = $"t-{Guid.NewGuid():N}".Substring(0, 12), IsActive = true };
            await _context.Categories.AddAsync(c);
            await _context.SaveChangesAsync();
            var p = new Product { Name = "D", Slug = $"d-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 5, IsAvailable = true, CategoryId = c.Id, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p);
            await _context.SaveChangesAsync();
            var id = p.Id;
            await _repository.DeleteAsync(p);
            await _context.SaveChangesAsync();
            var d = await _context.Products.FindAsync(id);
            d.Should().BeNull();
        }
    }
}