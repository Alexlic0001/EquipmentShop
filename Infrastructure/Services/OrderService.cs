using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Exceptions;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Data;
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
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            AppDbContext context,
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            IShoppingCartService cartService,
            ILogger<OrderService> logger)
        {
            _context = context;
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _cartService = cartService;
            _logger = logger;
        }

        public async Task<Order> CreateOrderFromCartAsync(string cartId, Order order)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Конвертируем корзину в заказ (заполняет OrderItems)
                var cart = await _cartService.ConvertToOrderAsync(cartId, order);

                // 2. Списываем остатки
                foreach (var item in order.OrderItems)
                {
                    if (item.ProductId.HasValue)
                    {
                        await _productRepository.UpdateStockAsync(item.ProductId.Value, -item.Quantity);
                    }
                }

                // 3. Сохраняем заказ
                await _orderRepository.AddAsync(order);

                // 4. Очищаем корзину
                await _cartService.ClearCartAsync(cartId);

                await transaction.CommitAsync();
                _logger.LogInformation("Заказ {OrderNumber} создан успешно", order.OrderNumber);
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
            {
                throw new Exception($"Заказ с ID {orderId} не найден");
            }

            //if (!order.CanBeCancelled())
            //{
            //    throw new OrderProcessingException(order.OrderNumber, order.Status,
            //        "Заказ не может быть отменен в текущем статусе");
            //}

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

            if (!string.IsNullOrEmpty(reason))
            {
                order.AdminNotes = $"Отменено: {reason}\n{order.AdminNotes}";
            }

            await _orderRepository.UpdateAsync(order);

            _logger.LogInformation("Заказ {OrderNumber} отменен", order.OrderNumber);
        }


        public async Task ProcessOrderAsync(int orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                throw new Exception($"Заказ с ID {orderId} не найден");
            }

            if (order.Status != OrderStatus.Pending)
            {
                throw new OrderProcessingException(order.OrderNumber, order.Status,
                    "Заказ уже обрабатывается или обработан");
            }

            order.Status = OrderStatus.Processing;
            order.ProcessingDate = DateTime.UtcNow;

            await _orderRepository.UpdateAsync(order);

            _logger.LogInformation("Заказ {OrderNumber} переведен в обработку", order.OrderNumber);
        }

        public async Task ShipOrderAsync(int orderId, string trackingNumber, string shippingProvider)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                throw new Exception($"Заказ с ID {orderId} не найден");
            }

            if (order.Status != OrderStatus.Processing)
            {
                throw new OrderProcessingException(order.OrderNumber, order.Status,
                    "Заказ не готов к отгрузке");
            }

            order.Status = OrderStatus.Shipped;
            order.TrackingNumber = trackingNumber;
            order.ShippingProvider = shippingProvider;
            order.ShippedDate = DateTime.UtcNow;

            await _orderRepository.UpdateAsync(order);

            _logger.LogInformation("Заказ {OrderNumber} отгружен. Трек: {TrackingNumber}",
                order.OrderNumber, trackingNumber);
        }

        public async Task MarkAsDeliveredAsync(int orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                throw new Exception($"Заказ с ID {orderId} не найден");
            }

            if (order.Status != OrderStatus.Shipped)
            {
                throw new OrderProcessingException(order.OrderNumber, order.Status,
                    "Заказ еще не был отгружен");
            }

            order.Status = OrderStatus.Delivered;
            order.DeliveredDate = DateTime.UtcNow;

            await _orderRepository.UpdateAsync(order);

            _logger.LogInformation("Заказ {OrderNumber} доставлен", order.OrderNumber);
        }

        public async Task UpdatePaymentStatusAsync(int orderId, PaymentStatus status)
        {
            await _orderRepository.UpdatePaymentStatusAsync(orderId, status);
        }

        // Реализация методов IOrderRepository через композицию
        //public async Task<Order?> GetByIdAsync(int id) => await _orderRepository.GetByIdAsync(id);
        public async Task<Order?> GetByOrderNumberAsync(string orderNumber) => await _orderRepository.GetByOrderNumberAsync(orderNumber);
        public async Task<Order?> GetWithItemsAsync(int id) => await _orderRepository.GetWithItemsAsync(id);
        public async Task<IEnumerable<Order>> GetAllAsync() => await _orderRepository.GetAllAsync();
        public async Task<IEnumerable<Order>> FindAsync(Expression<Func<Order, bool>> predicate) => await _orderRepository.FindAsync(predicate);
        public async Task<IEnumerable<Order>> GetByUserIdAsync(string userId) => await _orderRepository.GetByUserIdAsync(userId);
        public async Task<IEnumerable<Order>> GetByEmailAsync(string email) => await _orderRepository.GetByEmailAsync(email);
        public async Task<IEnumerable<Order>> GetRecentOrdersAsync(int count = 10) => await _orderRepository.GetRecentOrdersAsync(count);
        public async Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status) => await _orderRepository.GetOrdersByStatusAsync(status);
        public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate) => await _orderRepository.GetOrdersByDateRangeAsync(startDate, endDate);
        public async Task<OrderStats> GetOrderStatsAsync() => await _orderRepository.GetOrderStatsAsync();
        public async Task UpdateStatusAsync(int orderId, OrderStatus status) => await _orderRepository.UpdateStatusAsync(orderId, status);
        public async Task<int> GetTotalOrdersCountAsync() => await _orderRepository.GetTotalOrdersCountAsync();
        public async Task<decimal> GetTotalRevenueAsync() => await _orderRepository.GetTotalRevenueAsync();
        public async Task<Order> AddAsync(Order order) => await _orderRepository.AddAsync(order);
        public async Task UpdateAsync(Order order) => await _orderRepository.UpdateAsync(order);
        public async Task DeleteAsync(Order order) => await _orderRepository.DeleteAsync(order);
        public async Task<int> CountAsync(Expression<Func<Order, bool>>? predicate = null) => await _orderRepository.CountAsync(predicate);
        public async Task<bool> ExistsAsync(Expression<Func<Order, bool>> predicate) => await _orderRepository.ExistsAsync(predicate);

        public Task<IEnumerable<Product>> FilterAsync(ProductFilter filter)
        {
            throw new NotImplementedException();
        }
    }
}
