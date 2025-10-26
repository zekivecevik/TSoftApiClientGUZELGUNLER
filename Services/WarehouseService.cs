using TSoftApiClient.Models;
using TSoftApiClient.DTOs;

namespace TSoftApiClient.Services
{
    /// <summary>
    /// Depo ve Stok Yönetimi Servisi
    /// In-memory implementation - Production'da database kullanılacak
    /// </summary>
    public class WarehouseService
    {
        private readonly ILogger<WarehouseService> _logger;
        private readonly AuditLogService _auditLogService;
        private readonly TSoftApiService _tsoftService;

        // In-Memory Storage
        private static List<Warehouse> _warehouses = new();
        private static List<WarehouseStock> _warehouseStocks = new();
        private static List<StockMovement> _stockMovements = new();
        private static List<StockCount> _stockCounts = new();
        private static List<WarehouseTransfer> _transfers = new();

        private static int _nextId = 1;

        public WarehouseService(
            ILogger<WarehouseService> logger,
            AuditLogService auditLogService,
            TSoftApiService tsoftService)
        {
            _logger = logger;
            _auditLogService = auditLogService;
            _tsoftService = tsoftService;

            // Demo veriler
            InitializeDemoData();
        }

        #region Depo Yönetimi

        /// <summary>
        /// Tüm depoları listele
        /// </summary>
        public List<Warehouse> GetAllWarehouses()
        {
            return _warehouses.OrderBy(w => w.WarehouseName).ToList();
        }

        /// <summary>
        /// Aktif depoları listele
        /// </summary>
        public List<Warehouse> GetActiveWarehouses()
        {
            return _warehouses
                .Where(w => w.IsActive == "1")
                .OrderBy(w => w.WarehouseName)
                .ToList();
        }

        /// <summary>
        /// ID'ye göre depo getir
        /// </summary>
        public Warehouse? GetWarehouseById(string warehouseId)
        {
            return _warehouses.FirstOrDefault(w => w.WarehouseId == warehouseId);
        }

