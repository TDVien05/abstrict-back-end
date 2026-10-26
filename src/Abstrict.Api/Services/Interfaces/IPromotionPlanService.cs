using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Interfaces;

public interface IPromotionPlanService
{
    /// <summary>Mọi gói (kể cả đang tắt) để admin quản lý, lọc theo đối tượng và trạng thái; gói rẻ và ngắn hạn lên trước.</summary>
    Task<IReadOnlyList<AdminPromotionPlanResponse>> ListAdminAsync(PromotionAudience? audience, bool? isActive, CancellationToken cancellationToken);
    Task<PromotionReportResponse> GetReportAsync(CancellationToken cancellationToken);
    Task<AdminPromotionPlanResponse> GetAdminAsync(Guid planId, CancellationToken cancellationToken);
    Task<AdminPromotionPlanResponse> CreateAsync(Guid adminUserId, CreatePromotionPlanRequest request, CancellationToken cancellationToken);
    Task<AdminPromotionPlanResponse> UpdateAsync(Guid adminUserId, Guid planId, UpdatePromotionPlanRequest request, CancellationToken cancellationToken);

    /// <summary>Chỉ xóa được gói chưa có lượt mua; nếu không, tắt gói.</summary>
    Task DeleteAsync(Guid adminUserId, Guid planId, CancellationToken cancellationToken);

    /// <summary>Các gói đang bán cho đúng đối tượng của đối tác đang đăng nhập (freelancer hoặc doanh nghiệp).</summary>
    Task<IReadOnlyList<PartnerPromotionPlanResponse>> ListForPartnerAsync(Guid userId, CancellationToken cancellationToken);
}
