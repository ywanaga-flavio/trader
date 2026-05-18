using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Trader.Api.Auth;

/// <summary>
/// Issues JWT tokens for authenticated users.
/// Users are hardcoded for the initial implementation and must be replaced
/// with a proper user store (database + hashed passwords) before production.
/// </summary>
public sealed class JwtTokenService
{
    // -------------------------------------------------------------------------
    // Hardcoded users — replace with a real user store before production.
    // Passwords are stored as plain text only during initial development.
    // -------------------------------------------------------------------------
    private static readonly Dictionary<string, (string Password, string[] Roles)> _users = new()
    {
        ["admin"]  = ("Admin@1234!", ["admin", "trader", "marketdata"]),
        ["trader"] = ("Trader@1234!", ["trader", "marketdata"]),
        ["viewer"] = ("Viewer@1234!", ["marketdata"]),
    };

    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    /// <summary>
    /// Validates credentials and returns a signed JWT on success, or null on failure.
    /// </summary>
    public LoginResponse? Authenticate(string username, string password)
    {
        if (!_users.TryGetValue(username, out var entry) || entry.Password != password)
            return null;

        var key     = _config["Jwt:Key"]      ?? throw new InvalidOperationException("Missing Jwt:Key.");
        var issuer  = _config["Jwt:Issuer"]   ?? throw new InvalidOperationException("Missing Jwt:Issuer.");
        var audience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("Missing Jwt:Audience.");
        var expiryMinutes = int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var role in entry.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expires,
            signingCredentials: credentials);

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
