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

namespace EquipmentShop.Controllers
{
    public class CartController : Controller
    {
        private readonly IShoppingCartService _cartService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CartController> _logger;
        private readonly IOrderService _orderService;
        private readonly IOrderRepository _orderRepository;     //
        private readonly IProductRepository _productRepository; // 


        public CartController(
            IShoppingCartService cartService,
            UserManager<ApplicationUser> userManager,
            ILogger<CartController> logger,
            IOrderService orderService,
            IOrderRepository orderRepository,        // 
            IProductRepository productRepository)    // 
        {
            _cartService = cartService;
            _userManager = userManager;
            _logger = logger;
            _orderService = orderService;
            _orderRepository = orderRepository;      // 
            _productRepository = productRepository;  //
        }

        private string GetUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Пользователь не авторизован");
            return userId;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);
                var user = await _userManager.FindByIdAsync(userId);

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
                    Total = cart.Subtotal + CalculateShippingCost(cart.Subtotal) + CalculateTax(cart.Subtotal),
                    ShippingAddressPreview = user?.HasDefaultAddress == true
                        ? $"{user.City}, {user.Address}"
                        : "Адрес будет указан при оформлении"
                };
                return View(viewModel);
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке корзины");
                TempData["Error"] = "Ошибка при загрузке корзины";
                return RedirectToAction("Error", "Home");
            }
        }

        // === GET: /cart/checkout ===
        [Authorize]
        [HttpGet("checkout")]
        public async Task<IActionResult> CheckoutPage()
        {
            var userId = GetUserId();
            var cart = await _cartService.GetUserCartAsync(userId);
            if (cart?.Items == null || !cart.Items.Any())
            {
                TempData["Error"] = "Ваша корзина пуста";
                return RedirectToAction("Index");
            }
            // Можно показать страницу подтверждения без формы
            return View("Checkout"); 
        }

        // === POST: /cart/checkout ===
        [Authorize]
        [HttpPost("checkout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);
                if (cart?.Items == null || !cart.Items.Any())
                {
                    TempData["Error"] = "Ваша корзина пуста";
                    return RedirectToAction("Index");
                }

                var order = await _orderService.CreateOrderFromCartAsync(cart.Id, userId);

                TempData["Success"] = $"Ваш заказ #{order.OrderNumber} принят!";
                return RedirectToAction("OrderConfirmation", new { orderNumber = order.OrderNumber });
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Checkout") });
            }
            catch (EmptyCartException)
            {
                TempData["Error"] = "Корзина пуста";
                return RedirectToAction("Index");
            }
            catch (CartException ex)
            {
                _logger.LogWarning(ex, "Ошибка при оформлении заказа из-за корзины");
                TempData["Error"] = "Невозможно оформить заказ: " + ex.Message;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при оформлении заказа");
                TempData["Error"] = "Не удалось создать заказ. Попробуйте позже.";
                return RedirectToAction("Index");
            }
        }

        [Authorize]
        [HttpGet("order-confirmation")]
        public async Task<IActionResult> OrderConfirmation(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber)) return NotFound();
            var userId = GetUserId();
            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);
            if (order == null || order.UserId != userId) return NotFound();

            ViewBag.OrderNumber = order.OrderNumber;
            ViewBag.OrderDate = order.OrderDate.ToString("dd.MM.yyyy HH:mm");
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            try
            {
                var userId = GetUserId();
                if (quantity <= 0)
                {
                    TempData["Error"] = "Количество должно быть больше 0";
                    return RedirectToAction("Details", "Products", new { id = productId });
                }

                var product = await _productRepository.GetByIdAsync(productId);
                if (product == null)
                {
                    TempData["Error"] = "Товар не найден";
                    return RedirectToAction("Details", "Products", new { id = productId });
                }

                var cart = await _cartService.GetUserCartAsync(userId);
                await _cartService.AddItemAsync(cart.Id, productId, quantity);
                TempData["Success"] = $"«{product.Name}» добавлен в корзину";
                return RedirectToAction("Details", "Products", new { id = productId });
            }
            catch (UnauthorizedAccessException)
            {
                HttpContext.Session.SetString("PendingAddToCart", $"{productId},{quantity}");
                return RedirectToAction("Login", "Account", new
                {
                    returnUrl = Url.Action("Details", "Products", new { id = productId })
                });
            }
            catch (ProductNotAvailableException ex)
            {
                _logger.LogWarning(ex, "Товар недоступен: {ProductId}", productId);
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", "Products", new { id = productId });
            }
            catch (InsufficientStockException ex)
            {
                _logger.LogWarning(ex, "Недостаточно остатков: {ProductId}", productId);
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", "Products", new { id = productId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при добавлении в корзину");
                TempData["Error"] = "Произошла ошибка. Попробуйте позже.";
                return RedirectToAction("Details", "Products", new { id = productId });
            }
        }

        [Authorize]
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
                    TempData["Success"] = "Товар удалён из корзины";
                }
                else
                {
                    var product = await _productRepository.GetByIdAsync(productId);
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
                TempData["Error"] = "Ошибка при обновлении количества";
                return RedirectToAction("Index");
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(int productId)
        {
            try
            {
                var userId = GetUserId();
                var cart = await _cartService.GetUserCartAsync(userId);
                await _cartService.RemoveItemAsync(cart.Id, productId);
                TempData["Success"] = "Товар удалён из корзины";
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

        [Authorize]
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
        [AllowAnonymous]
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

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> MiniCart()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return PartialView("_MiniCartPartial", new MiniCartViewModel());

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
            catch
            {
                return PartialView("_MiniCartPartial", new MiniCartViewModel());
            }
        }

        private decimal CalculateShippingCost(decimal subtotal) =>
            subtotal >= AppConstants.FreeShippingThreshold ? 0m : AppConstants.DefaultShippingCostMinsk;

        private decimal CalculateTax(decimal subtotal) =>
            subtotal * AppConstants.DefaultTaxRate;
    }
}