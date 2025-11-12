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
    Task<(bool Success, string? ResetToken, string? ErrorMessage)> RequestPasswordResetAsync(string usernameOrEmail);
    Task<AuthenticationResult> ResetPasswordAsync(string token, string newPassword);
    bool IsStrongPassword(string password);
}

public record AuthenticationResult(bool Success, string? Token = null, string? ErrorMessage = null);

public record AppUser(string Username, string Email, string PasswordHash, string Role);

public class AuthenticationService : IAuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly bool _requireITForPrivilegedReset;
    
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
        var flag = _configuration["Security:RequireITForPrivilegedReset"];
        _requireITForPrivilegedReset = string.IsNullOrWhiteSpace(flag) ? true : bool.TryParse(flag, out var b) ? b : true;
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

    // Simple in-memory reset token store for demo
    private static readonly Dictionary<string, (string Username, DateTime Expires)> ResetTokens = new();
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(15);

    public Task<(bool Success, string? ResetToken, string? ErrorMessage)> RequestPasswordResetAsync(string usernameOrEmail)
    {
        try
        {
            var user = Users.FirstOrDefault(u =>
                u.Username.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                return Task.FromResult((false, (string?)null, "Account doesn't exist"));
            }

            // Non-enumeration for privileged roles (Admin/Manager)
            if (_requireITForPrivilegedReset &&
                (user.Role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                 user.Role.Equals("Manager", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("Silently suppressing reset token for privileged account: {Username}", user.Username);
                // Return success with no token
                return Task.FromResult((true, (string?)null, (string?)null));
            }

            // Generate token (guid for demo)
            var token = Guid.NewGuid().ToString("N");
            ResetTokens[token] = (user.Username, DateTime.UtcNow.Add(ResetTokenLifetime));
            _logger.LogInformation("🔑 Issued reset token for {Username}", user.Username);
            return Task.FromResult((true, token, (string?)null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error issuing reset token for {User}", usernameOrEmail);
            return Task.FromResult((false, (string?)null, "Failed to issue reset token"));
        }
    }

    public Task<AuthenticationResult> ResetPasswordAsync(string token, string newPassword)
    {
        try
        {
            if (!ResetTokens.TryGetValue(token, out var entry))
            {
                return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Invalid or expired token"));
            }
            if (entry.Expires < DateTime.UtcNow)
            {
                ResetTokens.Remove(token);
                return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Invalid or expired token"));
            }

            var user = Users.FirstOrDefault(u => u.Username.Equals(entry.Username, StringComparison.OrdinalIgnoreCase));
            if (user == null)
            {
                return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Invalid or expired token"));
            }

            if (!IsStrongPassword(newPassword))
            {
                return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Password too weak"));
            }

            // Block resets for privileged accounts when configured
            if (_requireITForPrivilegedReset &&
                (user.Role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                 user.Role.Equals("Manager", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Rejecting reset attempt for privileged account: {Username}", user.Username);
                return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Invalid or expired token"));
            }

            user = new AppUser(user.Username, user.Email, BCrypt.Net.BCrypt.HashPassword(newPassword), user.Role);
            // Replace user password (simple list update)
            var idx = Users.FindIndex(u => u.Username == user.Username);
            if (idx >= 0) Users[idx] = user;
            ResetTokens.Remove(token);
            _logger.LogInformation("🔄 Password reset successful for {Username}", user.Username);
            return Task.FromResult(new AuthenticationResult(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error resetting password with token {Token}", token);
            return Task.FromResult(new AuthenticationResult(false, ErrorMessage: "Reset failed"));
        }
    }

    public bool IsStrongPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6) return false;
        if (!password.Any(char.IsUpper)) return false;
        if (!password.Any(char.IsDigit)) return false;
        return true;
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