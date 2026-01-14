// EquipmentShop.Core/ViewModels/Admin/UserOrderExportModel.cs
namespace EquipmentShop.Core.ViewModels.Admin;

public class UserOrderExportModel
{
    // Пользователь
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer";

    // Заказ
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "Pending";
    public string PaymentMethod { get; set; } = "Card";
    public decimal Total { get; set; }

    // Адрес
    public string ShippingAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    // Товары в заказе (одна строка)
    public string Items { get; set; } = string.Empty; // Формат: "Товар1=100=2;Товар2=200=1"
}