using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Interfaces;

public sealed record KycDocumentContent(Stream Stream, string ContentType);

public interface IFreelancerKycService
{
    Task<FreelancerKycResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<FreelancerKycResponse> SaveProfileAsync(Guid userId, SaveFreelancerKycProfileRequest request, CancellationToken cancellationToken);
    Task<KycDocumentResponse> UploadDocumentAsync(Guid userId, VerificationDocumentType type, string originalFileName, long length, Stream content, CancellationToken cancellationToken);
    Task<FreelancerKycResponse> SubmitAsync(Guid userId, CancellationToken cancellationToken);
    Task<KycDocumentContent?> OpenDocumentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);
}