        /// <summary>
        /// Yeni depo oluştur
        /// </summary>
        public (bool success, string message, Warehouse? warehouse) CreateWarehouse(
            CreateWarehouseDto dto, int userId, string username)
        {
            try
            {
                if (_warehouses.Any(w => w.WarehouseCode == dto.WarehouseCode))
                {
                    return (false, "Bu depo kodu zaten kullanılıyor", null);
                }

                var warehouse = new Warehouse
                {
                    WarehouseId = _nextId++.ToString(),
                    WarehouseCode = dto.WarehouseCode,
                    WarehouseName = dto.WarehouseName,
                    City = dto.City,
                    Address = dto.Address,
                    Phone = dto.Phone,
                    Email = dto.Email,
                    ManagerName = dto.ManagerName,
                    IsActive = "1",
                    Capacity = dto.Capacity?.ToString(),
                    CurrentUsage = "0",
                    CreatedDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                };

                _warehouses.Add(warehouse);

                _auditLogService.LogCreate(
                    userId, username, "Warehouse", warehouse.WarehouseId,
                    $"Depo oluşturuldu: {warehouse.WarehouseName}"
                );

                _logger.LogInformation("✅ Warehouse created: {Code} - {Name}",
                    warehouse.WarehouseCode, warehouse.WarehouseName);

                return (true, "Depo başarıyla oluşturuldu", warehouse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error creating warehouse");
                return (false, "Depo oluşturulurken hata oluştu", null);
            }
        }

        #endregion

        #region Stok Yönetimi

        /// <summary>
        /// Depodaki tüm stokları getir
        /// </summary>
        public List<WarehouseStock> GetWarehouseStocks(string warehouseId)
        {
            return _warehouseStocks
                .Where(ws => ws.WarehouseId == warehouseId)
                .OrderBy(ws => ws.ProductName)
                .ToList();
        }

        /// <summary>
        /// Ürünün tüm depolardaki stoğunu getir
        /// </summary>
        public List<WarehouseStock> GetProductStockInAllWarehouses(string productCode)
        {
            return _warehouseStocks
                .Where(ws => ws.ProductCode == productCode)
                .ToList();
        }

        /// <summary>
        /// Kritik stokları getir (min seviyenin altında)
        /// </summary>
        public List<WarehouseStock> GetLowStockItems(string? warehouseId = null)
        {
            var query = _warehouseStocks.AsQueryable();

            if (!string.IsNullOrEmpty(warehouseId))
            {
                query = query.Where(ws => ws.WarehouseId == warehouseId);
            }

            return query
                .Where(ws =>
                {
                    if (!int.TryParse(ws.Quantity, out var qty)) return false;
                    if (!int.TryParse(ws.MinStockLevel, out var min)) return false;
                    return qty <= min;
                })
                .OrderBy(ws => ws.Quantity)
                .ToList();
        }

        /// <summary>
        /// Stok hareketi kaydet
        /// </summary>
        public (bool success, string message) CreateStockMovement(
            CreateStockMovementDto dto, int userId, string username)
        {
            try
            {
                var warehouse = GetWarehouseById(dto.WarehouseId);
                if (warehouse == null)
                {
                    return (false, "Depo bulunamadı");
                }

                var movement = new StockMovement
                {
                    Id = _nextId++.ToString(),
                    MovementDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    MovementType = dto.MovementType,
                    WarehouseId = dto.WarehouseId,
                    WarehouseName = warehouse.WarehouseName,
                    ProductCode = dto.ProductCode,
                    Quantity = dto.Quantity.ToString(),
                    UnitPrice = dto.UnitPrice?.ToString("F2"),
                    TotalPrice = (dto.UnitPrice.HasValue ? (dto.UnitPrice.Value * dto.Quantity).ToString("F2") : null),
                    ReferenceNumber = dto.ReferenceNumber,
                    Notes = dto.Notes,
                    UserId = userId.ToString(),
                    Username = username
                };

                _stockMovements.Add(movement);

                // Stok seviyesini güncelle
                UpdateWarehouseStock(dto.WarehouseId, dto.ProductCode, dto.Quantity, dto.MovementType);

                _auditLogService.LogCreate(
                    userId, username, "StockMovement", movement.Id,
                    $"Stok hareketi: {dto.MovementType} - {dto.ProductCode} - {dto.Quantity}"
                );

                _logger.LogInformation("✅ Stock movement created: {Type} - {Product} - {Qty}",
                    dto.MovementType, dto.ProductCode, dto.Quantity);

                return (true, "Stok hareketi kaydedildi");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error creating stock movement");
                return (false, "Stok hareketi kaydedilirken hata oluştu");
            }
        }

        /// <summary>
        /// Depo stoğunu güncelle (dahili method)
        /// </summary>
        private void UpdateWarehouseStock(string warehouseId, string productCode, int quantity, string movementType)
        {
            var stock = _warehouseStocks.FirstOrDefault(
                ws => ws.WarehouseId == warehouseId && ws.ProductCode == productCode
            );

            if (stock == null)
            {
                // İlk kayıt
                stock = new WarehouseStock
                {
                    Id = _nextId++.ToString(),
                    WarehouseId = warehouseId,
                    ProductCode = productCode,
                    Quantity = "0",
                    MinStockLevel = "10",
                    MaxStockLevel = "1000",
                    LastUpdateDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                };
                _warehouseStocks.Add(stock);
            }

            var currentQty = int.TryParse(stock.Quantity, out var qty) ? qty : 0;

            switch (movementType)
            {
                case "IN":
                case "ADJUSTMENT_IN":
                    currentQty += quantity;
                    break;
                case "OUT":
                case "ADJUSTMENT_OUT":
                    currentQty -= quantity;
                    break;
            }

            stock.Quantity = Math.Max(0, currentQty).ToString();
            stock.LastUpdateDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// Stok hareketlerini getir
        /// </summary>
        public List<StockMovement> GetStockMovements(
            string? warehouseId = null,
            string? productCode = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int limit = 100)
        {
            var query = _stockMovements.AsQueryable();

            if (!string.IsNullOrEmpty(warehouseId))
            {
                query = query.Where(sm => sm.WarehouseId == warehouseId);
            }

            if (!string.IsNullOrEmpty(productCode))
            {
                query = query.Where(sm => sm.ProductCode == productCode);
            }

            if (startDate.HasValue)
            {
                query = query.Where(sm =>
                    DateTime.TryParse(sm.MovementDate, out var date) && date >= startDate.Value
                );
            }

            if (endDate.HasValue)
            {
                query = query.Where(sm =>
                    DateTime.TryParse(sm.MovementDate, out var date) && date <= endDate.Value
                );
            }

            return query
                .OrderByDescending(sm => sm.MovementDate)
                .Take(limit)
                .ToList();
        }

        #endregion

        #region Stok Sayımı

        /// <summary>
        /// Yeni stok sayımı başlat
        /// </summary>
        public (bool success, string message, StockCount? count) StartStockCount(
            CreateStockCountDto dto, int userId, string username)
        {
            try
            {
                var warehouse = GetWarehouseById(dto.WarehouseId);
                if (warehouse == null)
                {
                    return (false, "Depo bulunamadı", null);
                }

                // Sayım numarası oluştur
                var countNumber = $"SC-{DateTime.UtcNow:yyyyMMdd}-{_nextId:D4}";

                var stockCount = new StockCount
                {
                    Id = _nextId++.ToString(),
                    CountNumber = countNumber,
                    CountDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    WarehouseId = dto.WarehouseId,
                    WarehouseName = warehouse.WarehouseName,
                    Status = "IN_PROGRESS",
                    CountedBy = username,
                    StartDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    Notes = dto.Notes,
                    Items = new List<StockCountItem>()
                };

                // Sayım kalemlerini oluştur
                var stocks = GetWarehouseStocks(dto.WarehouseId);

                if (dto.ProductCodes != null && dto.ProductCodes.Count > 0)
                {
                    stocks = stocks.Where(s => dto.ProductCodes.Contains(s.ProductCode)).ToList();
                }

                foreach (var stock in stocks)
                {
                    var item = new StockCountItem
                    {
                        Id = _nextId++.ToString(),
                        StockCountId = stockCount.Id,
                        ProductCode = stock.ProductCode,
                        ProductName = stock.ProductName,
                        SystemQuantity = stock.Quantity,
                        CountedQuantity = "0",
                        Difference = "0",
                        Location = stock.Location
                    };
                    stockCount.Items.Add(item);
                }

                _stockCounts.Add(stockCount);

                _auditLogService.LogCreate(
                    userId, username, "StockCount", stockCount.Id,
                    $"Stok sayımı başlatıldı: {countNumber}"
                );

                _logger.LogInformation("✅ Stock count started: {Number}", countNumber);

                return (true, "Stok sayımı başlatıldı", stockCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error starting stock count");
                return (false, "Stok sayımı başlatılırken hata oluştu", null);
            }
        }

        /// <summary>
        /// Stok sayım kalemini güncelle
        /// </summary>
        public (bool success, string message) UpdateStockCountItem(
            UpdateStockCountItemDto dto, int userId, string username)
        {
            try
            {
                var stockCount = _stockCounts.FirstOrDefault(sc => sc.Id == dto.StockCountId);
                if (stockCount == null)
                {
                    return (false, "Stok sayımı bulunamadı");
                }

                if (stockCount.Status != "IN_PROGRESS")
                {
                    return (false, "Sadece devam eden sayımlar güncellenebilir");
                }

                var item = stockCount.Items?.FirstOrDefault(i => i.ProductCode == dto.ProductCode);
                if (item == null)
                {
                    return (false, "Ürün sayımda bulunamadı");
                }

                item.CountedQuantity = dto.CountedQuantity.ToString();

                var systemQty = int.TryParse(item.SystemQuantity, out var sysQty) ? sysQty : 0;
                var difference = dto.CountedQuantity - systemQty;
                item.Difference = difference.ToString();

                if (!string.IsNullOrEmpty(dto.Location))
                {
                    item.Location = dto.Location;
                }

                if (!string.IsNullOrEmpty(dto.Notes))
                {
                    item.Notes = dto.Notes;
                }

                _auditLogService.LogUpdate(
                    userId, username, "StockCountItem", item.Id,
                    $"Sayım güncellendi: {dto.ProductCode} - {dto.CountedQuantity}"
                );

                return (true, "Sayım kaydedildi");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error updating stock count item");
                return (false, "Sayım güncellenirken hata oluştu");
            }
        }

        /// <summary>
        /// Stok sayımını tamamla
        /// </summary>
        public (bool success, string message) CompleteStockCount(
            string stockCountId, int userId, string username)
        {
            try
            {
                var stockCount = _stockCounts.FirstOrDefault(sc => sc.Id == stockCountId);
                if (stockCount == null)
                {
                    return (false, "Stok sayımı bulunamadı");
                }

                if (stockCount.Status != "IN_PROGRESS")
                {
                    return (false, "Sadece devam eden sayımlar tamamlanabilir");
                }

                stockCount.Status = "COMPLETED";
                stockCount.EndDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                stockCount.ApprovedBy = username;

                // Farklılıkları stok hareketlerine aktar
                if (stockCount.Items != null)
                {
                    foreach (var item in stockCount.Items.Where(i => i.Difference != "0"))
                    {
                        if (!int.TryParse(item.Difference, out var diff)) continue;

                        var movementType = diff > 0 ? "ADJUSTMENT_IN" : "ADJUSTMENT_OUT";
                        var quantity = Math.Abs(diff);

                        CreateStockMovement(new CreateStockMovementDto
                        {
                            MovementType = movementType,
                            WarehouseId = stockCount.WarehouseId!,
                            ProductCode = item.ProductCode!,
                            Quantity = quantity,
                            ReferenceNumber = stockCount.CountNumber,
                            Notes = $"Stok sayımı düzeltmesi: {item.Notes}"
                        }, userId, username);
                    }
                }

                _auditLogService.LogUpdate(
                    userId, username, "StockCount", stockCountId,
                    $"Stok sayımı tamamlandı: {stockCount.CountNumber}"
                );

                _logger.LogInformation("✅ Stock count completed: {Number}", stockCount.CountNumber);

                return (true, "Stok sayımı tamamlandı ve farklar otomatik olarak düzeltildi");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error completing stock count");
                return (false, "Stok sayımı tamamlanırken hata oluştu");
            }
        }

        /// <summary>
        /// Tüm stok sayımlarını listele
        /// </summary>
        public List<StockCount> GetStockCounts(string? warehouseId = null, string? status = null)
        {
            var query = _stockCounts.AsQueryable();

            if (!string.IsNullOrEmpty(warehouseId))
            {
                query = query.Where(sc => sc.WarehouseId == warehouseId);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(sc => sc.Status == status);
            }

            return query
                .OrderByDescending(sc => sc.CountDate)
                .ToList();
        }

        #endregion

        #region Depo Transferi

        /// <summary>
        /// Depo transferi oluştur
        /// </summary>
        public (bool success, string message, WarehouseTransfer? transfer) CreateWarehouseTransfer(
            CreateWarehouseTransferDto dto, int userId, string username)
        {
            try
            {
                var fromWarehouse = GetWarehouseById(dto.FromWarehouseId);
                var toWarehouse = GetWarehouseById(dto.ToWarehouseId);

                if (fromWarehouse == null || toWarehouse == null)
                {
                    return (false, "Kaynak veya hedef depo bulunamadı", null);
                }

                if (dto.FromWarehouseId == dto.ToWarehouseId)
                {
                    return (false, "Aynı depolar arası transfer yapılamaz", null);
                }

                // Transfer numarası oluştur
                var transferNumber = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{_nextId:D4}";

                var transfer = new WarehouseTransfer
                {
                    Id = _nextId++.ToString(),
                    TransferNumber = transferNumber,
                    TransferDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    FromWarehouseId = dto.FromWarehouseId,
                    FromWarehouseName = fromWarehouse.WarehouseName,
                    ToWarehouseId = dto.ToWarehouseId,
                    ToWarehouseName = toWarehouse.WarehouseName,
                    Status = "PENDING",
                    RequestedBy = username,
                    Notes = dto.Notes,
                    Items = new List<WarehouseTransferItem>()
                };

                // Transfer kalemlerini oluştur
                foreach (var itemDto in dto.Items)
                {
                    var item = new WarehouseTransferItem
                    {
                        Id = _nextId++.ToString(),
                        TransferId = transfer.Id,
                        ProductCode = itemDto.ProductCode,
                        RequestedQuantity = itemDto.Quantity.ToString(),
                        ShippedQuantity = "0",
                        ReceivedQuantity = "0",
                        Notes = itemDto.Notes
                    };
                    transfer.Items.Add(item);
                }

                _transfers.Add(transfer);

                _auditLogService.LogCreate(
                    userId, username, "WarehouseTransfer", transfer.Id,
                    $"Transfer oluşturuldu: {transferNumber} ({fromWarehouse.WarehouseName} → {toWarehouse.WarehouseName})"
                );

                _logger.LogInformation("✅ Warehouse transfer created: {Number}", transferNumber);

                return (true, "Transfer oluşturuldu", transfer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error creating warehouse transfer");
                return (false, "Transfer oluşturulurken hata oluştu", null);
            }
        }

        /// <summary>
        /// Transfer gönder (kaynak depodan stok düş)
        /// </summary>
        public (bool success, string message) ShipTransfer(
            string transferId, int userId, string username)
        {
            try
            {
                var transfer = _transfers.FirstOrDefault(t => t.Id == transferId);
                if (transfer == null)
                {
                    return (false, "Transfer bulunamadı");
                }

                if (transfer.Status != "PENDING")
                {
                    return (false, "Sadece bekleyen transferler gönderilebilir");
                }

                // Stok kontrolü
                foreach (var item in transfer.Items!)
                {
                    var stock = _warehouseStocks.FirstOrDefault(
                        ws => ws.WarehouseId == transfer.FromWarehouseId && ws.ProductCode == item.ProductCode
                    );

                    var requestedQty = int.TryParse(item.RequestedQuantity, out var qty) ? qty : 0;
                    var availableQty = stock != null && int.TryParse(stock.Quantity, out var avl) ? avl : 0;

                    if (availableQty < requestedQty)
                    {
                        return (false, $"Yetersiz stok: {item.ProductCode} (Mevcut: {availableQty}, İstenen: {requestedQty})");
                    }
                }

                // Stokları düş ve hareketleri kaydet
                foreach (var item in transfer.Items!)
                {
                    var requestedQty = int.TryParse(item.RequestedQuantity, out var qty) ? qty : 0;

                    item.ShippedQuantity = requestedQty.ToString();

                    // Kaynak depodan çıkış
                    CreateStockMovement(new CreateStockMovementDto
                    {
                        MovementType = "TRANSFER_OUT",
                        WarehouseId = transfer.FromWarehouseId!,
                        ProductCode = item.ProductCode!,
                        Quantity = requestedQty,
                        ReferenceNumber = transfer.TransferNumber,
                        Notes = $"Transfer: → {transfer.ToWarehouseName}"
                    }, userId, username);
                }

                transfer.Status = "SHIPPED";
                transfer.ApprovedBy = username;
                transfer.ShippedDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                _auditLogService.LogUpdate(
                    userId, username, "WarehouseTransfer", transferId,
                    $"Transfer gönderildi: {transfer.TransferNumber}"
                );

                return (true, "Transfer gönderildi");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error shipping transfer");
                return (false, "Transfer gönderilirken hata oluştu");
            }
        }

        /// <summary>
        /// Transfer al (hedef depoya stok ekle)
        /// </summary>
        public (bool success, string message) ReceiveTransfer(
            string transferId, Dictionary<string, int> receivedQuantities, int userId, string username)
        {
            try
            {
                var transfer = _transfers.FirstOrDefault(t => t.Id == transferId);
                if (transfer == null)
                {
                    return (false, "Transfer bulunamadı");
                }

                if (transfer.Status != "SHIPPED")
                {
                    return (false, "Sadece gönderilmiş transferler teslim alınabilir");
                }

                // Hedef depoya stok ekle
                foreach (var item in transfer.Items!)
                {
                    var receivedQty = receivedQuantities.ContainsKey(item.ProductCode!)
                        ? receivedQuantities[item.ProductCode!]
                        : int.TryParse(item.ShippedQuantity, out var shipped) ? shipped : 0;

                    item.ReceivedQuantity = receivedQty.ToString();

                    // Hedef depoya giriş
                    CreateStockMovement(new CreateStockMovementDto
                    {
                        MovementType = "TRANSFER_IN",
                        WarehouseId = transfer.ToWarehouseId!,
                        ProductCode = item.ProductCode!,
                        Quantity = receivedQty,
                        ReferenceNumber = transfer.TransferNumber,
                        Notes = $"Transfer: {transfer.FromWarehouseName} →"
                    }, userId, username);
                }

                transfer.Status = "RECEIVED";
                transfer.ReceivedDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                _auditLogService.LogUpdate(
                    userId, username, "WarehouseTransfer", transferId,
                    $"Transfer teslim alındı: {transfer.TransferNumber}"
                );

                return (true, "Transfer teslim alındı");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error receiving transfer");
                return (false, "Transfer teslim alınırken hata oluştu");
            }
        }

        /// <summary>
        /// Tüm transferleri listele
        /// </summary>
        public List<WarehouseTransfer> GetWarehouseTransfers(
            string? warehouseId = null, string? status = null)
        {
            var query = _transfers.AsQueryable();

            if (!string.IsNullOrEmpty(warehouseId))
            {
                query = query.Where(t =>
                    t.FromWarehouseId == warehouseId || t.ToWarehouseId == warehouseId
                );
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status == status);
            }

            return query
                .OrderByDescending(t => t.TransferDate)
                .ToList();
        }

        #endregion

        #region Demo Data

        private void InitializeDemoData()
        {
            if (_warehouses.Count > 0) return;

            _logger.LogInformation("🔧 Initializing demo warehouse data...");

            // Demo depolar
            _warehouses.AddRange(new[]
            {
                new Warehouse
                {
                    WarehouseId = "1",
                    WarehouseCode = "WH-IST",
                    WarehouseName = "İstanbul Ana Depo",
                    City = "İstanbul",
                    Address = "Ümraniye, İstanbul",
                    IsActive = "1",
                    Capacity = "10000",
                    CurrentUsage = "3500",
                    ManagerName = "Ahmet Yılmaz",
                    Phone = "0212 555 0101",
                    Email = "istanbul@warehouse.com",
                    CreatedDate = DateTime.UtcNow.AddMonths(-12).ToString("yyyy-MM-dd")
                },
                new Warehouse
                {
                    WarehouseId = "2",
                    WarehouseCode = "WH-ANK",
                    WarehouseName = "Ankara Bölge Deposu",
                    City = "Ankara",
                    Address = "Sincan, Ankara",
                    IsActive = "1",
                    Capacity = "5000",
                    CurrentUsage = "1200",
                    ManagerName = "Mehmet Kaya",
                    Phone = "0312 555 0202",
                    Email = "ankara@warehouse.com",
                    CreatedDate = DateTime.UtcNow.AddMonths(-8).ToString("yyyy-MM-dd")
                },
                new Warehouse
                {
                    WarehouseId = "3",
                    WarehouseCode = "WH-IZM",
                    WarehouseName = "İzmir Liman Deposu",
                    City = "İzmir",
                    Address = "Karşıyaka, İzmir",
                    IsActive = "1",
                    Capacity = "8000",
                    CurrentUsage = "2800",
                    ManagerName = "Ayşe Demir",
                    Phone = "0232 555 0303",
                    Email = "izmir@warehouse.com",
                    CreatedDate = DateTime.UtcNow.AddMonths(-6).ToString("yyyy-MM-dd")
                }
            });

            _nextId = 4;

            _logger.LogInformation("✅ Demo warehouse data initialized: {Count} warehouses", _warehouses.Count);
        }

        #endregion

        #region Raporlama

        /// <summary>
        /// Depo özet raporu
        /// </summary>
        public object GetWarehouseSummary(string warehouseId)
        {
            var warehouse = GetWarehouseById(warehouseId);
            if (warehouse == null) return new { error = "Depo bulunamadı" };

            var stocks = GetWarehouseStocks(warehouseId);
            var movements = GetStockMovements(warehouseId, limit: 1000);

            var totalItems = stocks.Count;
            var totalQuantity = stocks.Sum(s => int.TryParse(s.Quantity, out var q) ? q : 0);
            var lowStockCount = GetLowStockItems(warehouseId).Count;

            var movementsToday = movements.Count(m =>
                DateTime.TryParse(m.MovementDate, out var d) && d.Date == DateTime.Today
            );

            var movementsThisWeek = movements.Count(m =>
                DateTime.TryParse(m.MovementDate, out var d) && d >= DateTime.Today.AddDays(-7)
            );

            return new
            {
                warehouse = warehouse,
                summary = new
                {
                    totalItems,
                    totalQuantity,
                    lowStockCount,
                    capacityUsage = warehouse.Capacity != null && int.TryParse(warehouse.Capacity, out var cap) && cap > 0
                        ? (double)totalQuantity / cap * 100
                        : 0,
                    movementsToday,
                    movementsThisWeek
                },
                recentMovements = movements.Take(10).ToList()
            };
        }

        #endregion
    }
}