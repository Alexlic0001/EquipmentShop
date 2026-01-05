using Microsoft.AspNetCore.Mvc;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels;
using EquipmentShop.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using EquipmentShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentShop.Controllers
{
    [Authorize] // ТОЛЬКО авторизованные пользователи
    public class CartController : Controller
    {
        private readonly IShoppingCartService _cartService;
        private readonly IProductRepository _productRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CartController> _logger;
        private readonly AppDbContext _context; // Добавлен контекст

        public CartController(
            IShoppingCartService cartService,
            IProductRepository productRepository,
            UserManager<ApplicationUser> userManager,
            ILogger<CartController> logger,
            AppDbContext context) // Добавлен параметр
        {
            _cartService = cartService;
            _productRepository = productRepository;
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        // Получаем ID корзины пользователя (основано на UserId)
        private string GetUserCartId()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Пользователь не авторизован");
            }

            // Используем userId как основу для ID корзины
            return $"cart_{userId}";
        }

        // Получаем ID текущего пользователя
        private string GetUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Пользователь не авторизован");
            }
            return userId;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetUserId();

                // Получаем корзину пользователя (или создаем новую)
                var cart = await _cartService.GetUserCartAsync(userId);

                var viewModel = new CartViewModel
                {
                    CartId = cart.Id,
                    Items = cart.Items?.Select(item => new CartItemViewModel
                    {
                        Id = item.Id,
                        ProductId = item.ProductId,
                        ProductName = item.Product?.Name ?? "Товар",
                        ProductSlug = item.Product?.Slug ?? "",
                        ImageUrl = item.Product?.ImageUrl ?? "/images/products/default.jpg",
                        Price = item.Price,
                        Quantity = item.Quantity,
                        MaxQuantity = Math.Min(item.Product?.StockQuantity ?? 10, 10),
                        IsAvailable = item.Product?.IsAvailable ?? false,
                        SelectedAttributes = item.SelectedAttributes
                    }).ToList() ?? new List<CartItemViewModel>(),
                    Subtotal = cart.Subtotal,
                    ShippingCost = CalculateShippingCost(cart.Subtotal),
                    TaxAmount = CalculateTax(cart.Subtotal),
                    Total = cart.Subtotal + CalculateShippingCost(cart.Subtotal) + CalculateTax(cart.Subtotal)
                };

                return View(viewModel);
            }
            catch (UnauthorizedAccessException)
            {
                // Редирект на страницу входа если не авторизован
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке корзины");
                TempData["Error"] = "Ошибка при загрузке корзины";
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            try
            {
                // Проверяем авторизацию
                var userId = GetUserId();

                if (quantity <= 0)
                {
                    TempData["Error"] = "Количество должно быть больше 0";
                    return RedirectToAction("Details", "Products", new { id = productId });
                }

                // ИСПРАВЛЕНО: Используем прямой запрос к контексту вместо репозитория
                var product = await _context.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == productId);

                if (product == null)
                {
                    TempData["Error"] = "Товар не найден";
                    return RedirectToAction("Index", "Products");
                }

                // Получаем корзину пользователя
                var cart = await _cartService.GetUserCartAsync(userId);

                await _cartService.AddItemAsync(cart.Id, productId, quantity);
                TempData["Success"] = $"«{product.Name}» добавлен в корзину";

                return RedirectToAction("Details", "Products", new { id = productId });
            }
            catch (UnauthorizedAccessException)
            {
                // Сохраняем информацию о товаре для добавления после входа
                HttpContext.Session.SetString("PendingAddToCart", $"{productId},{quantity}");

                // Редирект на страницу входа
                return RedirectToAction("Login", "Account", new
                {
                    returnUrl = Url.Action("Details", "Products", new { id = productId })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении товара в корзину");
                TempData["Error"] = ex.Message.Contains("недостаточно") ? ex.Message : "Ошибка при добавлении в корзину";
                return RedirectToAction("Details", "Products", new { id = productId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);

                if (quantity <= 0)
                {
                    await _cartService.RemoveItemAsync(cart.Id, productId);
                    TempData["Success"] = "Товар удален из корзины";
                }
                else
                {
                    var product = await _context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == productId);

                    if (product != null && quantity > product.StockQuantity)
                    {
                        TempData["Error"] = $"Доступно только {product.StockQuantity} шт.";
                        return RedirectToAction("Index");
                    }

                    await _cartService.UpdateItemQuantityAsync(cart.Id, productId, quantity);
                    TempData["Success"] = "Количество товара обновлено";
                }

                return RedirectToAction("Index");
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении количества товара");
                TempData["Error"] = "Ошибка при обновлении количества товара";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(int productId)
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);

                await _cartService.RemoveItemAsync(cart.Id, productId);
                TempData["Success"] = "Товар удален из корзины";

                return RedirectToAction("Index");
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении товара из корзины");
                TempData["Error"] = "Ошибка при удалении товара";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);

                await _cartService.ClearCartAsync(cart.Id);
                TempData["Success"] = "Корзина очищена";

                return RedirectToAction("Index");
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке корзины");
                TempData["Error"] = "Ошибка при очистке корзины";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCartSummary()
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);

                var summary = new CartSummaryViewModel
                {
                    ItemCount = cart.TotalItems,
                    Total = cart.Subtotal
                };

                return Json(summary);
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new CartSummaryViewModel { ItemCount = 0, Total = 0m }); // Пустая корзина для неавторизованных
            }
            catch
            {
                return Json(new CartSummaryViewModel { ItemCount = 0, Total = 0m });
            }
        }

        [HttpGet]
        public async Task<IActionResult> MiniCart()
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);

                var miniCartViewModel = new MiniCartViewModel
                {
                    Items = cart.Items?.Select(item => new CartItemViewModel
                    {
                        Id = item.Id,
                        ProductId = item.ProductId,
                        ProductName = item.Product?.Name ?? "Товар",
                        ImageUrl = item.Product?.ImageUrl ?? "/images/products/default.jpg",
                        Price = item.Price,
                        Quantity = item.Quantity
                    }).ToList() ?? new List<CartItemViewModel>(),
                    TotalItems = cart.TotalItems,
                    Subtotal = cart.Subtotal
                };

                return PartialView("_MiniCartPartial", miniCartViewModel);
            }
            catch (UnauthorizedAccessException)
            {
                // Для неавторизованных показываем пустую корзину
                return PartialView("_MiniCartPartial", new MiniCartViewModel());
            }
            catch
            {
                return PartialView("_MiniCartPartial", new MiniCartViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Count()
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);
                return Json(new { count = cart.TotalItems });
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { count = 0 });
            }
            catch
            {
                return Json(new { count = 0 });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveToWishlist(int productId)
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);

                // Удаляем из корзины
                await _cartService.RemoveItemAsync(cart.Id, productId);

                // Ищем товар
                var product = await _context.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == productId);

                if (product != null)
                {
                    // TODO: Добавить логику перемещения в список желаний
                    TempData["Success"] = $"«{product.Name}» перемещен в список желаний";
                }
                else
                {
                    TempData["Success"] = "Товар удален из корзины";
                }

                return RedirectToAction("Index");
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при перемещении товара в список желаний");
                TempData["Error"] = "Ошибка при перемещении товара";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Validate()
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);

                if (cart.IsEmpty)
                {
                    return Json(new { valid = false, message = "Корзина пуста" });
                }

                var isValid = await _cartService.ValidateCartAsync(cart.Id);

                if (!isValid)
                {
                    return Json(new { valid = false, message = "Некоторые товары недоступны" });
                }

                return Json(new { valid = true });
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { valid = false, message = "Требуется авторизация" });
            }
            catch
            {
                return Json(new { valid = false, message = "Ошибка проверки корзины" });
            }
        }

        // Вспомогательные методы
        private decimal CalculateShippingCost(decimal subtotal)
        {
            return subtotal >= 500 ? 0 : 10m;
        }

        private decimal CalculateTax(decimal subtotal)
        {
            return subtotal * 0.20m;
        }
    }
}