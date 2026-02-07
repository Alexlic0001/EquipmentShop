using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;


namespace EquipmentShop.Infrastructure.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CategoryRepository> _logger;

        public CategoryRepository(AppDbContext context, ILogger<CategoryRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            return await _context.Categories
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories)
                .Include(c => c.Products.Where(p => p.IsAvailable))
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Category?> GetBySlugAsync(string slug)
        {
            return await _context.Categories
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories.Where(sc => sc.IsActive))
                .Include(c => c.Products.Where(p => p.IsAvailable))
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            return await _context.Categories
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories.Where(sc => sc.IsActive))
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> FindAsync(Expression<Func<Category, bool>> predicate)
        {
            return await _context.Categories
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories.Where(sc => sc.IsActive))
                .Where(predicate)
                .Where(c => c.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetMainCategoriesAsync()
        {
            return await _context.Categories
                .Include(c => c.SubCategories.Where(sc => sc.IsActive))
                .Where(c => c.ParentCategoryId == null && c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetSubCategoriesAsync(int parentId)
        {
            return await _context.Categories
                .Include(c => c.ParentCategory)
                .Where(c => c.ParentCategoryId == parentId && c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetCategoriesWithProductsAsync()
        {
            return await _context.Categories
                .Include(c => c.Products.Where(p => p.IsAvailable))
                .Where(c => c.IsActive && c.Products.Any(p => p.IsAvailable))
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetMenuCategoriesAsync()
        {
            return await _context.Categories
                .Include(c => c.SubCategories.Where(sc => sc.IsActive && sc.ShowInMenu))
                .Where(c => c.ShowInMenu && c.IsActive && c.ParentCategoryId == null)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetHierarchyAsync()
        {
            var categories = await GetAllAsync();
            return BuildHierarchy(categories, null);
        }

        private IEnumerable<Category> BuildHierarchy(IEnumerable<Category> categories, int? parentId)
        {
            return categories
                .Where(c => c.ParentCategoryId == parentId)
                .Select(c => new Category
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    ParentCategoryId = c.ParentCategoryId,
                    SubCategories = BuildHierarchy(categories, c.Id).ToList()
                });
        }

        public async Task<bool> HasProductsAsync(int categoryId)
        {
            return await _context.Products
                .AnyAsync(p => p.CategoryId == categoryId && p.IsAvailable);
        }

        public async Task<Category> AddAsync(Category category)
        {
            try
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                return category;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении категории {CategoryName}", category.Name);
                throw;
            }
        }

        public async Task UpdateAsync(Category category)
        {
            try
            {
                _context.Categories.Update(category);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении категории {CategoryId}", category.Id);
                throw;
            }
        }

        public async Task DeleteAsync(Category category)
        {
            try
            {
                // Проверяем, есть ли товары в категории
                if (await HasProductsAsync(category.Id))
                {
                    throw new InvalidOperationException("Нельзя удалить категорию, в которой есть товары");
                }

                // Проверяем, есть ли подкатегории
                var hasSubCategories = await _context.Categories.AnyAsync(c => c.ParentCategoryId == category.Id);
                if (hasSubCategories)
                {
                    throw new InvalidOperationException("Нельзя удалить категорию, у которой есть подкатегории");
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении категории {CategoryId}", category.Id);
                throw;
            }
        }

        public async Task<int> CountAsync(Expression<Func<Category, bool>>? predicate = null)
        {
            if (predicate == null)
            {
                return await _context.Categories.CountAsync(c => c.IsActive);
            }

            return await _context.Categories
                .Where(predicate)
                .CountAsync(c => c.IsActive);
        }

        public async Task<bool> ExistsAsync(Expression<Func<Category, bool>> predicate)
        {
            return await _context.Categories
                .Where(c => c.IsActive)
                .AnyAsync(predicate);
        }

    }
}
