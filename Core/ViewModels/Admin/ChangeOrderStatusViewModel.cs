using System.ComponentModel.DataAnnotations;

namespace EquipmentShop.Core.ViewModels.Admin
{
    public class ChangeOrderStatusViewModel
    {
        public string OrderNumber { get; set; } = string.Empty;

        [Range(1, 9, ErrorMessage = "Выберите корректный статус")]
        public int NewStatusId { get; set; }
    }
}