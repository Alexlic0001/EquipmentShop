// Infrastructure/Repositories/ProductRepository.cs
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace EquipmentShop.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductRepository> _logger;

        public ProductRepository(AppDbContext context, ILogger<ProductRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        // --- Реализация IProductRepository ---

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Product?> GetBySlugAsync(string slug)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Slug == slug);
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetFeaturedAsync(int count = 8)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.IsFeatured && p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetNewArrivalsAsync(int count = 8)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.IsNew && p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetOnSaleAsync(int count = 8)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.OldPrice.HasValue && p.IsAvailable)
                .OrderByDescending(p => p.GetDiscountPercentage())
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId, int page = 1, int pageSize = 12)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.CategoryId == categoryId && p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> SearchAsync(string query, int page = 1, int pageSize = 12)
        {
            if (string.IsNullOrWhiteSpace(query))
                return await GetByCategoryAsync(0, page, pageSize);

            var term = query.ToLower();
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p =>
                    p.Name.ToLower().Contains(term) ||
                    p.Description.ToLower().Contains(term) ||
                    p.ShortDescription.ToLower().Contains(term) ||
                    p.Brand.ToLower().Contains(term) ||
                    p.Tags.Any(t => t.ToLower().Contains(term)))
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetRelatedAsync(int productId, int count = 4)
        {
            var product = await GetByIdAsync(productId);
            if (product == null)
                return new List<Product>();

            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.Id != productId &&
                            (p.CategoryId == product.CategoryId || p.Brand == product.Brand) &&
                            p.IsAvailable)
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetBestsellersAsync(int count = 10)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.SoldCount)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetLowStockAsync()
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.IsLowStock && p.IsAvailable)
                .ToListAsync();
        }

        public async Task UpdateStockAsync(int productId, int quantityChange)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                product.StockQuantity += quantityChange;
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<string>> GetProductTagsAsync()
        {
            return await _context.Products
                .AsNoTracking()
                .SelectMany(p => p.Tags)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }

        public async Task<int> GetTotalCountByCategoryAsync(int categoryId)
        {
            return await _context.Products
                .CountAsync(p => p.CategoryId == categoryId && p.IsAvailable);
        }

        public async Task<int> GetTotalSearchCountAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return await _context.Products.CountAsync(p => p.IsAvailable);

            var term = query.ToLower();
            return await _context.Products
                .Where(p =>
                    p.Name.ToLower().Contains(term) ||
                    p.Description.ToLower().Contains(term) ||
                    p.ShortDescription.ToLower().Contains(term) ||
                    p.Brand.ToLower().Contains(term) ||
                    p.Tags.Any(t => t.ToLower().Contains(term)))
                .Where(p => p.IsAvailable)
                .CountAsync();
        }

        public async Task<IEnumerable<Product>> FilterAsync(ProductFilter filter)
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.IsAvailable);

            // Фильтрация по категории
            if (filter.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == filter.CategoryId.Value);

            // Фильтрация по цене
            if (filter.MinPrice.HasValue)
                query = query.Where(p => p.Price >= filter.MinPrice.Value);
            if (filter.MaxPrice.HasValue)
                query = query.Where(p => p.Price <= filter.MaxPrice.Value);

            // Фильтрация по бренду
            if (!string.IsNullOrEmpty(filter.Brand))
                query = query.Where(p => p.Brand == filter.Brand);

            // Фильтрация по тегам
            if (filter.Tags.Any())
                query = query.Where(p => p.Tags.Any(t => filter.Tags.Contains(t)));

            // Наличие
            if (filter.InStock.HasValue)
                query = filter.InStock.Value ? query.Where(p => p.IsAvailable) : query.Where(p => !p.IsAvailable);

            // Товары со скидкой
            if (filter.OnSale.HasValue)
                query = filter.OnSale.Value ? query.Where(p => p.OldPrice.HasValue) : query;

            // Рекомендуемые
            if (filter.IsFeatured.HasValue)
                query = query.Where(p => p.IsFeatured == filter.IsFeatured.Value);

            // Сортировка
            query = filter.SortBy?.ToLowerInvariant() switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "name_asc" => query.OrderBy(p => p.Name),
                "name_desc" => query.OrderByDescending(p => p.Name),
                "popular" => query.OrderByDescending(p => p.SoldCount),
                "rating" => query.OrderByDescending(p => p.Rating),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            // Пагинация
            return await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();
        }

        // --- Реализация IRepository<Product> ---

        public async Task<IEnumerable<Product>> FindAsync(Expression<Func<Product, bool>> predicate)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(predicate)
                .ToListAsync();
        }

        public async Task<Product> AddAsync(Product product)
        {
            try
            {
                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении товара {ProductName}", product.Name);
                throw;
            }
        }

        public async Task UpdateAsync(Product product)
        {
            try
            {
                Console.WriteLine($"=== UPDATE ASYNC НАЧАЛО ===");
                Console.WriteLine($"Товар ID: {product.Id}");
                Console.WriteLine($"Название: '{product.Name}'");
                Console.WriteLine($"Цена: {product.Price}");
                Console.WriteLine($"Дата обновления: {product.UpdatedAt}");

                // Находим товар в контексте с отслеживанием изменений
                var existingProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == product.Id);

                if (existingProduct == null)
                {
                    Console.WriteLine("ОШИБКА: Товар не найден в БД");
                    throw new Exception($"Товар с ID {product.Id} не найден");
                }

                Console.WriteLine($"Найден существующий товар: '{existingProduct.Name}'");
                Console.WriteLine($"Старая цена: {existingProduct.Price}");

                // Проверяем состояние сущности
                var entry = _context.Entry(existingProduct);
                Console.WriteLine($"Состояние сущности: {entry.State}");

                // Обновляем ВСЕ поля вручную
                existingProduct.Name = product.Name;
                existingProduct.Slug = product.Slug ?? existingProduct.Slug;
                existingProduct.Description = product.Description;
                existingProduct.ShortDescription = product.ShortDescription;
                existingProduct.Price = product.Price;
                existingProduct.OldPrice = product.OldPrice;
                existingProduct.ImageUrl = product.ImageUrl ?? existingProduct.ImageUrl;
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.Brand = product.Brand;
                existingProduct.StockQuantity = product.StockQuantity;
                existingProduct.MinStockThreshold = product.MinStockThreshold;
                existingProduct.IsFeatured = product.IsFeatured;
                existingProduct.IsNew = product.IsNew;
                existingProduct.IsAvailable = product.StockQuantity > 0;

                // Сохраняем статистику если не передана
                if (product.Rating == 0 && existingProduct.Rating > 0)
                    product.Rating = existingProduct.Rating;

                if (product.ReviewsCount == 0 && existingProduct.ReviewsCount > 0)
                    product.ReviewsCount = existingProduct.ReviewsCount;

                if (product.SoldCount == 0 && existingProduct.SoldCount > 0)
                    product.SoldCount = existingProduct.SoldCount;

                existingProduct.Rating = product.Rating;
                existingProduct.ReviewsCount = product.ReviewsCount;
                existingProduct.SoldCount = product.SoldCount;

                existingProduct.MetaTitle = product.MetaTitle;
                existingProduct.MetaDescription = product.MetaDescription;
                existingProduct.MetaKeywords = product.MetaKeywords;

                // Коллекции
                if (product.Tags != null && product.Tags.Any())
                {
                    existingProduct.Tags = product.Tags;
                }

                if (product.Specifications != null && product.Specifications.Any())
                {
                    existingProduct.Specifications = product.Specifications;
                }

                existingProduct.UpdatedAt = DateTime.UtcNow;

                Console.WriteLine($"После обновления: '{existingProduct.Name}'");
                Console.WriteLine($"Новая цена: {existingProduct.Price}");
                Console.WriteLine($"Новое состояние сущности: {_context.Entry(existingProduct).State}");

                // Показываем все измененные сущности
                var changedEntries = _context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Modified)
                    .ToList();

                Console.WriteLine($"Измененных сущностей: {changedEntries.Count}");
                foreach (var changedEntry in changedEntries)
                {
                    Console.WriteLine($"  - {changedEntry.Entity.GetType().Name}: {changedEntry.State}");
                }

                // Сохраняем изменения
                Console.WriteLine("Вызываем SaveChangesAsync...");
                var result = await _context.SaveChangesAsync();
                Console.WriteLine($"SaveChangesAsync вернул: {result} (количество измененных записей)");

                if (result > 0)
                {
                    Console.WriteLine("=== UPDATE УСПЕШНО ЗАВЕРШЕН ===");
                }
                else
                {
                    Console.WriteLine("=== ПРЕДУПРЕЖДЕНИЕ: SaveChangesAsync вернул 0 ===");
                    Console.WriteLine("Это означает, что Entity Framework не увидел изменений");

                    // Принудительно помечаем как измененный
                    _context.Entry(existingProduct).State = EntityState.Modified;
                    Console.WriteLine("Пометили сущность как Modified вручную");

                    var result2 = await _context.SaveChangesAsync();
                    Console.WriteLine($"Вторая попытка SaveChangesAsync: {result2}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ОШИБКА В UPDATE ASYNC ===");
                Console.WriteLine($"Сообщение: {ex.Message}");
                Console.WriteLine($"Тип: {ex.GetType().Name}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"InnerException: {ex.InnerException.Message}");
                }

                Console.WriteLine($"StackTrace: {ex.StackTrace}");

                _logger.LogError(ex, "Ошибка при обновлении товара {ProductId}", product.Id);
                throw;
            }
        }

        public async Task DeleteAsync(Product product)
        {
            try
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении товара {ProductId}", product.Id);
                throw;
            }
        }

        public async Task<int> CountAsync(Expression<Func<Product, bool>>? predicate = null)
        {
            var query = _context.Products.AsQueryable();
            if (predicate != null)
                query = query.Where(predicate);
            return await query.CountAsync();
        }

        public async Task<bool> ExistsAsync(Expression<Func<Product, bool>> predicate)
        {
            return await _context.Products.AnyAsync(predicate);
        }

        // --- Вспомогательные методы ---

        public async Task<bool> SlugExistsAsync(string slug, int? excludeId = null)
        {
            var query = _context.Products.Where(p => p.Slug == slug);
            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task IncreaseStockAsync(int productId, int quantity)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                product.StockQuantity += quantity;
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> DecreaseStockAsync(int productId, int quantity)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null || product.StockQuantity < quantity)
                return false;

            product.StockQuantity -= quantity;
            product.SoldCount += quantity;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task UpdateRatingAsync(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product?.Reviews != null)
            {
                var approved = product.Reviews.Where(r => r.IsApproved).ToList();
                if (approved.Any())
                {
                    product.Rating = Math.Round(approved.Average(r => r.Rating), 1);
                    product.ReviewsCount = approved.Count;
                }
                else
                {
                    product.Rating = 0;
                    product.ReviewsCount = 0;
                }
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Dictionary<string, int>> GetInventoryStatsAsync()
        {
            return new Dictionary<string, int>
            {
                ["total"] = await _context.Products.CountAsync(),
                ["available"] = await _context.Products.CountAsync(p => p.IsAvailable),
                ["lowStock"] = await _context.Products.CountAsync(p => p.IsLowStock),
                ["outOfStock"] = await _context.Products.CountAsync(p => p.IsOutOfStock)
            };
        }

        // Infrastructure/Repositories/ProductRepository.cs

        private static string GenerateSlug(string name)
        {
            if (string.IsNullOrEmpty(name)) return "product";
            var slug = name.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("&", "and")
                .Replace("+", "plus")
                .Replace("%", "percent")
                .Replace("$", "dollar")
                .Replace("#", "sharp")
                .Replace("@", "at");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            slug = slug.Trim('-');
            return string.IsNullOrEmpty(slug) ? $"product-{DateTime.UtcNow:yyyyMMddHHmmss}" : slug;
        }

        public async Task<string> GenerateUniqueSlugAsync(string baseName, int? excludeId = null)
        {
            var baseSlug = GenerateSlug(baseName);
            var slug = baseSlug;
            var counter = 1;

            while (await SlugExistsAsync(slug, excludeId))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }


        // Infrastructure/Repositories/ProductRepository.cs
        // _logger.LogInformation("User {UserId} purchased product IDs: {@Ids}", userId, purchasedProductIds);
        public async Task<IEnumerable<Product>> GetRecommendedForUserAsync(string userId, int count = 1)
        {
            // 1. Получаем ID купленных товаров
            var purchasedProductIds = await _context.OrderItems
                .Where(oi => oi.Order.UserId == userId && oi.ProductId.HasValue)
                .Select(oi => oi.ProductId.Value)
                .Distinct()
                .ToListAsync();

            if (!purchasedProductIds.Any())
            {
                return (await GetFeaturedAsync(count)).Take(count);
            }

            // 2. Получаем категории и бренды купленных товаров
            var purchasedCategories = await _context.Products
                .Where(p => purchasedProductIds.Contains(p.Id))
                .Select(p => p.CategoryId)
                .Distinct()
                .ToListAsync();

            var purchasedBrands = await _context.Products
                .Where(p => purchasedProductIds.Contains(p.Id))
                .Select(p => p.Brand)
                .Distinct()
                .ToListAsync();

            // 3. Загружаем кандидатов — только через простые .Contains() от списков значений
            var candidates = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .Where(p => !purchasedProductIds.Contains(p.Id)) // исключаем купленные
                .Where(p => purchasedCategories.Contains(p.CategoryId) || purchasedBrands.Contains(p.Brand))
                .OrderByDescending(p => p.SoldCount)
                .ThenByDescending(p => p.Rating)
                .Take(count * 3) // берём с запасом
                .ToListAsync();

            // 4. Если не хватает — добавляем бестселлеры (только не купленные)
            if (candidates.Count < count)
            {
                var bestsellers = await GetBestsellersAsync(count * 3);
                foreach (var bs in bestsellers)
                {
                    if (!purchasedProductIds.Contains(bs.Id) && !candidates.Any(c => c.Id == bs.Id))
                    {
                        candidates.Add(bs);
                        if (candidates.Count >= count) break;
                    }
                }
            }

            _logger.LogInformation("Рекомендации для {UserId}: куплено {@Purchased}, рекомендовано {@Recs}",
                userId, purchasedProductIds, candidates.Take(count).Select(r => r.Id));

            return candidates.Take(count);
        }
    }
}