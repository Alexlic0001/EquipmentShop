// EquipmentShop.Core/ViewModels/Admin/OrderImportModel.cs
namespace EquipmentShop.Core.ViewModels.Admin;

public class OrderImportModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string PaymentMethod { get; set; } = "Card";
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public string Items { get; set; } = string.Empty; // Формат: "Ноутбук ASUS=2899.99=1;Видеокарта RTX=1899.99=1"
}