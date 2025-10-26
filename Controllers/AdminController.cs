using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TSoftApiClient.Services;
using TSoftApiClient.Models;

namespace TSoftApiClient.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AuthService _authService;
        private readonly AuditLogService _auditLogService;
        private readonly TSoftApiService _tsoftService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            AuthService authService,
            AuditLogService auditLogService,
            TSoftApiService tsoftService,
            ILogger<AdminController> logger)
        {
            _authService = authService;
            _auditLogService = auditLogService;
            _tsoftService = tsoftService;
            _logger = logger;
        }

        [Route("/Admin")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var users = _authService.GetAllUsers();
                var logs = _auditLogService.GetLogs(page: 1, pageSize: 20);
                var stats = _auditLogService.GetStatistics();

                // System stats
                var products = await _tsoftService.GetProductsAsync(limit: 1000);
                var orders = await _tsoftService.GetOrdersAsync(limit: 1000);
                var customers = await _tsoftService.GetCustomersAsync(limit: 1000);

                ViewBag.Users = users;
                ViewBag.Logs = logs;
                ViewBag.Stats = stats;
                ViewBag.TotalProducts = products.Success && products.Data != null ? products.Data.Count : 0;
                ViewBag.TotalOrders = orders.Success && orders.Data != null ? orders.Data.Count : 0;
                ViewBag.TotalCustomers = customers.Success && customers.Data != null ? customers.Data.Count : 0;
                ViewBag.ActiveUsers = users.Count(u => u.Role != "Viewer");

                // Recent activity by user
                var userActivity = logs
                    .GroupBy(l => l.Username)
                    .Select(g => new
                    {
                        Username = g.Key,
                        ActionCount = g.Count(),
                        LastActivity = g.Max(x => x.Timestamp)
                    })
                    .OrderByDescending(x => x.LastActivity)
                    .Take(10)
                    .ToList();

                ViewBag.UserActivity = userActivity;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin dashboard error");
                ViewBag.Error = "Dashboard yüklenirken hata oluþtu";
                return View();
            }
        }

        [Route("/Admin/Logs")]
        public IActionResult Logs(int page = 1, int pageSize = 100)
        {
            var logs = _auditLogService.GetLogs(page, pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Stats = _auditLogService.GetStatistics();

            return View(logs);
        }

        [Route("/Admin/Users")]
        public IActionResult Users()
        {
            var users = _authService.GetAllUsers();
            return View(users);
        }

        [Route("/Admin/CreateUser")]
        [HttpGet]
        public IActionResult CreateUser()
        {
            return View();
        }

        [Route("/Admin/CreateUser")]
        [HttpPost]
        public IActionResult CreateUser(string username, string email, string password, string fullName, string role)
        {
            try
            {
                var (success, message, user) = _authService.CreateUser(username, email, password, fullName, role);

                if (success)
                {
                    var currentUser = User?.Identity?.Name ?? "Admin";
                    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    _auditLogService.LogCreate(
                        userId: HttpContext.Session.GetInt32("UserId") ?? 0,
                        username: currentUser,
                        entity: "User",
                        entityId: user?.Id.ToString(),
                        details: $"Created user: {username} with role: {role}",
                        ipAddress: ipAddress
                    );

                    TempData["Success"] = message;
                    return RedirectToAction("Users");
                }
                else
                {
                    ViewBag.Error = message;
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create user error");
                ViewBag.Error = "Kullanýcý oluþturulurken hata oluþtu";
                return View();
            }
        }

        [Route("/Admin/DeleteUser/{id}")]
        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                var success = _authService.DeleteUser(id);

                if (success)
                {
                    var currentUser = User?.Identity?.Name ?? "Admin";
                    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    _auditLogService.LogDelete(
                        userId: HttpContext.Session.GetInt32("UserId") ?? 0,
                        username: currentUser,
                        entity: "User",
                        entityId: id.ToString(),
                        details: $"Deleted user with ID: {id}",
                        ipAddress: ipAddress
                    );

                    TempData["Success"] = "Kullanýcý baþarýyla silindi";
                }
                else
                {
                    TempData["Error"] = "Kullanýcý silinemedi";
                }

                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete user error");
                TempData["Error"] = "Kullanýcý silinirken hata oluþtu";
                return RedirectToAction("Users");
            }
        }

        [Route("/Admin/ToggleUserStatus/{id}")]
        [HttpPost]
        public IActionResult ToggleUserStatus(int id)
        {
            try
            {
                var success = _authService.ToggleUserStatus(id);

                if (success)
                {
                    TempData["Success"] = "Kullanýcý durumu güncellendi";
                }
                else
                {
                    TempData["Error"] = "Kullanýcý durumu güncellenemedi";
                }

                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toggle user status error");
                TempData["Error"] = "Kullanýcý durumu güncellenirken hata oluþtu";
                return RedirectToAction("Users");
            }
        }

        [Route("/Admin/SystemInfo")]
        public IActionResult SystemInfo()
        {
            var systemInfo = new
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                DotNetVersion = Environment.Version.ToString(),
                WorkingSet = Environment.WorkingSet / 1024 / 1024, // MB
                SystemDirectory = Environment.SystemDirectory,
                CurrentDirectory = Environment.CurrentDirectory,
                UpTime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"dd\.hh\:mm\:ss")
            };

            return View(systemInfo);
        }

        [Route("/Admin/Settings")]
        public IActionResult Settings()
        {
            return View();
        }

        [Route("/Admin/Database")]
        public IActionResult Database()
        {
            var users = _authService.GetAllUsers();
            var logs = _auditLogService.GetLogs(page: 1, pageSize: 1000);

            ViewBag.UserCount = users.Count;
            ViewBag.LogCount = logs.Count;
            ViewBag.OldestLog = logs.Any() ? logs.Min(l => l.Timestamp) : DateTime.MinValue;
            ViewBag.NewestLog = logs.Any() ? logs.Max(l => l.Timestamp) : DateTime.MinValue;

            return View();
        }

        [Route("/Admin/CleanLogs")]
        [HttpPost]
        public IActionResult CleanLogs(int daysToKeep = 90)
        {
            try
            {
                var deletedCount = _auditLogService.CleanOldLogs(daysToKeep);
                TempData["Success"] = $"{deletedCount} eski log kaydý temizlendi";
                return RedirectToAction("Database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Clean logs error");
                TempData["Error"] = "Loglar temizlenirken hata oluþtu";
                return RedirectToAction("Database");
            }
        }

        [Route("/Admin/ExportLogs")]
        public IActionResult ExportLogs()
        {
            try
            {
                var logs = _auditLogService.GetLogs(page: 1, pageSize: 10000);
                var csv = "ID,Timestamp,Username,Action,Entity,EntityId,Success,IpAddress,Details\n";

                foreach (var log in logs)
                {
                    csv += $"{log.Id},{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.Username},{log.Action},{log.Entity},{log.EntityId},{log.Success},{log.IpAddress},\"{log.Details}\"\n";
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                return File(bytes, "text/csv", $"audit-logs-{DateTime.Now:yyyy-MM-dd}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export logs error");
                TempData["Error"] = "Loglar dýþa aktarýlýrken hata oluþtu";
                return RedirectToAction("Logs");
            }
        }

        [Route("/Admin/ApiStatus")]
        public async Task<IActionResult> ApiStatus()
        {
            var tests = new List<ApiTestResult>();

            // Test Products API
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await _tsoftService.GetProductsAsync(limit: 1);
                sw.Stop();

                tests.Add(new ApiTestResult
                {
                    Name = "Products API",
                    Success = result.Success,
                    ResponseTime = sw.ElapsedMilliseconds,
                    Message = result.Success ? "OK" : "Failed"
                });
            }
            catch (Exception ex)
            {
                tests.Add(new ApiTestResult
                {
                    Name = "Products API",
                    Success = false,
                    ResponseTime = 0,
                    Message = ex.Message
                });
            }

            // Test Orders API
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await _tsoftService.GetOrdersAsync(limit: 1);
                sw.Stop();

                tests.Add(new ApiTestResult
                {
                    Name = "Orders API",
                    Success = result.Success,
                    ResponseTime = sw.ElapsedMilliseconds,
                    Message = result.Success ? "OK" : "Failed"
                });
            }
            catch (Exception ex)
            {
                tests.Add(new ApiTestResult
                {
                    Name = "Orders API",
                    Success = false,
                    ResponseTime = 0,
                    Message = ex.Message
                });
            }

            // Test Categories API
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await _tsoftService.GetCategoriesAsync();
                sw.Stop();

                tests.Add(new ApiTestResult
                {
                    Name = "Categories API",
                    Success = result.Success,
                    ResponseTime = sw.ElapsedMilliseconds,
                    Message = result.Success ? "OK" : "Failed"
                });
            }
            catch (Exception ex)
            {
                tests.Add(new ApiTestResult
                {
                    Name = "Categories API",
                    Success = false,
                    ResponseTime = 0,
                    Message = ex.Message
                });
            }

            return View(tests);
        }
    }

    public class ApiTestResult
    {
        public string Name { get; set; } = "";
        public bool Success { get; set; }
        public long ResponseTime { get; set; }
        public string Message { get; set; } = "";
    }
}