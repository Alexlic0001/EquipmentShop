
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
        private readonly IPricingService _pricingService; // ← Добавлено

        public HomeController(
            ILogger<HomeController> logger,
            IProductRepository productRepository,
            IOrderRepository orderRepository,
            IPricingService pricingService) // ← Внедрено
        {
            _logger = logger;
            _productRepository = productRepository;
            _orderRepository = orderRepository;
            _pricingService = pricingService;
        }

        //public async Task<IActionResult> Index()
        //{
        //    // Получаем ID текущего пользователя
        //    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        //    _logger.LogInformation("Текущий пользователь: {UserId}",
        //        string.IsNullOrEmpty(userId) ? "(не авторизован)" : userId);

        //    // Загружаем глобальные блоки
        //    var featured = await _productRepository.GetFeaturedAsync(6);
        //    var newArrivals = await _productRepository.GetNewArrivalsAsync(6);
        //    var onSale = await _productRepository.GetOnSaleAsync(6);

        //    // Рассчитываем финальные цены для всех товаров
        //    var featuredWithPrices = await ApplyFinalPricesAsync(featured);
        //    var newArrivalsWithPrices = await ApplyFinalPricesAsync(newArrivals);
        //    var onSaleWithPrices = await ApplyFinalPricesAsync(onSale);

        //    ViewBag.Featured = featuredWithPrices;
        //    ViewBag.NewArrivals = newArrivalsWithPrices;
        //    ViewBag.OnSale = onSaleWithPrices;

        //    // Персонализированные рекомендации (только 1 товар)
        //    IEnumerable<Product> personalized = new List<Product>();
        //    if (!string.IsNullOrEmpty(userId))
        //    {
        //        var recs = await _productRepository.GetRecommendedForUserAsync(userId, 1);
        //        personalized = await ApplyFinalPricesAsync(recs);
        //    }

        //    ViewBag.Personalized = personalized;

        //    return View();
        //}

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var featured = await _productRepository.GetFeaturedAsync(8);
                var newArrivals = await _productRepository.GetNewArrivalsAsync(8);
                var bestsellers = await _productRepository.GetBestsellersAsync(8);

                ViewBag.Featured = featured;
                ViewBag.NewArrivals = newArrivals;
                ViewBag.Bestsellers = bestsellers;

                // Персонализация — ТОЛЬКО для авторизованных
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var personalized = await _productRepository.GetRecommendedForUserAsync(userId, 8);
                    ViewBag.Personalized = personalized;
                }
                // Для гостей ViewBag.Personalized == null → не отображается

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