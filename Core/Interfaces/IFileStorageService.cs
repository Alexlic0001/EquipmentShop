namespace EquipmentShop.Core.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> SaveProductImageAsync(Stream fileStream, string fileName);
        Task<string> SaveCategoryImageAsync(Stream fileStream, string fileName);
        Task<string> SaveUserAvatarAsync(Stream fileStream, string fileName);
        Task<bool> DeleteFileAsync(string filePath);
        Task<Stream> GetFileAsync(string filePath);
        Task<string> GenerateUniqueFileName(string originalFileName);
    }
}
