using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Interfaces;

/// <summary>Hồ sơ đăng ký của công ty đối tác: pháp nhân, người đại diện, khu vực nhận ca, giấy tờ và gửi duyệt.</summary>
public interface ICompanyOnboardingService
{
    Task<CompanyOnboardingResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<CompanyOnboardingResponse> SaveCompanyAsync(Guid userId, SaveCompanyProfileRequest request, CancellationToken cancellationToken);
    Task<CompanyOnboardingResponse> SaveRepresentativeAsync(Guid userId, SaveCompanyRepresentativeRequest request, CancellationToken cancellationToken);
    Task<CompanyOnboardingResponse> SaveServiceAreasAsync(Guid userId, SaveCompanyServiceAreasRequest request, CancellationToken cancellationToken);
    Task<CompanyOnboardingResponse> UploadDocumentAsync(Guid userId, VerificationDocumentType type, string originalFileName, long length, Stream content, CancellationToken cancellationToken);
    Task<CompanyOnboardingResponse> SubmitAsync(Guid userId, CancellationToken cancellationToken);
    Task<KycDocumentContent?> OpenDocumentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);
}
