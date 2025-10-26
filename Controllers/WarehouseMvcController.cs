using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TSoftApiClient.DTOs;
using TSoftApiClient.Services;

namespace TSoftApiClient.Controllers
{
    /// <summary>
    /// Depo Yönetimi MVC Controller
    /// </summary>
    [Authorize]
    public class WarehouseMvcController : Controller
    {
        private readonly WarehouseService _warehouseService;
        private readonly TSoftApiService _tsoftService;
        private readonly ILogger<WarehouseMvcController> _logger;

        public WarehouseMvcController(
            WarehouseService warehouseService,
            TSoftApiService tsoftService,
            ILogger<WarehouseMvcController> logger)
        {
            _warehouseService = warehouseService;
            _tsoftService = tsoftService;
            _logger = logger;
        }

        /// <summary>
        /// Depo listesi
        /// </summary>
        [Route("/Warehouses")]
        public IActionResult Index()
        {
            var warehouses = _warehouseService.GetAllWarehouses();
            ViewBag.ActiveCount = warehouses.Count(w => w.IsActive == "1");
            return View("~/Views/Warehouse/Index.cshtml", warehouses);
        }

        /// <summary>
        /// Depo detayı
        /// </summary>
        [Route("/Warehouses/{id}")]
        public IActionResult Details(string id)
        {
            var summary = _warehouseService.GetWarehouseSummary(id);
            return View("~/Views/Warehouse/Details.cshtml", summary);
        }

        /// <summary>
        /// Depo stokları
        /// </summary>
        [Route("/Warehouses/{id}/Stocks")]
        public IActionResult Stocks(string id)
        {
            var warehouse = _warehouseService.GetWarehouseById(id);
            if (warehouse == null)
            {
                TempData["Error"] = "Depo bulunamadı";
                return RedirectToAction("Index");
            }

            var stocks = _warehouseService.GetWarehouseStocks(id);

            ViewBag.Warehouse = warehouse;
            ViewBag.LowStockCount = stocks.Count(s =>
                int.TryParse(s.Quantity, out var qty) &&
                int.TryParse(s.MinStockLevel, out var min) &&
                qty <= min);

            return View("~/Views/Warehouse/Stocks.cshtml", stocks);
        }

        /// <summary>
        /// Stok hareketleri
        /// </summary>
        [Route("/Warehouses/Movements")]
        public IActionResult Movements(string? warehouseId = null, string? productCode = null)
        {
            var movements = _warehouseService.GetStockMovements(warehouseId, productCode);
            var warehouses = _warehouseService.GetAllWarehouses();

            ViewBag.Warehouses = warehouses;
            ViewBag.SelectedWarehouse = warehouseId;
            ViewBag.SelectedProduct = productCode;

            return View("~/Views/Warehouse/Movements.cshtml", movements);
        }

        /// <summary>
        /// Stok hareketi ekleme sayfası
        /// </summary>
        [Route("/Warehouses/Movements/Create")]
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> CreateMovement()
        {
            var warehouses = _warehouseService.GetAllWarehouses();
            var products = await _tsoftService.GetProductsAsync(limit: 100);

            ViewBag.Warehouses = warehouses;
            ViewBag.Products = products.Data ?? new List<Models.Product>();

            return View("~/Views/Warehouse/CreateMovement.cshtml");
        }

        /// <summary>
        /// Stok hareketi ekleme
        /// </summary>
        [Route("/Warehouses/Movements/Create")]
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult CreateMovement(CreateStockMovementDto dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message) = _warehouseService.CreateStockMovement(dto, userId, username);

            if (success)
            {
                TempData["Success"] = message;
                return RedirectToAction("Movements");
            }

            TempData["Error"] = message;
            return RedirectToAction("CreateMovement");
        }

        /// <summary>
        /// Stok sayımları
        /// </summary>
        [Route("/Warehouses/StockCounts")]
        public IActionResult StockCounts(string? warehouseId = null, string? status = null)
        {
            var counts = _warehouseService.GetStockCounts(warehouseId, status);
            var warehouses = _warehouseService.GetAllWarehouses();

            ViewBag.Warehouses = warehouses;
            ViewBag.SelectedWarehouse = warehouseId;
            ViewBag.SelectedStatus = status;

            return View("~/Views/Warehouse/StockCounts.cshtml", counts);
        }

