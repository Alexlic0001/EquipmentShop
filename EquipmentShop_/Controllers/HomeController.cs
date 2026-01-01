using Microsoft.AspNetCore.Mvc;
using EquipmentShop.Core.Interfaces;

namespace EquipmentShop.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IProductRepository _productRepository;

    public HomeController(ILogger<HomeController> logger, IProductRepository productRepository)
    {
        _logger = logger;
        _productRepository = productRepository;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var featuredProducts = await _productRepository.GetFeaturedAsync(8);
            var newArrivals = await _productRepository.GetNewArrivalsAsync(6);

            ViewBag.FeaturedProducts = featuredProducts;
            ViewBag.NewArrivals = newArrivals;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке данных для главной страницы");
            return View(new List<Core.Entities.Product>());
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