using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EquipmentShop.Infrastructure.Services
{
    public class WishlistService : IWishlistService
    {
        private readonly AppDbContext _context;
        private readonly IShoppingCartService _cartService;
        private readonly ILogger<WishlistService> _logger;

        public WishlistService(
            AppDbContext context,
            IShoppingCartService cartService,
            ILogger<WishlistService> logger)
        {
            _context = context;
            _cartService = cartService;
            _logger = logger;
        }

        public async Task<Wishlist> GetWishlistAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("UserId не может быть пустым", nameof(userId));

            var wishlist = await _context.Wishlists
                .Include(w => w.WishlistItems)
                    .ThenInclude(wi => wi.Product)
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wishlist == null)
                throw new InvalidOperationException($"Список желаний для пользователя {userId} не найден");

            return wishlist;
        }

        public async Task<Wishlist> GetOrCreateWishlistAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("UserId не может быть пустым", nameof(userId));

            var wishlist = await _context.Wishlists
                .Include(w => w.WishlistItems)
                    .ThenInclude(wi => wi.Product)
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wishlist == null)
            {
                wishlist = new Wishlist
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Wishlists.Add(wishlist);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Создан новый список желаний для пользователя {UserId}", userId);
            }

            return wishlist;
        }

        public async Task AddItemAsync(string userId, int productId, string? notes = null)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("UserId не может быть пустым", nameof(userId));

            // Проверяем, существует ли товар
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
                throw new InvalidOperationException($"Товар с ID {productId} не найден");

            // Получаем или создаём вишлист
            var wishlist = await GetOrCreateWishlistAsync(userId);

            // Проверяем, не добавлен ли товар уже
            var existingItem = wishlist.WishlistItems?.FirstOrDefault(wi => wi.ProductId == productId);
            if (existingItem != null)
            {
                _logger.LogWarning("Товар {ProductId} уже находится в избранном пользователя {UserId}", productId, userId);
                return;
            }

            // Добавляем товар
            var wishlistItem = new WishlistItem
            {
                WishlistId = wishlist.Id,
                ProductId = productId,
                Notes = notes,
                AddedAt = DateTime.UtcNow // ← Устанавливаем дату добавления
            };

            _context.WishlistItems.Add(wishlistItem);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Товар {ProductId} добавлен в избранное пользователя {UserId}", productId, userId);
        }

        public async Task RemoveItemAsync(string userId, int productId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("UserId не может быть пустым", nameof(userId));

            var wishlist = await GetWishlistAsync(userId);
            var item = wishlist.WishlistItems?.FirstOrDefault(wi => wi.ProductId == productId);

            if (item != null)
            {
                _context.WishlistItems.Remove(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Товар {ProductId} удалён из избранного пользователя {UserId}", productId, userId);
            }
        }

        public async Task ClearWishlistAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("UserId не может быть пустым", nameof(userId));

            var wishlist = await GetWishlistAsync(userId);

            if (wishlist.WishlistItems != null && wishlist.WishlistItems.Any())
            {
                _context.WishlistItems.RemoveRange(wishlist.WishlistItems);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Список желаний пользователя {UserId} очищен", userId);
            }
        }

        public async Task<bool> ContainsItemAsync(string userId, int productId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            var wishlist = await _context.Wishlists
                .Include(w => w.WishlistItems)
                .FirstOrDefaultAsync(w => w.UserId == userId);

            return wishlist?.WishlistItems?.Any(wi => wi.ProductId == productId) ?? false;
        }

        public async Task MoveToCartAsync(string userId, int productId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("UserId не может быть пустым", nameof(userId));

            // Получаем корзину пользователя
            var cart = await _cartService.GetUserCartAsync(userId);

            // Добавляем товар в корзину
            await _cartService.AddItemAsync(cart.Id, productId, quantity);

            // Удаляем из избранного
            await RemoveItemAsync(userId, productId);

            _logger.LogInformation("Товар {ProductId} перемещён из избранного в корзину пользователя {UserId}", productId, userId);
        }

        public async Task<int> GetWishlistItemCountAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return 0;

            var wishlist = await _context.Wishlists
                .Include(w => w.WishlistItems)
                .FirstOrDefaultAsync(w => w.UserId == userId);

            return wishlist?.WishlistItems?.Count ?? 0;
        }

        public async Task UpdateItemNotesAsync(string userId, int productId, string notes) // ← Реализация метода
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("UserId не может быть пустым", nameof(userId));
            if (notes == null)
                throw new ArgumentNullException(nameof(notes));

            var wishlist = await GetWishlistAsync(userId);
            var item = wishlist.WishlistItems?.FirstOrDefault(wi => wi.ProductId == productId);

            if (item != null)
            {
                item.Notes = notes;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Заметки к товару {ProductId} обновлены для пользователя {UserId}", productId, userId);
            }
            else
            {
                throw new InvalidOperationException($"Товар {productId} не найден в избранном пользователя {userId}");
            }
        }
    }
}