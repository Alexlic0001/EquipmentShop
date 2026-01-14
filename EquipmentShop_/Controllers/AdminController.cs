using CsvHelper;
using CsvHelper.Configuration;
using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace EquipmentShop.Controllers
{
    [Authorize(Roles = $"{AppConstants.AdminRole},{AppConstants.ManagerRole}")]
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IOrderRepository orderRepository,
            IFileStorageService fileStorageService,
            ILogger<AdminController> logger,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _orderRepository = orderRepository;
            _fileStorageService = fileStorageService;
            _logger = logger;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ========== DASHBOARD ==========
        [HttpGet("")]
        public async Task<IActionResult> Dashboard()
        {
            var totalProducts = await _productRepository.CountAsync();
            var totalOrders = await _orderRepository.CountAsync();
            var totalCategories = await _categoryRepository.CountAsync();
            var totalUsers = await _userManager.Users.CountAsync();

            ViewBag.TotalProducts = totalProducts;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalCategories = totalCategories;
            ViewBag.TotalUsers = totalUsers;

            return View();
        }

        // ========== ЭКСПОРТ / ИМПОРТ (ТОЛЬКО ADMIN) ==========

        [HttpGet("export/categories")]
        [Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> ExportCategories()
        {
            var categories = await _categoryRepository.GetAllAsync();
            var records = categories.Select(c => new CategoryImportModel
            {
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description ?? "",
                Parent = c.ParentCategory?.Name ?? "",
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive ? "Да" : "Нет",
                ShowInMenu = c.ShowInMenu ? "Да" : "Нет",
                ImageUrl = c.ImageUrl ?? AppConstants.DefaultCategoryImage
            });
            return GenerateCsv(records, "categories_export.csv");
        }

        [HttpGet("export/products")]
        [Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> ExportProducts()
        {
            var products = await _productRepository.GetAllAsync();
            var records = products.Select(p => new ProductImportModel
            {
                Name = p.Name,
                Slug = p.Slug,
                Description = p.Description,
                ShortDescription = p.ShortDescription,
                Price = p.Price,
                OldPrice = p.OldPrice?.ToString("F2"),
                ImageUrl = p.ImageUrl ?? AppConstants.DefaultProductImage,
                Brand = p.Brand,
                StockQuantity = p.StockQuantity,
                Category = p.Category?.Name ?? "",
                IsAvailable = p.IsAvailable ? "Да" : "Нет",
                IsFeatured = p.IsFeatured ? "Да" : "Нет",
                IsNew = p.IsNew ? "Да" : "Нет",
                Tags = string.Join(", ", p.Tags),
                Specifications = string.Join("; ", p.Specifications.Select(kv => $"{kv.Key}={kv.Value}"))
            });
            return GenerateCsv(records, "products_export.csv");
        }

        //[HttpGet("export/users-with-orders")]
        //[Authorize(Roles = AppConstants.AdminRole)]
        //public async Task<IActionResult> ExportUsersWithOrders()
        //{
        //    var users = await _userManager.Users.ToListAsync();
        //    var allOrders = await _orderRepository.GetAllAsync();

        //    var records = new List<dynamic>();
        //    foreach (var user in users)
        //    {
        //        var userOrders = allOrders.Where(o => o.UserId == user.Id).ToList();
        //        if (!userOrders.Any())
        //        {
        //            records.Add(new
        //            {
        //                UserId = user.Id,
        //                user.Email,
        //                user.FirstName,
        //                user.LastName,
        //                user.PhoneNumber,
        //                OrderNumber = (string?)null,
        //                OrderDate = (DateTime?)null,
        //                OrderTotal = (decimal?)null,
        //                Status = (string?)null
        //            });
        //        }
        //        else
        //        {
        //            foreach (var order in userOrders)
        //            {
        //                records.Add(new
        //                {
        //                    UserId = user.Id,
        //                    user.Email,
        //                    user.FirstName,
        //                    user.LastName,
        //                    user.PhoneNumber,
        //                    order.OrderNumber,
        //                    OrderDate = order.OrderDate,
        //                    OrderTotal = order.Total,
        //                    Status = order.Status.ToString()
        //                });
        //            }
        //        }
        //    }

        //    return GenerateCsv(records, "users_with_orders.csv");
        //}
        [HttpGet("export/users")]
        [Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> ExportUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var records = users.Select(async u => new UserImportModel
            {
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Phone = u.PhoneNumber ?? "",
                Role = (await _userManager.GetRolesAsync(u)).FirstOrDefault() ?? "Customer",
                EmailConfirmed = u.EmailConfirmed
            });
            return GenerateCsv(records, "users_export.csv");
        }


        [HttpGet("export/users-and-orders")]
        [Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> ExportUsersAndOrders()
        {
            var orders = await _orderRepository.GetAllAsync();
            var records = new List<UserOrderExportModel>();

            foreach (var order in orders)
            {
                // Находим пользователя по UserId или используем данные из заказа
                ApplicationUser? user = null;
                if (!string.IsNullOrEmpty(order.UserId))
                {
                    user = await _userManager.FindByIdAsync(order.UserId);
                }

                var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
                var role = roles.FirstOrDefault() ?? "Customer";

                records.Add(new UserOrderExportModel
                {
                    // Пользователь
                    Email = order.CustomerEmail,
                    FirstName = user?.FirstName ?? order.CustomerName.Split(' ').FirstOrDefault() ?? "",
                    LastName = user?.LastName ?? order.CustomerName.Split(' ').LastOrDefault() ?? "",
                    Phone = order.CustomerPhone,
                    Role = role,

                    // Заказ
                    OrderNumber = order.OrderNumber,
                    OrderDate = order.OrderDate,
                    Status = order.Status.ToString(),
                    PaymentMethod = order.PaymentMethod.ToString(),
                    Total = order.Total,

                    // Адрес
                    ShippingAddress = order.ShippingAddress,
                    City = order.ShippingCity ?? "",

                    // Товары
                    Items = string.Join("; ", order.OrderItems.Select(oi =>
                        $"{oi.ProductName.Replace(";", ",").Replace("=", ":")}={oi.UnitPrice:F2}={oi.Quantity}"))
                });
            }

            return GenerateCsv(records, "users_and_orders.csv");
        }



        private async Task ImportUsersAndOrdersFromCsv(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                PrepareHeaderForMatch = args => args.Header.Trim(),
                MissingFieldFound = null
            });

            var records = csv.GetRecords<UserOrderExportModel>().ToList();

            foreach (var rec in records)
            {
                // === ШАГ 1: Создаём/находим пользователя ===
                var user = await _userManager.FindByEmailAsync(rec.Email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = rec.Email,
                        Email = rec.Email,
                        FirstName = rec.FirstName.Trim(),
                        LastName = rec.LastName.Trim(),
                        PhoneNumber = rec.Phone.Trim(),
                        EmailConfirmed = true,
                        RegisteredAt = DateTime.UtcNow
                    };

                    var result = await _userManager.CreateAsync(user, "TempPass123!");
                    if (!result.Succeeded) continue;

                    // Назначаем роль
                    var roleName = rec.Role switch
                    {
                        "Admin" => AppConstants.AdminRole,
                        "Manager" => AppConstants.ManagerRole,
                        _ => AppConstants.CustomerRole
                    };
                    await _userManager.AddToRoleAsync(user, roleName);
                }

                // === ШАГ 2: Пропускаем, если заказ уже существует ===
                if (await _orderRepository.GetByOrderNumberAsync(rec.OrderNumber) != null)
                    continue;

                // === ШАГ 3: Создаём заказ ===
                var order = new Order
                {
                    OrderNumber = rec.OrderNumber,
                    UserId = user.Id,
                    CustomerEmail = rec.Email,
                    CustomerName = $"{rec.FirstName} {rec.LastName}".Trim(),
                    CustomerPhone = rec.Phone,
                    ShippingAddress = rec.ShippingAddress,
                    ShippingCity = rec.City,
                    Status = Enum.TryParse<OrderStatus>(rec.Status, out var s) ? s : OrderStatus.Pending,
                    PaymentMethod = Enum.TryParse<PaymentMethod>(rec.PaymentMethod, out var p) ? p : PaymentMethod.Card,
                    OrderDate = rec.OrderDate,
                    Subtotal = rec.Total,
                    ShippingCost = 0m,
                    TaxAmount = 0m,
                    DiscountAmount = 0m,
                    PaymentStatus = PaymentStatus.Paid // или Pending, если нужно
                };

                // Парсим товары
                if (!string.IsNullOrEmpty(rec.Items))
                {
                    foreach (var itemStr in rec.Items.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = itemStr.Split('=');
                        if (parts.Length >= 3)
                        {
                            var name = parts[0].Trim();
                            var price = decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var pr) ? pr : 0m;
                            var qty = int.TryParse(parts[2], out var q) ? q : 1;

                            order.OrderItems.Add(new OrderItem
                            {
                                ProductName = name,
                                UnitPrice = price,
                                Quantity = qty
                            });
                        }
                    }
                }

                await _orderRepository.AddAsync(order);
            }
        }


        [HttpGet("export/orders")]
        [Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> ExportOrders()
        {
            var orders = await _orderRepository.GetAllAsync();
            var records = orders.Select(o => new OrderImportModel
            {
                OrderNumber = o.OrderNumber,
                CustomerEmail = o.CustomerEmail,
                CustomerName = o.CustomerName,
                CustomerPhone = o.CustomerPhone,
                ShippingAddress = o.ShippingAddress,
                Status = o.Status.ToString(),
                PaymentMethod = o.PaymentMethod.ToString(),
                Subtotal = o.Subtotal,
                ShippingCost = o.ShippingCost,
                TaxAmount = o.TaxAmount,
                DiscountAmount = o.DiscountAmount,
                OrderDate = o.OrderDate,
                Items = string.Join(" || ", o.OrderItems.Select(oi =>
                {
                    var name = oi.ProductName
                        .Replace("\r", "")
                        .Replace("\n", " ")
                        .Replace(";", ",")
                        .Replace("||", " ");
                    return $"{name}|{oi.UnitPrice:F2}|{oi.Quantity}";
                }))
            });
            return GenerateCsv(records, "orders_export.csv");
        }
        private IActionResult GenerateCsv<T>(IEnumerable<T> records, string fileName)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true
            });
            csv.WriteRecords(records);
            writer.Flush();
            return File(memoryStream.ToArray(), "text/csv", fileName);
        }

        [HttpGet("import")]
        [Authorize(Roles = AppConstants.AdminRole)]
        public IActionResult ImportData()
        {
            return View();
        }

        [HttpPost("import")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> ImportData(
    IFormFile categoriesFile,
    IFormFile productsFile,
    IFormFile usersAndOrdersFile) // ← один файл вместо двух
        {
            try
            {
                if (categoriesFile?.Length > 0)
                    await ImportCategoriesFromCsv(categoriesFile.OpenReadStream());

                if (productsFile?.Length > 0)
                    await ImportProductsFromCsv(productsFile.OpenReadStream());

                if (usersAndOrdersFile?.Length > 0)
                    await ImportUsersAndOrdersFromCsv(usersAndOrdersFile.OpenReadStream());

                TempData["Success"] = "Данные успешно импортированы";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при импорте данных");
                TempData["Error"] = $"Ошибка импорта: {ex.Message}";
            }
            return RedirectToAction(nameof(ImportData));
        }

        private async Task ImportCategoriesFromCsv(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                PrepareHeaderForMatch = args => args.Header.Trim(),
                MissingFieldFound = null
            });

            var records = csv.GetRecords<CategoryImportModel>().ToList();
            var allCategories = (await _categoryRepository.GetAllAsync()).ToList();

            // Загружаем существующие слаги
            var existingSlugs = allCategories
                .Select(c => c.Slug)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var rec in records)
            {
                if (string.IsNullOrWhiteSpace(rec.Name)) continue;

                var baseSlug = string.IsNullOrWhiteSpace(rec.Slug)
                    ? GenerateSlug(rec.Name)
                    : rec.Slug.Trim();

                // Пропускаем, если такой slug уже есть
                if (existingSlugs.Contains(baseSlug))
                {
                    _logger.LogWarning("Пропущена категория с дублирующимся Slug: {Name} ({Slug})", rec.Name, baseSlug);
                    continue;
                }

                Category? parent = null;
                if (!string.IsNullOrEmpty(rec.Parent))
                {
                    parent = allCategories.FirstOrDefault(c =>
                        c.Name.Equals(rec.Parent.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                var category = new Category
                {
                    Name = rec.Name.Trim(),
                    Slug = baseSlug,
                    Description = rec.Description?.Trim() ?? "",
                    ParentCategoryId = parent?.Id,
                    DisplayOrder = rec.DisplayOrder,
                    IsActive = ParseBool(rec.IsActive),
                    ShowInMenu = ParseBool(rec.ShowInMenu),
                    ImageUrl = string.IsNullOrWhiteSpace(rec.ImageUrl) || rec.ImageUrl == AppConstants.DefaultCategoryImage
                        ? AppConstants.DefaultCategoryImage
                        : rec.ImageUrl.Trim(),
                    //CreatedAt = DateTime.UtcNow,
                    //UpdatedAt = DateTime.UtcNow
                };

                await _categoryRepository.AddAsync(category);
                allCategories.Add(category);
                existingSlugs.Add(baseSlug); // чтобы избежать коллизий внутри одного файла
            }
        }

        private async Task ImportProductsFromCsv(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                PrepareHeaderForMatch = args => args.Header.Trim(),
                MissingFieldFound = null
            });

            var records = csv.GetRecords<ProductImportModel>().ToList();
            var allCategories = await _categoryRepository.GetAllAsync();

            // Загружаем существующие слаги
            var existingSlugs = (await _productRepository.GetAllAsync())
                .Select(p => p.Slug)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var rec in records)
            {
                if (string.IsNullOrWhiteSpace(rec.Name)) continue;

                var baseSlug = string.IsNullOrWhiteSpace(rec.Slug)
                    ? GenerateSlug(rec.Name)
                    : rec.Slug.Trim();

                // Пропускаем дубликаты
                if (existingSlugs.Contains(baseSlug))
                {
                    _logger.LogWarning("Пропущен товар с дублирующимся Slug: {Name} ({Slug})", rec.Name, baseSlug);
                    continue;
                }

                var category = string.IsNullOrWhiteSpace(rec.Category)
                    ? null
                    : allCategories.FirstOrDefault(c =>
                        c.Name.Equals(rec.Category.Trim(), StringComparison.OrdinalIgnoreCase));

                decimal price = rec.Price;
                decimal? oldPrice = null;
                if (!string.IsNullOrEmpty(rec.OldPrice))
                {
                    if (decimal.TryParse(rec.OldPrice.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var op))
                        oldPrice = op;
                }

                var product = new Product
                {
                    Name = rec.Name.Trim(),
                    Slug = baseSlug,
                    Description = rec.Description?.Trim() ?? string.Empty,
                    ShortDescription = rec.ShortDescription?.Trim() ?? string.Empty,
                    Price = price,
                    OldPrice = oldPrice,
                    ImageUrl = string.IsNullOrWhiteSpace(rec.ImageUrl) || rec.ImageUrl == AppConstants.DefaultProductImage
                        ? AppConstants.DefaultProductImage
                        : rec.ImageUrl.Trim(),
                    Brand = rec.Brand?.Trim() ?? string.Empty,
                    StockQuantity = Math.Max(0, rec.StockQuantity),
                    MinStockThreshold = 5,
                    IsAvailable = ParseBool(rec.IsAvailable),
                    IsFeatured = ParseBool(rec.IsFeatured),
                    IsNew = ParseBool(rec.IsNew),
                    CategoryId = category?.Id ?? 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                product.Tags = string.IsNullOrWhiteSpace(rec.Tags)
                    ? new List<string>()
                    : rec.Tags
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();

                product.Specifications = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(rec.Specifications))
                {
                    foreach (var pair in rec.Specifications.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = pair.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();
                            if (!string.IsNullOrEmpty(key))
                                product.Specifications[key] = value;
                        }
                    }
                }

                await _productRepository.AddAsync(product);
                existingSlugs.Add(baseSlug); // для защиты от дублей внутри одного файла
            }
        }


        private async Task ImportUsersFromCsv(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                PrepareHeaderForMatch = args => args.Header.Trim(),
                MissingFieldFound = null
            });

            var records = csv.GetRecords<UserImportModel>().ToList();

            foreach (var rec in records)
            {
                if (string.IsNullOrWhiteSpace(rec.Email)) continue;

                // Пропускаем, если пользователь уже существует
                if (await _userManager.FindByEmailAsync(rec.Email) != null) continue;

                var user = new ApplicationUser
                {
                    UserName = rec.Email,
                    Email = rec.Email,
                    FirstName = rec.FirstName.Trim(),
                    LastName = rec.LastName.Trim(),
                    PhoneNumber = rec.Phone.Trim(),
                    EmailConfirmed = rec.EmailConfirmed,
                    RegisteredAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, "TempPass123!");
                if (!result.Succeeded) continue;

                // Назначаем роль
                var role = rec.Role switch
                {
                    "Admin" => AppConstants.AdminRole,
                    "Manager" => AppConstants.ManagerRole,
                    _ => AppConstants.CustomerRole
                };
                await _userManager.AddToRoleAsync(user, role);
            }
        }


        private async Task ImportOrdersFromCsv(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                PrepareHeaderForMatch = args => args.Header.Trim(),
                MissingFieldFound = null
            });

            var records = csv.GetRecords<OrderImportModel>().ToList();
            var existingOrderNumbers = (await _orderRepository.GetAllAsync())
                .Select(o => o.OrderNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var rec in records)
            {
                if (string.IsNullOrWhiteSpace(rec.OrderNumber) || existingOrderNumbers.Contains(rec.OrderNumber))
                    continue;

                // Находим пользователя по email
                var user = await _userManager.FindByEmailAsync(rec.CustomerEmail);
                var userId = user?.Id;

                var order = new Order
                {
                    OrderNumber = rec.OrderNumber,
                    UserId = userId,
                    CustomerEmail = rec.CustomerEmail,
                    CustomerName = rec.CustomerName,
                    CustomerPhone = rec.CustomerPhone,
                    ShippingAddress = rec.ShippingAddress,
                    Status = Enum.TryParse<OrderStatus>(rec.Status, out var s) ? s : OrderStatus.Pending,
                    PaymentMethod = Enum.TryParse<PaymentMethod>(rec.PaymentMethod, out var p) ? p : PaymentMethod.Card,
                    Subtotal = rec.Subtotal,
                    ShippingCost = rec.ShippingCost,
                    TaxAmount = rec.TaxAmount,
                    DiscountAmount = rec.DiscountAmount,
                    OrderDate = rec.OrderDate,
                    PaymentStatus = rec.PaymentMethod == "CashOnDelivery" ? PaymentStatus.Pending : PaymentStatus.Paid
                };

                // Парсим товары
                if (!string.IsNullOrEmpty(rec.Items))
                {
                    foreach (var itemStr in rec.Items.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = itemStr.Split('=');
                        if (parts.Length >= 3)
                        {
                            var name = parts[0].Trim();
                            var price = decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var pr) ? pr : 0m;
                            var qty = int.TryParse(parts[2], out var q) ? q : 1;

                            order.OrderItems.Add(new OrderItem
                            {
                                ProductName = name,
                                UnitPrice = price,
                                Quantity = qty
                            });
                        }
                    }
                }

                await _orderRepository.AddAsync(order);
                existingOrderNumbers.Add(rec.OrderNumber);
            }
        }







        private bool ParseBool(string value)
        {
            var v = value?.Trim();
            return v != null &&
                   (v.Equals("Да", StringComparison.OrdinalIgnoreCase) ||
                    v.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                    v.Equals("true", StringComparison.OrdinalIgnoreCase));
        }

        private string GenerateSlug(string name)
        {
            if (string.IsNullOrEmpty(name)) return "product";
            var slug = name.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("&", "and").Replace("+", "plus").Replace("%", "percent")
                .Replace("$", "dollar").Replace("#", "sharp").Replace("@", "at");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            slug = slug.Trim('-');
            return string.IsNullOrEmpty(slug) ? $"product-{DateTime.Now:yyyyMMddHHmmss}" : slug;
        }

        // ========== ТОВАРЫ ==========

        [HttpGet("products")]
        public async Task<IActionResult> Products()
        {
            var products = await _productRepository.GetAllAsync();
            return View(products);
        }

        [HttpGet("products/{id}")]
        public async Task<IActionResult> ProductDetails(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpGet("products/create")]
        public async Task<IActionResult> CreateProduct()
        {
            ViewBag.Categories = await _categoryRepository.GetAllAsync();
            return View();
        }

        [HttpPost("products/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product, IFormFile imageFile)
        {
            var allCategories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = allCategories;

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(product.Name)) errors.Add("Название товара обязательно");
            if (string.IsNullOrWhiteSpace(product.Description)) errors.Add("Описание товара обязательно");
            if (product.Price <= 0) errors.Add("Цена должна быть больше 0");
            if (product.StockQuantity < 0) errors.Add("Количество не может быть отрицательным");
            if (product.CategoryId <= 0) errors.Add("Категория обязательна");

            if (errors.Any())
            {
                foreach (var error in errors) ModelState.AddModelError("", error);
                return View(product);
            }

            try
            {
                product.ImageUrl = AppConstants.DefaultProductImage;
                if (imageFile?.Length > 0)
                {
                    try
                    {
                        var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                        var filePath = await _fileStorageService.SaveProductImageAsync(imageFile.OpenReadStream(), fileName);
                        product.ImageUrl = filePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при загрузке изображения");
                    }
                }

                product.Slug ??= GenerateSlug(product.Name);
                product.MetaTitle ??= product.Name;
                product.MetaDescription ??= product.Description.Length > 160 ? product.Description[..160] + "..." : product.Description;

                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
                product.IsAvailable = product.StockQuantity > 0;

                await _productRepository.AddAsync(product);
                TempData["Success"] = "Товар успешно создан";
                return RedirectToAction("Products");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании товара");
                ModelState.AddModelError("", $"Ошибка: {ex.Message}");
                return View(product);
            }
        }

        [HttpGet("products/edit/{id}")]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();

            if (product.Tags != null && product.Tags.Any())
                product.TagsString = string.Join(", ", product.Tags);

            ViewBag.Categories = await _categoryRepository.GetAllAsync();
            return View(product);
        }

        [HttpPost("products/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, IFormFile imageFile)
        {
            var existingProduct = await _productRepository.GetByIdAsync(id);
            if (existingProduct == null) return NotFound();

            ViewBag.Categories = await _categoryRepository.GetAllAsync();
            var form = Request.Form;

            var name = form["Name"].ToString().Trim();
            var description = form["Description"].ToString().Trim();
            var shortDescription = form["ShortDescription"].ToString().Trim();
            var slug = form["Slug"].ToString().Trim();
            var brand = form["Brand"].ToString().Trim();
            var metaTitle = form["MetaTitle"].ToString().Trim();
            var metaDescription = form["MetaDescription"].ToString().Trim();
            var metaKeywords = form["MetaKeywords"].ToString().Trim();
            var tagsString = form["TagsString"].ToString().Trim();

            if (!decimal.TryParse(form["Price"], out var price) || price <= 0)
            {
                ModelState.AddModelError("Price", "Цена обязательна и должна быть больше 0");
                return View(existingProduct);
            }

            decimal? oldPrice = decimal.TryParse(Request.Form["OldPrice"], out var op) ? op : null;

            if (!int.TryParse(form["StockQuantity"], out var stockQuantity) || stockQuantity < 0)
            {
                ModelState.AddModelError("StockQuantity", "Количество не может быть отрицательным");
                return View(existingProduct);
            }

            if (!int.TryParse(form["MinStockThreshold"], out var minStockThreshold))
                minStockThreshold = 5;

            if (!int.TryParse(form["CategoryId"], out var categoryId) || categoryId <= 0)
            {
                ModelState.AddModelError("CategoryId", "Выберите категорию");
                return View(existingProduct);
            }

            var isFeatured = form.ContainsKey("IsFeatured");
            var isNew = form.ContainsKey("IsNew");
            var isAvailable = form.ContainsKey("IsAvailable");

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
            {
                ModelState.AddModelError("", "Название и описание обязательны");
                return View(existingProduct);
            }

            try
            {
                if (imageFile?.Length > 0)
                {
                    if (!string.IsNullOrEmpty(existingProduct.ImageUrl) && !existingProduct.ImageUrl.Contains("default"))
                        await _fileStorageService.DeleteFileAsync(existingProduct.ImageUrl);

                    var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                    var filePath = await _fileStorageService.SaveProductImageAsync(imageFile.OpenReadStream(), fileName);
                    existingProduct.ImageUrl = filePath;
                }

                existingProduct.Name = name;
                existingProduct.Slug = string.IsNullOrEmpty(slug) ? GenerateSlug(name) : slug;
                existingProduct.Description = description;
                existingProduct.ShortDescription = shortDescription;
                existingProduct.Price = price;
                existingProduct.OldPrice = oldPrice;
                existingProduct.Brand = brand;
                existingProduct.StockQuantity = stockQuantity;
                existingProduct.MinStockThreshold = minStockThreshold;
                existingProduct.CategoryId = categoryId;
                existingProduct.IsFeatured = isFeatured;
                existingProduct.IsNew = isNew;
                existingProduct.IsAvailable = isAvailable;
                existingProduct.MetaTitle = metaTitle;
                existingProduct.MetaDescription = metaDescription;
                existingProduct.MetaKeywords = metaKeywords;
                existingProduct.UpdatedAt = DateTime.UtcNow;

                existingProduct.Tags = string.IsNullOrEmpty(tagsString)
                    ? new List<string>()
                    : tagsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim())
                                .Where(t => !string.IsNullOrEmpty(t))
                                .ToList();

                await _productRepository.UpdateAsync(existingProduct);
                TempData["Success"] = "Товар успешно обновлён";
                return RedirectToAction("ProductDetails", new { id = existingProduct.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении товара ID={Id}", id);
                ModelState.AddModelError("", "Произошла ошибка при сохранении");
                return View(existingProduct);
            }
        }

        [HttpPost("products/simple-edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SimpleEdit(int id, string name, decimal price, int stockQuantity)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                    return Json(new { success = false, message = "Товар не найден" });

                product.Name = name;
                product.Price = price;
                product.StockQuantity = stockQuantity;
                product.IsAvailable = stockQuantity > 0;
                product.UpdatedAt = DateTime.UtcNow;

                await _productRepository.UpdateAsync(product);
                return Json(new { success = true, message = "Товар успешно обновлён" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при простом редактировании товара ID={Id}", id);
                return Json(new { success = false, message = "Ошибка: " + ex.Message });
            }
        }

        [HttpPost("products/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            if (!User.IsInRole(AppConstants.AdminRole))
            {
                TempData["Error"] = "У вас недостаточно прав для удаления товаров.";
                return RedirectToAction("Products");
            }

            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();

            try
            {
                if (!string.IsNullOrEmpty(product.ImageUrl) && !product.ImageUrl.Contains("default"))
                    await _fileStorageService.DeleteFileAsync(product.ImageUrl);

                await _productRepository.DeleteAsync(product);
                TempData["Success"] = "Товар успешно удалён";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении товара ID={Id}", id);
                TempData["Error"] = "Не удалось удалить товар.";
            }

            return RedirectToAction("Products");
        }

        // ========== КАТЕГОРИИ ==========

        [HttpGet("categories")]
        public async Task<IActionResult> Categories()
        {
            var categories = await _categoryRepository.GetAllAsync();
            return View(categories);
        }

        [HttpGet("categories/create")]
        public async Task<IActionResult> CreateCategory()
        {
            var allCategories = await _categoryRepository.GetAllAsync();
            ViewBag.ParentCategories = allCategories.Where(c => c.IsActive).ToList();
            return View();
        }

        [HttpPost("categories/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(Category category, IFormFile imageFile)
        {
            var allCategories = await _categoryRepository.GetAllAsync();
            ViewBag.ParentCategories = allCategories.Where(c => c.IsActive).ToList();

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError("Name", "Название категории обязательно");
                return View(category);
            }

            try
            {
                category.ImageUrl = AppConstants.DefaultCategoryImage;
                if (imageFile?.Length > 0)
                {
                    try
                    {
                        var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                        var filePath = await _fileStorageService.SaveCategoryImageAsync(imageFile.OpenReadStream(), fileName);
                        category.ImageUrl = filePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при загрузке изображения категории");
                    }
                }

                category.Slug ??= category.Name.ToLower().Replace(" ", "-").Replace(".", "").Replace(",", "");
                category.MetaTitle ??= category.Name;
                category.MetaDescription ??= category.Description ?? category.Name;
                category.IsActive = true;

                await _categoryRepository.AddAsync(category);
                TempData["Success"] = "Категория успешно создана";
                return RedirectToAction("Categories");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании категории");
                ModelState.AddModelError("", $"Ошибка: {ex.Message}");
                return View(category);
            }
        }

        [HttpGet("categories/edit/{id}")]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null) return NotFound();

            var allCategories = await _categoryRepository.GetAllAsync();
            ViewBag.ParentCategories = allCategories.Where(c => c.IsActive && c.Id != id).ToList();
            return View(category);
        }

        [HttpPost("categories/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int id, Category category, IFormFile imageFile)
        {
            if (id != category.Id) return NotFound();

            var allCategories = await _categoryRepository.GetAllAsync();
            ViewBag.ParentCategories = allCategories.Where(c => c.IsActive && c.Id != id).ToList();

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError("Name", "Название категории обязательно");
                return View(category);
            }

            try
            {
                var existing = await _categoryRepository.GetByIdAsync(id);
                if (existing == null) return NotFound();

                if (imageFile?.Length > 0)
                {
                    if (!string.IsNullOrEmpty(existing.ImageUrl) && !existing.ImageUrl.Contains("default"))
                        await _fileStorageService.DeleteFileAsync(existing.ImageUrl);

                    var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                    var filePath = await _fileStorageService.SaveCategoryImageAsync(imageFile.OpenReadStream(), fileName);
                    category.ImageUrl = filePath;
                }
                else
                {
                    category.ImageUrl = existing.ImageUrl;
                }

                category.Slug ??= category.Name.ToLower().Replace(" ", "-").Replace(".", "").Replace(",", "");

                await _categoryRepository.UpdateAsync(category);
                TempData["Success"] = "Категория успешно обновлена";
                return RedirectToAction("Categories");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении категории ID={Id}", id);
                ModelState.AddModelError("", $"Ошибка: {ex.Message}");
                return View(category);
            }
        }

        [HttpPost("categories/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!User.IsInRole(AppConstants.AdminRole))
            {
                TempData["Error"] = "У вас недостаточно прав для удаления категорий.";
                return RedirectToAction("Categories");
            }

            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null) return NotFound();

            try
            {
                if (await _categoryRepository.HasProductsAsync(id))
                {
                    TempData["Error"] = "Нельзя удалить категорию, в которой есть товары";
                    return RedirectToAction("Categories");
                }

                if ((category.SubCategories?.Any() ?? false))
                {
                    TempData["Error"] = "Нельзя удалить категорию с подкатегориями";
                    return RedirectToAction("Categories");
                }

                if (!string.IsNullOrEmpty(category.ImageUrl) && !category.ImageUrl.Contains("default"))
                    await _fileStorageService.DeleteFileAsync(category.ImageUrl);

                await _categoryRepository.DeleteAsync(category);
                TempData["Success"] = "Категория успешно удалена";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении категории ID={Id}", id);
                TempData["Error"] = "Не удалось удалить категорию.";
            }

            return RedirectToAction("Categories");
        }

        // ========== ЗАКАЗЫ ==========

        [HttpGet("orders")]
        public async Task<IActionResult> Orders()
        {
            var orders = await _orderRepository.GetRecentOrdersAsync(50);
            return View(orders);
        }

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _orderRepository.GetWithItemsAsync(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost("orders/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            if (!User.IsInRole(AppConstants.AdminRole))
            {
                TempData["Error"] = "У вас недостаточно прав для удаления заказов.";
                return RedirectToAction("Orders");
            }

            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                TempData["Error"] = "Заказ не найден.";
                return RedirectToAction("Orders");
            }

            try
            {
                await _orderRepository.DeleteAsync(order);
                TempData["Success"] = $"Заказ {order.OrderNumber} успешно удалён.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении заказа ID={OrderId}", id);
                TempData["Error"] = "Не удалось удалить заказ.";
            }

            return RedirectToAction("Orders");
        }

        // ========== ПОЛЬЗОВАТЕЛИ ==========

        [HttpGet("users")]
        public async Task<IActionResult> Users()
        {
            if (!User.IsInRole(AppConstants.AdminRole))
            {
                TempData["Error"] = "У вас нет доступа к управлению пользователями.";
                return RedirectToAction("Dashboard");
            }

            var users = await _userManager.Users.OrderBy(u => u.FirstName).ToListAsync();
            return View(users);
        }

        [HttpGet("users/edit/{id}")]
        public async Task<IActionResult> EditUser(string id)
        {
            if (!User.IsInRole(AppConstants.AdminRole))
            {
                TempData["Error"] = "У вас нет доступа к управлению пользователями.";
                return RedirectToAction("Dashboard");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            ViewBag.Roles = await _roleManager.Roles.ToListAsync();
            ViewBag.UserRoles = await _userManager.GetRolesAsync(user);
            return View(user);
        }

        [HttpPost("users/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string id, string selectedRole)
        {
            if (!User.IsInRole(AppConstants.AdminRole))
                return Forbid();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (!string.IsNullOrEmpty(selectedRole))
                await _userManager.AddToRoleAsync(user, selectedRole);

            TempData["Success"] = "Роли пользователя обновлены";
            return RedirectToAction("Users");
        }

        [HttpPost("users/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (!User.IsInRole(AppConstants.AdminRole))
                return Forbid();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["Error"] = "Нельзя удалить собственную учётную запись";
                return RedirectToAction("Users");
            }

            await _userManager.DeleteAsync(user);
            TempData["Success"] = "Пользователь удалён";
            return RedirectToAction("Users");
        }
    }
}