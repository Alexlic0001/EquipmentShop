// EquipmentShop.Core/ViewModels/Admin/ProductImportModel.cs
using EquipmentShop.Core.Constants;

namespace EquipmentShop.Core.ViewModels.Admin;

public class ProductImportModel
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? OldPrice { get; set; }
    public string ImageUrl { get; set; } = AppConstants.DefaultProductImage;
    public string Brand { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string IsAvailable { get; set; } = "Да";
    public string IsFeatured { get; set; } = "Нет";
    public string IsNew { get; set; } = "Нет";
    public string Tags { get; set; } = string.Empty;
    public string Specifications { get; set; } = string.Empty;
}