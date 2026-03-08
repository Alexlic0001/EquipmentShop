// EquipmentShop.Core/Interfaces/IOrderService.cs
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.ViewModels;

namespace EquipmentShop.Core.Interfaces
{
    public interface IOrderService
    {
        Task<Order> CreateOrderFromCartAsync(string cartId, string userId, CheckoutViewModel? checkoutModel = null);
        Task CancelOrderAsync(int orderId, string reason = "");
        Task ProcessOrderAsync(int orderId);
        Task ShipOrderAsync(int orderId, string trackingNumber, string shippingProvider);
        Task MarkAsDeliveredAsync(int orderId);
        Task UpdatePaymentStatusAsync(int orderId, PaymentStatus status);
    }
}