using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Exceptions;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;



namespace EquipmentShop.Infrastructure.Services
{
    public class ShoppingCartService : IShoppingCartService
    {
        private readonly AppDbContext _context;
        private readonly IProductRepository _productRepository;
        private readonly ILogger<ShoppingCartService> _logger;

        public ShoppingCartService(
            AppDbContext context,
            IProductRepository productRepository,
            ILogger<ShoppingCartService> logger)
        {
            _context = context;
            _productRepository = productRepository;
            _logger = logger;
        }

        public async Task<ShoppingCart> GetCartAsync(string cartId)
        {
            var cart = await _context.ShoppingCarts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.Id == cartId);

            if (cart == null)
                throw new CartNotFoundException(cartId);

            if (cart.ExpiresAt.HasValue && cart.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogInformation("Корзина {CartId} просрочена, очищаем её", cartId);
                await ClearCartAsync(cartId);
                throw new CartException(cartId, "Корзина просрочена");
            }

            return cart;
        }

        public async Task<ShoppingCart> GetOrCreateCartAsync(string cartId, string? userId = null)
        {
            try
            {
                var cart = await GetCartAsync(cartId);
                if (!string.IsNullOrEmpty(userId) && cart.UserId != userId)
                {
                    await TransferCartToUserAsync(cartId, userId);
                    cart = await GetCartAsync(cartId);
                }
                return cart;
            }
            catch (CartNotFoundException)
            {
                return await CreateCartWithIdAsync(cartId, userId);
            }
        }

        private async Task<ShoppingCart> CreateCartWithIdAsync(string cartId, string? userId = null)
        {
            var cart = new ShoppingCart
            {
                Id = cartId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            _context.ShoppingCarts.Add(cart);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Создана новая корзина с ID: {CartId}", cartId);
            return cart;
        }

        public async Task<ShoppingCart> GetUserCartAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("UserId не может быть пустым", nameof(userId));

            // Пробуем найти существующую корзину
            var cart = await _context.ShoppingCarts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c =>
                    c.UserId == userId &&
                    (!c.ExpiresAt.HasValue || c.ExpiresAt.Value >= DateTime.UtcNow));

