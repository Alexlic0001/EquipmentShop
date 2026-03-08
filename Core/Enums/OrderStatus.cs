using System.ComponentModel.DataAnnotations;

namespace EquipmentShop.Core.Enums
{
    public enum OrderStatus
    {
        [Display(Name = "Ожидает обработки")]
        Pending = 1,

        [Display(Name = "В обработке")]
        Processing = 2,

        [Display(Name = "Ожидает оплаты")]
        AwaitingPayment = 3,

        [Display(Name = "Оплачен")]
        Paid = 4,

        [Display(Name = "Передан в доставку")]
        Shipped = 5,

        [Display(Name = "Доставлен")]
        Delivered = 6,

        [Display(Name = "Отменен")]
        Cancelled = 7,

        [Display(Name = "Возврат")]
        Refunded = 8,

    }
}
