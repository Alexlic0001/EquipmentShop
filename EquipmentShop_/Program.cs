using EquipmentShop.Infrastructure.Data;
using EquipmentShop.Infrastructure.Repositories;
using EquipmentShop.Infrastructure.Services;
using EquipmentShop.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReviewService, ReviewRepository>();

// Services
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "EquipmentShop.Session";
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();

    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Создаем базу если не существует
            await dbContext.Database.EnsureCreatedAsync();

            // Проверяем, пустая ли база
            if (!dbContext.Products.Any())
            {
                Console.WriteLine("База данных пустая, начинаем инициализацию...");

                // Создаем категорию
                var category = new Core.Entities.Category
                {
                    Name = "Ноутбуки",
                    Slug = "laptops-" + DateTime.Now.Ticks, // Уникальный slug
                    Description = "Игровые и рабочие ноутбуки",
                    IsActive = true,
                    ShowInMenu = true
                };

                dbContext.Categories.Add(category);
                await dbContext.SaveChangesAsync();
                Console.WriteLine($"Создана категория: {category.Name}");

                // Добавляем товары
                var product1 = new Core.Entities.Product
                {
                    Name = "Тестовый ноутбук 1",
                    Slug = "test-laptop-1-" + DateTime.Now.Ticks,
                    Description = "Описание тестового ноутбука",
                    Price = 999.99m,
                    ImageUrl = "/images/products/default.jpg",
                    CategoryId = category.Id,
                    Brand = "TestBrand",
                    StockQuantity = 10,
                    IsAvailable = true
                };

                dbContext.Products.Add(product1);
                await dbContext.SaveChangesAsync();
                Console.WriteLine($"Добавлен товар: {product1.Name}");
            }
            else
            {
                Console.WriteLine($"В базе уже есть {dbContext.Products.Count()} товаров");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при инициализации базы данных: {ex.Message}");

        // Выводим внутренние исключения
        var innerEx = ex.InnerException;
        while (innerEx != null)
        {
            Console.WriteLine($"Внутренняя ошибка: {innerEx.Message}");
            innerEx = innerEx.InnerException;
        }
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();