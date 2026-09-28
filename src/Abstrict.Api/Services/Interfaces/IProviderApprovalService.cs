using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface IProviderApprovalService
{
    Task<PagedResponse<AdminFreelancerApplicationSummaryResponse>> ListAsync(string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminFreelancerApplicationDetailResponse?> GetDetailAsync(Guid applicationId, CancellationToken cancellationToken);
    Task<DocumentContentResult?> GetDocumentContentAsync(Guid applicationId, Guid documentId, string? ipAddress, CancellationToken cancellationToken);
    Task<AdminFreelancerApplicationDetailResponse?> StartReviewAsync(Guid applicationId, int submissionVersion, Guid adminUserId, CancellationToken cancellationToken);
    Task<AdminFreelancerApplicationDetailResponse?> DecideAsync(Guid applicationId, AdminApplicationDecisionRequest request, Guid adminUserId, CancellationToken cancellationToken);
}
