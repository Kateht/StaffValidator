using Microsoft.AspNetCore.Mvc;
using StaffValidator.Core.Services;

namespace StaffValidator.WebApp.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _auth;

    public AuthController(IAuthenticationService auth)
    {
        _auth = auth;
    }

    public record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    [Consumes("application/json", "application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<IActionResult> Login()
    {

        var contentType = Request.ContentType ?? "(none)";

        string? username = null;
        string? password = null;

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            username = form["Username"].FirstOrDefault() ?? form["username"].FirstOrDefault();
            password = form["Password"].FirstOrDefault() ?? form["password"].FirstOrDefault();
        }
        else
        {

            try
            {
                var req = await Request.ReadFromJsonAsync<LoginRequest>();
                if (req != null)
                {
                    username = req.Username;
                    password = req.Password;
                }
            }
            catch
            {
                // Bỏ qua lỗi parse; sẽ kiểm tra ở dưới
            }
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return BadRequest(new { success = false, error = "Missing username or password", contentType });
        }

        var result = await _auth.AuthenticateAsync(username, password);
        if (!result.Success)
        {

            var accept = Request.Headers["Accept"].ToString();
            if (Request.HasFormContentType || (!string.IsNullOrEmpty(accept) && accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
            {
                return Redirect("/Auth/Login?error=invalid");
            }
            return Unauthorized(new { success = false, error = result.ErrorMessage });
        }

        // Set cookie AuthToken 
        var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(1)
        };

        if (!string.IsNullOrEmpty(result.Token))
        {
            Response.Cookies.Append("AuthToken", result.Token, cookieOptions);
        }


        var acceptHeader = Request.Headers["Accept"].ToString();
        if (Request.HasFormContentType || (!string.IsNullOrEmpty(acceptHeader) && acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
        {
            return Redirect("/");
        }

        // Default: return JSON token (for API/AJAX clients)
        return Ok(new { success = true, token = result.Token });
    }
}
