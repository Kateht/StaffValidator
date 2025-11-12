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
    public IActionResult Login(string? returnUrl = null, string? error = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!string.IsNullOrEmpty(error))
        {

            var message = error.ToLowerInvariant() switch
            {
                "invalid" => "Invalid username or password.",
                _ => "Authentication error. Please try again."
            };
            TempData["Error"] = message;
        }
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        _logger.LogInformation("🔐 Login attempt for user: {Username}", model.Username);
        _logger.LogInformation("Request Content-Type: {ContentType}", Request.ContentType ?? "(none)");

        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage + (e.Exception != null ? (": " + e.Exception.Message) : "")));
            _logger.LogWarning("ModelState invalid during login POST. Errors: {Errors}", string.IsNullOrEmpty(errors) ? "(none)" : errors);
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
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Staff");
        }

        _logger.LogWarning("❌ Login failed for user: {Username}", model.Username);
        ModelState.AddModelError("", result.ErrorMessage ?? "Login failed");
        return View(model);
    }

    private bool RegistrationEnabled => string.IsNullOrWhiteSpace(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Security:EnableSelfRegistration"])
        ? true
        : bool.TryParse(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Security:EnableSelfRegistration"], out var b) ? b : true;

    [HttpGet]
    public IActionResult Register()
    {
        if (!RegistrationEnabled)
        {
            TempData["Error"] = "Registration is disabled. Please contact your system administrator for access.";
            return RedirectToAction("Login");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!RegistrationEnabled)
        {
            TempData["Error"] = "Registration is disabled.";
            return RedirectToAction("Login");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!_authService.IsStrongPassword(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password too weak (need uppercase + digit)");
            return View(model);
        }

        var result = await _authService.RegisterAsync(model.Username, model.Email, model.Password);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Registration failed");
            return View(model);
        }


        if (!string.IsNullOrEmpty(result.Token))
        {
            Response.Cookies.Append("AuthToken", result.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            });
        }

        TempData["Success"] = "Account created successfully";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        _logger.LogInformation("📋 Displaying forgot password page");
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, resetToken, error) = await _authService.RequestPasswordResetAsync(model.UsernameOrEmail);
        if (!success)
        {
            TempData["Error"] = "Account doesn't exist"; // unified message
            return View(model);
        }

        if (!string.IsNullOrEmpty(resetToken))
        {
            TempData["Success"] = $"Reset token issued: {resetToken}"; // only in non-privileged case
            return RedirectToAction("ResetPassword", new { token = resetToken });
        }

        TempData["Error"] = "Account doesn't exist";
        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPassword(string? token = null)
    {
        return View(new ResetPasswordViewModel { Token = token ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!_authService.IsStrongPassword(model.NewPassword))
        {
            ModelState.AddModelError(nameof(model.NewPassword), "Password too weak");
            return View(model);
        }

        var result = await _authService.ResetPasswordAsync(model.Token, model.NewPassword);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, "Invalid token");
            return View(model);
        }

        TempData["Success"] = "Password reset successful";
        return RedirectToAction("Login");
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