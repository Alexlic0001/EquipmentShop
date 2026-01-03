using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Constants;
using Microsoft.AspNetCore.Http;

namespace EquipmentShop.Controllers
{
    [Authorize(Roles = AppConstants.AdminRole)]
    [Route("admin")]
    public class AdminController : Controller
    {
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
            ILogger<AdminController> logger)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _orderRepository = orderRepository;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Dashboard()
        {
            var totalProducts = await _productRepository.CountAsync();
            var totalOrders = await _orderRepository.CountAsync();
            var totalCategories = await _categoryRepository.CountAsync();

            ViewBag.TotalProducts = totalProducts;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalCategories = totalCategories;

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

            // Преобразуем теги в строку для отображения в форме
            if (product.Tags != null && product.Tags.Any())
            {
                product.TagsString = string.Join(", ", product.Tags);
            }

            var categoriesForEditView = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = categoriesForEditView;
            return View(product);
        }

        [HttpPost("products/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, Product product, IFormFile imageFile)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            // Получаем категории один раз в начале
            var categoriesForEdit = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = categoriesForEdit;

            if (ModelState.IsValid)
            {
                var existingProduct = await _productRepository.GetByIdAsync(id);
                if (existingProduct == null)
                {
                    return NotFound();
                }

                // Обновляем изображение если загружено новое
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        // Удаляем старое изображение если это не дефолтное
                        if (!string.IsNullOrEmpty(existingProduct.ImageUrl) &&
                            !existingProduct.ImageUrl.Contains("default"))
                        {
                            await _fileStorageService.DeleteFileAsync(existingProduct.ImageUrl);
                        }

                        // Сохраняем новое изображение
                        var fileName = await _fileStorageService.GenerateUniqueFileName(imageFile.FileName);
                        var filePath = await _fileStorageService.SaveProductImageAsync(
                            imageFile.OpenReadStream(),
                            fileName);

                        product.ImageUrl = filePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при загрузке изображения");
                        ModelState.AddModelError("", "Ошибка при загрузке изображения");
                        return View(product);
                    }
                }
                else
                {
                    // Сохраняем старое изображение
                    product.ImageUrl = existingProduct.ImageUrl;
                }

                // Убедимся, что Slug не пустой
                if (string.IsNullOrEmpty(product.Slug))
                {
                    product.Slug = GenerateSlug(product.Name);
                }

                // Сохраняем даты и статистику
                product.CreatedAt = existingProduct.CreatedAt;
                product.UpdatedAt = DateTime.UtcNow;
                product.IsAvailable = product.StockQuantity > 0;
                product.Rating = existingProduct.Rating;
                product.ReviewsCount = existingProduct.ReviewsCount;
                product.SoldCount = existingProduct.SoldCount;

                // Обработка тегов
                if (!string.IsNullOrEmpty(product.TagsString))
                {
                    product.Tags = product.TagsString
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                }
                else
                {
                    product.Tags = existingProduct.Tags;
                }

                try
                {
                    await _productRepository.UpdateAsync(product);
                    TempData["Success"] = "Товар успешно обновлен";
                    return RedirectToAction("ProductDetails", new { id = product.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обновлении товара");
                    ModelState.AddModelError("", "Ошибка при обновлении товара");
                }
            }

            return View(product);
        }

        [HttpPost("products/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
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

    }
}