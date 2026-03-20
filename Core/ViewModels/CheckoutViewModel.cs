using System.ComponentModel.DataAnnotations;

namespace EquipmentShop.Core.ViewModels
{
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Адрес доставки обязателен")]
        [Display(Name = "Адрес доставки *")]
        public string ShippingAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Город обязателен")]
        [Display(Name = "Город *")]
        public string ShippingCity { get; set; } = "Минск";

        [Display(Name = "Область")]
        public string ShippingRegion { get; set; } = "Минская обл.";

        [Display(Name = "Почтовый индекс")]
        public string ShippingPostalCode { get; set; } = string.Empty;

        [Display(Name = "Страна")]
        public string ShippingCountry { get; set; } = "Беларусь";

        // Опционально: использовать адрес по умолчанию
        public bool UseDefaultAddress { get; set; } = true;
    }
}