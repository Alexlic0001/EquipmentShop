using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace EquipmentShop.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderRepository> _logger;

        public OrderRepository(AppDbContext context, ILogger<OrderRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Order?> GetByIdAsync(int id)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<bool> UpdateOrderStatusAsync(string orderNumber, OrderStatus newStatus)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
            if (order == null) return false;

            order.Status = newStatus;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Order?> GetByOrderNumberAsync(string orderNumber)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        }

        public async Task<Order?> GetWithItemsAsync(int id)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<IEnumerable<Order>> GetAllAsync()
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> FindAsync(Expression<Func<Order, bool>> predicate)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .Where(predicate)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetByUserIdAsync(string userId)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetByEmailAsync(string email)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.CustomerEmail == email)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<Order> AddOrderWithItemsAsync(Order order)
        {
            if (string.IsNullOrEmpty(order.OrderNumber))
            {
                order.OrderNumber = Order.GenerateOrderNumber();
            }

            // Добавляем заказ — получаем Id
            _context.Orders.Add(order);
            await _context.SaveChangesAsync(); // ← order.Id теперь известен

            // Устанавливаем OrderId для всех OrderItems
            foreach (var item in order.OrderItems)
            {
                item.OrderId = order.Id;
            }

            // Добавляем OrderItems
            _context.OrderItems.AddRange(order.OrderItems);
            await _context.SaveChangesAsync();

            return order;
        }





        public async Task<IEnumerable<Order>> GetRecentOrdersAsync(int count = 10)
        {
            return await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(count)
                .ToListAsync(); // ← Без Include!
        }

        public async Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.Status == status)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<OrderStats> GetOrderStatsAsync()
        {
            var orders = await _context.Orders.ToListAsync();

            var stats = new OrderStats
            {
                TotalOrders = orders.Count,
                PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),
                ProcessingOrders = orders.Count(o => o.Status == OrderStatus.Processing),
                CompletedOrders = orders.Count(o => o.Status == OrderStatus.Delivered),
                TotalRevenue = orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.Total)
            };

            if (stats.TotalOrders > 0)
            {
                stats.AverageOrderValue = stats.TotalRevenue / stats.TotalOrders;
            }

            // Статистика по статусам
            var statusGroups = orders.GroupBy(o => o.Status.ToString())
                .ToDictionary(g => g.Key, g => g.Count());
            stats.OrdersByStatus = statusGroups;

            // Статистика по месяцам
            var monthGroups = orders
                .Where(o => o.OrderDate.Year == DateTime.UtcNow.Year)
                .GroupBy(o => o.OrderDate.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Count());
            stats.OrdersByMonth = monthGroups;

            return stats;
        }

        public async Task UpdateStatusAsync(int orderId, OrderStatus status)
        {
            var order = await GetByIdAsync(orderId);
            if (order == null)
            {
                throw new Exception($"Заказ с ID {orderId} не найден");
            }

            order.Status = status;
            await UpdateAsync(order);
        }

        public async Task UpdatePaymentStatusAsync(int orderId, PaymentStatus status)
        {
            var order = await GetByIdAsync(orderId);
            if (order == null)
            {
                throw new Exception($"Заказ с ID {orderId} не найден");
            }

            order.PaymentStatus = status;
            if (status == PaymentStatus.Paid)
            {
                order.PaymentDate = DateTime.UtcNow;
            }

            await UpdateAsync(order);
        }

        public async Task<int> GetTotalOrdersCountAsync()
        {
            return await _context.Orders.CountAsync();
        }

        public async Task<decimal> GetTotalRevenueAsync()
        {
            return await _context.Orders
                .Where(o => o.Status == OrderStatus.Delivered)
                .SumAsync(o => o.Total);
        }

        public async Task<Order> AddAsync(Order order)
        {
            if (string.IsNullOrEmpty(order.OrderNumber))
            {
                order.OrderNumber = Order.GenerateOrderNumber();
            }

            // Добавляем заказ — EF автоматически обработает OrderItems
            _context.Orders.Add(order);

            // ОДИН вызов SaveChanges — всё сохранится за раз
            await _context.SaveChangesAsync();

            return order;
        }

        public async Task UpdateAsync(Order order)
        {
            try
            {
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении заказа {OrderId}", order.Id);
                throw;
            }
        }

        public async Task DeleteAsync(Order order)
        {
            try
            {
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении заказа {OrderId}", order.Id);
                throw;
            }
        }

        public async Task<int> CountAsync(Expression<Func<Order, bool>>? predicate = null)
        {
            if (predicate == null)
            {
                return await _context.Orders.CountAsync();
            }

            return await _context.Orders.CountAsync(predicate);
        }

        public async Task<bool> ExistsAsync(Expression<Func<Order, bool>> predicate)
        {
            return await _context.Orders.AnyAsync(predicate);
        }

        public Task<IEnumerable<Product>> FilterAsync(ProductFilter filter)
        {
            throw new NotImplementedException();
        }
    }
}
