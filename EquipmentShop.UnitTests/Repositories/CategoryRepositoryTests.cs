using EquipmentShop.UnitTests.Helpers;
using Microsoft.Extensions.Logging;

namespace EquipmentShop.UnitTests.Repositories
{
    public class CategoryRepositoryTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly ICategoryRepository _repository;

        public CategoryRepositoryTests()
        {
            _context = TestDbContextFactory.Create();
            _repository = new CategoryRepository(_context, Mock.Of<ILogger<CategoryRepository>>());
        }

        public void Dispose()
        {
            TestDbContextFactory.Destroy(_context);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllCategories()
        {
            // Arrange - генерируем уникальные слаги
            var categories = new List<Category>
            {
                new Category {
                    Name = "Electronics",
                    Slug = $"electronics-{Guid.NewGuid():N}", // Уникальный слаг
                    Description = "Electronics category"
                },
                new Category {
                    Name = "Clothing",
                    Slug = $"clothing-{Guid.NewGuid():N}", // Уникальный слаг
                    Description = "Clothing category"
                }
            };
            await _context.Categories.AddRangeAsync(categories);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Select(c => c.Name).Should().Contain("Electronics");
            result.Select(c => c.Name).Should().Contain("Clothing");
        }

        [Fact]
        public async Task GetByIdAsync_ExistingCategory_ReturnsCategory()
        {
            // Arrange - уникальный слаг
            var category = new Category
            {
                Name = "Test",
                Slug = $"test-{Guid.NewGuid():N}",
                Description = "Test desc"
            };
            await _context.Categories.AddAsync(category);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(category.Id);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Test");
        }
    }
}