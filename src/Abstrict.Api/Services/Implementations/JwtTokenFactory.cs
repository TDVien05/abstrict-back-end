using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Abstrict.Api.Models.Enums;
using Microsoft.IdentityModel.Tokens;

namespace Abstrict.Api.Services.Implementations;

public sealed class JwtTokenFactory(IConfiguration configuration, TimeProvider timeProvider)
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public (string Token, DateTimeOffset ExpiresAtUtc) CreateAccessToken(Guid userId, UserRole role, string phoneNumber)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now + AccessTokenLifetime;
        var issuer = configuration["Jwt:Issuer"] ?? "abstrict-api";
        var audience = configuration["Jwt:Audience"] ?? "abstrict-client";
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
            signingKey = "ABSTRICT-development-only-JWT-signing-key-do-not-use-in-production-2026";

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("phone_number", phoneNumber)
            ],
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
