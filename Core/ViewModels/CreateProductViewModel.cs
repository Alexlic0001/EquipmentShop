using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EquipmentShop.Core.ViewModels
{
    public class CreateProductViewModel
    {
        [Required(ErrorMessage = "Название товара обязательно")]
        [Display(Name = "Название товара")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "URL-адрес (slug)")]
        public string Slug { get; set; } = string.Empty;

        [Required(ErrorMessage = "Описание товара обязательно")]
        [Display(Name = "Описание")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Краткое описание")]
        public string ShortDescription { get; set; } = string.Empty;

        [Required(ErrorMessage = "Цена товара обязательна")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Цена должна быть больше 0")]
        [Display(Name = "Цена")]
        public decimal Price { get; set; }

        [Display(Name = "Старая цена")]
        public decimal? OldPrice { get; set; }

        [Display(Name = "Изображение")]
        public IFormFile? ImageFile { get; set; }

        public string ImageUrl { get; set; } = "/images/products/default.jpg";

        [Required(ErrorMessage = "Категория обязательна")]
        [Display(Name = "Категория")]
        public int CategoryId { get; set; }

        [Display(Name = "Бренд")]
        public string Brand { get; set; } = string.Empty;

        [Required(ErrorMessage = "Количество товара обязательно")]
        [Range(0, int.MaxValue, ErrorMessage = "Количество не может быть отрицательным")]
        [Display(Name = "Количество на складе")]
        public int StockQuantity { get; set; } = 0;

        [Display(Name = "Минимальный запас")]
        public int MinStockThreshold { get; set; } = 5;

        [Display(Name = "Рекомендуемый товар")]
        public bool IsFeatured { get; set; }

        [Display(Name = "Новинка")]
        public bool IsNew { get; set; }

        [Display(Name = "Доступен для продажи")]
        public bool IsAvailable { get; set; } = true;

        [Display(Name = "Теги (через запятую)")]
        public string TagsString { get; set; } = string.Empty;

        [Display(Name = "Характеристики")]
        public string SpecificationsString { get; set; } = string.Empty;

        [Display(Name = "Meta Title")]
        public string MetaTitle { get; set; } = string.Empty;

        [Display(Name = "Meta Description")]
        public string MetaDescription { get; set; } = string.Empty;

        [Display(Name = "Meta Keywords")]
        public string MetaKeywords { get; set; } = string.Empty;
    }
}