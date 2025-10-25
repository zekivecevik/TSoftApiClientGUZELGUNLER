using Microsoft.AspNetCore.Mvc;
using TSoftApiClient.Services;
using System.Text.Json;

namespace TSoftApiClient.Controllers
{
    [Route("DashboardDebug")]
    public class DashboardDebugController : Controller
    {
        private readonly TSoftApiService _tsoftService;
        private readonly ILogger<DashboardDebugController> _logger;

        public DashboardDebugController(
            TSoftApiService tsoftService,
            ILogger<DashboardDebugController> logger)
        {
            _tsoftService = tsoftService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var logs = new List<string>();

            try
            {
                logs.Add("🔍 DASHBOARD DEBUG - EN ÇOK SATAN ÜRÜNLER");
                logs.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                logs.Add("");

                // 1. Siparişleri çek
                logs.Add("📦 STEP 1: İlk 5 siparişi getiriyoruz...");
                var ordersResult = await _tsoftService.GetOrdersAsync(limit: 5);

                if (!ordersResult.Success || ordersResult.Data == null || ordersResult.Data.Count == 0)
                {
                    logs.Add("❌ HATA: Sipariş bulunamadı!");
                    ViewBag.Logs = logs;
                    return View();
                }

                logs.Add($"✅ {ordersResult.Data.Count} sipariş bulundu");
                logs.Add("");

                // 2. Her sipariş için detayları kontrol et
                logs.Add("📊 STEP 2: Sipariş detaylarını kontrol ediyoruz...");
                logs.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                logs.Add("");

                var productSales = new Dictionary<string, int>();

                foreach (var order in ordersResult.Data.Take(5))
                {
                    logs.Add($"🛒 Sipariş: {order.OrderCode} (ID: {order.OrderId})");

                    // Sipariş nesnesinde zaten OrderDetails var mı?
                    if (order.OrderDetails != null && order.OrderDetails.Count > 0)
                    {
                        logs.Add($"  ✅ OrderDetails zaten var! ({order.OrderDetails.Count} ürün)");

                        foreach (var detail in order.OrderDetails)
                        {
                            var productCode = detail.ProductCode ?? detail.ProductId ?? "N/A";
                            var quantity = detail.Quantity ?? "0";
                            logs.Add($"     - {productCode}: {quantity} adet");

                            if (!string.IsNullOrEmpty(productCode) && productCode != "N/A")
                            {
                                if (!productSales.ContainsKey(productCode))
                                    productSales[productCode] = 0;

                                if (int.TryParse(quantity, out var qty))
                                    productSales[productCode] += qty;
                            }
                        }
                    }
                    else
                    {
                        logs.Add($"  ⚠️ OrderDetails NULL veya BOŞ! API'den çekmeyi deniyoruz...");

                        if (int.TryParse(order.OrderId, out var orderId))
                        {
                            var detailsResult = await _tsoftService.GetOrderDetailsByOrderIdAsync(orderId);

                            if (detailsResult.Success && detailsResult.Data != null && detailsResult.Data.Count > 0)
                            {
                                logs.Add($"  ✅ API'den {detailsResult.Data.Count} ürün detayı geldi!");

                                foreach (var detail in detailsResult.Data)
                                {
                                    var productCode = detail.ProductCode ?? detail.ProductId ?? "N/A";
                                    var quantity = detail.Quantity ?? "0";
                                    logs.Add($"     - {productCode}: {quantity} adet");

                                    if (!string.IsNullOrEmpty(productCode) && productCode != "N/A")
                                    {
                                        if (!productSales.ContainsKey(productCode))
                                            productSales[productCode] = 0;

                                        if (int.TryParse(quantity, out var qty))
                                            productSales[productCode] += qty;
                                    }
                                }
                            }
                            else
                            {
                                logs.Add($"  ❌ API'den detay gelmedi!");
                                if (detailsResult.Message != null)
                                {
                                    foreach (var msg in detailsResult.Message)
                                    {
                                        if (msg.Text != null)
                                        {
                                            foreach (var text in msg.Text)
                                            {
                                                logs.Add($"     Hata: {text}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    logs.Add("");
                }

                // 3. Toplamları göster
                logs.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                logs.Add($"📊 TOPLAM {productSales.Count} FARKLI ÜRÜN BULUNDU:");
                logs.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                logs.Add("");

                if (productSales.Count > 0)
                {
                    var topProducts = productSales.OrderByDescending(x => x.Value).Take(5).ToList();

                    logs.Add("🏆 EN ÇOK SATAN 5 ÜRÜN:");
                    int rank = 1;
                    foreach (var (productCode, quantity) in topProducts)
                    {
                        logs.Add($"  {rank}. {productCode}: {quantity} adet");
                        rank++;
                    }

                    ViewBag.TopProducts = topProducts;
                }
                else
                {
                    logs.Add("❌ HİÇ ÜRÜN BULUNAMADI!");
                    logs.Add("");
                    logs.Add("💡 OLASI NEDENLER:");
                    logs.Add("  1. Siparişlerde OrderDetails yok");
                    logs.Add("  2. GetOrderDetailsByOrderIdAsync API'si çalışmıyor");
                    logs.Add("  3. Sipariş detaylarında ProductCode field'ı yok");
                    logs.Add("  4. API yetki sorunu");
                }

                ViewBag.Logs = logs;
            }
            catch (Exception ex)
            {
                logs.Add($"💥 EXCEPTION: {ex.Message}");
                logs.Add($"Stack: {ex.StackTrace}");
                ViewBag.Logs = logs;
            }

            return View();
        }
    }
}