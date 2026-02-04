namespace EquipmentShop.UnitTests.Entities
{
    public class CategoryTests
    {
        [Fact]
        public void Category_Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange & Act
            var category = new Category
            {
                Id = 1,
                Name = "Electronics",
                Description = "Electronic devices",
                ParentCategoryId = null
            };

            // Assert
            category.Id.Should().Be(1);
            category.Name.Should().Be("Electronics");
            category.Description.Should().Be("Electronic devices");
            category.ParentCategoryId.Should().BeNull();
        }

        [Fact]
        public void Category_CanHaveParentCategory()
        {
            // Arrange
            var parent = new Category { Id = 1, Name = "Parent" };
            var child = new Category { Id = 2, Name = "Child", ParentCategoryId = 1 };

            // Act & Assert
            child.ParentCategoryId.Should().Be(1);
        }

        [Fact]
        public void Category_CanHaveProductsCollection()
        {
            // Arrange
            var category = new Category
            {
                Id = 1,
                Name = "Test Category",
                Products = new List<Product>() // Стандартное навигационное свойство
            };

            // Act
            category.Products.Add(new Product { Id = 1, Name = "Product 1", Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = 1 });

            // Assert
            category.Products.Should().HaveCount(1);
            category.Products.First().Name.Should().Be("Product 1");
        }
    }
}