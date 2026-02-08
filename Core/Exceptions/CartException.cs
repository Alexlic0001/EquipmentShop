
namespace EquipmentShop.Core.Exceptions
{
    public class CartException : Exception
    {
        public string CartId { get; }

        public CartException(string cartId, string message)
            : base($"Ошибка корзины {cartId}: {message}")
        {
            CartId = cartId;
        }
    }

    public class EmptyCartException : CartException
    {
        public EmptyCartException(string cartId)
            : base(cartId, "Корзина пуста")
        {
        }
    }

    public class CartNotFoundException : CartException
    {
        public CartNotFoundException(string cartId)
            : base(cartId, "Корзина не найдена")
        {
        }
    }
}
