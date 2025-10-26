namespace TSoftApiClient.DTOs
{
    /// <summary>
    /// Stok hareketi ekleme DTO
    /// </summary>
    public class CreateStockMovementDto
    {
        public required string MovementType { get; set; } // IN, OUT, TRANSFER, ADJUSTMENT
        public required string WarehouseId { get; set; }
        public required string ProductCode { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Stok sayımı başlatma DTO
    /// </summary>
    public class CreateStockCountDto
    {
        public required string WarehouseId { get; set; }
        public string? Notes { get; set; }
        public List<string>? ProductCodes { get; set; } // Null ise tüm ürünler
    }

    /// <summary>
    /// Stok sayım kalemi güncelleme DTO
    /// </summary>
    public class UpdateStockCountItemDto
    {
        public required string StockCountId { get; set; }
        public required string ProductCode { get; set; }
        public int CountedQuantity { get; set; }
        public string? Location { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Depo transferi oluşturma DTO
    /// </summary>
    public class CreateWarehouseTransferDto
    {
        public required string FromWarehouseId { get; set; }
        public required string ToWarehouseId { get; set; }
        public required List<TransferItemDto> Items { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Transfer kalemi DTO
    /// </summary>
    public class TransferItemDto
    {
        public required string ProductCode { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Stok seviyesi güncelleme DTO
    /// </summary>
    public class UpdateStockLevelDto
    {
        public required string ProductCode { get; set; }
        public required string WarehouseId { get; set; }
        public int NewQuantity { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Depo oluşturma DTO
    /// </summary>
    public class CreateWarehouseDto
    {
        public required string WarehouseCode { get; set; }
        public required string WarehouseName { get; set; }
        public required string City { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? ManagerName { get; set; }
        public int? Capacity { get; set; }
    }

    /// <summary>
    /// Toplu ürün transfer DTO
    /// </summary>
    public class BulkTransferDto
    {
        public required string FromWarehouseId { get; set; }
        public required string ToWarehouseId { get; set; }
        public required List<TransferItemDto> Items { get; set; }
        public string? Notes { get; set; }
        public bool AutoApprove { get; set; } = false;
    }
}