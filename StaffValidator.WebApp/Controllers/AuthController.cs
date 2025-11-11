using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffValidator.Core.Services;
using StaffValidator.WebApp.Models;

namespace StaffValidator.WebApp.Controllers;

public class AuthController : Controller
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthenticationService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        _logger.LogInformation("🔐 Login attempt for user: {Username}", model.Username);
        
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.AuthenticateAsync(model.Username, model.Password);
        
        if (result.Success && result.Token != null)
        {
            // Store JWT token in cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            };
            
            Response.Cookies.Append("AuthToken", result.Token, cookieOptions);
            
            _logger.LogInformation("✅ Login successful for user: {Username}", model.Username);
            TempData["Success"] = $"Welcome back, {model.Username}!";
            
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            
            return RedirectToAction("Index", "Staff");
        }

        _logger.LogWarning("❌ Login failed for user: {Username}", model.Username);
        ModelState.AddModelError("", result.ErrorMessage ?? "Login failed");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register()
    {
        // Registration disabled in enterprise environment
        _logger.LogWarning("� Registration attempt blocked - feature disabled");
        TempData["Error"] = "Registration is disabled. Please contact your system administrator for account access.";
        return RedirectToAction("Login");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(RegisterViewModel model)
    {
        // Registration disabled in enterprise environment
        _logger.LogWarning("🚫 Registration POST attempt blocked - feature disabled");
        TempData["Error"] = "Registration is disabled. Please contact your system administrator for account access.";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        _logger.LogInformation("📋 Displaying forgot password information page");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        _logger.LogInformation("🚪 User logout");
        
        // Remove JWT token cookie
        Response.Cookies.Delete("AuthToken");
        
        TempData["Success"] = "You have been logged out successfully.";
        return RedirectToAction("Login");
    }

    [Authorize]
    [HttpGet]
    public IActionResult Profile()
    {
        var username = User.Identity?.Name;
        var role = User.FindFirst("role")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        
        var model = new ProfileViewModel
        {
            Username = username ?? "Unknown",
            Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "",
            Role = role ?? "User"
        };
        
        return View(model);
    }
}