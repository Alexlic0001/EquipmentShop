using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // ========== ЗАКАЗЫ (только просмотр) ==========

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

        // ========== ПОЛЬЗОВАТЕЛИ (только для админа) ==========

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
    }
}