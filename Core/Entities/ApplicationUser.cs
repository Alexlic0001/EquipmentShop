// EquipmentShop.Core.Entities/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace EquipmentShop.Core.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [Display(Name = "Имя")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Фамилия")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Аватар")]
        public string? AvatarUrl { get; set; }

        [Display(Name = "Адрес")]
        public string? Address { get; set; }

        [Display(Name = "Город")]
        public string? City { get; set; }

        [Display(Name = "Область")]
        public string? Region { get; set; }

        [Display(Name = "Почтовый индекс")]
        public string? PostalCode { get; set; }

        [Display(Name = "Страна")]
        public string? Country { get; set; } = "Беларусь";

        // Дополнительные адреса для доставки
        public List<UserAddress> AdditionalAddresses { get; set; } = new();

        // Настройки уведомлений
        public bool SubscribeToNewsletter { get; set; } = true;
        public bool EmailNotifications { get; set; } = true;
        public bool SmsNotifications { get; set; } = false;

        // Статистика
        public int TotalOrders { get; set; } = 0;
        public decimal TotalSpent { get; set; } = 0m;
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Навигационные свойства
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();

        // Вычисляемые свойства
        public string FullName => $"{FirstName} {LastName}";
        public bool HasDefaultAddress => !string.IsNullOrEmpty(Address) && !string.IsNullOrEmpty(City);

        // Методы
        public void AddAddress(UserAddress address)
        {
            AdditionalAddresses.Add(address);
        }

        public void UpdateLastLogin()
        {
            LastLoginAt = DateTime.UtcNow;
        }

        public void AddOrderStats(decimal orderTotal)
        {
            TotalOrders++;
            TotalSpent += orderTotal;
        }
    }

    public class UserAddress
    {
        public string Title { get; set; } = "Домашний адрес";
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string? Region { get; set; }
        public string? PostalCode { get; set; }
        public string Country { get; set; } = "Беларусь";
        public bool IsDefault { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? FullName { get; set; }

        public string FullAddress => $"{City}, {AddressLine1}" +
            (!string.IsNullOrEmpty(AddressLine2) ? $", {AddressLine2}" : "") +
            (!string.IsNullOrEmpty(Region) ? $", {Region}" : "");
    }
}