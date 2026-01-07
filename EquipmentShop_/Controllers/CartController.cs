// EquipmentShop/Controllers/CartController.cs
using Microsoft.AspNetCore.Mvc;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels;
using EquipmentShop.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using EquipmentShop.Core.Enums;

namespace EquipmentShop.Controllers
{
    public class CartController : Controller
    {
        private readonly IShoppingCartService _cartService;
        private readonly IProductRepository _productRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CartController> _logger;
        private readonly IOrderRepository _orderRepository;

        public CartController(
        IShoppingCartService cartService,
        IProductRepository productRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<CartController> logger,
        IOrderRepository orderRepository)
        {
            _cartService = cartService;
            _productRepository = productRepository;
            _userManager = userManager;
            _logger = logger;
            _orderRepository = orderRepository;
        }

        private string GetUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Пользователь не авторизован");
            }
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
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке корзины");
                TempData["Error"] = "Ошибка при загрузке корзины";
                return RedirectToAction("Error", "Home");
            }
        }

        [Authorize]
        [HttpPost("checkout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            try
            {
                var userId = GetUserId();
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return RedirectToAction("Login", "Account");

                var cart = await _cartService.GetUserCartAsync(userId);
                _logger.LogInformation("Оформление заказа. UserId: {UserId}, Items.Count: {ItemCount}", userId, cart?.Items?.Count ?? 0);

                if (cart?.Items == null || !cart.Items.Any())
                {
                    TempData["Error"] = "Ваша корзина пуста";
                    return RedirectToAction("Index");
                }

                // Создаём заказ
                var order = new Order
                {
                    OrderNumber = Order.GenerateOrderNumber(),
                    UserId = userId,
                    CustomerName = user.FullName,
                    CustomerEmail = user.Email,
                    CustomerPhone = user.PhoneNumber ?? string.Empty,
                    Status = OrderStatus.Pending,
                    OrderDate = DateTime.UtcNow,
                    Subtotal = cart.Subtotal,
                    ShippingCost = 0m,
                    TaxAmount = 0m,
                    DiscountAmount = 0m
                };

                // Адрес доставки
                if (user.HasDefaultAddress)
                {
                    order.ShippingAddress = user.Address!;
                    order.ShippingCity = user.City!;
                    order.ShippingRegion = user.Region;
                    order.ShippingCountry = user.Country;
                    order.ShippingPostalCode = user.PostalCode;
                }
                else
                {
                    order.ShippingAddress = "Адрес не указан";
                    order.ShippingCity = "—";
                }

                // Добавляем позиции заказа
                foreach (var item in cart.Items)
                {
                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Product?.Name ?? "Товар",
                        UnitPrice = item.Price,
                        OriginalPrice = item.Product?.OldPrice,
                        Quantity = item.Quantity
                        // Id НЕ УКАЗЫВАЕМ!
                    });
                }

                // Сохраняем заказ И позиции за один вызов
                await _orderRepository.AddAsync(order);

                // Очищаем корзину и обновляем статистику
                await _cartService.ClearCartAsync(cart.Id);
                user.AddOrderStats(order.Total);
                await _userManager.UpdateAsync(user);

                TempData["Success"] = $"Ваш заказ #{order.OrderNumber} принят в обработку!";
                return RedirectToAction("OrderConfirmation", new { orderNumber = order.OrderNumber });
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Критическая ошибка при оформлении заказа");
                TempData["Error"] = "Не удалось оформить заказ. Попробуйте позже.";
                return RedirectToAction("Index");
            }
        }

        [Authorize]
        [HttpGet("order-confirmation")]
        public async Task<IActionResult> OrderConfirmation(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber))
                return NotFound();

            var userId = GetUserId();
            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);

            if (order == null || order.UserId != userId)
                return NotFound();

            ViewBag.OrderNumber = order.OrderNumber;
            ViewBag.OrderDate = order.OrderDate.ToString("dd.MM.yyyy HH:mm");
            return View(); // ← Ищет Views/Cart/OrderConfirmation.cshtml
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
                    return RedirectToAction("Index", "Products");
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении товара в корзину");
                TempData["Error"] = ex.Message.Contains("недоступно") || ex.Message.Contains("недостаточно")
                    ? ex.Message
                    : "Ошибка при добавлении в корзину";
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
                    TempData["Success"] = "Товар удален из корзины";
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
                TempData["Error"] = $"Ошибка: {ex.Message}";
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

        private decimal CalculateShippingCost(decimal subtotal) => subtotal >= 500 ? 0 : 10m;
        private decimal CalculateTax(decimal subtotal) => subtotal * 0.20m;
    }
}