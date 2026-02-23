using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Exceptions;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentShop_.Controllers
{
    public class CartController(
        IShoppingCartService cartService,
        UserManager<ApplicationUser> userManager,
        ILogger<CartController> logger,
        IOrderService orderService,
        IOrderRepository orderRepository,
        IProductRepository productRepository) : Controller
    {
        private readonly IShoppingCartService _cartService = cartService;
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly ILogger<CartController> _logger = logger;
        private readonly IOrderService _orderService = orderService;
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly IProductRepository _productRepository = productRepository;

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Пользователь не авторизован");

        private async Task<ShoppingCart> GetUserCartAsync() => await _cartService.GetUserCartAsync(GetUserId());

        [Authorize, HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetUserId();
                var cart = await GetUserCartAsync();
                var user = await _userManager.FindByIdAsync(userId);

                return View(new CartViewModel
                {
                    CartId = cart.Id,
                    Items = BuildCartItems(cart),
                    Subtotal = cart.Subtotal,
                    ShippingCost = CalculateShippingCost(cart.Subtotal),
                    TaxAmount = CalculateTax(cart.Subtotal),
                    Total = cart.Subtotal + CalculateShippingCost(cart.Subtotal) + CalculateTax(cart.Subtotal),
                    ShippingAddressPreview = user?.HasDefaultAddress == true
                        ? $"{user.City}, {user.Address}"
                        : "Адрес будет указан при оформлении"
                });
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToLogin(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке корзины");
                TempData["Error"] = "Ошибка при загрузке корзины";
                return RedirectToAction("Error", "Home");
            }
        }

        [Authorize, HttpGet("checkout")]
        public async Task<IActionResult> CheckoutPage()
        {
            var cart = await GetUserCartAsync();
            if (cart?.Items == null || cart.Items.Count == 0)
            {
                TempData["Error"] = "Ваша корзина пуста";
                return RedirectToAction(nameof(Index));
            }
            return View("Checkout");
        }

        [Authorize, HttpPost("checkout"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            try
            {
                var userId = GetUserId();
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Пользователь с ID {UserId} не найден в БД (возможно, удалён)", userId);
                    /*await _signInManager.SignOutAsync();*/ // Разлогинить
                    TempData["Error"] = "Ваша учётная запись была удалена. Пожалуйста, войдите снова.";
                    return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
                }

                var cart = await GetUserCartAsync();
                if (cart?.Items == null || cart.Items.Count == 0)
                {
                    TempData["Error"] = "Ваша корзина пуста";
                    return RedirectToAction(nameof(Index));
                }

                var order = await _orderService.CreateOrderFromCartAsync(cart.Id, userId);
                TempData["Success"] = $"Ваш заказ #{order.OrderNumber} принят!";
                return RedirectToAction(nameof(OrderConfirmation), new { orderNumber = order.OrderNumber });
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToLogin(nameof(Checkout));
            }
            catch (EmptyCartException)
            {
                TempData["Error"] = "Корзина пуста";
                return RedirectToAction(nameof(Index));
            }
            catch (CartException ex)
            {
                _logger.LogWarning(ex, "Ошибка при оформлении заказа");
                TempData["Error"] = "Невозможно оформить заказ: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при оформлении заказа");
                TempData["Error"] = "Не удалось создать заказ. Попробуйте позже.";
                return RedirectToAction(nameof(Index));
            }
        }

        [Authorize, HttpGet("order-confirmation")]
        public async Task<IActionResult> OrderConfirmation(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber)) return NotFound();

            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);
            if (order == null || order.UserId != GetUserId()) return NotFound();

            ViewBag.OrderNumber = order.OrderNumber;
            ViewBag.OrderDate = order.OrderDate.ToString("dd.MM.yyyy HH:mm");
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            // Проверка авторизации
            if (!User.Identity.IsAuthenticated)
            {
                HttpContext.Session.SetString("PendingAddToCart", $"{productId},{quantity}");
                return RedirectToAction("Register", "Account",
                    new { returnUrl = returnUrl ?? Url.Action("Details", "Products", new { id = productId }) });
            }

            try
            {
                if (quantity <= 0)
                    return HandleError("Количество должно быть больше 0", productId);

                var product = await _productRepository.GetByIdAsync(productId);
                if (product == null)
                    return HandleError("Товар не найден", productId);

                var cart = await GetUserCartAsync();
                await _cartService.AddItemAsync(cart.Id, productId, quantity);

                TempData["Success"] = $"«{product.Name}» добавлен в корзину";

                // 🔑 Возвращаемся на ту же страницу, откуда был вызов
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                else
                    return RedirectToAction("Details", "Products", new { id = productId });
            }
                    returnUrl = Url.Action("Details", "Products", new { id = productId })
                });
            }
            catch (ProductNotAvailableException ex)
            {
                return HandleProductError(ex, productId, "Товар недоступен");
            }
            catch (InsufficientStockException ex)
            {
                return HandleProductError(ex, productId, "Недостаточно остатков");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении в корзину");
                return HandleError("Произошла ошибка. Попробуйте позже.", productId);
            }
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            try
            {
                var cart = await GetUserCartAsync();

                if (quantity <= 0)
                {
                    await _cartService.RemoveItemAsync(cart.Id, productId);
                    TempData["Success"] = "Товар удалён из корзины";
                }
                else
                {
                    var product = await _productRepository.GetByIdAsync(productId);
                    if (product != null && quantity > product.StockQuantity)
                    {
                        TempData["Error"] = $"Доступно только {product.StockQuantity} шт.";
                        return RedirectToAction(nameof(Index));
                    }
                    await _cartService.UpdateItemQuantityAsync(cart.Id, productId, quantity);
                    TempData["Success"] = "Количество товара обновлено";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToLogin(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении количества");
                TempData["Error"] = "Ошибка при обновлении количества";
                return RedirectToAction(nameof(Index));
            }
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(int productId)
        {
            try
            {
                var cart = await GetUserCartAsync();
                await _cartService.RemoveItemAsync(cart.Id, productId);
                TempData["Success"] = "Товар удалён из корзины";
                return RedirectToAction(nameof(Index));
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToLogin(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении товара");
                TempData["Error"] = "Ошибка при удалении товара";
                return RedirectToAction(nameof(Index));
            }
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                var cart = await GetUserCartAsync();
                await _cartService.ClearCartAsync(cart.Id);
                TempData["Success"] = "Корзина очищена";
                return RedirectToAction(nameof(Index));
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToLogin(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке корзины");
                TempData["Error"] = "Ошибка при очистке корзины";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> GetCartSummary()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return Json(new CartSummaryViewModel { ItemCount = 0, Total = 0m });

                var cart = await _cartService.GetUserCartAsync(userId);
                return Json(new CartSummaryViewModel
                {
                    ItemCount = cart.TotalItems,
                    Total = cart.Subtotal
                });
            }
            catch
            {
                return Json(new CartSummaryViewModel { ItemCount = 0, Total = 0m });
            }
        }

        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> MiniCart()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return PartialView("_MiniCartPartial", new MiniCartViewModel());

                var cart = await _cartService.GetUserCartAsync(userId);
                return PartialView("_MiniCartPartial", new MiniCartViewModel
                {
                    Items = BuildMiniCartItems(cart),
                    TotalItems = cart.TotalItems,
                    Subtotal = cart.Subtotal
                });
            }
            catch
            {
                return PartialView("_MiniCartPartial", new MiniCartViewModel());
            }
        }

        // === Вспомогательные методы ===
        private static List<CartItemViewModel> BuildCartItems(ShoppingCart cart) => cart.Items?.Select(item => new CartItemViewModel
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
        }).ToList() ?? [];

        private static List<CartItemViewModel> BuildMiniCartItems(ShoppingCart cart) => cart.Items?.Select(item => new CartItemViewModel
        {
            Id = item.Id,
            ProductId = item.ProductId,
            ProductName = item.Product?.Name ?? "Товар",
            ImageUrl = item.Product?.ImageUrl ?? "/images/products/default.jpg",
            Price = item.Price,
            Quantity = item.Quantity
        }).ToList() ?? [];

        private IActionResult RedirectToLogin(string action, string controller = "Cart", object? routeValues = null) => RedirectToAction("Login", "Account", new { returnUrl = Url.Action(action, controller, routeValues) });

        private IActionResult HandleError(string message, int productId) => RedirectToAction("Details", "Products", new { id = productId, error = message });

        private IActionResult HandleProductError(Exception ex, int productId, string context)
        {
            _logger.LogWarning(ex, "{Context}: {ProductId}", context, productId);
            TempData["Error"] = ex.Message;
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        private static decimal CalculateShippingCost(decimal subtotal) => subtotal >= AppConstants.FreeShippingThreshold ? 0m : AppConstants.DefaultShippingCostMinsk;

        private static decimal CalculateTax(decimal subtotal) => subtotal * AppConstants.DefaultTaxRate;
    }
}