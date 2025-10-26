namespace TSoftApiClient.Models
{
    /// <summary>
    /// Depo modeli
    /// </summary>
    public class Warehouse
    {
        public string? WarehouseId { get; set; }
        public string? WarehouseCode { get; set; }
        public string? WarehouseName { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? ManagerName { get; set; }
        public string? IsActive { get; set; }
        public string? Capacity { get; set; }
        public string? CurrentUsage { get; set; }
        public string? CreatedDate { get; set; }
        public string? UpdateDate { get; set; }
    }

    /// <summary>
    /// Depo stok modeli
    /// </summary>
    public class WarehouseStock
    {
        public string? Id { get; set; }
        public string? WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public string? ProductId { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public string? Quantity { get; set; }
        public string? MinStockLevel { get; set; }
        public string? MaxStockLevel { get; set; }
        public string? Location { get; set; } // Raf konumu
        public string? LastUpdateDate { get; set; }
    }

    /// <summary>
    /// Stok hareketi modeli
    /// </summary>
    public class StockMovement
    {
        public string? Id { get; set; }
        public string? MovementDate { get; set; }
        public string? MovementType { get; set; } // IN, OUT, TRANSFER, ADJUSTMENT
        public string? WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public string? ProductId { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public string? Quantity { get; set; }
        public string? UnitPrice { get; set; }
        public string? TotalPrice { get; set; }
        public string? ReferenceNumber { get; set; } // Sipariş no, transfer no vb.
        public string? Notes { get; set; }
        public string? UserId { get; set; }
        public string? Username { get; set; }
    }

    /// <summary>
    /// Stok sayım modeli
    /// </summary>
    public class StockCount
    {
        public string? Id { get; set; }
        public string? CountNumber { get; set; }
        public string? CountDate { get; set; }
        public string? WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public string? Status { get; set; } // PENDING, IN_PROGRESS, COMPLETED, CANCELLED
        public string? CountedBy { get; set; }
        public string? ApprovedBy { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? Notes { get; set; }
        public List<StockCountItem>? Items { get; set; }
    }

    /// <summary>
    /// Stok sayım kalemi
    /// </summary>
    public class StockCountItem
    {
        public string? Id { get; set; }
        public string? StockCountId { get; set; }
        public string? ProductId { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public string? SystemQuantity { get; set; } // Sistemdeki miktar
        public string? CountedQuantity { get; set; } // Sayılan miktar
        public string? Difference { get; set; } // Fark
        public string? Location { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Depo transferi modeli
    /// </summary>
    public class WarehouseTransfer
    {
        public string? Id { get; set; }
        public string? TransferNumber { get; set; }
        public string? TransferDate { get; set; }
        public string? FromWarehouseId { get; set; }
        public string? FromWarehouseName { get; set; }
        public string? ToWarehouseId { get; set; }
        public string? ToWarehouseName { get; set; }
        public string? Status { get; set; } // PENDING, SHIPPED, RECEIVED, CANCELLED
        public string? RequestedBy { get; set; }
        public string? ApprovedBy { get; set; }
        public string? ShippedDate { get; set; }
        public string? ReceivedDate { get; set; }
        public string? Notes { get; set; }
        public List<WarehouseTransferItem>? Items { get; set; }
    }

    /// <summary>
    /// Depo transfer kalemi
    /// </summary>
    public class WarehouseTransferItem
    {
        public string? Id { get; set; }
        public string? TransferId { get; set; }
        public string? ProductId { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public string? RequestedQuantity { get; set; }
        public string? ShippedQuantity { get; set; }
        public string? ReceivedQuantity { get; set; }
        public string? Notes { get; set; }
    }
}