using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels.Admin; 
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

        public async Task<IEnumerable<Order>> GetAllWithItemsAsync()
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
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

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var item in order.OrderItems)
            {
                item.OrderId = order.Id;
            }

            _context.OrderItems.AddRange(order.OrderItems);
            await _context.SaveChangesAsync();

            return order;
        }

        public async Task<IEnumerable<Order>> GetRecentOrdersAsync(int count = 10)
        {
            return await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(count)
                .ToListAsync();
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

            var statusGroups = orders.GroupBy(o => o.Status.ToString())
                .ToDictionary(g => g.Key, g => g.Count());
            stats.OrdersByStatus = statusGroups;

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

            _context.Orders.Add(order);
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

        public async Task<IEnumerable<SalesReportData>> GetSalesReportAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? categoryId = null,
            string? brand = null)
        {
            var query = _context.OrderItems
                .Where(oi => oi.ProductId.HasValue)
                .Include(oi => oi.Product)
                    .ThenInclude(p => p!.Category)
                .Join(_context.Orders,
                    oi => oi.OrderId,
                    o => o.Id,
                    (oi, o) => new { OrderItem = oi, Order = o })
                .Where(x => x.Order.Status != OrderStatus.Cancelled &&
                            x.Order.Status != OrderStatus.Refunded);

            if (startDate.HasValue)
                query = query.Where(x => x.Order.OrderDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(x => x.Order.OrderDate <= endDate.Value);

            if (categoryId.HasValue)
                query = query.Where(x => x.OrderItem.Product!.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(brand))
                query = query.Where(x => x.OrderItem.Product!.Brand == brand);

            var reportData = await query
                .GroupBy(x => x.OrderItem.ProductId)
                .Select(g => new SalesReportData  // ← Теперь однозначно из ViewModels.Admin
                {
                    ProductId = g.Key ?? 0,
                    ProductName = g.First().OrderItem.ProductName,
                    ProductSku = g.First().OrderItem.ProductSku ?? string.Empty,
                    CategoryName = g.First().OrderItem.Product!.Category!.Name,
                    Brand = g.First().OrderItem.Product!.Brand ?? string.Empty,

                    TotalQuantitySold = g.Sum(x => x.OrderItem.Quantity),
                    OrderCount = g.Select(x => x.Order.Id).Distinct().Count(),

                    UnitPrice = g.Average(x => x.OrderItem.UnitPrice),
                    TotalRevenue = g.Sum(x => x.OrderItem.UnitPrice * x.OrderItem.Quantity),

                    FirstSaleDate = g.Min(x => x.Order.OrderDate),
                    LastSaleDate = g.Max(x => x.Order.OrderDate),

                    IsAvailable = g.First().OrderItem.Product!.IsAvailable
                })
                .ToListAsync();

            return reportData;
        }


        public async Task<SalesSummaryData> GetSalesSummaryAsync(
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var query = _context.Orders
                .Where(o => o.Status != OrderStatus.Cancelled &&
                            o.Status != OrderStatus.Refunded);

            if (startDate.HasValue)
                query = query.Where(o => o.OrderDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(o => o.OrderDate <= endDate.Value);

            var orders = await query.ToListAsync();

            return new SalesSummaryData
            {
                PeriodStart = startDate ?? DateTime.MinValue,
                PeriodEnd = endDate ?? DateTime.UtcNow,
                TotalOrders = orders.Count,
                TotalRevenue = orders.Sum(o => o.Total),
                TotalItemsSold = orders.Sum(o => o.OrderItems.Sum(oi => oi.Quantity)),
                AverageOrderValue = orders.Any() ? orders.Average(o => o.Total) : 0,
                UniqueProductsSold = orders
                    .SelectMany(o => o.OrderItems)
                    .Where(oi => oi.ProductId.HasValue)
                    .Select(oi => oi.ProductId)
                    .Distinct()
                    .Count()
            };
        }
    }
}