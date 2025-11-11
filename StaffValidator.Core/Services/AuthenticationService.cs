using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StaffValidator.Core.Services;

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(string username, string password);
    Task<AuthenticationResult> RegisterAsync(string username, string email, string password, string role = "User");
}

public record AuthenticationResult(bool Success, string? Token = null, string? ErrorMessage = null);

public record AppUser(string Username, string Email, string PasswordHash, string Role);

public class AuthenticationService : IAuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticationService> _logger;
    
    // Simple in-memory store for demo purposes (in production, use proper user management)
    private static readonly List<AppUser> Users = new()
    {
        new("admin", "admin@staffvalidator.com", BCrypt.Net.BCrypt.HashPassword("admin123"), "Administrator"),
        new("manager", "manager@staffvalidator.com", BCrypt.Net.BCrypt.HashPassword("manager123"), "Manager"),
        new("user", "user@staffvalidator.com", BCrypt.Net.BCrypt.HashPassword("user123"), "User")
    };

    public AuthenticationService(IConfiguration configuration, ILogger<AuthenticationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<AuthenticationResult> AuthenticateAsync(string username, string password)
    {
        _logger.LogInformation("🔐 Authentication attempt for user: {Username}", username);
        
        try
        {
            var user = Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            
            if (user == null)
            {
                _logger.LogWarning("⚠️ User not found: {Username}", username);
                return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Invalid username or password"));
            }

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                _logger.LogWarning("⚠️ Invalid password for user: {Username}", username);
                return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Invalid username or password"));
            }

            var token = GenerateJwtToken(user);
            _logger.LogInformation("✅ Authentication successful for user: {Username}", username);
            
            return Task.FromResult(new AuthenticationResult(true, Token: token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Authentication error for user: {Username}", username);
            return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Authentication failed"));
        }
    }

    public Task<AuthenticationResult> RegisterAsync(string username, string email, string password, string role = "User")
    {
        _logger.LogInformation("📝 Registration attempt for user: {Username}", username);
        
        try
        {
            if (Users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("⚠️ Username already exists: {Username}", username);
                return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Username already exists"));
            }

            if (Users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("⚠️ Email already exists: {Email}", email);
                return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Email already exists"));
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var newUser = new AppUser(username, email, passwordHash, role);
            Users.Add(newUser);

            var token = GenerateJwtToken(newUser);
            _logger.LogInformation("✅ Registration successful for user: {Username}", username);
            
            return Task.FromResult(new AuthenticationResult(true, Token: token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Registration error for user: {Username}", username);
            return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Registration failed"));
        }
    }

    private string GenerateJwtToken(AppUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "StaffValidator-Super-Secret-Key-For-JWT-Tokens-2024!";
        var issuer = jwtSettings["Issuer"] ?? "StaffValidator";
        var audience = jwtSettings["Audience"] ?? "StaffValidator-Users";
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("username", user.Username),
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}