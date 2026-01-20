// EquipmentShop.Core/Exceptions/ProductNotAvailableException.cs
namespace EquipmentShop.Core.Exceptions
{
    public class ProductNotAvailableException : Exception
    {
        public int ProductId { get; }
        public string ProductName { get; }

        public ProductNotAvailableException(int productId, string productName)
            : base($"Товар \"{productName}\" (ID: {productId}) недоступен для заказа.")
        {
            ProductId = productId;
            ProductName = productName;
        }
    }
}