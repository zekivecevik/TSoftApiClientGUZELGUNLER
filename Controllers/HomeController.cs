using Microsoft.AspNetCore.Mvc;
using TSoftApiClient.Services;
using System.Globalization;

namespace TSoftApiClient.Controllers
{
    /// <summary>
    /// ULTRA FAST Dashboard - Minimal Data Loading
    /// </summary>
    public class HomeController : Controller
    {
        private readonly TSoftApiService _tsoftService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            TSoftApiService tsoftService,
            ILogger<HomeController> logger)
        {
            _tsoftService = tsoftService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("⚡ Loading ULTRA FAST Dashboard...");

                var productsTask = _tsoftService.GetProductsAsync(limit: 100);
                var ordersTask = _tsoftService.GetOrdersAsync(limit: 600);  // ✅ 300 sipariş - optimal!
                var customersTask = _tsoftService.GetCustomersAsync(limit: 20);

                var categoriesTask = Task.FromResult(new Models.TSoftApiResponse<List<Models.Category>>
                {
                    Success = true,
                    Data = new List<Models.Category>()
                });

                await Task.WhenAll(productsTask, ordersTask, customersTask, categoriesTask);

                var products = await productsTask;
                var orders = await ordersTask;
                var customers = await customersTask;

                var productList = products.Success && products.Data != null ? products.Data : new List<Models.Product>();
                var orderList = orders.Success && orders.Data != null ? orders.Data : new List<Models.Order>();
                var customerList = customers.Success && customers.Data != null ? customers.Data : new List<Models.Customer>();

                _logger.LogInformation($"✅ FAST Load: {productList.Count} products, {orderList.Count} orders, {customerList.Count} customers");

                var today = DateTime.Now.Date;

                ViewBag.TotalOrders = orderList.Count;
                ViewBag.OrdersToday = orderList.Count(o => ParseDate(o.OrderDate) >= today);
                ViewBag.OrdersThisWeek = orderList.Count(o => ParseDate(o.OrderDate) >= today.AddDays(-7));
                ViewBag.OrdersThisMonth = orderList.Count(o => ParseDate(o.OrderDate) >= today.AddDays(-30));

                ViewBag.TotalRevenue = CalculateRevenue(orderList);
                ViewBag.RevenueToday = CalculateRevenue(orderList.Where(o => ParseDate(o.OrderDate) >= today));
                ViewBag.RevenueThisWeek = CalculateRevenue(orderList.Where(o => ParseDate(o.OrderDate) >= today.AddDays(-7)));
                ViewBag.RevenueThisMonth = CalculateRevenue(orderList.Where(o => ParseDate(o.OrderDate) >= today.AddDays(-30)));

                ViewBag.TotalCustomers = customerList.Count;
                ViewBag.NewCustomersToday = customerList.Count(c => ParseDate(c.CreatedDate ?? c.DateCreated) >= today);
                ViewBag.NewCustomersThisWeek = customerList.Count(c => ParseDate(c.CreatedDate ?? c.DateCreated) >= today.AddDays(-7));
                ViewBag.NewCustomersThisMonth = customerList.Count(c => ParseDate(c.CreatedDate ?? c.DateCreated) >= today.AddDays(-30));

                ViewBag.TotalProducts = productList.Count;
                ViewBag.ActiveProducts = productList.Count(p => p.IsActive == "1" || string.IsNullOrEmpty(p.IsActive));
                ViewBag.PassiveProducts = productList.Count - ViewBag.ActiveProducts;

                int totalStock = 0;
                int lowStockCount = 0;
                int outOfStockCount = 0;

                foreach (var p in productList)
                {
                    if (int.TryParse(p.Stock, out var stock))
                    {
                        totalStock += stock;
                        if (stock == 0) outOfStockCount++;
                        else if (stock <= 10) lowStockCount++;
                    }
                }

                ViewBag.TotalStock = totalStock;
                ViewBag.LowStockCount = lowStockCount;
                ViewBag.OutOfStockCount = outOfStockCount;
                ViewBag.TotalStockValue = 0;

                ViewBag.LowStockProducts = productList
                    .Where(p => int.TryParse(p.Stock, out var s) && s > 0 && s <= 10)
                    .Take(10)
                    .ToList();

                ViewBag.PendingOrders = orderList.Count(o => o.OrderStatusId == "1");
                ViewBag.ProcessingOrders = orderList.Count(o => o.OrderStatusId == "2");
                ViewBag.CompletedOrders = orderList.Count(o => o.OrderStatusId == "3");
                ViewBag.CancelledOrders = orderList.Count(o => o.OrderStatusId == "4");

                // ========== FIXED: REAL LAST 30 & 5 DAYS CHART ==========
                var ordersByDate = orderList
                    .GroupBy(o => ParseDate(o.OrderDate).Date)
                    .Where(g => g.Key != DateTime.MinValue.Date)
                    .ToDictionary(g => g.Key, g => g.ToList());

                _logger.LogInformation($"📊 Order dates available: {string.Join(", ", ordersByDate.Keys.Select(d => d.ToString("dd.MM")))}");

                // ✅ FIXED: Son 5 günü oluştur (boş günler dahil!)
                var last5Days = new List<(DateTime Date, decimal Revenue, int OrderCount)>();

                for (int i = 4; i >= 0; i--)
                {
                    var targetDate = today.AddDays(-i);
                    var dayOrders = ordersByDate.ContainsKey(targetDate)
                        ? ordersByDate[targetDate]
                        : new List<Models.Order>();

                    var revenue = CalculateRevenue(dayOrders);
                    last5Days.Add((targetDate, revenue, dayOrders.Count));

                    _logger.LogInformation($"📊 Last5Days Chart: {targetDate:dd.MM.yyyy} - {dayOrders.Count} orders, {revenue:F2} TL");
                }

                // Son 30 gün için de aynı mantık
                var last30Days = new List<(DateTime Date, decimal Revenue, int OrderCount)>();

                for (int i = 29; i >= 0; i--)
                {
                    var targetDate = today.AddDays(-i);
                    var dayOrders = ordersByDate.ContainsKey(targetDate)
                        ? ordersByDate[targetDate]
                        : new List<Models.Order>();

                    var revenue = CalculateRevenue(dayOrders);
                    last30Days.Add((targetDate, revenue, dayOrders.Count));
                }

                ViewBag.Last7DaysChart = last5Days;
                ViewBag.Last30DaysChart = last30Days;

                // EN ÇOK SATAN 5 ÜRÜN
                var topSellingProducts = new List<TopSellingProduct>();
                var productSales = new Dictionary<string, int>();

                foreach (var order in orderList)
                {
                    if (order.OrderDetails != null)
                    {
                        foreach (var detail in order.OrderDetails)
                        {
                            var productCode = detail.ProductCode ?? detail.ProductId ?? "";
                            if (!string.IsNullOrEmpty(productCode))
                            {
                                if (!productSales.ContainsKey(productCode))
                                    productSales[productCode] = 0;

                                if (int.TryParse(detail.Quantity, out var qty))
                                    productSales[productCode] += qty;
                            }
                        }
                    }
                }

                var topProducts = productSales.OrderByDescending(x => x.Value).Take(5).ToList();

                foreach (var (productCode, quantity) in topProducts)
                {
                    var product = productList.FirstOrDefault(p => p.ProductCode == productCode);
                    if (product != null)
                    {
                        topSellingProducts.Add(new TopSellingProduct
                        {
                            ProductCode = productCode,
                            ProductName = product.ProductName ?? "Bilinmeyen",
                            TotalSold = quantity,
                            Revenue = 0
                        });
                    }
                }

                ViewBag.TopSellingProducts = topSellingProducts;
                ViewBag.CategoryStats = new List<CategoryStat>();
                ViewBag.PaymentTypeStats = new List<PaymentStat>();
                ViewBag.RecentOrders = orderList.Take(10).ToList();
                ViewBag.RecentCustomers = customerList.Take(5).ToList();
                ViewBag.RecentProducts = productList.Take(5).ToList();

                // ALERTS
                var alerts = new List<DashboardAlert>();

                if (lowStockCount > 0)
                {
                    alerts.Add(new DashboardAlert
                    {
                        Type = "warning",
                        Icon = "⚠️",
                        Title = "Kritik Stok",
                        Message = $"{lowStockCount} ürünün stoğu 10'un altında!",
                        Count = lowStockCount,
                        Link = "/Products?filter=lowstock"
                    });
                }

                if (outOfStockCount > 0)
                {
                    alerts.Add(new DashboardAlert
                    {
                        Type = "danger",
                        Icon = "🚫",
                        Title = "Tükenen Ürünler",
                        Message = $"{outOfStockCount} ürünün stoğu tükenmiş!",
                        Count = outOfStockCount,
                        Link = "/Products?filter=outofstock"
                    });
                }

                if (ViewBag.PendingOrders > 0)
                {
                    alerts.Add(new DashboardAlert
                    {
                        Type = "info",
                        Icon = "⏳",
                        Title = "Bekleyen Siparişler",
                        Message = $"{ViewBag.PendingOrders} sipariş onay bekliyor!",
                        Count = ViewBag.PendingOrders,
                        Link = "/Orders?status=1"
                    });
                }

                ViewBag.Alerts = alerts;
                ViewBag.AverageOrderValue = orderList.Count > 0 ? (decimal)ViewBag.TotalRevenue / orderList.Count : 0;
                ViewBag.ConversionRate = 3.2m;
                ViewBag.CompletionRate = orderList.Count > 0 ? (decimal)ViewBag.CompletedOrders / orderList.Count * 100 : 0;
                ViewBag.AverageCycleDays = 3;
                ViewBag.CityStats = new List<object>();

                _logger.LogInformation("✅ ULTRA FAST Dashboard loaded!");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Dashboard error");
                ViewBag.Error = "Veriler yüklenirken bir hata oluştu.";
                InitializeEmptyViewBag();
                return View();
            }
        }

        private DateTime ParseDate(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return DateTime.MinValue;
            return DateTime.TryParse(dateStr, out var date) ? date.Date : DateTime.MinValue;
        }

        private decimal CalculateRevenue(IEnumerable<Models.Order> orders)
        {
            decimal total = 0;
            foreach (var order in orders)
            {
                var priceStr = order.OrderTotalPrice ?? order.Total ?? order.TotalAmount ?? "0";
                if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                {
                    total += price;
                }
            }
            return total;
        }

        private void InitializeEmptyViewBag()
        {
            ViewBag.TotalOrders = 0;
            ViewBag.OrdersToday = 0;
            ViewBag.OrdersThisWeek = 0;
            ViewBag.OrdersThisMonth = 0;
            ViewBag.TotalRevenue = 0;
            ViewBag.RevenueToday = 0;
            ViewBag.RevenueThisWeek = 0;
            ViewBag.RevenueThisMonth = 0;
            ViewBag.TotalCustomers = 0;
            ViewBag.NewCustomersToday = 0;
            ViewBag.NewCustomersThisWeek = 0;
            ViewBag.TotalProducts = 0;
            ViewBag.ActiveProducts = 0;
            ViewBag.LowStockCount = 0;
            ViewBag.OutOfStockCount = 0;
            ViewBag.Last7DaysChart = new List<(DateTime, decimal, int)>();
            ViewBag.Last30DaysChart = new List<(DateTime, decimal, int)>();
            ViewBag.CategoryStats = new List<CategoryStat>();
            ViewBag.PaymentTypeStats = new List<PaymentStat>();
            ViewBag.RecentOrders = new List<Models.Order>();
            ViewBag.RecentCustomers = new List<Models.Customer>();
            ViewBag.RecentProducts = new List<Models.Product>();
            ViewBag.LowStockProducts = new List<Models.Product>();
            ViewBag.Alerts = new List<DashboardAlert>();
            ViewBag.CityStats = new List<object>();
            ViewBag.AverageOrderValue = 0;
            ViewBag.ConversionRate = 0;
            ViewBag.CompletionRate = 0;
            ViewBag.AverageCycleDays = 0;
            ViewBag.PendingOrders = 0;
            ViewBag.ProcessingOrders = 0;
            ViewBag.CompletedOrders = 0;
            ViewBag.CancelledOrders = 0;
            ViewBag.TotalStock = 0;
            ViewBag.TotalStockValue = 0;
            ViewBag.PassiveProducts = 0;
            ViewBag.NewCustomersThisMonth = 0;
            ViewBag.TopSellingProducts = new List<TopSellingProduct>();
        }
    }

    public class DashboardAlert
    {
        public string Type { get; set; } = "info";
        public string Icon { get; set; } = "ℹ️";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public int Count { get; set; }
        public string Link { get; set; } = "#";
    }

    public class CategoryStat
    {
        public string CategoryCode { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public int ProductCount { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class PaymentStat
    {
        public string PaymentType { get; set; } = "";
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class TopSellingProduct
    {
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int TotalSold { get; set; }
        public decimal Revenue { get; set; }
    }
}