using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EquipmentShop.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly IWishlistService _wishlistService;
        private readonly IShoppingCartService _cartService;
        private readonly ILogger<WishlistController> _logger;

        public WishlistController(
            IWishlistService wishlistService,
            IShoppingCartService cartService,
            ILogger<WishlistController> logger)
        {
            _wishlistService = wishlistService;
            _cartService = cartService;
            _logger = logger;
        }

        private string? GetUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("UserId не найден в Claims. Claims: {Claims}",
                    string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
            }
            return userId;
        }

        [HttpPost]
        public async Task<IActionResult> Add(int productId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Попытка добавить в избранное без авторизации");
                    return Json(new { success = false, requiresAuth = true, message = "Требуется авторизация" });
                }

                _logger.LogInformation("Добавление товара {ProductId} в избранное пользователя {UserId}", productId, userId);

                await _wishlistService.AddItemAsync(userId, productId);

                _logger.LogInformation("Товар {ProductId} успешно добавлен в избранное пользователя {UserId}", productId, userId);
                return Json(new { success = true, message = "Товар добавлен в избранное" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении товара {ProductId} в избранное", productId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost] // ← Добавьте этот атрибут
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveToCart(int productId, int quantity = 1)
        {
            try
            {
                var userId = GetUserId();
                await _wishlistService.MoveToCartAsync(userId, productId, quantity);
                TempData["Success"] = "Товар добавлен в корзину";
                return RedirectToAction("Index", "Cart");
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении в корзину");
                TempData["Error"] = "Ошибка при добавлении в корзину";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int productId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                    return RedirectToAction("Login", "Account");

                await _wishlistService.RemoveItemAsync(userId, productId);
                TempData["Success"] = "Товар удалён из избранного";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка удаления товара {ProductId}", productId);
                TempData["Error"] = "Ошибка при удалении";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetWishlistCount()
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { count = 0 });
                }

                var count = await _wishlistService.GetWishlistItemCountAsync(userId);
                return Json(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении количества товаров в избранном");
                return Json(new { count = 0 });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Wishlist") });
                }

                var wishlist = await _wishlistService.GetOrCreateWishlistAsync(userId);
                return View(wishlist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке списка желаний");
                TempData["Error"] = "Ошибка при загрузке списка желаний";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}