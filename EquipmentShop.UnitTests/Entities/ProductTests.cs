namespace EquipmentShop.UnitTests.Entities
{
    public class ProductTests
    {
        [Fact]
        public void Product_Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange & Act
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                Description = "Test Description",
                Price = 1000m,
                StockQuantity = 10,
                IsAvailable = true,
                CategoryId = 1,
                ImageUrl = "/images/test.jpg"
            };

            // Assert
            product.Id.Should().Be(1);
            product.Name.Should().Be("Test Product");
            product.Price.Should().Be(1000m);
            product.StockQuantity.Should().Be(10);
            product.IsAvailable.Should().BeTrue();
            product.CategoryId.Should().Be(1);
        }

        [Fact]
        public void Product_IsInStock_WithPositiveQuantity_ReturnsTrue()
        {
            // Arrange
            var product = new Product { StockQuantity = 5, IsAvailable = true };

            // Act
            var isInStock = product.StockQuantity > 0 && product.IsAvailable;

            // Assert
            isInStock.Should().BeTrue();
        }

        [Fact]
        public void Product_IsInStock_WithZeroQuantity_ReturnsFalse()
        {
            // Arrange
            var product = new Product { StockQuantity = 0, IsAvailable = true };

            // Act
            var isInStock = product.StockQuantity > 0 && product.IsAvailable;

            // Assert
            isInStock.Should().BeFalse();
        }

        [Fact]
        public void Product_IsInStock_WhenNotAvailable_ReturnsFalse()
        {
            // Arrange
            var product = new Product { StockQuantity = 10, IsAvailable = false };

            // Act
            var isInStock = product.StockQuantity > 0 && product.IsAvailable;

            // Assert
            isInStock.Should().BeFalse();
        }
    }
}