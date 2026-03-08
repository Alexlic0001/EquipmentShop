using CsvHelper;
using CsvHelper.Configuration;
using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace EquipmentShop_.Controllers
{
    [Authorize(Roles = $"{AppConstants.AdminRole},{AppConstants.ManagerRole}")]
    [Route("admin/reports")]
    public class AdminReportController(
        IOrderRepository orderRepository,
        ICategoryRepository categoryRepository,
        ILogger<AdminReportController> logger) : Controller
    {
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly ILogger<AdminReportController> _logger = logger;

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // ✅ Гарантируем, что Categories не будет null
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = categories ?? new List<Category>();

            return View(new SalesReportFilterViewModel
            {
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow
            });
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateReport(SalesReportFilterViewModel filter)
        {
            try
            {
                var reportData = await _orderRepository.GetSalesReportAsync(
                    filter.StartDate,
                    filter.EndDate,
                    filter.CategoryId,
                    filter.Brand
                );

                // Сортировка
                reportData = filter.SortBy switch
                {
                    "TotalQuantitySold" => filter.SortDescending
                        ? reportData.OrderByDescending(r => r.TotalQuantitySold)
                        : reportData.OrderBy(r => r.TotalQuantitySold),
                    "ProductName" => filter.SortDescending
                        ? reportData.OrderByDescending(r => r.ProductName)
                        : reportData.OrderBy(r => r.ProductName),
                    _ => filter.SortDescending
                        ? reportData.OrderByDescending(r => r.TotalRevenue)
                        : reportData.OrderBy(r => r.TotalRevenue)
                };

                var viewModel = reportData.Select(r => new SalesReportViewModel
                {
                    ProductId = r.ProductId,
                    ProductName = r.ProductName,
                    ProductSku = r.ProductSku,
                    CategoryName = r.CategoryName,
                    Brand = r.Brand,
                    TotalQuantitySold = r.TotalQuantitySold,
                    OrderCount = r.OrderCount,
                    UnitPrice = r.UnitPrice,
                    TotalRevenue = r.TotalRevenue,
                    AverageOrderValue = r.OrderCount > 0 ? r.TotalRevenue / r.OrderCount : 0,
                    FirstSaleDate = r.FirstSaleDate,
                    LastSaleDate = r.LastSaleDate,
                    Status = r.IsAvailable ? "Активен" : "Снят с продажи"
                }).ToList();

                ViewBag.Filter = filter;
                ViewBag.TotalRevenue = viewModel.Sum(r => r.TotalRevenue);
                ViewBag.TotalQuantitySold = viewModel.Sum(r => r.TotalQuantitySold);
                ViewBag.TotalProducts = viewModel.Count;
                ViewBag.ReportData = viewModel;
                ViewBag.Categories = await _categoryRepository.GetAllAsync(); // ✅ Для повторного отображения формы

                return View("Index", filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации отчета");
                TempData["Error"] = "Ошибка при формировании отчета";
                return RedirectToAction("Index");
            }
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportToCsv(
            DateTime? startDate,
            DateTime? endDate,
            int? categoryId,
            string? brand)
        {
            try
            {
                var reportData = await _orderRepository.GetSalesReportAsync(
                    startDate, endDate, categoryId, brand
                );

                var records = reportData.Select(r => new SalesReportViewModel
                {
                    ProductId = r.ProductId,
                    ProductName = r.ProductName,
                    ProductSku = r.ProductSku,
                    CategoryName = r.CategoryName,
                    Brand = r.Brand,
                    TotalQuantitySold = r.TotalQuantitySold,
                    OrderCount = r.OrderCount,
                    UnitPrice = r.UnitPrice,
                    TotalRevenue = r.TotalRevenue,
                    AverageOrderValue = r.OrderCount > 0 ? r.TotalRevenue / r.OrderCount : 0,
                    FirstSaleDate = r.FirstSaleDate,
                    LastSaleDate = r.LastSaleDate,
                    Status = r.IsAvailable ? "Активен" : "Снят с продажи"
                });

                return GenerateCsv(records, $"sales_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте отчета");
                TempData["Error"] = "Ошибка при экспорте отчета";
                return RedirectToAction("Index");
            }
        }

        [HttpGet("export-summary")]
        public async Task<IActionResult> ExportSummaryCsv(
            DateTime? startDate,
            DateTime? endDate)
        {
            try
            {
                var reportData = await _orderRepository.GetSalesReportAsync(startDate, endDate);

                var summary = new
                {
                    ReportPeriod = $"{startDate?.ToString("dd.MM.yyyy") ?? "Начало"} - {endDate?.ToString("dd.MM.yyyy") ?? "Сегодня"}",
                    TotalProducts = reportData.Count(),
                    TotalQuantitySold = reportData.Sum(r => r.TotalQuantitySold),
                    TotalRevenue = reportData.Sum(r => r.TotalRevenue),
                    AverageOrderValue = reportData.Any() ? reportData.Sum(r => r.TotalRevenue) / reportData.Sum(r => r.OrderCount) : 0,
                    GeneratedAt = DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm")
                };

                var records = new[] { summary };
                return GenerateCsv(records, $"sales_summary_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте сводки");
                TempData["Error"] = "Ошибка при экспорте сводки";
                return RedirectToAction("Index");
            }
        }

        private IActionResult GenerateCsv<T>(IEnumerable<T> records, string fileName)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                Encoding = Encoding.UTF8
            });

            csv.WriteRecords(records);
            writer.Flush();

            return File(memoryStream.ToArray(), "text/csv; charset=utf-8", fileName);
        }
    }
}