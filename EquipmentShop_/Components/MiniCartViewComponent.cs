using Microsoft.AspNetCore.Mvc;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels;
using Microsoft.AspNetCore.Http;

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
            try
            {
                var cartId = _httpContextAccessor.HttpContext?.Session.GetString("CartId");
                if (string.IsNullOrEmpty(cartId))
                {
                    return View(new MiniCartViewModel());
                }

                var cart = await _cartService.GetOrCreateCartAsync(cartId, null);

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