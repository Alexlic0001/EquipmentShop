
using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace EquipmentShop.Core.ViewModels
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public decimal Total { get; set; }
        public DateTime OrderDate { get; set; }
        public List<OrderItemViewModel> Items { get; set; } = new();

        public string StatusDisplay => GetStatusDisplay();
        public string PaymentStatusDisplay => GetPaymentStatusDisplay();
        public string FormattedTotal => $"{Total:N0}{AppConstants.CurrencySymbol}";
        public bool CanBeCancelled => Status == OrderStatus.Pending || Status == OrderStatus.Processing;
        public bool IsPaid => PaymentStatus == PaymentStatus.Paid;

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

    public class OrderItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
        public string? Attributes { get; set; }
    }

    public class CreateOrderViewModel
    {
        [Required(ErrorMessage = "Имя обязательно")]
        [Display(Name = "ФИО")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Некорректный Email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Телефон обязателен")]
        [Phone(ErrorMessage = "Некорректный номер телефона")]
        [Display(Name = "Телефон")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Адрес обязателен")]
        [Display(Name = "Адрес доставки")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "Город")]
        public string City { get; set; } = "Минск";

        [Display(Name = "Область")]
        public string? Region { get; set; }

        [Display(Name = "Индекс")]
        public string? PostalCode { get; set; }

        [Display(Name = "Способ оплаты")]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Card;

        [Display(Name = "Способ доставки")]
        public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.Courier;

        [Display(Name = "Комментарий к заказу")]
        public string? Notes { get; set; }

        public bool AcceptTerms { get; set; }
        public bool SubscribeToNewsletter { get; set; }
    }

    public class OrderConfirmationViewModel
    {
        public string OrderNumber { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public PaymentMethod PaymentMethod { get; set; }

        public string FormattedTotal => Total.ToString("C0");
        public bool RequiresPayment => PaymentMethod != PaymentMethod.CashOnDelivery;
    }
}
