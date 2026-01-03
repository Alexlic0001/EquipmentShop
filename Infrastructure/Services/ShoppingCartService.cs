using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Exceptions;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
            {
                throw new CartNotFoundException(cartId);
            }

            // Проверяем срок действия корзины
            if (cart.IsExpired)
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

                // Если пользователь авторизовался, привязываем корзину к пользователю
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
                ExpiresAt = DateTime.UtcNow.AddDays(30) // Корзина хранится 30 дней
            };

            _context.ShoppingCarts.Add(cart);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Создана новая корзина с ID: {CartId}", cartId);

            return cart;
        }

        public async Task<ShoppingCart> GetUserCartAsync(string userId)
        {
            var cart = await _context.ShoppingCarts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId && !c.IsExpired);

            if (cart == null)
            {
                // Создаем новую корзину для пользователя
                return await CreateCartWithIdAsync(Guid.NewGuid().ToString(), userId);
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
            {
                throw new ArgumentException("Количество должно быть больше 0", nameof(quantity));
            }

            var cart = await GetOrCreateCartAsync(cartId);
            var product = await _productRepository.GetByIdAsync(productId);

            if (product == null)
            {
                throw new ProductNotFoundException(productId);
            }

            if (!product.IsAvailable)
            {
                throw new InsufficientStockException(productId, product.Name, quantity, 0);
            }

            if (quantity > product.StockQuantity)
            {
                throw new InsufficientStockException(productId, product.Name, quantity, product.StockQuantity);
            }

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (existingItem != null)
            {
                var newQuantity = existingItem.Quantity + quantity;
                if (newQuantity > product.StockQuantity)
                {
                    throw new InsufficientStockException(productId, product.Name, newQuantity, product.StockQuantity);
                }

                existingItem.Quantity = newQuantity;
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var cartItem = new CartItem
                {
                    CartId = cartId,
                    ProductId = productId,
                    Product = product,
                    Price = product.Price,
                    Quantity = quantity,
                    SelectedAttributes = attributes,
                    AddedAt = DateTime.UtcNow
                };

                cart.Items.Add(cartItem);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Добавлен товар {ProductId} в корзину {CartId}", productId, cartId);
        }

        public async Task UpdateItemQuantityAsync(string cartId, int productId, int quantity)
        {
            if (quantity < 0)
            {
                throw new ArgumentException("Количество не может быть отрицательным", nameof(quantity));
            }

            var cart = await GetCartAsync(cartId);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
            {
                throw new Exception($"Товар с ID {productId} не найден в корзине");
            }

            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
            {
                throw new ProductNotFoundException(productId);
            }

            if (quantity == 0)
            {
                await RemoveItemAsync(cartId, productId);
                return;
            }

            if (quantity > product.StockQuantity)
            {
                throw new InsufficientStockException(productId, product.Name, quantity, product.StockQuantity);
            }

            item.Quantity = quantity;
            item.UpdatedAt = DateTime.UtcNow;
            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
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
                _logger.LogInformation("Удален товар {ProductId} из корзины {CartId}", productId, cartId);
            }
        }

        public async Task ClearCartAsync(string cartId)
        {
            var cart = await GetCartAsync(cartId);

            foreach (var item in cart.Items.ToList())
            {
                _context.CartItems.Remove(item);
            }

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

                if (targetItem != null)
                {
                    // Проверяем доступное количество
                    var product = await _productRepository.GetByIdAsync(sourceItem.ProductId);
                    if (product != null)
                    {
                        var newQuantity = targetItem.Quantity + sourceItem.Quantity;
                        if (newQuantity > product.StockQuantity)
                        {
                            newQuantity = product.StockQuantity;
                        }

                        targetItem.Quantity = newQuantity;
                        targetItem.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    var newItem = new CartItem
                    {
                        CartId = targetCartId,
                        ProductId = sourceItem.ProductId,
                        Product = sourceItem.Product,
                        Price = sourceItem.Price,
                        Quantity = sourceItem.Quantity,
                        SelectedAttributes = sourceItem.SelectedAttributes,
                        AddedAt = DateTime.UtcNow
                    };

                    targetCart.Items.Add(newItem);
                }
            }

            // Удаляем исходную корзину
            _context.ShoppingCarts.Remove(sourceCart);
            targetCart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task TransferCartToUserAsync(string cartId, string userId)
        {
            var cart = await GetCartAsync(cartId);

            // Проверяем, есть ли у пользователя другая корзина
            var userCart = await _context.ShoppingCarts
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Id != cartId && !c.IsExpired);

            if (userCart != null)
            {
                // Объединяем корзины
                await MergeCartsAsync(cartId, userCart.Id);
            }
            else
            {
                // Просто привязываем корзину к пользователю
                cart.UserId = userId;
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
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
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ShoppingCart> ConvertToOrderAsync(string cartId, Order order)
        {
            var cart = await GetCartAsync(cartId);

            if (cart.IsEmpty)
            {
                throw new EmptyCartException(cartId);
            }

            // Валидация корзины
            if (!await ValidateCartAsync(cartId))
            {
                throw new CartException(cartId, "Корзина содержит недоступные товары");
            }

            // Создаем элементы заказа на основе корзины
            order.OrderItems = cart.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? "Товар",
                ProductSku = item.Product?.Slug,
                UnitPrice = item.Price,
                Quantity = item.Quantity,
                ProductAttributes = item.SelectedAttributes
            }).ToList();

            order.Subtotal = cart.Items.Sum(i => i.TotalPrice);

            // Очищаем корзину
            await ClearCartAsync(cartId);

            return cart;
        }

        // Новый метод для продления срока действия корзины
        public async Task RenewCartExpirationAsync(string cartId)
        {
            var cart = await GetCartAsync(cartId);
            cart.ExpiresAt = DateTime.UtcNow.AddDays(30);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        Task IShoppingCartService.CreateCartWithIdAsync(string cartId, string? v)
        {
            return CreateCartWithIdAsync(cartId, v);
        }
    }
}