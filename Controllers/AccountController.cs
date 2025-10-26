using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TSoftApiClient.Models;
using TSoftApiClient.Services;

namespace TSoftApiClient.Controllers
{
    /// <summary>
    /// Login & Auth MVC Controller
    /// </summary>
    public class AccountController : Controller
    {
        private readonly AuthService _authService;
        private readonly AuditLogService _auditLog;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            AuthService authService,
            AuditLogService auditLog,
            ILogger<AccountController> logger)
        {
            _authService = authService;
            _auditLog = auditLog;
            _logger = logger;
        }

        /// <summary>
        /// Login sayfasý
        /// </summary>
        [HttpGet]
        [Route("/Login")]
        [Route("/Account/Login")]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            // Zaten giriþ yapmýþsa dashboard'a yönlendir
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View("~/Views/Account/Login.cshtml");
        }

        /// <summary>
        /// Login iþlemi
        /// </summary>
        [HttpPost]
        [Route("/Login")]
        [Route("/Account/Login")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginRequest model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Lütfen tüm alanlarý doldurun";
                return View("~/Views/Account/Login.cshtml", model);
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = Request.Headers["User-Agent"].ToString();

            var response = _authService.Login(model, ipAddress, userAgent);

            if (!response.Success)
            {
                ViewBag.Error = response.Message;
                return View("~/Views/Account/Login.cshtml", model);
            }

            // JWT Token'ý cookie'ye kaydet
            HttpContext.Response.Cookies.Append("AuthToken", response.Token!, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            });

            // Kullanýcý bilgilerini session'a kaydet (opsiyonel)
            HttpContext.Session.SetString("Username", response.User!.Username);
            HttpContext.Session.SetString("Role", response.User.Role);

            TempData["Success"] = $"Hoþgeldiniz, {response.User.FullName}!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Logout
        /// </summary>
        [HttpGet]
        [Route("/Logout")]
        [Route("/Account/Logout")]
        [Authorize]
        public IActionResult Logout()
        {
            var username = User.Identity?.Name ?? "Unknown";
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            _auditLog.LogLogout(userId, username, ipAddress);

            // Cookie'yi sil
            HttpContext.Response.Cookies.Delete("AuthToken");

            // Session temizle
            HttpContext.Session.Clear();

            TempData["Info"] = "Çýkýþ yapýldý";

            return RedirectToAction("Login");
        }

        /// <summary>
        /// Access Denied sayfasý
        /// </summary>
        [HttpGet]
        [Route("/Account/AccessDenied")]
        public IActionResult AccessDenied()
        {
            return View("~/Views/Account/AccessDenied.cshtml");
        }

        /// <summary>
        /// Kullanýcý profili
        /// </summary>
        [HttpGet]
        [Route("/Account/Profile")]
        [Authorize]
        public IActionResult Profile()
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "";
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var fullName = User.FindFirst("FullName")?.Value ?? "";
            var apiKey = User.FindFirst("ApiKey")?.Value ?? "";

            ViewBag.User = new UserInfo
            {
                Id = userId,
                Username = username,
                Email = email,
                FullName = fullName,
                Role = role,
                ApiKey = apiKey
            };

            return View("~/Views/Account/Profile.cshtml");
        }
    }
}