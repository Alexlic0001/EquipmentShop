using Microsoft.AspNetCore.Mvc;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EquipmentShop.Components
{
    public class MiniCartViewComponent : ViewComponent
    {
        private readonly IShoppingCartService _cartService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MiniCartViewComponent(
            IShoppingCartService cartService,
            IHttpContextAccessor httpContextAccessor)
        {
            _cartService = cartService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Если пользователь не авторизован - показываем пустую корзину
            if (string.IsNullOrEmpty(userId))
            {
                return View(new MiniCartViewModel());
            }

            try
            {
                var cart = await _cartService.GetUserCartAsync(userId);

                var viewModel = new MiniCartViewModel
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

                return View(viewModel);
            }
            catch
            {
                return View(new MiniCartViewModel());
            }
        }
    }
}