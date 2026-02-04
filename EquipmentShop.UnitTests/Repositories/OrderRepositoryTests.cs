using EquipmentShop.UnitTests.Helpers;
using Microsoft.Extensions.Logging;

namespace EquipmentShop.UnitTests.Repositories
{
    public class OrderRepositoryTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IOrderRepository _repository;
        private readonly int _cId, _pId;

        public OrderRepositoryTests()
        {
            _context = TestDbContextFactory.Create();
            var c = new Category { Name = "T", Slug = $"t-{Guid.NewGuid():N}".Substring(0, 12), IsActive = true };
            _context.Categories.Add(c);
            _context.SaveChanges();
            _cId = c.Id;
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = _cId, ImageUrl = "/img.jpg" };
            _context.Products.Add(p);
            _context.SaveChanges();
            _pId = p.Id;
            _repository = new OrderRepository(_context, Mock.Of<ILogger<OrderRepository>>());
        }

        public void Dispose() => TestDbContextFactory.Destroy(_context);

        [Fact]
        public async Task AddAsync_ValidOrder_AddsToDatabase()
        {
            var o = new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = null, CustomerName = "U", CustomerEmail = "u@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Pending, Subtotal = 100m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P", UnitPrice = 100m, Quantity = 1 } } };
            var r = await _repository.AddAsync(o);
            await _context.SaveChangesAsync();
            r.Should().NotBeNull(); r.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetByUserIdAsync_ExistingUser_ReturnsUserOrders()
        {
            await _context.Orders.AddRangeAsync(new List<Order>
            {
                new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = "u1", CustomerName = "U1", CustomerEmail = "u1@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Pending, Subtotal = 100m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P1", UnitPrice = 100m, Quantity = 1 } } },
                new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = "u1", CustomerName = "U1", CustomerEmail = "u1@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Delivered, Subtotal = 200m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P2", UnitPrice = 200m, Quantity = 1 } } },
                new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = "u2", CustomerName = "U2", CustomerEmail = "u2@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Pending, Subtotal = 300m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P3", UnitPrice = 300m, Quantity = 1 } } }
            });
            await _context.SaveChangesAsync();
            var r = await _repository.GetByUserIdAsync("u1");
            r.Should().HaveCount(2); r.All(o => o.UserId == "u1").Should().BeTrue();
        }

        [Fact]
        public async Task GetByIdAsync_ExistingOrder_ReturnsOrderWithItems()
        {
            var o = new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = null, CustomerName = "U", CustomerEmail = "u@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Pending, Subtotal = 350m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P1", UnitPrice = 100m, Quantity = 2 }, new OrderItem { ProductId = _pId, ProductName = "P2", UnitPrice = 50m, Quantity = 3 } } };
            await _context.Orders.AddAsync(o); await _context.SaveChangesAsync();
            var r = await _repository.GetByIdAsync(o.Id);
            r.Should().NotBeNull(); r.OrderItems.Should().HaveCount(2); r.Total.Should().Be(350m);
        }

        [Fact]
        public async Task GetWithItemsAsync_ExistingOrder_ReturnsOrderWithItems()
        {
            var o = new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = null, CustomerName = "U", CustomerEmail = "u@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Pending, Subtotal = 350m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P1", UnitPrice = 100m, Quantity = 2 }, new OrderItem { ProductId = _pId, ProductName = "P2", UnitPrice = 50m, Quantity = 3 } } };
            await _context.Orders.AddAsync(o); await _context.SaveChangesAsync();
            var r = await _repository.GetWithItemsAsync(o.Id);
            r.Should().NotBeNull(); r.OrderItems.Should().HaveCount(2); r.Total.Should().Be(350m);
        }

        [Fact]
        public async Task GetOrdersByStatusAsync_PendingStatus_ReturnsPendingOrders()
        {
            await _context.Orders.AddRangeAsync(new List<Order>
            {
                new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = null, CustomerName = "U1", CustomerEmail = "u1@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Pending, Subtotal = 100m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P1", UnitPrice = 100m, Quantity = 1 } } },
                new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = null, CustomerName = "U2", CustomerEmail = "u2@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Delivered, Subtotal = 200m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P2", UnitPrice = 200m, Quantity = 1 } } },
                new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = null, CustomerName = "U3", CustomerEmail = "u3@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Pending, Subtotal = 300m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P3", UnitPrice = 300m, Quantity = 1 } } }
            });
            await _context.SaveChangesAsync();
            var r = await _repository.GetOrdersByStatusAsync(OrderStatus.Pending);
            r.Should().HaveCount(2); r.All(o => o.Status == OrderStatus.Pending).Should().BeTrue();
        }

        [Fact]
        public async Task UpdateStatusAsync_ExistingOrder_UpdatesStatus()
        {
            var o = new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = null, CustomerName = "U", CustomerEmail = "u@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Pending, Subtotal = 100m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P", UnitPrice = 100m, Quantity = 1 } } };
            await _context.Orders.AddAsync(o); await _context.SaveChangesAsync();
            await _repository.UpdateStatusAsync(o.Id, OrderStatus.Processing); await _context.SaveChangesAsync();
            var u = await _context.Orders.FindAsync(o.Id);
            u.Status.Should().Be(OrderStatus.Processing);
        }

        [Fact]
        public async Task UpdatePaymentStatusAsync_ExistingOrder_UpdatesPaymentStatus()
        {
            var o = new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = null, CustomerName = "U", CustomerEmail = "u@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Pending, PaymentStatus = PaymentStatus.Pending, Subtotal = 100m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P", UnitPrice = 100m, Quantity = 1 } } };
            await _context.Orders.AddAsync(o); await _context.SaveChangesAsync();
            await _repository.UpdatePaymentStatusAsync(o.Id, PaymentStatus.Paid); await _context.SaveChangesAsync();
            var u = await _context.Orders.FindAsync(o.Id);
            u.PaymentStatus.Should().Be(PaymentStatus.Paid); u.PaymentDate.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteAsync_ExistingOrder_RemovesFromDatabase()
        {
            var o = new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = null, CustomerName = "U", CustomerEmail = "u@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Status = OrderStatus.Pending, Subtotal = 100m, ShippingCost = 0, TaxAmount = 0, DiscountAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = _pId, ProductName = "P", UnitPrice = 100m, Quantity = 1 } } };
            await _context.Orders.AddAsync(o); await _context.SaveChangesAsync();
            var id = o.Id; await _repository.DeleteAsync(o); await _context.SaveChangesAsync();
            var d = await _context.Orders.FindAsync(id);
            d.Should().BeNull();
        }
    }
}