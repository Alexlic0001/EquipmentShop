// EquipmentShop.Infrastructure.Services/OrderService.cs
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Exceptions;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace EquipmentShop.Infrastructure.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IShoppingCartService _cartService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            AppDbContext context,
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            IShoppingCartService cartService,
            UserManager<ApplicationUser> userManager,
            ILogger<OrderService> logger)
        {
            _context = context;
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _cartService = cartService;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<Order> CreateOrderFromCartAsync(string cartId, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Получаем пользователя
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    throw new InvalidOperationException("Пользователь не найден");

                // 2. Получаем корзину
                var cart = await _cartService.GetCartAsync(cartId);
                if (cart.IsEmpty)
                    throw new EmptyCartException(cartId);
                if (!await _cartService.ValidateCartAsync(cartId))
                    throw new CartException(cartId, "Корзина содержит недоступные товары");

                // 3. Определяем адрес доставки
                string shippingAddress = "";
                string shippingCity = "Минск";
                string shippingRegion = "Минская обл.";
                string shippingCountry = "Беларусь";
                string shippingPostalCode = "";

                // Используем адрес по умолчанию из дополнительных адресов или основной адрес
                var defaultAddress = user.AdditionalAddresses?.FirstOrDefault(a => a.IsDefault);
                if (defaultAddress != null)
                {
                    shippingAddress = defaultAddress.AddressLine1;
                    if (!string.IsNullOrEmpty(defaultAddress.AddressLine2))
                        shippingAddress += $", {defaultAddress.AddressLine2}";
                    shippingCity = defaultAddress.City;
                    shippingRegion = defaultAddress.Region ?? "Минская обл.";
                    shippingCountry = defaultAddress.Country ?? "Беларусь";
                    shippingPostalCode = defaultAddress.PostalCode ?? "";
                }
                else if (!string.IsNullOrEmpty(user.Address))
                {
                    shippingAddress = user.Address;
                    shippingCity = user.City ?? "Минск";
                    shippingRegion = user.Region ?? "Минская обл.";
                    shippingCountry = user.Country ?? "Беларусь";
                    shippingPostalCode = user.PostalCode ?? "";
                }

                // 4. Создаём заказ
                var order = new Order
                {
                    OrderNumber = Order.GenerateOrderNumber(),
                    UserId = userId,
                    CustomerName = user.FullName,
                    CustomerEmail = user.Email,
                    CustomerPhone = user.PhoneNumber ?? string.Empty,
                    Status = OrderStatus.Pending,
                    OrderDate = DateTime.UtcNow,
                    // Адрес доставки
                    ShippingAddress = shippingAddress,
                    ShippingCity = shippingCity,
                    ShippingRegion = shippingRegion,
                    ShippingCountry = shippingCountry,
                    ShippingPostalCode = shippingPostalCode,
                    Subtotal = cart.Subtotal,
                    ShippingCost = 0m,
                    TaxAmount = 0m,
                    //DiscountAmount = 0m
                };

                // 5. Конвертируем CartItems → OrderItems + списываем остатки
                foreach (var cartItem in cart.Items)
                {
                    var product = await _productRepository.GetByIdAsync(cartItem.ProductId);
                    if (product == null || !product.IsAvailable || cartItem.Quantity > product.StockQuantity)
                        continue;

                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = cartItem.ProductId,
                        ProductName = product.Name,
                        ProductSku = product.Slug,
                        UnitPrice = cartItem.Price,
                        Quantity = cartItem.Quantity,
                        ProductAttributes = cartItem.SelectedAttributes
                    });

                    // Списываем остатки
                    await _productRepository.UpdateStockAsync(cartItem.ProductId, -cartItem.Quantity);
                }

                // 6. Сохраняем заказ
                await _orderRepository.AddAsync(order);

                // 7. Очищаем корзину
                await _cartService.ClearCartAsync(cartId);

                await transaction.CommitAsync();
                _logger.LogInformation("Заказ {OrderNumber} успешно создан из корзины {CartId}", order.OrderNumber, cartId);

                return order;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task CancelOrderAsync(int orderId, string reason = "")
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                throw new Exception($"Заказ с ID {orderId} не найден");

            // Возвращаем товары на склад
            foreach (var item in order.OrderItems)
            {
                if (item.ProductId.HasValue)
                {
                    await _productRepository.UpdateStockAsync(item.ProductId.Value, item.Quantity);
                }
            }

            order.Status = OrderStatus.Cancelled;
            order.CancelledDate = DateTime.UtcNow;
            //if (!string.IsNullOrEmpty(reason))
            //{
            //    order.AdminNotes = $"Отменено: {reason}\n{order.AdminNotes}";
            //}

            await _orderRepository.UpdateAsync(order);
            _logger.LogInformation("Заказ {OrderNumber} отменен", order.OrderNumber);
        }

        public async Task ProcessOrderAsync(int orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                throw new Exception($"Заказ с ID {orderId} не найден");

            if (order.Status != OrderStatus.Pending)
                throw new OrderProcessingException(order.OrderNumber, order.Status, "Заказ уже обрабатывается");

            order.Status = OrderStatus.Processing;
            order.ProcessingDate = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);
            _logger.LogInformation("Заказ {OrderNumber} переведен в обработку", order.OrderNumber);
        }

        public async Task ShipOrderAsync(int orderId, string trackingNumber, string shippingProvider)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                throw new Exception($"Заказ с ID {orderId} не найден");

            if (order.Status != OrderStatus.Processing)
                throw new OrderProcessingException(order.OrderNumber, order.Status, "Заказ не готов к отгрузке");

            order.Status = OrderStatus.Shipped;
            //order.TrackingNumber = trackingNumber;
            //order.ShippingProvider = shippingProvider;
            order.ShippedDate = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);
            _logger.LogInformation("Заказ {OrderNumber} отгружен. Трек: {TrackingNumber}", order.OrderNumber, trackingNumber);
        }

        public async Task MarkAsDeliveredAsync(int orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                throw new Exception($"Заказ с ID {orderId} не найден");

            if (order.Status != OrderStatus.Shipped)
                throw new OrderProcessingException(order.OrderNumber, order.Status, "Заказ ещё не был отгружен");

            order.Status = OrderStatus.Delivered;
            order.DeliveredDate = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);
            _logger.LogInformation("Заказ {OrderNumber} доставлен", order.OrderNumber);
        }

        public async Task UpdatePaymentStatusAsync(int orderId, PaymentStatus status)
        {
            await _orderRepository.UpdatePaymentStatusAsync(orderId, status);
        }
    }
}