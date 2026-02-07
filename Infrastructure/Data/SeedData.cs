using EquipmentShop.Core.Entities;

namespace EquipmentShop.Infrastructure.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(AppDbContext context)
        {
            try
            {
                await DbInitializer.Initialize(context);
                await SeedAdditionalData(context);
            }
            catch (Exception ex)
            {
                // Логирование ошибки инициализации
                Console.WriteLine($"Ошибка при инициализации БД: {ex.Message}");
            }
        }

        private static async Task SeedAdditionalData(AppDbContext context)
        {
            // Дополнительные данные если нужно
            if (!context.ShoppingCarts.Any())
            {
                var cart = new ShoppingCart
                {
                    Id = "test-cart-1",
                    UserId = null,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                };

                await context.ShoppingCarts.AddAsync(cart);
                await context.SaveChangesAsync();
            }
        }

        public static async Task ClearTestDataAsync(AppDbContext context)
        {
            // Очистка тестовых данных (для тестов)
            context.Orders.RemoveRange(context.Orders);
            context.ShoppingCarts.RemoveRange(context.ShoppingCarts);
            context.CartItems.RemoveRange(context.CartItems);
            await context.SaveChangesAsync();
        }
    }
}
