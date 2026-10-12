using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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

    public async Task<LoginResult?> LoginAsync(CustomerLoginRequest request, CancellationToken cancellationToken)
    {
        var phoneNumber = VietnamesePhoneNumber.Normalize(request.PhoneNumber);
        if (phoneNumber is null)
            return null;

        var user = await dbContext.Users.Include(x => x.CustomerProfile)
            .SingleOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
        if (user is null || !CanSignIn(user) || string.IsNullOrEmpty(user.PasswordHash))
            return null;

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var now = timeProvider.GetUtcNow();
        var refreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new AuthSession
        {
            UserId = user.Id,
            RefreshTokenHash = HashRefreshToken(refreshToken),
            IsPersistent = true,
            ExpiresAtUtc = now.AddDays(RefreshTokenDays),
            LastSeenAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.AuthSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResult(await BuildResponseAsync(user, now, cancellationToken), refreshToken, session.ExpiresAtUtc);
    }

    public async Task<LoginResult?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var now = timeProvider.GetUtcNow();
        var hash = HashRefreshToken(refreshToken);
        var session = await dbContext.AuthSessions
            .Include(x => x.User).ThenInclude(x => x.CustomerProfile)
            .SingleOrDefaultAsync(x => x.RefreshTokenHash == hash, cancellationToken);
        if (session is null || session.RevokedAtUtc.HasValue || session.ExpiresAtUtc <= now || !CanSignIn(session.User))
            return null;

        session.LastSeenAtUtc = now;
        session.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResult(await BuildResponseAsync(session.User, now, cancellationToken), refreshToken, session.ExpiresAtUtc);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var hash = HashRefreshToken(refreshToken);
        var session = await dbContext.AuthSessions.SingleOrDefaultAsync(x => x.RefreshTokenHash == hash, cancellationToken);
        if (session is null || session.RevokedAtUtc.HasValue)
            return;

        var now = timeProvider.GetUtcNow();
        session.RevokedAtUtc = now;
        session.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private int RefreshTokenDays => Math.Clamp(configuration.GetValue("Auth:RefreshTokenDays", 30), 1, 365);

    private static bool CanSignIn(User user) =>
        user.Status == AccountStatus.Active && user.PhoneVerifiedAtUtc.HasValue;

    // Refresh token là 32 byte ngẫu nhiên nên SHA-256 là đủ; DB không lưu token thô.
    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private async Task<CustomerLoginResponse> BuildResponseAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
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
             new Claim(ClaimTypes.Role, user.Role.ToString()),
             new Claim("phone_number", user.PhoneNumber)],
            now.UtcDateTime, expiresAt.UtcDateTime, credentials);

        return new CustomerLoginResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expiresAt,
            user.Id, user.CustomerProfile?.FullName ?? string.Empty, user.PhoneNumber, user.Role.ToString(),
            user.Role == UserRole.Customer && user.CustomerProfile is { } profile
                ? CustomerAvatarService.ToProfileResponse(user, profile,
                    await CustomerAddressService.LoadAsync(dbContext, user.Id, cancellationToken))
                : null,
            user.Role == UserRole.Freelancer ? await BuildOnboardingAsync(user.Id, cancellationToken) : null,
            user.Role == UserRole.Company ? await CompanyOnboardingService.SummarizeAsync(dbContext, user.Id, cancellationToken) : null);
    }

    private async Task<FreelancerOnboardingResponse> BuildOnboardingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.FreelancerProfiles.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.ProviderId, x.Provider.ApprovalStatus, HasProfileData = x.CitizenIdHash != "" })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null)
            return new FreelancerOnboardingResponse(null, "NotStarted", FreelancerOnboardingStep.Profile);

        var documents = await dbContext.VerificationDocuments.AsNoTracking()
            .Where(x => x.ProviderId == profile.ProviderId)
            .Select(x => new { x.Type, x.Status })
            .ToListAsync(cancellationToken);
        var documentsReady = FreelancerKycService.RequiredDocumentTypes.All(type => documents.Any(x => x.Type == type))
            && documents.All(x => x.Status != VerificationStatus.Rejected);

        var step = profile.ApprovalStatus switch
        {
            ApprovalStatus.Approved => FreelancerOnboardingStep.Completed,
            ApprovalStatus.Suspended => FreelancerOnboardingStep.Suspended,
            ApprovalStatus.Submitted or ApprovalStatus.UnderReview => FreelancerOnboardingStep.AwaitingReview,
            _ when !profile.HasProfileData => FreelancerOnboardingStep.Profile,
            _ when !documentsReady => FreelancerOnboardingStep.Documents,
            _ => FreelancerOnboardingStep.Submit
        };
        return new FreelancerOnboardingResponse(profile.ProviderId, profile.ApprovalStatus.ToString(), step);
    }
}
