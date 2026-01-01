using Microsoft.AspNetCore.Mvc;
using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace EquipmentShop.Controllers
{
    public class CartController : Controller
    {
        private readonly IShoppingCartService _cartService;
        private readonly IProductRepository _productRepository;

        public CartController(
            IShoppingCartService cartService,
            IProductRepository productRepository)
        {
            _cartService = cartService;
            _productRepository = productRepository;
        }

        private string GetOrCreateCartId()
        {
            var cartId = HttpContext.Session.GetString("CartId");
            if (string.IsNullOrEmpty(cartId))
            {
                cartId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("CartId", cartId);
            }
            return cartId;
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            try
            {
                var cartId = GetOrCreateCartId();
                await _cartService.AddItemAsync(cartId, productId, quantity);

                TempData["Success"] = "Товар добавлен в корзину";
                return RedirectToAction("Details", "Products", new { id = productId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", "Products", new { id = productId });
            }
        }

        public async Task<IActionResult> Index()
        {
            var cartId = GetOrCreateCartId();
            try
            {
                var cart = await _cartService.GetCartAsync(cartId);
                return View(cart);
            }
            catch
            {
                return View(new Core.Entities.ShoppingCart { Id = cartId });
            }
        }
    }
}