            if (cart == null)
            {
                // Используем Guid для уникальности + пытаемся вставить с обработкой конфликта
                var maxRetries = 3;
                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        cart = new ShoppingCart
                        {
                            Id = $"cart_{userId}_{Guid.NewGuid():N}", // Уникальный Id
                            UserId = userId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.AddDays(30)
                        };
                        _context.ShoppingCarts.Add(cart);
                        await _context.SaveChangesAsync();
                        break;
                    }
                    catch (DbUpdateException ex) when
                        (ex.InnerException?.Message?.Contains("UNIQUE constraint") == true && i < maxRetries - 1)
                    {
                        // Повторная попытка при конфликте
                        await Task.Delay(100);
                    }
                }

                if (cart == null)
                    throw new InvalidOperationException("Не удалось создать корзину после нескольких попыток");
            }

            return cart;
        }

        public async Task<ShoppingCart> CreateCartAsync(string? userId = null)
        {
            var cartId = Guid.NewGuid().ToString();
            return await CreateCartWithIdAsync(cartId, userId);
        }

        public async Task AddItemAsync(string cartId, int productId, int quantity = 1, string? attributes = null)
        {
            if (quantity <= 0)
                throw new ArgumentException("Количество должно быть больше 0", nameof(quantity));

            var cart = await GetOrCreateCartAsync(cartId);
            var product = await _productRepository.GetByIdAsync(productId);

            if (product == null)
                throw new ProductNotFoundException(productId);

            if (!product.IsAvailable)
                throw new ProductNotAvailableException(productId, product.Name);

            if (quantity > product.StockQuantity)
                throw new InsufficientStockException(productId, product.Name, quantity, product.StockQuantity);

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (existingItem != null)
            {
                var newTotal = existingItem.Quantity + quantity;
                if (newTotal > product.StockQuantity)
                    throw new InsufficientStockException(productId, product.Name, newTotal, product.StockQuantity);

                existingItem.Quantity = newTotal;
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var cartItem = new CartItem
                {
                    CartId = cartId,
                    ProductId = productId,
                    Price = product.Price,
                    Quantity = quantity,
                    SelectedAttributes = attributes,
                    AddedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                cart.Items.Add(cartItem);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Товар {ProductId} добавлен в корзину {CartId} (количество: {Quantity})", productId, cartId, quantity);
        }

        public async Task UpdateItemQuantityAsync(string cartId, int productId, int newQuantity)
        {
            if (newQuantity < 0)
                throw new ArgumentException("Количество не может быть отрицательным", nameof(newQuantity));

            var cart = await GetCartAsync(cartId);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
                throw new Exception($"Товар с ID {productId} не найден в корзине");

            if (newQuantity == 0)
            {
                await RemoveItemAsync(cartId, productId);
                return;
            }

            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null || !product.IsAvailable)
                throw new ProductNotAvailableException(productId, product?.Name ?? "Unknown");

            if (newQuantity > product.StockQuantity)
                throw new InsufficientStockException(productId, product.Name, newQuantity, product.StockQuantity);

            item.Quantity = newQuantity;
            item.UpdatedAt = DateTime.UtcNow;
            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Количество товара {ProductId} в корзине {CartId} обновлено до {Quantity}", productId, cartId, newQuantity);
        }

        public async Task RemoveItemAsync(string cartId, int productId)
        {
            var cart = await GetCartAsync(cartId);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item != null)
            {
                cart.Items.Remove(item);
                _context.CartItems.Remove(item);
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Товар {ProductId} удалён из корзины {CartId}", productId, cartId);
            }
        }

        public async Task ClearCartAsync(string cartId)
        {
            var cart = await GetCartAsync(cartId);
            _context.CartItems.RemoveRange(cart.Items);
            cart.Items.Clear();
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Очищена корзина {CartId}", cartId);
        }

        public async Task MergeCartsAsync(string sourceCartId, string targetCartId)
        {
            var sourceCart = await GetCartAsync(sourceCartId);
            var targetCart = await GetOrCreateCartAsync(targetCartId);

            foreach (var sourceItem in sourceCart.Items.ToList())
            {
                var targetItem = targetCart.Items.FirstOrDefault(i => i.ProductId == sourceItem.ProductId);
                var product = await _productRepository.GetByIdAsync(sourceItem.ProductId);

                if (product == null || !product.IsAvailable)
                    continue;

                if (targetItem != null)
                {
                    var newQty = targetItem.Quantity + sourceItem.Quantity;
                    if (newQty <= product.StockQuantity)
                    {
                        targetItem.Quantity = newQty;
                        targetItem.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _logger.LogWarning("При слиянии превышен остаток товара {ProductId}. Установлено: {Available}", product.Id, product.StockQuantity);
                        targetItem.Quantity = product.StockQuantity;
                        targetItem.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    var newItem = new CartItem
                    {
                        CartId = targetCartId,
                        ProductId = sourceItem.ProductId,
                        Price = sourceItem.Price,
                        Quantity = Math.Min(sourceItem.Quantity, product.StockQuantity),
                        SelectedAttributes = sourceItem.SelectedAttributes,
                        AddedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    targetCart.Items.Add(newItem);
                }
            }

            _context.ShoppingCarts.Remove(sourceCart);
            targetCart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task TransferCartToUserAsync(string cartId, string userId)
        {
            var cart = await GetCartAsync(cartId);

            var existingUserCart = await _context.ShoppingCarts
                .FirstOrDefaultAsync(c =>
                    c.UserId == userId &&
                    c.Id != cartId &&
                    (!c.ExpiresAt.HasValue || c.ExpiresAt.Value >= DateTime.UtcNow));

            if (existingUserCart != null)
            {
                await MergeCartsAsync(cartId, existingUserCart.Id);
            }
            else
            {
                cart.UserId = userId;
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Корзина {CartId} привязана к пользователю {UserId}", cartId, userId);
            }
        }

        public async Task<int> GetCartItemCountAsync(string cartId)
        {
            try
            {
                var cart = await GetCartAsync(cartId);
                return cart.TotalItems;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<decimal> GetCartTotalAsync(string cartId)
        {
            try
            {
                var cart = await GetCartAsync(cartId);
                return cart.Subtotal;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> ValidateCartAsync(string cartId)
        {
            try
            {
                var cart = await GetCartAsync(cartId);
                foreach (var item in cart.Items)
                {
                    var product = await _productRepository.GetByIdAsync(item.ProductId);
                    if (product == null || !product.IsAvailable || item.Quantity > product.StockQuantity)
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task RenewCartExpirationAsync(string cartId)
        {
            var cart = await GetCartAsync(cartId);
            cart.ExpiresAt = DateTime.UtcNow.AddDays(30);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        // Явная реализация интерфейса (если требуется)
        Task IShoppingCartService.CreateCartWithIdAsync(string cartId, string? userId)
        {
            return CreateCartWithIdAsync(cartId, userId);
        }
    }
}