using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Interfaces;

public interface IAdminCompanyReviewService
{
    /// <param name="status">Null = hàng chờ (Submitted + UnderReview).</param>
    /// <param name="page">Trang, bắt đầu từ 1.</param>
    /// <param name="pageSize">Số dòng mỗi trang, tối đa 50.</param>
    /// <param name="cancellationToken">Token hủy.</param>
    Task<PagedResponse<AdminCompanyListItemResponse>> ListAsync(ApprovalStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminCompanyDetailResponse> GetAsync(Guid adminUserId, Guid providerId, CancellationToken cancellationToken);
    Task<AdminCompanyDetailResponse> StartReviewAsync(Guid adminUserId, Guid providerId, CancellationToken cancellationToken);
    Task<AdminCompanyDetailResponse> DecideAsync(Guid adminUserId, Guid providerId, KycDecisionRequest request, CancellationToken cancellationToken);
    Task<KycDocumentContent?> OpenDocumentAsync(Guid adminUserId, Guid providerId, Guid documentId, CancellationToken cancellationToken);
}
