using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace EquipmentShop.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(AppDbContext context)
        {
            // Гарантируем создание БД
            await context.Database.EnsureCreatedAsync();

            // Проверяем, есть ли уже данные
            if (context.Products.Any() || context.Categories.Any())
            {
                return; // База уже заполнена
            }

            await SeedCategories(context);
            await SeedProducts(context);
            await SeedUsersAndOrders(context);
        }

        private static async Task SeedCategories(AppDbContext context)
        {
            var categories = new List<Category>
        {
            //new Category
            //{
            //    Name = "Компьютеры и ноутбуки",
            //    Slug = "computers-laptops",
            //    Description = "Стационарные компьютеры, ноутбуки, моноблоки",
            //    IconClass = "bi bi-laptop",
            //    DisplayOrder = 1,
            //    ShowInMenu = true,
            //    ShowOnHomepage = true
            //},
            //new Category
            //{
            //    Name = "Комплектующие",
            //    Slug = "components",
            //    Description = "Процессоры, видеокарты, материнские платы",
            //    IconClass = "bi bi-cpu",
            //    DisplayOrder = 2,
            //    ShowInMenu = true
            //},
            //new Category
            //{
            //    Name = "Периферия",
            //    Slug = "peripherals",
            //    Description = "Клавиатуры, мыши, мониторы, принтеры",
            //    IconClass = "bi bi-keyboard",
            //    DisplayOrder = 3,
            //    ShowInMenu = true
            //},
            //new Category
            //{
            //    Name = "Сети и оборудование",
            //    Slug = "networking",
            //    Description = "Маршрутизаторы, коммутаторы, Wi-Fi оборудование",
            //    IconClass = "bi bi-router",
            //    DisplayOrder = 4,
            //    ShowInMenu = true
            //},
            //new Category
            //{
            //    Name = "Хранение данных",
            //    Slug = "storage",
            //    Description = "Жесткие диски, SSD, USB-накопители",
            //    IconClass = "bi bi-hdd",
            //    DisplayOrder = 5,
            //    ShowInMenu = true
            //},
            //new Category
            //{
            //    Name = "Игры и софт",
            //    Slug = "games-software",
            //    Description = "Игры, операционные системы, офисные пакеты",
            //    IconClass = "bi bi-controller",
            //    DisplayOrder = 6,
            //    ShowInMenu = true
            //}
        };

            // Подкатегории для Компьютеры и ноутбуки
            var computersCategory = categories[0];
            computersCategory.SubCategories = new List<Category>
        {
            //new Category
            //{
            //    Name = "Ноутбуки",
            //    Slug = "laptops",
            //    Description = "Игровые, рабочие, ультрабуки",
            //    DisplayOrder = 1,
            //    ShowInMenu = true
            //},
            //new Category
            //{
            //    Name = "Стационарные компьютеры",
            //    Slug = "desktop-pcs",
            //    Description = "Готовые сборки ПК",
            //    DisplayOrder = 2,
            //    ShowInMenu = true
            //},
            //new Category
            //{
            //    Name = "Моноблоки",
            //    Slug = "all-in-ones",
            //    Description = "Компьютеры-моноблоки",
            //    DisplayOrder = 3,
            //    ShowInMenu = true
            //}
        };

            // Подкатегории для Комплектующие
            var componentsCategory = categories[1];
            componentsCategory.SubCategories = new List<Category>
        {
            //new Category
            //{
            //    Name = "Процессоры",
            //    Slug = "processors",
            //    Description = "CPU Intel и AMD",
            //    DisplayOrder = 1,
            //    ShowInMenu = true
            //},
            //new Category
            //{
            //    Name = "Видеокарты",
            //    Slug = "video-cards",
            //    Description = "Графические ускорители",
            //    DisplayOrder = 2,
            //    ShowInMenu = true
            //},
            //new Category
            //{
            //    Name = "Материнские платы",
            //    Slug = "motherboards",
            //    Description = "Системные платы",
            //    DisplayOrder = 3,
            //    ShowInMenu = true
            //},
            //new Category
            //{
            //    Name = "Оперативная память",
            //    Slug = "ram",
            //    Description = "Модули памяти DDR4/DDR5",
            //    DisplayOrder = 4,
            //    ShowInMenu = true
            //}
        };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }

        private static async Task SeedProducts(AppDbContext context)
        {
            var categories = await context.Categories.ToListAsync();
            var laptopsCategory = categories.First(c => c.Slug == "laptops");
            var processorsCategory = categories.First(c => c.Slug == "processors");
            var videoCardsCategory = categories.First(c => c.Slug == "video-cards");

            var products = new List<Product>
        {
            // Ноутбуки
            //new Product
            //{
            //    Name = "Ноутбук ASUS ROG Strix G15",
            //    Slug = "asus-rog-strix-g15",
            //    Description = "Игровой ноутбук с процессором Intel Core i7-12700H и видеокартой NVIDIA GeForce RTX 3060",
            //    ShortDescription = "Мощный игровой ноутбук для современных игр",
            //    Price = 2899.99m,
            //    OldPrice = 3299.99m,
            //    ImageUrl = "/images/products/laptop1.jpg",
            //    GalleryImages = new List<string> { "/images/products/laptop1-1.jpg", "/images/products/laptop1-2.jpg" },
            //    CategoryId = laptopsCategory.Id,
            //    Brand = "ASUS",
            //    StockQuantity = 15,
            //    IsFeatured = true,
            //    IsNew = true,
            //    Rating = 4.7,
            //    ReviewsCount = 42,
            //    SoldCount = 127,
            //    Specifications = new Dictionary<string, string>
            //    {
            //        { "Процессор", "Intel Core i7-12700H" },
            //        { "Видеокарта", "NVIDIA GeForce RTX 3060 6GB" },
            //        { "Оперативная память", "16 GB DDR5" },
            //        { "Накопитель", "1 TB NVMe SSD" },
            //        { "Дисплей", "15.6\" FHD 144Hz" },
            //        { "Операционная система", "Windows 11 Pro" }
            //    },
            //    Tags = new List<string> { "игровой", "ноутбук", "asus", "rtx3060", "16gb" }
            //},
            //new Product
            //{
            //    Name = "Ноутбук Apple MacBook Pro 14\"",
            //    Slug = "macbook-pro-14",
            //    Description = "Профессиональный ноутбук с чипом Apple M2 Pro, дисплеем Liquid Retina XDR",
            //    ShortDescription = "Профессиональный MacBook для дизайнеров и разработчиков",
            //    Price = 3499.99m,
            //    ImageUrl = "/images/products/macbook.jpg",
            //    GalleryImages = new List<string> { "/images/products/macbook-1.jpg" },
            //    CategoryId = laptopsCategory.Id,
            //    Brand = "Apple",
            //    StockQuantity = 8,
            //    IsFeatured = true,
            //    Rating = 4.9,
            //    ReviewsCount = 89,
            //    SoldCount = 231,
            //    Specifications = new Dictionary<string, string>
            //    {
            //        { "Процессор", "Apple M2 Pro (12 ядер)" },
            //        { "Графика", "19-ядерный GPU" },
            //        { "Оперативная память", "16 GB Unified Memory" },
            //        { "Накопитель", "512 GB SSD" },
            //        { "Дисплей", "14.2\" Liquid Retina XDR" },
            //        { "Аккумулятор", "До 18 часов работы" }
            //    },
            //    Tags = new List<string> { "apple", "macbook", "профессиональный", "m2", "дизайн" }
            //},
            //new Product
            //{
            //    Name = "Ноутбук Lenovo IdeaPad Gaming 3",
            //    Slug = "lenovo-ideapad-gaming-3",
            //    Description = "Бюджетный игровой ноутбук с AMD Ryzen 5 и NVIDIA GeForce GTX 1650",
            //    ShortDescription = "Доступный игровой ноутбук для начинающих",
            //    Price = 1299.99m,
            //    OldPrice = 1499.99m,
            //    ImageUrl = "/images/products/laptop2.jpg",
            //    CategoryId = laptopsCategory.Id,
            //    Brand = "Lenovo",
            //    StockQuantity = 25,
            //    Rating = 4.3,
            //    ReviewsCount = 67,
            //    SoldCount = 189,
            //    Specifications = new Dictionary<string, string>
            //    {
            //        { "Процессор", "AMD Ryzen 5 5600H" },
            //        { "Видеокарта", "NVIDIA GeForce GTX 1650 4GB" },
            //        { "Оперативная память", "8 GB DDR4" },
            //        { "Накопитель", "512 GB NVMe SSD" },
            //        { "Дисплей", "15.6\" FHD 120Hz" },
            //        { "Операционная система", "Windows 11 Home" }
            //    },
            //    Tags = new List<string> { "игровой", "бюджетный", "lenovo", "amd", "gtx1650" }
            //},
            
            // Процессоры
            //new Product
            //{
            //    Name = "Процессор Intel Core i9-13900K",
            //    Slug = "intel-core-i9-13900k",
            //    Description = "Флагманский процессор Intel 13-го поколения с 24 ядрами",
            //    ShortDescription = "Мощнейший процессор для игр и профессиональных задач",
            //    Price = 699.99m,
            //    ImageUrl = "/images/products/cpu1.jpg",
            //    CategoryId = processorsCategory.Id,
            //    Brand = "Intel",
            //    StockQuantity = 12,
            //    IsFeatured = true,
            //    IsNew = true,
            //    Rating = 4.8,
            //    ReviewsCount = 56,
            //    SoldCount = 78,
            //    Specifications = new Dictionary<string, string>
            //    {
            //        { "Сокет", "LGA 1700" },
            //        { "Количество ядер", "24 (8P+16E)" },
            //        { "Количество потоков", "32" },
            //        { "Базовая частота", "3.0 GHz" },
            //        { "Максимальная частота", "5.8 GHz" },
            //        { "Кэш L3", "36 MB" },
            //        { "Тепловыделение", "125W" }
            //    },
            //    Tags = new List<string> { "процессор", "intel", "i9", "13gen", "флагман" }
            //},
            //new Product
            //{
            //    Name = "Процессор AMD Ryzen 7 7800X3D",
            //    Slug = "amd-ryzen-7-7800x3d",
            //    Description = "Игровой процессор с технологией 3D V-Cache для максимальной производительности",
            //    ShortDescription = "Лучший игровой процессор от AMD",
            //    Price = 499.99m,
            //    OldPrice = 549.99m,
            //    ImageUrl = "/images/products/cpu2.jpg",
            //    CategoryId = processorsCategory.Id,
            //    Brand = "AMD",
            //    StockQuantity = 18,
            //    IsFeatured = true,
            //    Rating = 4.9,
            //    ReviewsCount = 92,
            //    SoldCount = 145,
            //    Specifications = new Dictionary<string, string>
            //    {
            //        { "Сокет", "AM5" },
            //        { "Количество ядер", "8" },
            //        { "Количество потоков", "16" },
            //        { "Базовая частота", "4.2 GHz" },
            //        { "Максимальная частота", "5.0 GHz" },
            //        { "Кэш L3", "96 MB (с 3D V-Cache)" },
            //        { "Тепловыделение", "120W" }
            //    },
            //    Tags = new List<string> { "процессор", "amd", "ryzen", "7800x3d", "игровой" }
            //},
            
            //// Видеокарты
            //new Product
            //{
            //    Name = "Видеокарта NVIDIA GeForce RTX 4090",
            //    Slug = "nvidia-rtx-4090",
            //    Description = "Флагманская видеокарта NVIDIA с архитектурой Ada Lovelace",
            //    ShortDescription = "Самая мощная игровая видеокарта на рынке",
            //    Price = 1899.99m,
            //    ImageUrl = "/images/products/gpu1.jpg",
            //    CategoryId = videoCardsCategory.Id,
            //    Brand = "NVIDIA",
            //    StockQuantity = 5,
            //    IsFeatured = true,
            //    IsNew = true,
            //    Rating = 4.7,
            //    ReviewsCount = 34,
            //    SoldCount = 42,
            //    Specifications = new Dictionary<string, string>
            //    {
            //        { "Графический процессор", "GeForce RTX 4090" },
            //        { "Архитектура", "Ada Lovelace" },
            //        { "Видеопамять", "24 GB GDDR6X" },
            //        { "Шина памяти", "384-bit" },
            //        { "Тактовая частота", "2235 MHz" },
            //        { "Разъемы питания", "1x 16-pin" },
            //        { "Рекомендуемый БП", "850W" }
            //    },
            //    Tags = new List<string> { "видеокарта", "nvidia", "rtx4090", "флагман", "игры" }
            //},
            //new Product
            //{
            //    Name = "Видеокарта AMD Radeon RX 7900 XTX",
            //    Slug = "amd-radeon-rx-7900-xtx",
            //    Description = "Флагманская видеокарта AMD с архитектурой RDNA 3",
            //    ShortDescription = "Топовая видеокарта от AMD для игр в 4K",
            //    Price = 1199.99m,
            //    OldPrice = 1299.99m,
            //    ImageUrl = "/images/products/gpu2.jpg",
            //    CategoryId = videoCardsCategory.Id,
            //    Brand = "AMD",
            //    StockQuantity = 9,
            //    IsFeatured = true,
            //    Rating = 4.6,
            //    ReviewsCount = 47,
            //    SoldCount = 68,
            //    Specifications = new Dictionary<string, string>
            //    {
            //        { "Графический процессор", "Radeon RX 7900 XTX" },
            //        { "Архитектура", "RDNA 3" },
            //        { "Видеопамять", "24 GB GDDR6" },
            //        { "Шина памяти", "384-bit" },
            //        { "Тактовая частота", "2300 MHz" },
            //        { "Разъемы питания", "2x 8-pin" },
            //        { "Рекомендуемый БП", "800W" }
            //    },
            //    Tags = new List<string> { "видеокарта", "amd", "rx7900", "4k", "игры" }
            //}
        };

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }

        private static async Task SeedUsersAndOrders(AppDbContext context)
        {
            // Примеры заказов для тестирования
            var products = await context.Products.Take(3).ToListAsync();

            var orders = new List<Order>
        {
            //new Order
            //{
            //    OrderNumber = "DS202412010001",
            //    CustomerEmail = "test1@example.com",
            //    CustomerPhone = "+375291234567",
            //    CustomerName = "Иван Иванов",
            //    ShippingAddress = "ул. Ленина, д. 10, кв. 25",
            //    ShippingCity = "Минск",
            //    ShippingRegion = "Минская область",
            //    ShippingPostalCode = "220100",
            //    Status = OrderStatus.Delivered,
            //    PaymentMethod = PaymentMethod.Card,
            //    PaymentStatus = PaymentStatus.Paid,
            //    Subtotal = 2899.99m,
            //    ShippingCost = 10m,
            //    TaxAmount = 580m,
            //    DiscountAmount = 0,
            //    OrderDate = DateTime.UtcNow.AddDays(-15),
            //    PaymentDate = DateTime.UtcNow.AddDays(-15),
            //    ShippedDate = DateTime.UtcNow.AddDays(-13),
            //    DeliveredDate = DateTime.UtcNow.AddDays(-10),
            //    OrderItems = new List<OrderItem>
            //    {
            //        new OrderItem
            //        {
            //            ProductId = products[0].Id,
            //            ProductName = products[0].Name,
            //            UnitPrice = products[0].Price,
            //            Quantity = 1
            //        }
            //    }
            //},
            //new Order
            //{
            //    OrderNumber = "DS202412020002",
            //    CustomerEmail = "test2@example.com",
            //    CustomerPhone = "+375292345678",
            //    CustomerName = "Петр Петров",
            //    ShippingAddress = "пр. Независимости, д. 58, оф. 304",
            //    ShippingCity = "Минск",
            //    Status = OrderStatus.Processing,
            //    PaymentMethod = PaymentMethod.CashOnDelivery,
            //    PaymentStatus = PaymentStatus.Pending,
            //    Subtotal = 3499.99m * 2,
            //    ShippingCost = 0m, // Бесплатная доставка от 5000
            //    TaxAmount = 699.99m,
            //    DiscountAmount = 0,
            //    OrderDate = DateTime.UtcNow.AddDays(-2),
            //    OrderItems = new List<OrderItem>
            //    {
            //        new OrderItem
            //        {
            //            ProductId = products[1].Id,
            //            ProductName = products[1].Name,
            //            UnitPrice = products[1].Price,
            //            Quantity = 2
            //        }
            //    }
            //}
        };

            await context.Orders.AddRangeAsync(orders);
            await context.SaveChangesAsync();

            // Примеры отзывов
            var reviews = new List<Review>
        {
            //new Review
            //{
            //    ProductId = products[0].Id,
            //    UserName = "Александр",
            //    UserEmail = "alex@example.com",
            //    Title = "Отличный ноутбук для игр",
            //    Comment = "Купил две недели назад, полностью удовлетворен. В Cyberpunk 2077 на ультра настройках стабильные 60 FPS. Качество сборки на высоте, подсветка клавиатуры очень красивая.",
            //    Rating = 5,
            //    Pros = new List<string> { "Высокая производительность", "Хорошее охлаждение", "Качественный дисплей" },
            //    Cons = new List<string> { "Немного тяжелый", "Шумноват под нагрузкой" },
            //    IsVerifiedPurchase = true,
            //    IsRecommended = true,
            //    IsApproved = true,
            //    CreatedAt = DateTime.UtcNow.AddDays(-5)
            //},
            //new Review
            //{
            //    ProductId = products[0].Id,
            //    UserName = "Мария",
            //    UserEmail = "maria@example.com",
            //    Title = "Хорошо, но есть недостатки",
            //    Comment = "Ноутбук мощный, но аккумулятора хватает всего на 2-3 часа в играх. Для стационарного использования отличный, для мобильного - не очень.",
            //    Rating = 4,
            //    Pros = new List<string> { "Мощная видеокарта", "Быстрый SSD", "Много портов" },
            //    Cons = new List<string> { "Слабый аккумулятор", "Греется при нагрузке" },
            //    IsVerifiedPurchase = true,
            //    IsRecommended = true,
            //    IsApproved = true,
            //    CreatedAt = DateTime.UtcNow.AddDays(-3)
            //}
        };

            await context.Reviews.AddRangeAsync(reviews);
            await context.SaveChangesAsync();
        }
    }
}
