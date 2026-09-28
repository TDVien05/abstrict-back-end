using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Repositories.Interfaces;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Abstrict.Api.Services.Implementations;

public sealed class FreelancerLoginService(
    IUserRepository userRepository,
    IFreelancerApplicationRepository applicationRepository,
    IPasswordHasher<Abstrict.Api.Models.Entities.User> passwordHasher,
    JwtTokenFactory jwtTokenFactory) : IFreelancerLoginService
{
    public async Task<FreelancerLoginResponse?> LoginAsync(FreelancerLoginRequest request, CancellationToken cancellationToken)
    {
        var phoneNumber = VietnamesePhoneNumber.Normalize(request.PhoneNumber);
        if (phoneNumber is null)
            return null;

        var user = await userRepository.GetByPhoneNumberAsync(phoneNumber, track: true, cancellationToken);
        if (user is null || user.Role != UserRole.Freelancer || user.Status != AccountStatus.Active ||
            !user.PhoneVerifiedAtUtc.HasValue || string.IsNullOrEmpty(user.PasswordHash))
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return null;

        var application = await applicationRepository.GetByUserIdAsync(user.Id, track: false, cancellationToken);
        var (token, expiresAt) = jwtTokenFactory.CreateAccessToken(user.Id, user.Role, user.PhoneNumber);

        return new FreelancerLoginResponse(
            token,
            "Bearer",
            expiresAt,
            user.Id,
            application?.LegalFullName ?? string.Empty,
            user.PhoneNumber,
            user.Role.ToString(),
            application?.Status.ToString() ?? FreelancerApplicationStatus.Draft.ToString(),
            application?.CurrentStep.ToString() ?? OnboardingStep.Personal.ToString());
    }
}
