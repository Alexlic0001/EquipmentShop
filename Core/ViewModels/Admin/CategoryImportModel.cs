using EquipmentShop.Core.Constants;

namespace EquipmentShop.Core.ViewModels.Admin;

public class CategoryImportModel
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Parent { get; set; } = string.Empty; // Имя родительской категории
    public int DisplayOrder { get; set; }
    public string IsActive { get; set; } = "Да";
    public string ShowInMenu { get; set; } = "Да";
    public string ImageUrl { get; set; } = AppConstants.DefaultCategoryImage;
}