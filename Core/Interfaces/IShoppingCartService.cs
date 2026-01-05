using EquipmentShop.Core.Entities;

namespace EquipmentShop.Core.Interfaces
{
    public interface IShoppingCartService
    {
        Task<ShoppingCart> GetCartAsync(string cartId);
        Task<ShoppingCart> GetOrCreateCartAsync(string cartId, string? userId = null);
        Task<ShoppingCart> GetUserCartAsync(string userId);
        Task<ShoppingCart> CreateCartAsync(string? userId = null);
        Task AddItemAsync(string cartId, int productId, int quantity = 1, string? attributes = null);
        Task UpdateItemQuantityAsync(string cartId, int productId, int quantity);
        Task RemoveItemAsync(string cartId, int productId);
        Task ClearCartAsync(string cartId);
        Task MergeCartsAsync(string sourceCartId, string targetCartId);
        Task TransferCartToUserAsync(string cartId, string userId);
        Task<int> GetCartItemCountAsync(string cartId);
        Task<decimal> GetCartTotalAsync(string cartId);
        Task<bool> ValidateCartAsync(string cartId);
        Task<ShoppingCart> ConvertToOrderAsync(string cartId, Order order);
        Task CreateCartWithIdAsync(string cartId, string? v);
        Task RenewCartExpirationAsync(string cartId);
    }
}
