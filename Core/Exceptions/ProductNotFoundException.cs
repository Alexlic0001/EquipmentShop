
namespace EquipmentShop.Core.Exceptions
{
    public class ProductNotFoundException : Exception
    {
        public int ProductId { get; }

        public ProductNotFoundException(int productId)
            : base($"Товар с ID {productId} не найден")
        {
            ProductId = productId;
        }
    }
}
