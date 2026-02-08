
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EquipmentShop.Controllers
{
    public class ProductsController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IPricingService _pricingService;

        public ProductsController(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IPricingService pricingService)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _pricingService = pricingService;
        }

        public async Task<IActionResult> Index(string? search = null)
        {
            ViewData["Title"] = "Каталог товаров";
            ViewData["Search"] = search;

            // Получаем все товары
            var products = await _productRepository.GetAllAsync();

            // Фильтрация по поиску
            if (!string.IsNullOrEmpty(search))
            {
                var searchTerm = search.ToLowerInvariant();
                products = products.Where(p =>
                    p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    p.Brand.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            // Рассчитываем финальные цены
            var productsWithFinalPrices = await _pricingService.ApplyFinalPricesToProductsAsync(products);


            var allCategories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = allCategories.ToList();

            return View(productsWithFinalPrices);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Рассчитываем и устанавливаем финальную цену
            var finalPrice = await _pricingService.CalculateFinalPriceAsync(product.Id);
            product.Price = finalPrice;

            ViewData["Title"] = product.Name;
            return View(product);
        }
    }
}