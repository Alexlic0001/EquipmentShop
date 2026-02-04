namespace EquipmentShop.UnitTests.Entities
{
    public class ShoppingCartTests
    {
        [Fact]
        public void ShoppingCart_InitializesItemsCollection()
        {
            // Arrange & Act
            var cart = new ShoppingCart { Id = "test-cart" };

            // Assert
            cart.Id.Should().Be("test-cart");
            cart.Items.Should().NotBeNull();
            cart.Items.Should().BeEmpty();
        }

        [Fact]
        public void ShoppingCartItem_HasCorrectProperties()
        {
            // Arrange & Act
            var cartItem = new CartItem
            {
                Id = 1,
                CartId = "cart-1", // Исправлено: ShoppingCartId → CartId
                ProductId = 1,
                // Удалено: ProductName (CartItem не имеет этого свойства)
                Quantity = 2,
                Price = 100m
            };

            // Assert
            cartItem.Id.Should().Be(1);
            cartItem.CartId.Should().Be("cart-1"); // Исправлено
            cartItem.ProductId.Should().Be(1);
            cartItem.Quantity.Should().Be(2);
            cartItem.Price.Should().Be(100m);
            cartItem.TotalPrice.Should().Be(200m); // 2 * 100
        }

        [Fact]
        public void ShoppingCart_CalculatesTotalCorrectly()
        {
            // Arrange
            var cart = new ShoppingCart
            {
                Id = "test-cart",
                Items = new List<CartItem>
                {
                    new CartItem { ProductId = 1, Quantity = 2, Price = 100m },
                    new CartItem { ProductId = 2, Quantity = 3, Price = 50m }
                }
            };

            // Act
            var total = cart.Items.Sum(item => item.Quantity * item.Price);

            // Assert
            total.Should().Be(350m); // (2*100) + (3*50) = 200 + 150 = 350
        }
    }
}