using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SPRMS.Services.Domain;

/// <summary>
/// JWT token generation and validation service.
/// </summary>
public interface IJwtService
{
    /// <summary>Generates a JWT access token (15 minutes, short-lived).</summary>
    string GenerateAccessToken(long userId, string username, string[] roles);

    /// <summary>Generates a JWT refresh token (7 days, long-lived).</summary>
    string GenerateRefreshToken(long userId);

    /// <summary>Validates JWT token and returns principal if valid.</summary>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>Extracts userId from JWT token claims.</summary>
    long? GetUserIdFromToken(string token);
}

/// <summary>
/// JWT token service implementation.
/// Configuration from appsettings.json (JWT:Secret, JWT:Issuer, JWT:Audience)
/// </summary>
public sealed class JwtService(IConfiguration config) : IJwtService
{
    private readonly string _secret = config["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret not configured");
    private readonly string _issuer = config["JWT:Issuer"] ?? "SPRMS_API";
    private readonly string _audience = config["JWT:Audience"] ?? "SPRMS_Client";
    private readonly int _accessTokenMinutes = int.Parse(config["JWT:AccessTokenMinutes"] ?? "15");
    private readonly int _refreshTokenDays = int.Parse(config["JWT:RefreshTokenDays"] ?? "7");

    /// <summary>
    /// Generates a short-lived access token (default 15 minutes).
    /// Contains claims: sub (userId), name (username), role (array of roles), iat, exp.
    /// </summary>
    public string GenerateAccessToken(long userId, string username, string[] roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new("uid", userId.ToString()),  // Custom claim for userId
        };

        // Add roles as separate claims
        foreach (var role in roles)
        {
            claims.Add(new(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a long-lived refresh token (default 7 days).
    /// Used to obtain a new access token without re-authenticating.
    /// Should be stored in database with expiration tracking.
    /// </summary>
    public string GenerateRefreshToken(long userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("type", "refresh"),  // Mark this as a refresh token
            new("jti", Guid.NewGuid().ToString()),  // Token ID for revocation tracking
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(_refreshTokenDays),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Validates JWT token signature and returns claims principal.
    /// Returns null if token is invalid, expired, or signature verification fails.
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var handler = new JwtSecurityTokenHandler();

            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,  // No clock skew tolerance
            }, out SecurityToken validatedToken);

            return principal;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts userId from token claims (from "uid" custom claim or "sub" standard claim).
    /// </summary>
    public long? GetUserIdFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Try custom "uid" claim first
            var uidClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "uid");
            if (uidClaim != null && long.TryParse(uidClaim.Value, out long uid))
                return uid;

            // Fall back to "sub" (NameIdentifier)
            var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (subClaim != null && long.TryParse(subClaim.Value, out long sub))
                return sub;

            return null;
        }
        catch
        {
            return null;
        }
    }
}
