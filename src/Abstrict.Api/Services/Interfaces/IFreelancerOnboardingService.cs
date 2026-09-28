using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface IFreelancerOnboardingService
{
    Task<FreelancerOnboardingResponse> GetOnboardingAsync(Guid userId, CancellationToken cancellationToken);
    Task<FreelancerOnboardingResponse> UpdateOnboardingAsync(Guid userId, UpdateFreelancerOnboardingRequest request, int? expectedVersion, CancellationToken cancellationToken);
    Task RecordConsentAsync(Guid userId, RecordKycConsentRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<FreelancerDocumentResponse> UploadDocumentAsync(Guid userId, Stream content, string fileName, string? contentType, UploadOnboardingDocumentRequest metadata, CancellationToken cancellationToken);
    Task<DocumentContentResult?> GetDocumentContentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);
    Task DeleteDocumentAsync(Guid userId, Guid documentId, int? expectedVersion, CancellationToken cancellationToken);
    Task<KycOperationResponse> StartOcrAsync(Guid userId, StartIdentityOcrRequest request, string? idempotencyKey, int? expectedVersion, CancellationToken cancellationToken);
    Task<FreelancerOnboardingResponse> ConfirmIdentityAsync(Guid userId, ConfirmIdentityRequest request, CancellationToken cancellationToken);
    Task<KycOperationResponse> StartFaceMatchAsync(Guid userId, StartFaceMatchRequest request, string? idempotencyKey, CancellationToken cancellationToken);
    Task<KycOperationResponse?> GetOperationAsync(Guid userId, Guid operationId, CancellationToken cancellationToken);
    Task<FreelancerSubmissionSummaryResponse> SubmitAsync(Guid userId, SubmitApplicationRequest request, int? expectedVersion, CancellationToken cancellationToken);
    Task<FreelancerOnboardingResponse> ReopenAsync(Guid userId, ReopenApplicationRequest request, CancellationToken cancellationToken);
}

public sealed record DocumentContentResult(Stream Stream, string ContentType, string FileName);
