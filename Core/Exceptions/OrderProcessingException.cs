using EquipmentShop.Core.Enums;

namespace EquipmentShop.Core.Exceptions
{
    public class OrderProcessingException : Exception
    {
        public string OrderNumber { get; }
        public OrderStatus OrderStatus { get; }

        public OrderProcessingException(string orderNumber, OrderStatus status, string message)
            : base($"Ошибка обработки заказа {orderNumber}: {message}")
        {
            OrderNumber = orderNumber;
            OrderStatus = status;
        }

    }
}
