using Microsoft.AspNetCore.Mvc;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels;
using EquipmentShop.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace EquipmentShop.Controllers
{
    public class CartController : Controller
    {
        private readonly IShoppingCartService _cartService;
        private readonly IProductRepository _productRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CartController> _logger;

        public CartController(
            IShoppingCartService cartService,
            IProductRepository productRepository,
            UserManager<ApplicationUser> userManager,
            ILogger<CartController> logger)
        {
            _cartService = cartService;
            _productRepository = productRepository;
            _userManager = userManager;
            _logger = logger;
        }

        private string GetOrCreateCartId()
        {
            var cartId = HttpContext.Session.GetString("CartId");
            if (string.IsNullOrEmpty(cartId))
            {
                cartId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("CartId", cartId);
                _logger.LogInformation("Создан новый ID корзины: {CartId}", cartId);
            }
            return cartId;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var cartId = GetOrCreateCartId();
                var cart = await _cartService.GetOrCreateCartAsync(cartId, GetUserId());

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке корзины");
                return View(new CartViewModel { CartId = GetOrCreateCartId() });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            try
            {
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

                var cartId = GetOrCreateCartId();

                await _cartService.AddItemAsync(cartId, productId, quantity);
                TempData["Success"] = $"«{product.Name}» добавлен в корзину";

                return RedirectToAction("Details", "Products", new { id = productId });
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
        public async Task<IActionResult> RemoveFromCart(int productId)
        {
            try
            {
                var cartId = GetOrCreateCartId();
                await _cartService.RemoveItemAsync(cartId, productId);
                TempData["Success"] = "Товар удален из корзины";

                return RedirectToAction("Index");
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
                var cartId = GetOrCreateCartId();
                await _cartService.ClearCartAsync(cartId);
                TempData["Success"] = "Корзина очищена";

                return RedirectToAction("Index");
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
                var cartId = GetOrCreateCartId();
                var cart = await _cartService.GetOrCreateCartAsync(cartId, GetUserId());

                var summary = new CartSummaryViewModel
                {
                    ItemCount = cart.TotalItems,
                    Total = cart.Subtotal
                };

                return Json(summary);
            }
            catch
            {
                return Json(new CartSummaryViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> MiniCart()
        {
            try
            {
                var cartId = GetOrCreateCartId();
                var cart = await _cartService.GetOrCreateCartAsync(cartId, GetUserId());

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

        // Вспомогательные методы
        private string? GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        private decimal CalculateShippingCost(decimal subtotal)
        {
            // Бесплатная доставка от 500 BYN
            return subtotal >= 500 ? 0 : 10m;
        }

        private decimal CalculateTax(decimal subtotal)
        {
            // НДС 20%
            return subtotal * 0.20m;
        }
    }
}