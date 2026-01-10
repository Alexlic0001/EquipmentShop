using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;


namespace EquipmentShop.Core.Interfaces
{
    public interface IOrderRepository : IRepository<Order>
    {
        Task<Order?> GetByOrderNumberAsync(string orderNumber);
        Task<Order?> GetWithItemsAsync(int id);
        Task<IEnumerable<Order>> GetByUserIdAsync(string userId);
        Task<IEnumerable<Order>> GetByEmailAsync(string email);
        Task<IEnumerable<Order>> GetRecentOrdersAsync(int count = 10);
        Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status);
        Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<OrderStats> GetOrderStatsAsync();
        Task UpdateStatusAsync(int orderId, OrderStatus status);
        Task UpdatePaymentStatusAsync(int orderId, PaymentStatus status);
        Task<int> GetTotalOrdersCountAsync();
        Task<decimal> GetTotalRevenueAsync();
        Task<Order> AddOrderWithItemsAsync(Order order);

        Task<bool> UpdateOrderStatusAsync(string orderNumber, OrderStatus newStatus);
    }

    public class OrderStats
    {
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int ProcessingOrders { get; set; }
        public int CompletedOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public Dictionary<string, int> OrdersByStatus { get; set; } = new();
        public Dictionary<string, int> OrdersByMonth { get; set; } = new();
    }
}
