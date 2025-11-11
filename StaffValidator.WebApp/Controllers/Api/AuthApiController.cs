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
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _auth.AuthenticateAsync(req.Username, req.Password);
        if (!result.Success)
        {
            return Unauthorized(new { success = false, error = result.ErrorMessage });
        }

        return Ok(new { success = true, token = result.Token });
    }
}
