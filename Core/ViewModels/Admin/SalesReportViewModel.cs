namespace EquipmentShop.Core.ViewModels.Admin
{
    public class SalesReportViewModel
    {
        // Информация о товаре
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSku { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        
        // Количество
        public int TotalQuantitySold { get; set; }
        public int OrderCount { get; set; } // Количество заказов с этим товаром
        
        // Финансовые показатели
        public decimal UnitPrice { get; set; }
        public decimal TotalRevenue { get; set; } // Общая выручка
        public decimal AverageOrderValue { get; set; } // Средний чек на товар
        
        // Период
        public DateTime FirstSaleDate { get; set; }
        public DateTime LastSaleDate { get; set; }
        
        // Дополнительные метрики
        public decimal ProfitMargin { get; set; } // Маржа (если будет себестоимость)
        public string Status { get; set; } = "Активен"; // Активен/Снят с продажи
    }
    
    public class SalesReportFilterViewModel
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CategoryId { get; set; }
        public string? Brand { get; set; }
        public string? SortBy { get; set; } = "TotalRevenue"; // TotalRevenue, TotalQuantitySold, ProductName
        public bool SortDescending { get; set; } = true;
    }
}