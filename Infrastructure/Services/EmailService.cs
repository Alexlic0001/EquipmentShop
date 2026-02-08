using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EquipmentShop.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        public async Task SendOrderConfirmationAsync(Order order)
        {
            _logger.LogInformation("Отправка подтверждения заказа {OrderNumber} на email {Email}",
                order.OrderNumber, order.CustomerEmail);

            await Task.CompletedTask;
        }

        public async Task SendOrderShippedAsync(Order order)
        {
            _logger.LogInformation("Отправка уведомления об отгрузке заказа {OrderNumber} на email {Email}",
                order.OrderNumber, order.CustomerEmail);

            await Task.CompletedTask;
        }

        public async Task SendOrderDeliveredAsync(Order order)
        {
            _logger.LogInformation("Отправка уведомления о доставке заказа {OrderNumber} на email {Email}",
                order.OrderNumber, order.CustomerEmail);

            await Task.CompletedTask;
        }

        public async Task SendPasswordResetAsync(string email, string resetLink)
        {
            _logger.LogInformation("Отправка ссылки для сброса пароля на email {Email}", email);

            await Task.CompletedTask;
        }

        public async Task SendWelcomeEmailAsync(string email, string userName)
        {
            _logger.LogInformation("Отправка приветственного письма на email {Email}", email);

            await Task.CompletedTask;
        }

        public async Task SendNewsletterAsync(List<string> emails, string subject, string content)
        {
            _logger.LogInformation("Отправка рассылки на {Count} email адресов", emails.Count);

            await Task.CompletedTask;
        }

        public async Task SendContactFormAsync(string name, string email, string message)
        {
            _logger.LogInformation("Отправка сообщения из контактной формы от {Name} ({Email})", name, email);

            await Task.CompletedTask;
        }
    }
}
