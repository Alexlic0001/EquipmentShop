using EquipmentShop.Core.Entities;

namespace EquipmentShop.Core.Interfaces
{
    public interface IEmailService
    {
        Task SendOrderConfirmationAsync(Order order);
        Task SendOrderShippedAsync(Order order);
        Task SendOrderDeliveredAsync(Order order);
        Task SendPasswordResetAsync(string email, string resetLink);
        Task SendWelcomeEmailAsync(string email, string userName);
        Task SendNewsletterAsync(List<string> emails, string subject, string content);
        Task SendContactFormAsync(string name, string email, string message);
    }
}
