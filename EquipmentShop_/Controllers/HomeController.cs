
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EquipmentShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProductRepository _productRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IPricingService _pricingService; // ← 

        public HomeController(
            ILogger<HomeController> logger,
            IProductRepository productRepository,
            IOrderRepository orderRepository,
            IPricingService pricingService) // ← 
        {
            _logger = logger;
            _productRepository = productRepository;
            _orderRepository = orderRepository;
            _pricingService = pricingService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Загружаем товары
                var featured = await _productRepository.GetFeaturedAsync(8);
                var newArrivals = await _productRepository.GetNewArrivalsAsync(8);
                var bestsellers = await _productRepository.GetBestsellersAsync(8);

                // Применяем правила ценообразования
                ViewBag.Featured = await _pricingService.ApplyFinalPricesToProductsAsync(featured);
                ViewBag.NewArrivals = await ApplyFinalPricesAsync(newArrivals);
                ViewBag.Bestsellers = await ApplyFinalPricesAsync(bestsellers);

                // Персонализация — ТОЛЬКО для авторизованных
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var personalized = await _productRepository.GetRecommendedForUserAsync(userId, 3);
                    ViewBag.Personalized = await ApplyFinalPricesAsync(personalized);
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

        // Вспомогательный метод для расчёта финальных цен
        private async Task<List<Product>> ApplyFinalPricesAsync(IEnumerable<Product> products)
        {
            var result = new List<Product>();
            foreach (var p in products)
            {
                var finalPrice = await _pricingService.CalculateFinalPriceAsync(p.Id);
                p.Price = finalPrice; // Обновляем цену для отображения
                result.Add(p);
            }
            return result;
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}