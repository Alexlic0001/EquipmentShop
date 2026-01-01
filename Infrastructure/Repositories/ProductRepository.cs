using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels;
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

        public async Task<Product?> GetByIdAsync(int id)
        {
            try
            {
                return await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Reviews.Where(r => r.IsApproved))
                    .FirstOrDefaultAsync(p => p.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении товара по ID {ProductId}", id);
                throw;
            }
        }

        public async Task<Product?> GetBySlugAsync(string slug)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Reviews.Where(r => r.IsApproved))
                .FirstOrDefaultAsync(p => p.Slug == slug);
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> FindAsync(Expression<Func<Product, bool>> predicate)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(predicate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetFeaturedAsync(int count = 8)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsFeatured && p.IsAvailable)
                .OrderByDescending(p => p.Rating)
                .ThenByDescending(p => p.SoldCount)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetNewArrivalsAsync(int count = 8)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsNew && p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetOnSaleAsync(int count = 8)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.OldPrice.HasValue && p.IsAvailable)
                .OrderByDescending(p => p.GetDiscountPercentage())
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId, int page = 1, int pageSize = 12)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId == categoryId && p.IsAvailable);

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> SearchAsync(string query, int page = 1, int pageSize = 12)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return await GetAllAsync();
            }

            var normalizedQuery = query.Trim().ToLower();

            var searchQuery = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable &&
                       (p.Name.ToLower().Contains(normalizedQuery) ||
                        p.Description.ToLower().Contains(normalizedQuery) ||
                        p.ShortDescription.ToLower().Contains(normalizedQuery) ||
                        p.Brand.ToLower().Contains(normalizedQuery) ||
                        p.Tags.Any(t => t.ToLower().Contains(normalizedQuery)) ||
                        p.Category.Name.ToLower().Contains(normalizedQuery)));

            return await searchQuery
                .OrderByDescending(p =>
                    (p.Name.ToLower().Contains(normalizedQuery) ? 3 : 0) +
                    (p.Brand.ToLower().Contains(normalizedQuery) ? 2 : 0) +
                    (p.Tags.Any(t => t.ToLower().Contains(normalizedQuery)) ? 1 : 0))
                .ThenByDescending(p => p.Rating)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetRelatedAsync(int productId, int count = 4)
        {
            var product = await GetByIdAsync(productId);
            if (product == null)
            {
                return new List<Product>();
            }

            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Id != productId &&
                       p.CategoryId == product.CategoryId &&
                       p.IsAvailable)
                .OrderByDescending(p => p.Rating)
                .ThenByDescending(p => p.SoldCount)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetBestsellersAsync(int count = 10)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.SoldCount)
                .ThenByDescending(p => p.Rating)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetLowStockAsync()
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsLowStock)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();
        }

        public async Task UpdateStockAsync(int productId, int quantityChange)
        {
            var product = await GetByIdAsync(productId);
            if (product == null)
            {
                throw new Exception($"Товар с ID {productId} не найден");
            }

            product.StockQuantity += quantityChange;
            if (product.StockQuantity < 0)
            {
                product.StockQuantity = 0;
            }

            await UpdateAsync(product);
        }

        public async Task<IEnumerable<string>> GetProductTagsAsync()
        {
            return await _context.Products
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
            {
                return await _context.Products.CountAsync(p => p.IsAvailable);
            }

            var normalizedQuery = query.Trim().ToLower();

            return await _context.Products
                .CountAsync(p => p.IsAvailable &&
                       (p.Name.ToLower().Contains(normalizedQuery) ||
                        p.Description.ToLower().Contains(normalizedQuery) ||
                        p.ShortDescription.ToLower().Contains(normalizedQuery) ||
                        p.Brand.ToLower().Contains(normalizedQuery) ||
                        p.Tags.Any(t => t.ToLower().Contains(normalizedQuery)) ||
                        p.Category.Name.ToLower().Contains(normalizedQuery)));
        }

        public async Task<IEnumerable<Product>> FilterAsync(Core.Interfaces.ProductFilter filter)////
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .AsQueryable();

            // Применяем фильтры
            if (filter.CategoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
            }

            if (filter.MinPrice.HasValue)
            {
                query = query.Where(p => p.Price >= filter.MinPrice.Value);
            }

            if (filter.MaxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= filter.MaxPrice.Value);
            }

            if (!string.IsNullOrEmpty(filter.Brand))
            {
                query = query.Where(p => p.Brand == filter.Brand);
            }

            if (filter.Tags.Any())
            {
                query = query.Where(p => p.Tags.Any(t => filter.Tags.Contains(t)));
            }

            if (filter.InStock.HasValue && filter.InStock.Value)
            {
                query = query.Where(p => p.StockQuantity > 0);
            }

            if (filter.OnSale.HasValue && filter.OnSale.Value)
            {
                query = query.Where(p => p.OldPrice.HasValue);
            }

            if (filter.IsFeatured.HasValue && filter.IsFeatured.Value)
            {
                query = query.Where(p => p.IsFeatured);
            }

            // Сортировка
            query = filter.SortBy?.ToLower() switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
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
                product.UpdatedAt = DateTime.UtcNow;
                _context.Products.Update(product);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
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
            if (predicate == null)
            {
                return await _context.Products.CountAsync();
            }

            return await _context.Products.CountAsync(predicate);
        }

        public async Task<bool> ExistsAsync(Expression<Func<Product, bool>> predicate)
        {
            return await _context.Products.AnyAsync(predicate);
        }

    }
}
