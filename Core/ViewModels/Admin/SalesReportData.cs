namespace EquipmentShop.Core.ViewModels.Admin
{
    public class SalesReportData
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSku { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;

        public int TotalQuantitySold { get; set; }
        public int OrderCount { get; set; }

        public decimal UnitPrice { get; set; }
        public decimal TotalRevenue { get; set; }

        public DateTime FirstSaleDate { get; set; }
        public DateTime LastSaleDate { get; set; }

        public bool IsAvailable { get; set; }
    }

    public class SalesSummaryData
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalItemsSold { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int UniqueProductsSold { get; set; }
    }
}