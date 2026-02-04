using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using EquipmentShop.Infrastructure.Data;

namespace EquipmentShop.UnitTests.Helpers
{
    public static class TestDbContextFactory
    {
        public static AppDbContext Create()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        public static void Destroy(AppDbContext context)
        {
            context.Database.EnsureDeleted();
            context.Dispose();
        }
    }
}