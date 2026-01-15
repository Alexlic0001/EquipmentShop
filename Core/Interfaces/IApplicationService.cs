//namespace EquipmentShop.Core.Interfaces

//{
//    public interface IApplicationService
//    {
//        Task<ApplicationInfo> GetApplicationInfoAsync();
//        Task<SystemStats> GetSystemStatsAsync();
//        Task<bool> IsDatabaseConnectedAsync();
//        Task<ApplicationHealth> CheckHealthAsync();
//    }

//    public class ApplicationInfo
//    {
//        public string Name { get; set; } = string.Empty;
//        public string Version { get; set; } = string.Empty;
//        public string Environment { get; set; } = string.Empty;
//        public DateTime StartupTime { get; set; }
//        public TimeSpan Uptime => DateTime.UtcNow - StartupTime;
//    }

//    public class SystemStats
//    {
//        public int TotalProducts { get; set; }
//        public int TotalCategories { get; set; }
//        public int TotalOrders { get; set; }
//        public int TotalUsers { get; set; }
//        public int TotalReviews { get; set; }
//        public decimal TotalRevenue { get; set; }
//        public int TodayOrders { get; set; }
//        public int LowStockProducts { get; set; }
//    }

//    public class ApplicationHealth
//    {
//        public bool Database { get; set; }
//        public bool Cache { get; set; }
//        public bool Storage { get; set; }
//        public bool Email { get; set; }
//        public bool Overall => Database && Cache && Storage && Email;
//        public string Status => Overall ? "Healthy" : "Unhealthy";
//        public Dictionary<string, string> Details { get; set; } = new();
//    }
//}
