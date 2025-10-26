using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TSoftApiClient.Models;
using TSoftApiClient.Services;

namespace TSoftApiClient.Controllers
{
    /// <summary>
    /// Admin Kullan�c� Y�netimi Controller
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("Admin/Users")]
    public class AdminUsersController : Controller
    {
        private readonly AuthService _authService;
        private readonly AuditLogService _auditLogService;
        private readonly ILogger<AdminUsersController> _logger;

        public AdminUsersController(
            AuthService authService,
            AuditLogService auditLogService,
            ILogger<AdminUsersController> logger)
        {
            _authService = authService;
            _auditLogService = auditLogService;
            _logger = logger;
        }

        /// <summary>
        /// Kullan�c� listesi sayfas�
        /// </summary>
        [HttpGet("")]
        public IActionResult Index()
        {
            try
            {
                var users = _authService.GetAllUsers();
                var currentUserId = HttpContext.Session.GetInt32("UserId") ?? 0;

                ViewBag.CurrentUserId = currentUserId;
                ViewBag.TotalUsers = users.Count;
                ViewBag.AdminCount = users.Count(u => u.Role == Roles.Admin);
                ViewBag.ManagerCount = users.Count(u => u.Role == Roles.Manager);
                ViewBag.ViewerCount = users.Count(u => u.Role == Roles.Viewer);

                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
                TempData["Error"] = "Kullan�c�lar y�klenirken hata olu�tu";
                return RedirectToAction("Index", "Admin");
            }
        }

        /// <summary>
        /// Yeni kullan�c� ekleme sayfas�
        /// </summary>
        [HttpGet("Create")]
        public IActionResult Create()
        {
            ViewBag.Roles = Roles.All;
            return View();
        }

        /// <summary>
        /// Yeni kullan�c� ekleme i�lemi
        /// </summary>
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(string username, string email, string password, string fullName, string role)
        {
            try
            {
                var currentUsername = User?.Identity?.Name ?? "Unknown";
                var currentUserId = HttpContext.Session.GetInt32("UserId") ?? 0;

                var (success, message, user) = _authService.CreateUser(username, email, password, fullName, role);

                if (success && user != null)
                {
                    _auditLogService.LogCreate(
                        currentUserId,
                        currentUsername,
                        "User",
                        user.Id.ToString(),
                        $"Created user: {username} ({role})",
                        HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
                    );

                    TempData["Success"] = "Kullan�c� ba�ar�yla olu�turuldu!";
                    return RedirectToAction("Index");
                }

                ViewBag.Error = message;
                ViewBag.Roles = Roles.All;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                ViewBag.Error = "Kullan�c� olu�turulurken hata olu�tu";
                ViewBag.Roles = Roles.All;
                return View();
            }
        }

        /// <summary>
        /// Kullan�c� silme i�lemi
        /// </summary>
        [HttpPost("Delete/{userId}")]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int userId)
        {
            try
            {
                var currentUserId = HttpContext.Session.GetInt32("UserId") ?? 0;
                var currentUsername = User?.Identity?.Name ?? "Unknown";

                if (currentUserId == userId)
                {
                    TempData["Error"] = "Kendi hesab�n�z� silemezsiniz!";
                    return RedirectToAction("Index");
                }

                var users = _authService.GetAllUsers();
                var userToDelete = users.FirstOrDefault(u => u.Id == userId);

                if (userToDelete == null)
                {
                    TempData["Error"] = "Kullan�c� bulunamad�";
                    return RedirectToAction("Index");
                }

                var success = _authService.DeleteUser(userId);

                if (success)
                {
                    _auditLogService.LogDelete(
                        currentUserId,
                        currentUsername,
                        "User",
                        userId.ToString(),
                        $"Deleted user: {userToDelete.Username}",
                        HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
                    );

                    TempData["Success"] = "Kullan�c� ba�ar�yla silindi!";
                }
                else
                {
                    TempData["Error"] = "Kullan�c� silinemedi";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                TempData["Error"] = "Kullan�c� silinirken hata olu�tu";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Kullan�c� aktif/pasif durumu de�i�tirme
        /// </summary>
        [HttpPost("ToggleStatus/{userId}")]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(int userId)
        {
            try
            {
                var currentUserId = HttpContext.Session.GetInt32("UserId") ?? 0;
                var currentUsername = User?.Identity?.Name ?? "Unknown";

                if (currentUserId == userId)
                {
                    TempData["Error"] = "Kendi hesab�n�z�n durumunu de�i�tiremezsiniz!";
                    return RedirectToAction("Index");
                }

                var success = _authService.ToggleUserStatus(userId);

                if (success)
                {
                    _auditLogService.LogUpdate(
                        currentUserId,
                        currentUsername,
                        "User",
                        userId.ToString(),
                        "Toggled user status",
                        HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
                    );

                    TempData["Success"] = "Kullan�c� durumu de�i�tirildi!";
                }
                else
                {
                    TempData["Error"] = "��lem ba�ar�s�z";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status");
                TempData["Error"] = "Durum de�i�tirilirken hata olu�tu";
                return RedirectToAction("Index");
            }
        }
    }
}