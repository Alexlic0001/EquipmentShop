using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace EquipmentShop_.Controllers
{
    [Authorize(Roles = $"{AppConstants.AdminRole},{AppConstants.ManagerRole}")]
    public class AdminOrderController(IOrderRepository orderRepository) : Controller
    {
        private readonly IOrderRepository _orderRepository = orderRepository;

        // Допустимые переходы между статусами
        private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
        {
            [OrderStatus.Pending] = [OrderStatus.Processing, OrderStatus.Cancelled],
            [OrderStatus.Processing] = [OrderStatus.AwaitingPayment, OrderStatus.Paid, OrderStatus.Cancelled],
            [OrderStatus.AwaitingPayment] = [OrderStatus.Paid, OrderStatus.Cancelled],
            [OrderStatus.Paid] = [OrderStatus.Shipped, OrderStatus.Cancelled],
            [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Refunded, OrderStatus.Cancelled],
            [OrderStatus.Delivered] = [OrderStatus.Refunded],
            [OrderStatus.Cancelled] = [],
            [OrderStatus.Refunded] = [],
        };

        [HttpGet]
        public async Task<IActionResult> ChangeStatus(string orderNumber)
        {
            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);
            if (order == null) return NotFound();

            ViewBag.OrderNumber = orderNumber;
            ViewBag.CurrentStatusName = GetDisplayName(order.Status);
            ViewBag.StatusOptions = GetStatusSelectList();

            return View(new ChangeOrderStatusViewModel { OrderNumber = orderNumber });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(ChangeOrderStatusViewModel model)
        {
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(ChangeStatus), new { orderNumber = model.OrderNumber });

            var order = await _orderRepository.GetByOrderNumberAsync(model.OrderNumber);
            if (order == null)
            {
                TempData["Error"] = "Заказ не найден.";
                return RedirectToAction("Orders", "Admin");
            }

            var newStatus = (OrderStatus)model.NewStatusId;
            if (!IsStatusTransitionAllowed(order.Status, newStatus))
            {
                TempData["Error"] = "Недопустимый переход статуса.";
                return RedirectToAction(nameof(ChangeStatus), new { orderNumber = model.OrderNumber });
            }

            var success = await _orderRepository.UpdateOrderStatusAsync(model.OrderNumber, newStatus);
            TempData[success ? "Success" : "Error"] = success
                ? $"Статус изменён на «{GetDisplayName(newStatus)}»"
                : "Не удалось обновить статус.";

            return RedirectToAction("OrderDetails", "Admin", new { id = order.Id });
        }

        private static bool IsStatusTransitionAllowed(OrderStatus current, OrderStatus next) =>
            AllowedTransitions.GetValueOrDefault(current, []).Contains(next);

        private static List<SelectListItem> GetStatusSelectList() =>
            [.. Enum.GetValues<OrderStatus>().Select(s => new SelectListItem
            {
                Value = ((int)s).ToString(),
                Text = GetDisplayName(s)
            })];

        private static string GetDisplayName(OrderStatus status)
        {
            var field = typeof(OrderStatus).GetField(status.ToString());
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute?.Name ?? status.ToString();
        }
    }
}