        /// <summary>
        /// Stok sayımı başlatma sayfası
        /// </summary>
        [Route("/Warehouses/StockCounts/Create")]
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult CreateStockCount()
        {
            var warehouses = _warehouseService.GetAllWarehouses();
            ViewBag.Warehouses = warehouses;

            return View("~/Views/Warehouse/CreateStockCount.cshtml");
        }

        /// <summary>
        /// Stok sayımı başlatma
        /// </summary>
        [Route("/Warehouses/StockCounts/Create")]
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult CreateStockCount(CreateStockCountDto dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message, count) = _warehouseService.StartStockCount(dto, userId, username);

            if (success)
            {
                TempData["Success"] = message;
                return RedirectToAction("StockCountDetail", new { id = count!.Id });
            }

            TempData["Error"] = message;
            return RedirectToAction("CreateStockCount");
        }

        /// <summary>
        /// Stok sayımı detayı
        /// </summary>
        [Route("/Warehouses/StockCounts/{id}")]
        public IActionResult StockCountDetail(string id)
        {
            var counts = _warehouseService.GetStockCounts();
            var count = counts.FirstOrDefault(c => c.Id == id);

            if (count == null)
            {
                TempData["Error"] = "Stok sayımı bulunamadı";
                return RedirectToAction("StockCounts");
            }

            return View("~/Views/Warehouse/StockCountDetail.cshtml", count);
        }

        /// <summary>
        /// Stok sayım kalemini güncelle
        /// </summary>
        [Route("/Warehouses/StockCounts/UpdateItem")]
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult UpdateStockCountItem(UpdateStockCountItemDto dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message) = _warehouseService.UpdateStockCountItem(dto, userId, username);

            if (success)
            {
                return Json(new { success = true, message });
            }

            return Json(new { success = false, message });
        }

        /// <summary>
        /// Stok sayımını tamamla
        /// </summary>
        [Route("/Warehouses/StockCounts/{id}/Complete")]
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult CompleteStockCount(string id)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message) = _warehouseService.CompleteStockCount(id, userId, username);

            if (success)
            {
                TempData["Success"] = message;
            }
            else
            {
                TempData["Error"] = message;
            }

            return RedirectToAction("StockCountDetail", new { id });
        }

        /// <summary>
        /// Depo transferleri
        /// </summary>
        [Route("/Warehouses/Transfers")]
        public IActionResult Transfers(string? warehouseId = null, string? status = null)
        {
            var transfers = _warehouseService.GetWarehouseTransfers(warehouseId, status);
            var warehouses = _warehouseService.GetAllWarehouses();

            ViewBag.Warehouses = warehouses;
            ViewBag.SelectedWarehouse = warehouseId;
            ViewBag.SelectedStatus = status;

            return View("~/Views/Warehouse/Transfers.cshtml", transfers);
        }

        /// <summary>
        /// Transfer oluşturma sayfası
        /// </summary>
        [Route("/Warehouses/Transfers/Create")]
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> CreateTransfer()
        {
            var warehouses = _warehouseService.GetAllWarehouses();
            var products = await _tsoftService.GetProductsAsync(limit: 100);

            ViewBag.Warehouses = warehouses;
            ViewBag.Products = products.Data ?? new List<Models.Product>();

            return View("~/Views/Warehouse/CreateTransfer.cshtml");
        }

        /// <summary>
        /// Transfer oluşturma
        /// </summary>
        [Route("/Warehouses/Transfers/Create")]
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult CreateTransfer([FromBody] CreateWarehouseTransferDto dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message, transfer) = _warehouseService.CreateWarehouseTransfer(dto, userId, username);

            if (success)
            {
                return Json(new { success = true, message, transferId = transfer!.Id });
            }

            return Json(new { success = false, message });
        }

        /// <summary>
        /// Transfer gönder
        /// </summary>
        [Route("/Warehouses/Transfers/{id}/Ship")]
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult ShipTransfer(string id)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message) = _warehouseService.ShipTransfer(id, userId, username);

            if (success)
            {
                TempData["Success"] = message;
            }
            else
            {
                TempData["Error"] = message;
            }

            return RedirectToAction("Transfers");
        }

        /// <summary>
        /// Transfer teslim al
        /// </summary>
        [Route("/Warehouses/Transfers/{id}/Receive")]
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult ReceiveTransfer(string id, [FromBody] Dictionary<string, int> receivedQuantities)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message) = _warehouseService.ReceiveTransfer(id, receivedQuantities, userId, username);

            if (success)
            {
                return Json(new { success = true, message });
            }

            return Json(new { success = false, message });
        }
    }
}