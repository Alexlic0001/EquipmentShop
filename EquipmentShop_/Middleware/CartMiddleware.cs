using EquipmentShop.Core.Exceptions;
using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EquipmentShop.Middleware
{
    public class CartMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CartMiddleware> _logger;

        public CartMiddleware(RequestDelegate next, ILogger<CartMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IShoppingCartService cartService)
        {
            // Пропускаем статические файлы
            if (context.Request.Path.StartsWithSegments("/css") ||
                context.Request.Path.StartsWithSegments("/js") ||
                context.Request.Path.StartsWithSegments("/images") ||
                context.Request.Path.StartsWithSegments("/lib"))
            {
                await _next(context);
                return;
            }

            try
            {
                // Восстанавливаем корзину из куки если нет в сессии
                var cartIdFromCookie = context.Request.Cookies["CartId"];
                var cartIdFromSession = context.Session.GetString("CartId");

                if (!string.IsNullOrEmpty(cartIdFromCookie) &&
                    string.IsNullOrEmpty(cartIdFromSession))
                {
                    context.Session.SetString("CartId", cartIdFromCookie);

                    // Безопасное обновление срока действия — игнорируем, если корзины нет
                    try
                    {
                        await cartService.RenewCartExpirationAsync(cartIdFromCookie);
                        _logger.LogInformation("Восстановлена корзина из куки: {CartId}", cartIdFromCookie);
                    }
                    catch (CartNotFoundException)
                    {
                        // Игнорируем — старая/несуществующая корзина
                        _logger.LogWarning("Корзина из куки не найдена в БД: {CartId}", cartIdFromCookie);
                    }
                }
                // Если есть сессия но нет куки - устанавливаем куку
                else if (!string.IsNullOrEmpty(cartIdFromSession) &&
                         string.IsNullOrEmpty(cartIdFromCookie))
                {
                    context.Response.Cookies.Append("CartId", cartIdFromSession, new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(30),
                        HttpOnly = true,
                        IsEssential = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в CartMiddleware");
            }

            await _next(context);
        }
    }
}