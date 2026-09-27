using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Data;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Abstrict.Api.Services.Implementations;

public sealed class CustomerLoginService(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration,
    TimeProvider timeProvider) : ICustomerLoginService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public async Task<CustomerLoginResponse?> LoginAsync(CustomerLoginRequest request, CancellationToken cancellationToken)
    {
        var phoneNumber = VietnamesePhoneNumber.Normalize(request.PhoneNumber);
        if (phoneNumber is null)
            return null;

        var user = await dbContext.Users.Include(x => x.CustomerProfile)
            .SingleOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
        if (user is null || user.Role != UserRole.Customer || user.Status != AccountStatus.Active ||
            !user.PhoneVerifiedAtUtc.HasValue || string.IsNullOrEmpty(user.PasswordHash))
            return null;

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now + AccessTokenLifetime;
        var issuer = configuration["Jwt:Issuer"] ?? "abstrict-api";
        var audience = configuration["Jwt:Audience"] ?? "abstrict-client";
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
            signingKey = "ABSTRICT-development-only-JWT-signing-key-do-not-use-in-production-2026";
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
             new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
             new Claim(ClaimTypes.Role, UserRole.Customer.ToString()),
             new Claim("phone_number", user.PhoneNumber)],
            now.UtcDateTime, expiresAt.UtcDateTime, credentials);

        return new CustomerLoginResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expiresAt,
            user.Id, user.CustomerProfile?.FullName ?? string.Empty, user.PhoneNumber, UserRole.Customer.ToString());
    }
}
