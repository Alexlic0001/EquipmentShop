namespace EquipmentShop.UnitTests.Entities
{
    public class OrderTests
    {
        [Fact]
        public void Order_Constructor_InitializesCollections()
        {
            // Arrange & Act
            var order = new Order
            {
                Id = 1,
                UserId = "user-1",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                OrderItems = new List<OrderItem>(),
                ShippingAddress = "Minsk, Test St 1",
                CustomerPhone = "+375291234567" // Исправлено: PhoneNumber → CustomerPhone
            };

            // Assert
            order.Id.Should().Be(1);
            order.UserId.Should().Be("user-1");
            order.Status.Should().Be(OrderStatus.Pending);
            order.OrderItems.Should().BeEmpty();
            order.ShippingAddress.Should().Be("Minsk, Test St 1");
            order.CustomerPhone.Should().Be("+375291234567"); // Исправлено
        }

        [Fact]
        public void Order_CanHaveMultipleItems()
        {
            // Arrange
            var order = new Order
            {
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 2, UnitPrice = 100m }, // Исправлено: Price → UnitPrice
                    new OrderItem { ProductId = 2, Quantity = 3, UnitPrice = 50m }   // Исправлено: Price → UnitPrice
                }
            };

            // Act & Assert
            order.OrderItems.Should().HaveCount(2);
            order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice).Should().Be(350m); // (2*100) + (3*50)
        }

        [Fact]
        public void OrderItem_HasCorrectProperties()
        {
            // Arrange & Act
            var orderItem = new OrderItem
            {
                Id = 1,
                OrderId = 1,
                ProductId = 1,
                ProductName = "Test Product", // Доступно в OrderItem
                Quantity = 2,
                UnitPrice = 100m // Исправлено: Price → UnitPrice
            };

            // Assert
            orderItem.Id.Should().Be(1);
            orderItem.ProductId.Should().Be(1);
            orderItem.Quantity.Should().Be(2);
            orderItem.UnitPrice.Should().Be(100m); // Исправлено
            orderItem.TotalPrice.Should().Be(200m); // 2 * 100
        }
    }
}