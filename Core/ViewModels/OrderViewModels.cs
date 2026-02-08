using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace EquipmentShop.Core.ViewModels
{
    public class OrderViewModel
    {
        public OrderStatus Status { get; set; }

        public PaymentStatus PaymentStatus { get; set; }

        public string StatusDisplay => GetStatusDisplay();
        public string PaymentStatusDisplay => GetPaymentStatusDisplay();


        private string GetStatusDisplay()
        {
            return Status switch
            {
                OrderStatus.Pending => "Ожидает обработки",
                OrderStatus.Processing => "В обработке",
                OrderStatus.AwaitingPayment => "Ожидает оплаты",
                OrderStatus.Paid => "Оплачен",
                OrderStatus.Shipped => "Передан в доставку",
                OrderStatus.Delivered => "Доставлен",
                OrderStatus.Cancelled => "Отменен",
                OrderStatus.Refunded => "Возврат",
                OrderStatus.OnHold => "На удержании",
                _ => "Неизвестно"
            };
        }

        private string GetPaymentStatusDisplay()
        {
            return PaymentStatus switch
            {
                PaymentStatus.Pending => "Ожидает оплаты",
                PaymentStatus.Paid => "Оплачен",
                PaymentStatus.Failed => "Ошибка оплаты",
                PaymentStatus.Refunded => "Возвращен",
                PaymentStatus.PartiallyRefunded => "Частично возвращен",
                _ => "Неизвестно"
            };
        }
    }
}
