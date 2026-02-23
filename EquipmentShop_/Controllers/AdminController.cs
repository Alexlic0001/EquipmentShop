using CsvHelper;
using CsvHelper.Configuration;
using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Exceptions;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels.Admin;
using EquipmentShop.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EquipmentShop_.Controllers
{
    [Authorize(Roles = $"{AppConstants.AdminRole},{AppConstants.ManagerRole}")]
    [Route("admin")]
    public partial class AdminController(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IOrderRepository orderRepository,
        IFileStorageService fileStorageService,
        ILogger<AdminController> logger,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        RoleManager<IdentityRole> roleManager,
        IOrderService orderService) : Controller

    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly RoleManager<IdentityRole> _roleManager = roleManager;
        private readonly IProductRepository _productRepository = productRepository;
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly IFileStorageService _fileStorageService = fileStorageService;
        private readonly ILogger<AdminController> _logger = logger;
        private readonly AppDbContext _context = context;
        private readonly IOrderService _orderService = orderService;
        private static readonly string[] data = ["Произошла ошибка при создании товара"];

        [GeneratedRegex(@"[^a-z0-9\-]")]
        private static partial Regex NonAlphanumericRegex();

        // ========== DASHBOARD ==========
        [HttpGet("")]
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalProducts = await _productRepository.CountAsync();
            ViewBag.TotalOrders = await _orderRepository.CountAsync();
            ViewBag.TotalCategories = await _categoryRepository.CountAsync();
            ViewBag.TotalUsers = await _userManager.Users.CountAsync();
            return View();
        }

        // ========== ЭКСПОРТ / ИМПОРТ ==========
        [HttpGet("export/categories"), Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> ExportCategories()
        {
            var categories = await _categoryRepository.GetAllAsync();
            var records = categories.Select(MapCategoryToImportModel);
            return GenerateCsv(records, "categories_export.csv");
        }

        [HttpGet("export/products"), Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> ExportProducts()
        {
            var products = await _productRepository.GetAllAsync();
            var records = products.Select(MapProductToImportModel);
            return GenerateCsv(records, "products_export.csv");
        }

        [HttpGet("export/users-and-orders"), Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> ExportUsersAndOrders()
        {
            var orders = await _orderRepository.GetAllWithItemsAsync();
            var records = await MapOrdersToExportModelsAsync(orders);
            return GenerateCsv(records, "users_and_orders.csv");
        }

        [HttpGet("import"), Authorize(Roles = AppConstants.AdminRole)]
        public IActionResult ImportData() => View();

        [HttpPost("import"), ValidateAntiForgeryToken, Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> ImportData(
            IFormFile categoriesFile,
            IFormFile productsFile,
            IFormFile usersAndOrdersFile)
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

        // ========== ТОВАРЫ ==========
        [HttpGet("products")]
        public async Task<IActionResult> Products()
        {
            var products = await _productRepository.GetAllAsync();
            var categories = await _categoryRepository.GetAllAsync();

            var productsByCategory = categories
                .Where(c => products.Any(p => p.CategoryId == c.Id))
                .ToDictionary(
                    c => c,
                    c => products.Where(p => p.CategoryId == c.Id).ToList()
                );

            ViewBag.ProductsByCategory = productsByCategory;
            return View("ProductsByCategory");
        }

        [HttpGet("products/{id}")]
        public async Task<IActionResult> ProductDetails(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            return product == null ? NotFound() : View(product);
        }

        [HttpGet("products/create")]
        public async Task<IActionResult> CreateProduct()
        {
            ViewBag.Categories = await _categoryRepository.GetAllAsync();
            return View();
        }

        [HttpPost("products/create"), ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product, IFormFile imageFile)
        {
            var allCategories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = allCategories;

            if (!ModelState.IsValid)
                return HandleValidationFailure(product);

            try
            {
                await ProcessProductImageAsync(product, imageFile, null);
                product.Slug = await _productRepository.GenerateUniqueSlugAsync(product.Name);
                product.CreatedAt = product.UpdatedAt = DateTime.UtcNow;
                product.IsAvailable = product.StockQuantity > 0;

                await _productRepository.AddAsync(product);
                return HandleSuccess("Товар успешно создан", "Products");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании товара");
                return HandleError(ex, product);
            }
        }

        [HttpGet("products/edit/{id}")]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();

            if (product.Tags?.Count > 0)
            {
                product.TagsString = string.Join(", ", product.Tags);
            }

            ViewBag.Categories = await _categoryRepository.GetAllAsync();
            return View(product);
        }

        [HttpPost("products/edit/{id}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, IFormFile imageFile)
        {
            var existingProduct = await _productRepository.GetByIdAsync(id);
            if (existingProduct == null) return NotFound();

            ViewBag.Categories = await _categoryRepository.GetAllAsync();

            try
            {
                var form = Request.Form;
                if (!TryParseProductFormData(form, out var errors, out var parsedData))
                {
                    foreach (var error in errors)
                        ModelState.AddModelError(error.Key, error.Value);
                    return View(existingProduct);
                }

                await ProcessProductImageAsync(existingProduct, imageFile, existingProduct);
                UpdateProductFromFormData(existingProduct, parsedData, form);

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

        [HttpPost("products/simple-edit/{id}"), ValidateAntiForgeryToken]
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

        [HttpPost("products/delete/{id}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            if (!EnsureAdminRole("У вас недостаточно прав для удаления товаров."))
                return RedirectToAction("Products");

            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();

            try
            {
                await DeleteProductImageAsync(product);
                await _productRepository.DeleteAsync(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении товара ID={Id}", id);
                TempData["Error"] = "Не удалось удалить товар.";
            }

            return RedirectToAction("Products", new { success = "Товар успешно удалён" });
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

        [HttpPost("categories/create"), ValidateAntiForgeryToken]
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
                await ProcessCategoryImageAsync(category, imageFile, null);
                category.Slug ??= GenerateSlug(category.Name);
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

        [HttpPost("categories/edit/{id}"), ValidateAntiForgeryToken]
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

                await ProcessCategoryImageAsync(category, imageFile, existing);
                category.Slug ??= GenerateSlug(category.Name);

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

        [HttpPost("categories/delete/{id}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!EnsureAdminRole("У вас недостаточно прав для удаления категорий."))
                return RedirectToAction("Categories");

            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null) return NotFound();

            try
            {
                if (await _categoryRepository.HasProductsAsync(id))
                {
                    TempData["Error"] = "Нельзя удалить категорию, в которой есть товары";
                    return RedirectToAction("Categories");
                }

                if (category.SubCategories?.Count > 0)
                {
                }
                else
                {
                    TempData["Error"] = "Нельзя удалить категорию с подкатегориями";
                    return RedirectToAction("Categories");
                }

                await DeleteCategoryImageAsync(category);
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
            return order == null ? NotFound() : View(order);
        }

        [HttpPost("orders/delete/{id}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            if (!EnsureAdminRole("У вас недостаточно прав для удаления заказов."))
                return RedirectToAction("Orders");

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

        [HttpPost("orders/cancel/{id}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id, string reason = "Отменено администратором")
        {
            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                TempData["Error"] = "Заказ не найден.";
                return RedirectToAction("Orders");
            }

            try
            {
                await _orderService.CancelOrderAsync(id, reason);
                TempData["Success"] = $"Заказ #{order.OrderNumber} успешно отменён.";
            }
            catch (OrderProcessingException ex)
            {
                _logger.LogWarning(ex, "Невозможно отменить заказ {OrderNumber}", order.OrderNumber);
                TempData["Error"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене заказа {OrderId}", id);
                TempData["Error"] = "Не удалось отменить заказ.";
            }

            return RedirectToAction("OrderDetails", new { id });
        }

        // ========== ЦЕНООБРАЗОВАНИЕ ==========
        [HttpGet("pricing-rules"), Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> PricingRules()
        {
            var rules = await _context.PricingRules
                .Include(r => r.Category)
                .Include(r => r.Product)
                .OrderBy(r => r.Priority)
                .ToListAsync();
            return View(rules);
        }

        [HttpGet("pricing-rules/create"), Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> CreatePricingRule()
        {
            await PopulatePricingRuleSelectListsAsync();
            return View(new PricingRule());
        }

        [HttpPost("pricing-rules/create"), ValidateAntiForgeryToken, Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> CreatePricingRule(PricingRule rule)
        {
            if (!ModelState.IsValid)
            {
                await PopulatePricingRuleSelectListsAsync();
                return View(rule);
            }

            rule.CreatedAt = rule.UpdatedAt = DateTime.UtcNow;
            _context.PricingRules.Add(rule);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Правило успешно создано";
            return RedirectToAction(nameof(PricingRules));
        }

        [HttpGet("pricing-rules/edit/{id}"), Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> EditPricingRule(int id)
        {
            var rule = await _context.PricingRules.FindAsync(id);
            if (rule == null) return NotFound();

            await PopulatePricingRuleSelectListsAsync();
            return View(rule);
        }

        [HttpPost("pricing-rules/edit/{id}"), ValidateAntiForgeryToken, Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> EditPricingRule(int id, PricingRule rule)
        {
            if (id != rule.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                await PopulatePricingRuleSelectListsAsync();
                return View(rule);
            }

            rule.UpdatedAt = DateTime.UtcNow;
            _context.Update(rule);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Правило успешно обновлено";
            return RedirectToAction(nameof(PricingRules));
        }

        [HttpPost("pricing-rules/delete/{id}"), ValidateAntiForgeryToken, Authorize(Roles = AppConstants.AdminRole)]
        public async Task<IActionResult> DeletePricingRule(int id)
        {
            var rule = await _context.PricingRules.FindAsync(id);
            if (rule != null)
            {
                _context.PricingRules.Remove(rule);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Правило удалено";
            }
            return RedirectToAction(nameof(PricingRules));
        }

        // ========== ПОЛЬЗОВАТЕЛИ ==========
        [HttpGet("users")]
        public async Task<IActionResult> Users()
        {
            if (!EnsureAdminRole("У вас нет доступа к управлению пользователями."))
                return RedirectToAction("Dashboard");

            var users = await _userManager.Users.OrderBy(u => u.FirstName).ToListAsync();
            return View(users);
        }

        [HttpGet("users/edit/{id}")]
        public async Task<IActionResult> EditUser(string id)
        {
            if (!EnsureAdminRole("У вас нет доступа к управлению пользователями."))
                return RedirectToAction("Dashboard");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            ViewBag.Roles = await _roleManager.Roles.ToListAsync();
            ViewBag.UserRoles = await _userManager.GetRolesAsync(user);
            return View(user);
        }

        [HttpPost("users/edit/{id}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string id, string selectedRole)
        {
            if (!EnsureAdminRole()) return Forbid();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (!string.IsNullOrEmpty(selectedRole))
                await _userManager.AddToRoleAsync(user, selectedRole);

            TempData["Success"] = "Роли пользователя обновлены";
            return RedirectToAction("Users");
        }

        [HttpPost("users/delete/{id}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (!EnsureAdminRole()) return Forbid();

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

        // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========
        private CategoryImportModel MapCategoryToImportModel(Category c) => new()
        {
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description ?? "",
            Parent = c.ParentCategory?.Name ?? "",
            DisplayOrder = c.DisplayOrder,
            IsActive = BoolToYesNo(c.IsActive),
            ShowInMenu = BoolToYesNo(c.ShowInMenu),
            ImageUrl = c.ImageUrl ?? AppConstants.DefaultCategoryImage
        };

        private ProductImportModel MapProductToImportModel(Product p) => new()
        {
            Name = p.Name,
            Slug = p.Slug,
            Description = p.Description,
            ShortDescription = p.ShortDescription,
            Price = p.Price,
            //OldPrice = p.OldPrice?.ToString("F2"),
            ImageUrl = p.ImageUrl ?? AppConstants.DefaultProductImage,
            Brand = p.Brand,
            StockQuantity = p.StockQuantity,
            Category = p.Category?.Name ?? "",
            IsAvailable = BoolToYesNo(p.IsAvailable),
            IsFeatured = BoolToYesNo(p.IsFeatured),
            IsNew = BoolToYesNo(p.IsNew),
            Tags = string.Join(", ", p.Tags),
            Specifications = string.Join("; ", p.Specifications.Select(kv => $"{kv.Key}={kv.Value}"))
        };

        private async Task<List<UserOrderExportModel>> MapOrdersToExportModelsAsync(IEnumerable<Order> orders)
        {
            var records = new List<UserOrderExportModel>();
            foreach (var order in orders)
            {
                var user = !string.IsNullOrEmpty(order.UserId)
                    ? await _userManager.FindByIdAsync(order.UserId)
                    : null;

                var roles = user != null ? await _userManager.GetRolesAsync(user) : [];
                var role = roles.FirstOrDefault() ?? "Customer";

                var itemsString = order.OrderItems?.Any() == true
                    ? string.Join("; ", order.OrderItems.Select(oi =>
                        $"{(oi.ProductName ?? "Без названия").Replace(";", ",").Replace("=", ":")}={oi.UnitPrice:F2}={oi.Quantity}"))
                    : "Нет товаров";

                records.Add(new UserOrderExportModel
                {
                    Email = order.CustomerEmail,
                    FirstName = user?.FirstName ?? order.CustomerName.Split(' ').FirstOrDefault() ?? "",
                    LastName = user?.LastName ?? order.CustomerName.Split(' ').LastOrDefault() ?? "",
                    Phone = order.CustomerPhone,
                    Role = role,
                    OrderNumber = order.OrderNumber,
                    OrderDate = order.OrderDate,
                    Status = order.Status.ToString(),
                    PaymentMethod = order.PaymentMethod.ToString(),
                    Total = order.Total,
                    ShippingAddress = order.ShippingAddress,
                    City = order.ShippingCity ?? "",
                    Items = itemsString
                });
            }
            return records;
        }

        private async Task ImportCategoriesFromCsv(Stream stream)
        {
            var records = ReadCsvRecords<CategoryImportModel>(stream);
            var allCategories = (await _categoryRepository.GetAllAsync()).ToList();
            var existingSlugs = allCategories.Select(c => c.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var rec in records)
            {
                if (string.IsNullOrWhiteSpace(rec.Name)) continue;

                var baseSlug = string.IsNullOrWhiteSpace(rec.Slug) ? GenerateSlug(rec.Name) : rec.Slug.Trim();
                if (existingSlugs.Contains(baseSlug))
                {
                    _logger.LogWarning("Пропущена категория с дублирующимся Slug: {Name} ({Slug})", rec.Name, baseSlug);
                    continue;
                }

                var parent = string.IsNullOrWhiteSpace(rec.Parent)
                    ? null
                    : allCategories.FirstOrDefault(c => c.Name.Equals(rec.Parent.Trim(), StringComparison.OrdinalIgnoreCase));

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
                        : rec.ImageUrl.Trim()
                };

                await _categoryRepository.AddAsync(category);
                allCategories.Add(category);
                existingSlugs.Add(baseSlug);
            }
        }

        private async Task ImportProductsFromCsv(Stream stream)
        {
            var records = ReadCsvRecords<ProductImportModel>(stream);
            var allCategories = await _categoryRepository.GetAllAsync();
            var existingSlugs = (await _productRepository.GetAllAsync())
                .Select(p => p.Slug)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var rec in records)
            {
                if (string.IsNullOrWhiteSpace(rec.Name)) continue;

                var baseSlug = string.IsNullOrWhiteSpace(rec.Slug) ? GenerateSlug(rec.Name) : rec.Slug.Trim();
                if (existingSlugs.Contains(baseSlug))
                {
                    _logger.LogWarning("Пропущен товар с дублирующимся Slug: {Name} ({Slug})", rec.Name, baseSlug);
                    continue;
                }

                var category = string.IsNullOrWhiteSpace(rec.Category)
                    ? null
                    : allCategories.FirstOrDefault(c => c.Name.Equals(rec.Category.Trim(), StringComparison.OrdinalIgnoreCase));

                decimal price = rec.Price;
                //decimal? oldPrice = null;
                //if (!string.IsNullOrEmpty(rec.OldPrice) &&
                //    decimal.TryParse(rec.OldPrice.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var op))
                //{
                //    oldPrice = op;
                //}

                var product = new Product
                {
                    Name = rec.Name.Trim(),
                    Slug = baseSlug,
                    Description = rec.Description?.Trim() ?? string.Empty,
                    ShortDescription = rec.ShortDescription?.Trim() ?? string.Empty,
                    Price = price,
                    //OldPrice = oldPrice,
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
                    UpdatedAt = DateTime.UtcNow,
                    Tags = string.IsNullOrWhiteSpace(rec.Tags)
                        ? []
                        : [.. rec.Tags
                            .Split([','], StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .Where(t => !string.IsNullOrEmpty(t))],

                    Specifications = ParseSpecifications(rec.Specifications)
                };

                await _productRepository.AddAsync(product);
                existingSlugs.Add(baseSlug);
            }
        }

        private async Task ImportUsersAndOrdersFromCsv(Stream stream)
        {
            var records = ReadCsvRecords<UserOrderExportModel>(stream);

            foreach (var rec in records)
            {
                var user = await _userManager.FindByEmailAsync(rec.Email);
                if (user == null)
                {
                    user = await CreateUserFromExportAsync(rec);
                    if (user == null) continue;
                }

                if (await _orderRepository.GetByOrderNumberAsync(rec.OrderNumber) != null)
                    continue;

                var order = CreateOrderFromExport(rec, user);
                ParseOrderItems(rec.Items, order);
                await _orderRepository.AddAsync(order);
            }
        }

        private async Task<ApplicationUser?> CreateUserFromExportAsync(UserOrderExportModel rec)
        {
            var user = new ApplicationUser
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
            if (!result.Succeeded) return null;

            var roleName = rec.Role switch
            {
                "Admin" => AppConstants.AdminRole,
                "Manager" => AppConstants.ManagerRole,
                _ => AppConstants.CustomerRole
            };

            await _userManager.AddToRoleAsync(user, roleName);
            return user;
        }

        private Order CreateOrderFromExport(UserOrderExportModel rec, ApplicationUser user) => new()
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
            //DiscountAmount = 0m,
            PaymentStatus = PaymentStatus.Paid
        };

        private void ParseOrderItems(string itemsString, Order order)
        {
            if (string.IsNullOrEmpty(itemsString)) return;

            foreach (var itemStr in itemsString.Split([';'], StringSplitOptions.RemoveEmptyEntries))
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

        private List<T> ReadCsvRecords<T>(Stream stream) where T : class
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                PrepareHeaderForMatch = args => args.Header.Trim(),
                MissingFieldFound = null
            });
            return [.. csv.GetRecords<T>()];
        }

        private Dictionary<string, string> ParseSpecifications(string specifications)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(specifications)) return dict;

            foreach (var pair in specifications.Split([';'], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split(['='], 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    if (!string.IsNullOrEmpty(key))
                        dict[key] = value;
                }
            }
            return dict;
        }

        private async Task ProcessProductImageAsync(Product product, IFormFile imageFile, Product? existingProduct)
        {
            product.ImageUrl = AppConstants.DefaultProductImage;

            if (imageFile?.Length > 0)
            {
                try
                {
                    if (existingProduct != null && !string.IsNullOrEmpty(existingProduct.ImageUrl) &&
                        !existingProduct.ImageUrl.Contains("default"))
                    {
                        await _fileStorageService.DeleteFileAsync(existingProduct.ImageUrl);
                    }

                    var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                    var filePath = await _fileStorageService.SaveProductImageAsync(imageFile.OpenReadStream(), fileName);
                    product.ImageUrl = filePath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при загрузке изображения");
                }
            }
        }

        private async Task ProcessCategoryImageAsync(Category category, IFormFile imageFile, Category? existingCategory)
        {
            category.ImageUrl = AppConstants.DefaultCategoryImage;

            if (imageFile?.Length > 0)
            {
                try
                {
                    if (existingCategory != null && !string.IsNullOrEmpty(existingCategory.ImageUrl) &&
                        !existingCategory.ImageUrl.Contains("default"))
                    {
                        await _fileStorageService.DeleteFileAsync(existingCategory.ImageUrl);
                    }

                    var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                    var filePath = await _fileStorageService.SaveCategoryImageAsync(imageFile.OpenReadStream(), fileName);
                    category.ImageUrl = filePath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при загрузке изображения категории");
                }
            }
        }

        private async Task DeleteProductImageAsync(Product product)
        {
            if (!string.IsNullOrEmpty(product.ImageUrl) && !product.ImageUrl.Contains("default"))
                await _fileStorageService.DeleteFileAsync(product.ImageUrl);
        }

        private async Task DeleteCategoryImageAsync(Category category)
        {
            if (!string.IsNullOrEmpty(category.ImageUrl) && !category.ImageUrl.Contains("default"))
                await _fileStorageService.DeleteFileAsync(category.ImageUrl);
        }

        private bool TryParseProductFormData(
     IFormCollection form,
     out Dictionary<string, string> errors,
     out (string name, string description, string shortDescription, string slug, string brand,
         decimal price, decimal? oldPrice, int stockQuantity, int minStockThreshold, int categoryId,
         string tagsString) parsedData)
        {
            errors = [];
            parsedData = default;

            var name = form["Name"].ToString().Trim();
            var description = form["Description"].ToString().Trim();
            var shortDescription = form["ShortDescription"].ToString().Trim();
            var slug = form["Slug"].ToString().Trim();
            var brand = form["Brand"].ToString().Trim();
            var tagsString = form["TagsString"].ToString().Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
            {
                errors[""] = "Название и описание обязательны"; // ✅ Исправлено
                return false;
            }

            if (!decimal.TryParse(form["Price"], out var price) || price <= 0)
            {
                errors["Price"] = "Цена обязательна и должна быть больше 0";
                return false;
            }

            decimal? oldPrice = decimal.TryParse(form["OldPrice"], out var op) ? op : null;

            if (!int.TryParse(form["StockQuantity"], out var stockQuantity) || stockQuantity < 0)
            {
                errors["StockQuantity"] = "Количество не может быть отрицательным";
                return false;
            }

            var minStockThreshold = int.TryParse(form["MinStockThreshold"], out var mst) ? mst : 5;

            if (!int.TryParse(form["CategoryId"], out var categoryId) || categoryId <= 0)
            {
                errors["CategoryId"] = "Выберите категорию";
                return false;
            }

            parsedData = (name, description, shortDescription, slug, brand, price, oldPrice,
                stockQuantity, minStockThreshold, categoryId, tagsString);
            return true;
        }

        private void UpdateProductFromFormData(
            Product product,
            (string name, string description, string shortDescription, string slug, string brand,
                decimal price, decimal? oldPrice, int stockQuantity, int minStockThreshold, int categoryId,
                string tagsString) data,
            IFormCollection form)
        {
            product.Name = data.name;
            product.Slug = string.IsNullOrEmpty(data.slug) ? GenerateSlug(data.name) : data.slug;
            product.Description = data.description;
            product.ShortDescription = data.shortDescription;
            product.Price = data.price;
            //product.OldPrice = data.oldPrice;
            product.Brand = data.brand;
            product.StockQuantity = data.stockQuantity;
            product.MinStockThreshold = data.minStockThreshold;
            product.CategoryId = data.categoryId;
            product.IsFeatured = form.ContainsKey("IsFeatured");
            product.IsNew = form.ContainsKey("IsNew");
            product.IsAvailable = form.ContainsKey("IsAvailable");
            product.UpdatedAt = DateTime.UtcNow;

            product.Tags = string.IsNullOrEmpty(data.tagsString)
                ? []
                : [.. data.tagsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))];
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

        private static string BoolToYesNo(bool value) => value ? "Да" : "Нет";

        private static bool ParseBool(string value)
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
            slug = NonAlphanumericRegex().Replace(slug, ""); // 
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            slug = slug.Trim('-');
            return string.IsNullOrEmpty(slug) ? $"product-{DateTime.Now:yyyyMMddHHmmss}" : slug;
        }

        private IActionResult HandleValidationFailure(object model)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
                return Json(new { success = false, errors });

            return View(model);
        }

        private IActionResult HandleSuccess(string message, string actionName)
        {
            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
                return Json(new { success = true, message });

            return RedirectToAction(actionName, new { success = message });
        }

        private IActionResult HandleError(Exception ex, object model)
        {
            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
                return Json(new { success = false, errors = data });

            ModelState.AddModelError("", $"Ошибка: {ex.Message}");
            return View(model);
        }

        private bool EnsureAdminRole(string? errorMessage = null)
        {
            if (!User.IsInRole(AppConstants.AdminRole))
            {
                if (errorMessage != null)
                    TempData["Error"] = errorMessage;
                return false;
            }
            return true;
        }

        private async Task PopulatePricingRuleSelectListsAsync()
        {
            ViewBag.Categories = new SelectList(await _categoryRepository.GetAllAsync(), "Id", "Name");
            ViewBag.Products = new SelectList(await _productRepository.GetAllAsync(), "Id", "Name");
        }
    }
}