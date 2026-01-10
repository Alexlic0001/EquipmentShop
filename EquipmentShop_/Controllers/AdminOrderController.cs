using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using EquipmentShop.Core.ViewModels.Admin;
using System.Reflection;

namespace EquipmentShop.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
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
            { OrderStatus.OnHold, new[] { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Cancelled } }
        };

        [HttpGet]
        public async Task<IActionResult> ChangeStatus(string orderNumber)
        {
            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);
            if (order == null) return NotFound();

            var current = order.Status;
            var allowed = AllowedTransitions.GetValueOrDefault(current, Array.Empty<OrderStatus>());

            ViewBag.OrderNumber = orderNumber;
            ViewBag.CurrentStatusName = GetDisplayName(current);
            ViewBag.StatusOptions = allowed.Select(s => new SelectListItem
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
                return RedirectToAction("Index", "AdminDashboard"); // или другая страница
            }

            var newStatus = (OrderStatus)model.NewStatusId;
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

            // Перенаправляем на детали заказа (если есть) или на список
            return RedirectToAction("Details", "AdminOrder", new { orderNumber = model.OrderNumber });
        }

        // Вспомогательный метод для получения [Display(Name = "...")]
        private string GetDisplayName(OrderStatus status)
        {
            var field = typeof(OrderStatus).GetField(status.ToString());
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute?.Name ?? status.ToString();
        }
    }

    public class ChangeOrderStatusViewModel
    {
        public string OrderNumber { get; set; } = string.Empty;

        [Range(1, 9, ErrorMessage = "Выберите корректный статус")]
        public int NewStatusId { get; set; }
    }
}