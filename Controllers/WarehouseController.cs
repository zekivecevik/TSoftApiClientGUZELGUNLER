using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TSoftApiClient.DTOs;
using TSoftApiClient.Services;

namespace TSoftApiClient.Controllers
{
    /// <summary>
    /// Depo Yönetimi API Controller
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class WarehouseController : ControllerBase
    {
        private readonly WarehouseService _warehouseService;
        private readonly ILogger<WarehouseController> _logger;

        public WarehouseController(
            WarehouseService warehouseService,
            ILogger<WarehouseController> logger)
        {
            _warehouseService = warehouseService;
            _logger = logger;
        }

        #region Depo Yönetimi

        /// <summary>
        /// Tüm depoları listele
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetAllWarehouses([FromQuery] bool activeOnly = false)
        {
            var warehouses = activeOnly
                ? _warehouseService.GetActiveWarehouses()
                : _warehouseService.GetAllWarehouses();

            return Ok(new { success = true, data = warehouses });
        }

        /// <summary>
        /// ID'ye göre depo getir
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetWarehouseById(string id)
        {
            var warehouse = _warehouseService.GetWarehouseById(id);

            if (warehouse == null)
                return NotFound(new { success = false, message = "Depo bulunamadı" });

            return Ok(new { success = true, data = warehouse });
        }

        /// <summary>
        /// Yeni depo oluştur (Admin/Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CreateWarehouse([FromBody] CreateWarehouseDto dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message, warehouse) = _warehouseService.CreateWarehouse(dto, userId, username);

            if (!success)
                return BadRequest(new { success = false, message });

            return CreatedAtAction(nameof(GetWarehouseById), new { id = warehouse!.WarehouseId },
                new { success = true, message, data = warehouse });
        }

        /// <summary>
        /// Depo özet raporu
        /// </summary>
        [HttpGet("{id}/summary")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetWarehouseSummary(string id)
        {
            var summary = _warehouseService.GetWarehouseSummary(id);
            return Ok(new { success = true, data = summary });
        }

        #endregion

        #region Stok Yönetimi

        /// <summary>
        /// Depodaki tüm stokları listele
        /// </summary>
        [HttpGet("{id}/stocks")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetWarehouseStocks(string id)
        {
            var stocks = _warehouseService.GetWarehouseStocks(id);
            return Ok(new { success = true, data = stocks });
        }

        /// <summary>
        /// Ürünün tüm depolardaki stoklarını getir
        /// </summary>
        [HttpGet("stocks/product/{productCode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetProductStockInAllWarehouses(string productCode)
        {
            var stocks = _warehouseService.GetProductStockInAllWarehouses(productCode);
            return Ok(new { success = true, data = stocks });
        }

        /// <summary>
        /// Kritik stokları listele
        /// </summary>
        [HttpGet("stocks/low-stock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetLowStockItems([FromQuery] string? warehouseId = null)
        {
            var stocks = _warehouseService.GetLowStockItems(warehouseId);
            return Ok(new { success = true, data = stocks });
        }

        /// <summary>
        /// Stok hareketi kaydet (Admin/Manager only)
        /// </summary>
        [HttpPost("stocks/movements")]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CreateStockMovement([FromBody] CreateStockMovementDto dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message) = _warehouseService.CreateStockMovement(dto, userId, username);

            if (!success)
                return BadRequest(new { success = false, message });

            return CreatedAtAction(nameof(GetStockMovements), null,
                new { success = true, message });
        }

        /// <summary>
        /// Stok hareketlerini listele
        /// </summary>
        [HttpGet("stocks/movements")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetStockMovements(
            [FromQuery] string? warehouseId = null,
            [FromQuery] string? productCode = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int limit = 100)
        {
            var movements = _warehouseService.GetStockMovements(
                warehouseId, productCode, startDate, endDate, limit
            );

            return Ok(new { success = true, data = movements });
        }

        #endregion

        #region Stok Sayımı

        /// <summary>
        /// Stok sayımı başlat (Admin/Manager only)
        /// </summary>
        [HttpPost("stock-counts")]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult StartStockCount([FromBody] CreateStockCountDto dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message, count) = _warehouseService.StartStockCount(dto, userId, username);

            if (!success)
                return BadRequest(new { success = false, message });

            return CreatedAtAction(nameof(GetStockCounts), null,
                new { success = true, message, data = count });
        }

        /// <summary>
        /// Stok sayımlarını listele
        /// </summary>
        [HttpGet("stock-counts")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetStockCounts(
            [FromQuery] string? warehouseId = null,
            [FromQuery] string? status = null)
        {
            var counts = _warehouseService.GetStockCounts(warehouseId, status);
            return Ok(new { success = true, data = counts });
        }

        /// <summary>
        /// Stok sayım kalemini güncelle (Admin/Manager only)
        /// </summary>
        [HttpPut("stock-counts/items")]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult UpdateStockCountItem([FromBody] UpdateStockCountItemDto dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message) = _warehouseService.UpdateStockCountItem(dto, userId, username);

            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message });
        }

        /// <summary>
        /// Stok sayımını tamamla (Admin/Manager only)
        /// </summary>
        [HttpPost("stock-counts/{id}/complete")]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CompleteStockCount(string id)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message) = _warehouseService.CompleteStockCount(id, userId, username);

            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message });
        }

        #endregion

        #region Depo Transferi

        /// <summary>
        /// Depo transferi oluştur (Admin/Manager only)
        /// </summary>
        [HttpPost("transfers")]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CreateWarehouseTransfer([FromBody] CreateWarehouseTransferDto dto)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message, transfer) = _warehouseService.CreateWarehouseTransfer(dto, userId, username);

            if (!success)
                return BadRequest(new { success = false, message });

            return CreatedAtAction(nameof(GetWarehouseTransfers), null,
                new { success = true, message, data = transfer });
        }

        /// <summary>
        /// Transferleri listele
        /// </summary>
        [HttpGet("transfers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetWarehouseTransfers(
            [FromQuery] string? warehouseId = null,
            [FromQuery] string? status = null)
        {
            var transfers = _warehouseService.GetWarehouseTransfers(warehouseId, status);
            return Ok(new { success = true, data = transfers });
        }

        /// <summary>
        /// Transfer gönder (Admin/Manager only)
        /// </summary>
        [HttpPost("transfers/{id}/ship")]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ShipTransfer(string id)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message) = _warehouseService.ShipTransfer(id, userId, username);

            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message });
        }

        /// <summary>
        /// Transfer teslim al (Admin/Manager only)
        /// </summary>
        [HttpPost("transfers/{id}/receive")]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ReceiveTransfer(string id, [FromBody] Dictionary<string, int> receivedQuantities)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "Unknown";

            var (success, message) = _warehouseService.ReceiveTransfer(id, receivedQuantities, userId, username);

            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message });
        }

        #endregion
    }
}