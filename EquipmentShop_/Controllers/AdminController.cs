using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.ViewModels.Admin;


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
            UserManager<ApplicationUser> userManager,      // ← новое
            RoleManager<IdentityRole> roleManager)         // ← новое
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _orderRepository = orderRepository;
            _fileStorageService = fileStorageService;
            _logger = logger;
            _userManager = userManager;                    // ← новое
            _roleManager = roleManager;                    // ← новое
        }


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

        private IEnumerable<OrderStatus> GetAllowedTransitions(OrderStatus current)
        {
            return AllowedTransitions.GetValueOrDefault(current, Array.Empty<OrderStatus>());
        }

        private string GetStatusDisplayName(OrderStatus status)
        {
            var field = typeof(OrderStatus).GetField(status.ToString());
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute?.Name ?? status.ToString();
        }


        [HttpGet("")]
        public async Task<IActionResult> Dashboard()
        {
            var totalProducts = await _productRepository.CountAsync();
            var totalOrders = await _orderRepository.CountAsync();
            var totalCategories = await _categoryRepository.CountAsync();
            var totalUsers = await _userManager.Users.CountAsync(); // ← НОВАЯ СТРОКА

            ViewBag.TotalProducts = totalProducts;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalCategories = totalCategories;
            ViewBag.TotalUsers = totalUsers; // ← НОВАЯ СТРОКА

            return View();
        }

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
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }






        [HttpGet("orders/{orderNumber}/change-status")]
        public async Task<IActionResult> ChangeOrderStatus(string orderNumber)
        {
            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);
            if (order == null) return NotFound();

            var current = order.Status;
            var allowed = GetAllowedTransitions(current);

            ViewBag.OrderNumber = orderNumber;
            ViewBag.CurrentStatusName = GetStatusDisplayName(current);
            ViewBag.StatusOptions = allowed.Select(s => new SelectListItem
            {
                Value = ((int)s).ToString(),
                Text = GetStatusDisplayName(s)
            }).ToList();

            return View(new ChangeOrderStatusViewModel { OrderNumber = orderNumber });
        }

        [HttpPost("orders/{orderNumber}/change-status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeOrderStatus(string orderNumber, ChangeOrderStatusViewModel model)
        {
            if (!ModelState.IsValid || model.OrderNumber != orderNumber)
                return RedirectToAction("ChangeOrderStatus", new { orderNumber });

            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);
            if (order == null)
            {
                TempData["Error"] = "Заказ не найден.";
                return RedirectToAction("Orders");
            }

            var newStatus = (OrderStatus)model.NewStatusId;
            var allowed = GetAllowedTransitions(order.Status);

            if (!allowed.Contains(newStatus))
            {
                TempData["Error"] = "Недопустимый переход статуса.";
                return RedirectToAction("ChangeOrderStatus", new { orderNumber });
            }

            var success = await _orderRepository.UpdateOrderStatusAsync(orderNumber, newStatus);
            if (success)
            {
                TempData["Success"] = $"Статус изменён на «{GetStatusDisplayName(newStatus)}»";
            }
            else
            {
                TempData["Error"] = "Не удалось обновить статус.";
            }

            return RedirectToAction("OrderDetails", new { id = order.Id });
        }


















        [HttpGet("products/create")]
        public async Task<IActionResult> CreateProduct()
        {
            var categoriesForCreate = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = categoriesForCreate;
            return View();
        }


        [HttpPost("products/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product, IFormFile imageFile)
        {
            // Получаем категории для представления
            var allCategories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = allCategories;

            // Проверяем только обязательные поля вручную
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(product.Name))
                errors.Add("Название товара обязательно");

            if (string.IsNullOrWhiteSpace(product.Description))
                errors.Add("Описание товара обязательно");

            if (product.Price <= 0)
                errors.Add("Цена должна быть больше 0");

            if (product.StockQuantity < 0)
                errors.Add("Количество не может быть отрицательным");

            if (product.CategoryId <= 0)
                errors.Add("Категория обязательна");

            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError("", error);
                }
                return View(product);
            }

            try
            {
                // Обработка изображения
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                        var filePath = await _fileStorageService.SaveProductImageAsync(
                            imageFile.OpenReadStream(),
                            fileName);

                        product.ImageUrl = filePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при загрузке изображения");
                        product.ImageUrl = AppConstants.DefaultProductImage;
                    }
                }
                else
                {
                    product.ImageUrl = AppConstants.DefaultProductImage;
                }

                // Устанавливаем значения по умолчанию
                if (string.IsNullOrEmpty(product.Slug))
                {
                    product.Slug = GenerateSlug(product.Name);
                }

                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
                product.IsAvailable = product.StockQuantity > 0;

                // Устанавливаем дефолтные мета-данные
                if (string.IsNullOrEmpty(product.MetaTitle))
                    product.MetaTitle = product.Name;

                if (string.IsNullOrEmpty(product.MetaDescription))
                    product.MetaDescription = product.Description.Length > 160
                        ? product.Description.Substring(0, 160) + "..."
                        : product.Description;

                // Сохраняем товар
                await _productRepository.AddAsync(product);

                TempData["Success"] = "Товар успешно создан";
                return RedirectToAction("Products");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании товара");
                ModelState.AddModelError("", $"Ошибка при создании товара: {ex.Message}");
                return View(product);
            }
        }









        [HttpGet("products/edit/{id}")]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Преобразуем теги и характеристики в строки для отображения в форме (если нужно)
            if (product.Tags != null && product.Tags.Any())
            {
                product.TagsString = string.Join(", ", product.Tags);
            }

            ViewBag.Categories = await _categoryRepository.GetAllAsync();
            return View(product);
        }


        [HttpPost("products/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, IFormFile imageFile)
        {
            var existingProduct = await _productRepository.GetByIdAsync(id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            ViewBag.Categories = await _categoryRepository.GetAllAsync();

            // Получаем данные из формы
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

            // Попытка парсинга числовых значений
            if (!decimal.TryParse(form["Price"], out var price) || price <= 0)
            {
                ModelState.AddModelError("Price", "Цена обязательна и должна быть больше 0");
                return View(existingProduct);
            }

            decimal? oldPrice = null;
            if (decimal.TryParse(Request.Form["OldPrice"], out var parsedOldPrice))
            {
                oldPrice = parsedOldPrice;
            }

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

            // Чекбоксы
            var isFeatured = form.ContainsKey("IsFeatured");
            var isNew = form.ContainsKey("IsNew");
            var isAvailable = form.ContainsKey("IsAvailable");

            // Валидация
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("Name", "Название обязательно");
                return View(existingProduct);
            }
            if (string.IsNullOrWhiteSpace(description))
            {
                ModelState.AddModelError("Description", "Описание обязательно");
                return View(existingProduct);
            }

            try
            {
                // Обработка изображения
                if (imageFile?.Length > 0)
                {
                    if (!string.IsNullOrEmpty(existingProduct.ImageUrl) &&
                        !existingProduct.ImageUrl.Contains("default"))
                    {
                        await _fileStorageService.DeleteFileAsync(existingProduct.ImageUrl);
                    }
                    var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                    var filePath = await _fileStorageService.SaveProductImageAsync(imageFile.OpenReadStream(), fileName);
                    existingProduct.ImageUrl = filePath;
                }

                // Обновляем только нужные поля
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

                // Теги
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
                ModelState.AddModelError("", "Произошла ошибка при сохранении товара");
                return View(existingProduct);
            }
        }





        [HttpPost("products/simple-edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SimpleEdit(int id, string name, decimal price, int stockQuantity)
        {
            Console.WriteLine($"=== ПРОСТОЕ РЕДАКТИРОВАНИЕ ТОВАРА ID: {id} ===");
            Console.WriteLine($"Полученные данные: name='{name}', price={price}, stock={stockQuantity}");

            try
            {
                // Получаем товар
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                {
                    Console.WriteLine("Товар не найден в БД");
                    return Json(new { success = false, message = "Товар не найден" });
                }

                Console.WriteLine($"Найден товар: ID={product.Id}, Name='{product.Name}', Price={product.Price}");

                // Обновляем поля
                product.Name = name;
                product.Price = price;
                product.StockQuantity = stockQuantity;
                product.UpdatedAt = DateTime.UtcNow;
                product.IsAvailable = stockQuantity > 0;

                Console.WriteLine("Пытаемся сохранить через UpdateAsync...");

                // Сохраняем изменения
                await _productRepository.UpdateAsync(product);

                Console.WriteLine("UpdateAsync выполнен успешно!");

                // Проверяем, обновился ли товар
                var updatedProduct = await _productRepository.GetByIdAsync(id);
                Console.WriteLine($"Проверка после обновления: Name='{updatedProduct.Name}', Price={updatedProduct.Price}");

                return Json(new { success = true, message = "Товар успешно обновлен" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"InnerException: {ex.InnerException.Message}");
                }

                return Json(new { success = false, message = $"Ошибка: {ex.Message}" });
            }
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
            if (product == null)
            {
                return NotFound();
            }

            try
            {
                // Удаляем изображение если это не дефолтное
                if (!string.IsNullOrEmpty(product.ImageUrl) &&
                    !product.ImageUrl.Contains("default"))
                {
                    await _fileStorageService.DeleteFileAsync(product.ImageUrl);
                }

                await _productRepository.DeleteAsync(product);
                TempData["Success"] = "Товар успешно удален";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении товара");
                TempData["Error"] = "Ошибка при удалении товара";
            }

            return RedirectToAction("Products");
        }

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
            if (order == null)
            {
                return NotFound();
            }
            return View(order);
        }

        [HttpGet("categories")]
        public async Task<IActionResult> Categories()
        {
            var categories = await _categoryRepository.GetAllAsync();
            return View(categories);
        }

        private string GenerateSlug(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            var slug = name.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("--", "-")
                .Replace("---", "-")
                .Replace("----", "-")
                .Replace("&", "and")
                .Replace("+", "plus")
                .Replace("%", "percent")
                .Replace("$", "dollar")
                .Replace("#", "sharp")
                .Replace("@", "at")
                .Replace("!", "")
                .Replace("?", "")
                .Replace(".", "")
                .Replace(",", "")
                .Replace(":", "")
                .Replace(";", "")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("[", "")
                .Replace("]", "")
                .Replace("{", "")
                .Replace("}", "")
                .Replace("<", "")
                .Replace(">", "")
                .Replace("=", "")
                .Replace("~", "")
                .Replace("`", "")
                .Replace("^", "")
                .Replace("*", "");

            // Удаляем все не-ASCII символы
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^\u0000-\u007F]+", string.Empty);

            // Удаляем двойные дефисы
            while (slug.Contains("--"))
                slug = slug.Replace("--", "-");

            // Удаляем дефисы в начале и конце
            slug = slug.Trim('-');

            // Если после всех преобразований slug пустой, генерируем на основе даты
            if (string.IsNullOrEmpty(slug))
                slug = $"product-{DateTime.Now:yyyyMMddHHmmss}";

            return slug;
        }

        // Методы для работы с категориями
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
                // Обработка изображения
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                        var filePath = await _fileStorageService.SaveCategoryImageAsync(
                            imageFile.OpenReadStream(),
                            fileName);

                        category.ImageUrl = filePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при загрузке изображения");
                        category.ImageUrl = AppConstants.DefaultCategoryImage;
                    }
                }
                else if (string.IsNullOrEmpty(category.ImageUrl))
                {
                    category.ImageUrl = AppConstants.DefaultCategoryImage;
                }

                // Генерируем slug если пустой
                if (string.IsNullOrEmpty(category.Slug))
                {
                    category.Slug = category.Name.ToLower()
                        .Replace(" ", "-")
                        .Replace(".", "")
                        .Replace(",", "");
                }

                // Устанавливаем значения по умолчанию
                if (string.IsNullOrEmpty(category.MetaTitle))
                    category.MetaTitle = category.Name;

                if (string.IsNullOrEmpty(category.MetaDescription))
                    category.MetaDescription = category.Description ?? category.Name;

                category.IsActive = true;

                await _categoryRepository.AddAsync(category);

                TempData["Success"] = "Категория успешно создана";
                return RedirectToAction("Categories");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании категории");
                ModelState.AddModelError("", $"Ошибка при создании категории: {ex.Message}");
                return View(category);
            }
        }

        [HttpGet("categories/edit/{id}")]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            var allCategories = await _categoryRepository.GetAllAsync();
            ViewBag.ParentCategories = allCategories.Where(c => c.IsActive && c.Id != id).ToList();

            return View(category);
        }

        [HttpPost("categories/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int id, Category category, IFormFile imageFile)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            var allCategories = await _categoryRepository.GetAllAsync();
            ViewBag.ParentCategories = allCategories.Where(c => c.IsActive && c.Id != id).ToList();

            if (string.IsNullOrWhiteSpace(category.Name))
            {
                ModelState.AddModelError("Name", "Название категории обязательно");
                return View(category);
            }

            try
            {
                var existingCategory = await _categoryRepository.GetByIdAsync(id);
                if (existingCategory == null)
                {
                    return NotFound();
                }

                // Обработка изображения
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        // Удаляем старое изображение если это не дефолтное
                        if (!string.IsNullOrEmpty(existingCategory.ImageUrl) &&
                            !existingCategory.ImageUrl.Contains("default"))
                        {
                            await _fileStorageService.DeleteFileAsync(existingCategory.ImageUrl);
                        }

                        // Сохраняем новое изображение
                        var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                        var filePath = await _fileStorageService.SaveCategoryImageAsync(
                            imageFile.OpenReadStream(),
                            fileName);

                        category.ImageUrl = filePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при загрузке изображения");
                        category.ImageUrl = existingCategory.ImageUrl;
                    }
                }
                else
                {
                    category.ImageUrl = existingCategory.ImageUrl;
                }

                // Генерируем slug если пустой
                if (string.IsNullOrEmpty(category.Slug))
                {
                    category.Slug = category.Name.ToLower()
                        .Replace(" ", "-")
                        .Replace(".", "")
                        .Replace(",", "");
                }

                await _categoryRepository.UpdateAsync(category);

                TempData["Success"] = "Категория успешно обновлена";
                return RedirectToAction("Categories");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении категории");
                ModelState.AddModelError("", $"Ошибка при обновлении категории: {ex.Message}");
                return View(category);
            }
        }

        [HttpPost("categories/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!User.IsInRole(AppConstants.AdminRole))
            {
                TempData["Error"] = "У вас недостаточно прав для удаления категории.";
                return RedirectToAction("Categories");
            }
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            try
            {
                // Проверяем, есть ли товары в категории
                var hasProducts = await _categoryRepository.HasProductsAsync(id);
                if (hasProducts)
                {
                    TempData["Error"] = "Нельзя удалить категорию, в которой есть товары";
                    return RedirectToAction("Categories");
                }

                // Проверяем, есть ли подкатегории
                var hasSubCategories = category.SubCategories?.Any() ?? false;
                if (hasSubCategories)
                {
                    TempData["Error"] = "Нельзя удалить категорию, у которой есть подкатегории";
                    return RedirectToAction("Categories");
                }

                // Удаляем изображение если это не дефолтное
                if (!string.IsNullOrEmpty(category.ImageUrl) &&
                    !category.ImageUrl.Contains("default"))
                {
                    await _fileStorageService.DeleteFileAsync(category.ImageUrl);
                }

                await _categoryRepository.DeleteAsync(category);
                TempData["Success"] = "Категория успешно удалена";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении категории");
                TempData["Error"] = "Ошибка при удалении категории";
            }

            return RedirectToAction("Categories");
        }



        [HttpGet("users")]
        public async Task<IActionResult> Users()
        {
            if (!User.IsInRole(AppConstants.AdminRole))
            {
                TempData["Error"] = "У вас недостаточно прав для удаления пользователей.";
                return RedirectToAction("users");
            }
            var users = await _userManager.Users
                .OrderBy(u => u.FirstName)
                .ToListAsync(); 

            return View(users);
        }

        [HttpGet("users/edit/{id}")]
        public async Task<IActionResult> EditUser(string id)
        {
            if (!User.IsInRole(AppConstants.AdminRole))
            {
                TempData["Error"] = "У вас недостаточно прав для изменения пользователей.";
                return RedirectToAction("user");
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _roleManager.Roles.ToListAsync();
            ViewBag.Roles = roles;

            var userRoles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRoles = userRoles;

            return View(user);
        }

        [HttpPost("users/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string id, string selectedRole)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (!string.IsNullOrEmpty(selectedRole))
            {
                await _userManager.AddToRoleAsync(user, selectedRole);
            }

            TempData["Success"] = "Роли пользователя обновлены";
            return RedirectToAction("Users");
        }

        [HttpPost("users/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Нельзя удалить самого себя
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