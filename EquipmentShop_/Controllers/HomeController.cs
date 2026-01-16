using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Security.Claims;

namespace EquipmentShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProductRepository _productRepository;
        private readonly IOrderRepository _orderRepository;

        public HomeController(ILogger<HomeController> logger, IProductRepository productRepository, IOrderRepository orderRepository)
        {
            _logger = logger;
            _productRepository = productRepository;
            _orderRepository = orderRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var featured = await _productRepository.GetFeaturedAsync(8);
                var newArrivals = await _productRepository.GetNewArrivalsAsync(6);

                IEnumerable<Product> personalized = Enumerable.Empty<Product>();
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation("Текущий пользователь: {UserId}", userId ?? "(не авторизован)");

                if (!string.IsNullOrEmpty(userId))
                {
                    // Получаем заказы через репозиторий
                    var orders = await _orderRepository.GetByUserIdAsync(userId);
                    var orderCount = orders.Count();
                    var purchasedIds = orders
                        .SelectMany(o => o.OrderItems)
                        .Where(oi => oi.ProductId.HasValue)
                        .Select(oi => oi.ProductId.Value)
                        .ToList();

                    _logger.LogInformation("Заказов: {OrderCount}, Куплено товаров (ID): {@ProductIds}", orderCount, purchasedIds);

                    // Получаем персонализированные рекомендации
                    var recs = await _productRepository.GetRecommendedForUserAsync(userId, 1);
                    personalized = recs.Take(1);
                }
                else
                {
                    personalized = featured; // fallback
                }

                ViewBag.FeaturedProducts = featured;
                ViewBag.NewArrivals = newArrivals;
                ViewBag.Personalized = personalized;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных для главной страницы");
                ViewBag.FeaturedProducts = new List<Product>();
                ViewBag.NewArrivals = new List<Product>();
                ViewBag.Personalized = new List<Product>();
                return View();
            }
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