using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace EquipmentShop.Controllers.Admin
{
    [Authorize(Roles = $"{AppConstants.AdminRole},{AppConstants.ManagerRole}")]
    public class AdminOrderController : Controller
    {
        private readonly IOrderRepository _orderRepository;

        public AdminOrderController(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        // Логика допустимых переходов между статусами
        private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
        {
            { OrderStatus.Pending, new[] { OrderStatus.Processing, OrderStatus.OnHold, OrderStatus.Cancelled } },
            { OrderStatus.Processing, new[] { OrderStatus.AwaitingPayment, OrderStatus.Paid, OrderStatus.OnHold, OrderStatus.Cancelled } },
            { OrderStatus.AwaitingPayment, new[] { OrderStatus.Paid, OrderStatus.Cancelled, OrderStatus.OnHold } },
            { OrderStatus.Paid, new[] { OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.OnHold } },
            { OrderStatus.Shipped, new[] { OrderStatus.Delivered, OrderStatus.Refunded, OrderStatus.Cancelled } },
            { OrderStatus.Delivered, new[] { OrderStatus.Refunded } },
            { OrderStatus.Cancelled, Array.Empty<OrderStatus>() },
            { OrderStatus.Refunded, Array.Empty<OrderStatus>() },
            { OrderStatus.OnHold, new[] { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.AwaitingPayment, OrderStatus.Paid, OrderStatus.Cancelled } }
        };

        [HttpGet]
        public async Task<IActionResult> ChangeStatus(string orderNumber)
        {
            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);
            if (order == null) return NotFound();

            ViewBag.OrderNumber = orderNumber;
            ViewBag.CurrentStatusName = GetDisplayName(order.Status);

            // Показываем ВСЕ статусы для удобства менеджера
            var allStatuses = Enum.GetValues<OrderStatus>();
            ViewBag.StatusOptions = allStatuses.Select(s => new SelectListItem
            {
                Value = ((int)s).ToString(),
                Text = GetDisplayName(s)
            }).ToList();

            return View(new ChangeOrderStatusViewModel { OrderNumber = orderNumber });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(ChangeOrderStatusViewModel model)
        {
            if (!ModelState.IsValid)
                return RedirectToAction("ChangeStatus", new { orderNumber = model.OrderNumber });

            var order = await _orderRepository.GetByOrderNumberAsync(model.OrderNumber);
            if (order == null)
            {
                TempData["Error"] = "Заказ не найден.";
                return RedirectToAction("Orders", "Admin");
            }

            var newStatus = (OrderStatus)model.NewStatusId;

            // Проверяем допустимость перехода
            var allowed = AllowedTransitions.GetValueOrDefault(order.Status, Array.Empty<OrderStatus>());
            if (!allowed.Contains(newStatus))
            {
                TempData["Error"] = "Недопустимый переход статуса.";
                return RedirectToAction("ChangeStatus", new { orderNumber = model.OrderNumber });
            }

            var success = await _orderRepository.UpdateOrderStatusAsync(model.OrderNumber, newStatus);
            if (success)
            {
                TempData["Success"] = $"Статус изменён на «{GetDisplayName(newStatus)}»";
            }
            else
            {
                TempData["Error"] = "Не удалось обновить статус.";
            }

            // Перенаправляем на детали заказа в AdminController
            return RedirectToAction("OrderDetails", "Admin", new { id = order.Id });
        }

        private string GetDisplayName(OrderStatus status)
        {
            var field = typeof(OrderStatus).GetField(status.ToString());
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute?.Name ?? status.ToString();
        }
    }
}