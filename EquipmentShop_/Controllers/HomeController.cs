using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EquipmentShop_.Controllers
{
    public class HomeController(
        ILogger<HomeController> logger,
        IProductRepository productRepository,
        IOrderRepository orderRepository,
        IPricingService pricingService) : Controller
    {
        private readonly ILogger<HomeController> _logger = logger;
        private readonly IProductRepository _productRepository = productRepository;
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly IPricingService _pricingService = pricingService;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Загружаем товары
                ViewBag.Featured = await LoadProductsAsync(_productRepository.GetFeaturedAsync(8));
                ViewBag.NewArrivals = await LoadProductsAsync(_productRepository.GetNewArrivalsAsync(8));
                ViewBag.Bestsellers = await LoadProductsAsync(_productRepository.GetBestsellersAsync(8));

                // Персонализация — только для авторизованных
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    ViewBag.Personalized = await LoadProductsAsync(
                        _productRepository.GetRecommendedForUserAsync(userId, 3));
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке главной страницы");
                ViewBag.Featured = new List<Product>();
                ViewBag.NewArrivals = new List<Product>();
                ViewBag.Bestsellers = new List<Product>();
                ViewBag.Personalized = null;
                return View();
            }
        }

        private async Task<List<Product>> LoadProductsAsync(Task<IEnumerable<Product>> productsTask)
        {
            var products = await productsTask;
            var result = new List<Product>();
            foreach (var p in products)
            {
                p.Price = await _pricingService.CalculateFinalPriceAsync(p.Id);
                result.Add(p);
            }
            return result;
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}