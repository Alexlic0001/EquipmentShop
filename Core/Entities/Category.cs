namespace EquipmentShop.Core.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? IconClass { get; set; } = "bi bi-pc-display";

        // Иерархия
        public int? ParentCategoryId { get; set; }
        public Category? ParentCategory { get; set; }
        public ICollection<Category> SubCategories { get; set; } = new List<Category>();

        // Настройки отображения
        public int DisplayOrder { get; set; }
        public bool ShowInMenu { get; set; } = true;
        public bool ShowOnHomepage { get; set; } = false;
        public bool IsActive { get; set; } = true;

        // Навигационные свойства
        public ICollection<Product> Products { get; set; } = new List<Product>();

    }
}
