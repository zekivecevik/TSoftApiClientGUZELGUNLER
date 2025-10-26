using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TSoftApiClient.Models;
using TSoftApiClient.Services;

namespace TSoftApiClient.Controllers
{
    /// <summary>
    /// Admin Panel - Kullanýcý ve Log Yönetimi
    /// </summary>
    [Authorize(Roles = Roles.Admin)]
    [Route("/Admin")]
    public class AdminController : Controller
    {
        private readonly AuthService _authService;
        private readonly AuditLogService _auditLog;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            AuthService authService,
            AuditLogService auditLog,
            ILogger<AdminController> logger)
        {
            _authService = authService;
            _auditLog = auditLog;
            _logger = logger;
        }

        /// <summary>
        /// Admin Dashboard
        /// </summary>
        [HttpGet]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            var users = _authService.GetAllUsers();
            var logs = _auditLog.GetLogs(page: 1, pageSize: 20);
            var stats = _auditLog.GetStatistics();

            ViewBag.Users = users;
            ViewBag.Logs = logs;
            ViewBag.Stats = stats;

            return View("~/Views/Admin/Index.cshtml");
        }

        /// <summary>
        /// Kullanýcý Yönetimi
        /// </summary>
        [HttpGet("Users")]
        public IActionResult Users()
        {
            var users = _authService.GetAllUsers();
            return View("~/Views/Admin/Users.cshtml", users);
        }

        /// <summary>
        /// Audit Loglarý
        /// </summary>
        [HttpGet("Logs")]
        public IActionResult Logs(int page = 1)
        {
            var logs = _auditLog.GetLogs(page, 100);
            ViewBag.CurrentPage = page;
            ViewBag.HasMore = logs.Count >= 100;

            return View("~/Views/Admin/Logs.cshtml", logs);
        }
    }
}