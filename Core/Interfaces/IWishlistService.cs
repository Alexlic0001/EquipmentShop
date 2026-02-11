using EquipmentShop.Core.Entities;
using System.Threading.Tasks;

namespace EquipmentShop.Core.Interfaces
{
    public interface IWishlistService
    {
        Task<Wishlist> GetWishlistAsync(string userId);
        Task<Wishlist> GetOrCreateWishlistAsync(string userId);
        Task AddItemAsync(string userId, int productId, string? notes = null);
        Task RemoveItemAsync(string userId, int productId);
        Task ClearWishlistAsync(string userId);
        Task<bool> ContainsItemAsync(string userId, int productId);
        Task MoveToCartAsync(string userId, int productId, int quantity = 1);
        Task<int> GetWishlistItemCountAsync(string userId);
        Task UpdateItemNotesAsync(string userId, int productId, string notes);
    }
}