using EquipmentShop.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EquipmentShop.Infrastructure.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(ILogger<FileStorageService> logger)
        {
            _logger = logger;
        }

        public async Task<string> SaveProductImageAsync(Stream fileStream, string fileName)
        {
            return await SaveFileAsync(fileStream, fileName, "products");
        }

        public async Task<string> SaveCategoryImageAsync(Stream fileStream, string fileName)
        {
            return await SaveFileAsync(fileStream, fileName, "categories");
        }

        public async Task<string> SaveUserAvatarAsync(Stream fileStream, string fileName)
        {
            return await SaveFileAsync(fileStream, fileName, "avatars");
        }

        private async Task<string> SaveFileAsync(Stream fileStream, string fileName, string subFolder)
        {
            try
            {
                var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var uploadsFolder = Path.Combine(webRootPath, "uploads", subFolder);

                // Создаем папку если не существует
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Генерируем уникальное имя файла
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(fileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Сохраняем файл
                using (var file = new FileStream(filePath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(file);
                }

                return $"/uploads/{subFolder}/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении файла {FileName}", fileName);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return false;

                var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var fullPath = Path.Combine(webRootPath, filePath.TrimStart('/'));

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении файла {FilePath}", filePath);
                return false;
            }
        }

        public async Task<Stream> GetFileAsync(string filePath)
        {
            try
            {
                var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var fullPath = Path.Combine(webRootPath, filePath.TrimStart('/'));

                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("Файл не найден");

                return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении файла {FilePath}", filePath);
                throw;
            }
        }

        public async Task<string> GenerateUniqueFileName(string originalFileName)
        {
            var extension = Path.GetExtension(originalFileName);
            return $"{Guid.NewGuid()}{extension}";
        }

        public async Task<IEnumerable<string>> GetProductGalleryAsync(int productId)
        {
            // Заглушка для демо
            return new List<string>();
        }

        public async Task ClearProductGalleryAsync(int productId)
        {
            // Заглушка для демо
        }
    }
}