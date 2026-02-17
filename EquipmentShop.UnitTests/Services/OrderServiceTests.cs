using EquipmentShop.Core.Exceptions;
using EquipmentShop.UnitTests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace EquipmentShop.UnitTests.Services
{
    public class OrderServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly IOrderService _orderService;
        private readonly IProductRepository _productRepository;
        private readonly IShoppingCartService _cartService;
        private readonly int _categoryId;
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;

        public OrderServiceTests()
        {
            _context = TestDbContextFactory.Create();
            var c = new Category { Name = "T", Slug = $"t-{Guid.NewGuid():N}".Substring(0, 12), IsActive = true };
            _context.Categories.Add(c);
            _context.SaveChanges();
            _categoryId = c.Id;

            var storeMock = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(storeMock.Object, null, null, null, null, null, null, null, null);
            _userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((string id) => new ApplicationUser { Id = id, Email = "t@test.com", FirstName = "T", LastName = "U", PhoneNumber = "+375291234567" });

            _productRepository = new ProductRepository(_context, Mock.Of<ILogger<ProductRepository>>());
            _cartService = new ShoppingCartService(_context, _productRepository, Mock.Of<ILogger<ShoppingCartService>>());
            _orderService = new OrderService(_context, new OrderRepository(_context, Mock.Of<ILogger<OrderRepository>>()), _productRepository, _cartService, _userManagerMock.Object, Mock.Of<ILogger<OrderService>>());
        }

        public void Dispose() => TestDbContextFactory.Destroy(_context);

        [Fact]
        public async Task CreateOrderFromCartAsync_ValidCart_CreatesOrder()
        {
            var p1 = new Product { Name = "P1", Slug = $"p1-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            var p2 = new Product { Name = "P2", Slug = $"p2-{Guid.NewGuid():N}".Substring(0, 12), Price = 200m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddRangeAsync(new[] { p1, p2 }); await _context.SaveChangesAsync();
            var cid = "c1"; await _cartService.AddItemAsync(cid, p1.Id, 2); await _cartService.AddItemAsync(cid, p2.Id, 1);
            var uid = "u1"; var o = await _orderService.CreateOrderFromCartAsync(cid, uid);
            o.Should().NotBeNull(); o.UserId.Should().Be(uid); o.Status.Should().Be(OrderStatus.Pending); o.OrderItems.Count.Should().Be(2); o.Total.Should().Be(400m);
            var c = await _cartService.GetCartAsync(cid); c.Items.Should().BeEmpty();
            (await _productRepository.GetByIdAsync(p1.Id)).StockQuantity.Should().Be(8);
            (await _productRepository.GetByIdAsync(p2.Id)).StockQuantity.Should().Be(9);
        }

        [Fact]
        public async Task CreateOrderFromCartAsync_EmptyCart_Throws()
        {
            var cid = "empty"; var uid = "u1";
            await Assert.ThrowsAsync<CartNotFoundException>(() => _orderService.CreateOrderFromCartAsync(cid, uid));
        }

        [Fact]
        public async Task CancelOrderAsync_PendingOrder_CancelsAndRestoresStock()
        {
            var p = new Product { Name = "P", Slug = $"p-{Guid.NewGuid():N}".Substring(0, 12), Price = 100m, StockQuantity = 10, IsAvailable = true, CategoryId = _categoryId, ImageUrl = "/img.jpg" };
            await _context.Products.AddAsync(p); await _context.SaveChangesAsync();
            var o = new Order { OrderNumber = Order.GenerateOrderNumber(), UserId = "u1", Status = OrderStatus.Pending, OrderDate = DateTime.UtcNow, CustomerName = "U", CustomerEmail = "u@test.com", CustomerPhone = "+375291234567", ShippingAddress = "Minsk", Subtotal = 200m, ShippingCost = 0, TaxAmount = 0, OrderItems = new List<OrderItem> { new OrderItem { ProductId = p.Id, Quantity = 2, UnitPrice = 100m, ProductName = "P" } } };
            await _context.Orders.AddAsync(o); await _context.SaveChangesAsync();
            await _orderService.CancelOrderAsync(o.Id, "Отменено");
            var co = await _context.Orders.FindAsync(o.Id); co.Status.Should().Be(OrderStatus.Cancelled);
            (await _productRepository.GetByIdAsync(p.Id)).StockQuantity.Should().Be(12);
        }
    }
}