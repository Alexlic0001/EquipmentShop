using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace EquipmentShop.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(AppDbContext context)
        {
            // Гарантируем создание БД
            await context.Database.EnsureCreatedAsync();

            // Проверяем, есть ли уже данные
            if (context.Products.Any() || context.Categories.Any())
            {
                return; // База уже заполнена
            }

            await SeedCategories(context);
            await SeedProducts(context);
            await SeedUsersAndOrders(context);
        }

        private static async Task SeedCategories(AppDbContext context)
        {
            var categories = new List<Category>
        {
        };

            // Подкатегории для Компьютеры и ноутбуки
            var computersCategory = categories[0];
            computersCategory.SubCategories = new List<Category>
        {
        };

            // Подкатегории для Комплектующие
            var componentsCategory = categories[1];
            componentsCategory.SubCategories = new List<Category>
        {
        };
            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }

        private static async Task SeedProducts(AppDbContext context)
        {
            var categories = await context.Categories.ToListAsync();
            var laptopsCategory = categories.First(c => c.Slug == "laptops");
            var processorsCategory = categories.First(c => c.Slug == "processors");
            var videoCardsCategory = categories.First(c => c.Slug == "video-cards");
            var products = new List<Product>
        {

        };
            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }

        private static async Task SeedUsersAndOrders(AppDbContext context)
        {
            // Примеры заказов для тестирования
            var products = await context.Products.Take(3).ToListAsync();
            var orders = new List<Order>
        {
        };
            await context.Orders.AddRangeAsync(orders);
            await context.SaveChangesAsync();
        }
    }
}
