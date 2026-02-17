using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EquipmentShop_.Controllers
{
    public class ProductsController(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IPricingService pricingService) : Controller
    {
        private readonly IProductRepository _productRepository = productRepository;
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly IPricingService _pricingService = pricingService;

        public async Task<IActionResult> Index(string? search = null)
        {
            ViewData["Title"] = "Каталог товаров";
            ViewData["Search"] = search;

            var products = await _productRepository.GetAllAsync();
            if (!string.IsNullOrEmpty(search))
                products = FilterProducts(products, search);

            ViewBag.Categories = (await _categoryRepository.GetAllAsync()).ToList();
            return View(await _pricingService.ApplyFinalPricesToProductsAsync(products));
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();

            product.Price = await _pricingService.CalculateFinalPriceAsync(product.Id);
            ViewData["Title"] = product.Name;
            return View(product);
        }

        private static IEnumerable<Product> FilterProducts(IEnumerable<Product> products, string searchTerm)
        {
            var term = searchTerm.ToLowerInvariant();
            return products.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Brand.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }
}