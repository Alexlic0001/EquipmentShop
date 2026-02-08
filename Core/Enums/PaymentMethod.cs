using System.ComponentModel.DataAnnotations;

namespace EquipmentShop.Core.Enums
{
    public enum PaymentMethod
    {
        [Display(Name = "Банковская карта")]
        Card = 1,

        [Display(Name = "Наличные при получении")]
        CashOnDelivery = 2,

        [Display(Name = "Банковский перевод")]
        BankTransfer = 3,

        [Display(Name = "ЕРИП")]
        ERIP = 4,

        [Display(Name = "Оплата курьеру")]
        CardToCourier = 5
    }
}
