using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Interfaces;

public interface IAdminKycService
{
    /// <param name="status">Null = hàng chờ (Submitted + UnderReview).</param>
    Task<PagedResponse<AdminKycListItemResponse>> ListAsync(ApprovalStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminKycDetailResponse> GetAsync(Guid adminUserId, Guid providerId, CancellationToken cancellationToken);
    Task<AdminKycDetailResponse> StartReviewAsync(Guid adminUserId, Guid providerId, CancellationToken cancellationToken);
    Task<AdminKycDetailResponse> DecideAsync(Guid adminUserId, Guid providerId, KycDecisionRequest request, CancellationToken cancellationToken);
    Task<KycDocumentContent?> OpenDocumentAsync(Guid adminUserId, Guid providerId, Guid documentId, CancellationToken cancellationToken);
}
