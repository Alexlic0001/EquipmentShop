using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace EquipmentShop_.Controllers
{
    [Authorize]
    public class WishlistController(
        IWishlistService wishlistService,
        IShoppingCartService cartService,
        ILogger<WishlistController> logger) : Controller
    {
        private readonly IWishlistService _wishlistService = wishlistService;
        private readonly IShoppingCartService _cartService = cartService;
        private readonly ILogger<WishlistController> _logger = logger;

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

        private async Task<IActionResult> HandleWishlistAction(Func<string, Task> action, string successMessage)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                    return RedirectToLogin();

                await action(userId);
                TempData["Success"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выполнении действия с избранным");
                TempData["Error"] = "Ошибка при выполнении операции";
                return RedirectToAction(nameof(Index));
            }
        }

        private RedirectToActionResult RedirectToLogin(string? returnUrl = null) =>
                  RedirectToAction("Login", "Account", new { returnUrl });

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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveToCart(int productId, int quantity = 1) =>
            await HandleWishlistAction(
                (userId) => _wishlistService.MoveToCartAsync(userId, productId, quantity),
                "Товар добавлен в корзину"
            );

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int productId) =>
            await HandleWishlistAction(
                (userId) => _wishlistService.RemoveItemAsync(userId, productId),
                "Товар удалён из избранного"
            );

        [HttpGet]
        public async Task<IActionResult> GetWishlistCount()
        {
            try
            {
                var userId = GetUserId();
                var count = string.IsNullOrEmpty(userId)
                    ? 0
                    : await _wishlistService.GetWishlistItemCountAsync(userId);

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
                    return RedirectToLogin(Url.Action(nameof(Index)));

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