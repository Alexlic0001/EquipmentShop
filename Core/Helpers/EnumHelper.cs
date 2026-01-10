using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace EquipmentShop.Core.Helpers
{
    public static class EnumHelper<T>
    {
        public static string GetDisplayName(T value)
        {
            var field = typeof(T).GetField(value.ToString());
            if (field == null) return value.ToString();

            var attribute = field.GetCustomAttribute<DisplayAttribute>();
            return attribute?.Name ?? value.ToString();
        }
    }
}