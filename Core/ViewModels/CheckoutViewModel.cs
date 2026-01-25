namespace EquipmentShop.Core.ViewModels
{
    public class CheckoutViewModel
    {
        public string ShippingAddress { get; set; } = string.Empty;
        public string ShippingCity { get; set; } = "Минск";
        public string ShippingRegion { get; set; } = "Минская обл.";
        public string ShippingPostalCode { get; set; } = string.Empty;
        public string ShippingCountry { get; set; } = "Беларусь";

        // Опционально: использовать адрес по умолчанию
        public bool UseDefaultAddress { get; set; } = true;
    }
}