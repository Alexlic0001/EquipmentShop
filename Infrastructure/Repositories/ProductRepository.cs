using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EquipmentShop.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _context;

        public ProductRepository(AppDbContext context)
        {
            _context = context;
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
                return await GetByCategoryAsync(0, page, pageSize); // или вызвать GetAll с пагинацией

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
            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return product;
        }

        public async Task UpdateAsync(Product product)
        {
            product.UpdatedAt = DateTime.UtcNow;
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Product product)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
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

        // --- Вспомогательные методы (не в интерфейсе, но используются в коде) ---

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
    }
}