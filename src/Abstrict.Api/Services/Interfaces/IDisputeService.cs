using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Interfaces;

public interface IDisputeService
{
    IReadOnlyCollection<DisputeCategoryResponse> GetCategories();
    Task<DisputeResponse> OpenAsync(Guid customerUserId, Guid bookingId, OpenDisputeRequest request, CancellationToken cancellationToken);
    Task<DisputeEvidenceResponse> UploadEvidenceAsync(Guid customerUserId, Guid disputeId, string? description, long length, Stream content, CancellationToken cancellationToken);
    Task<DisputeEvidenceContent?> OpenEvidenceAsync(Guid userId, bool isAdmin, Guid disputeId, Guid evidenceId, CancellationToken cancellationToken);
    Task<PagedResponse<DisputeResponse>> ListCustomerAsync(Guid customerUserId, int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResponse<DisputeResponse>> ListAdminAsync(DisputeStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<DisputeResponse> GetCustomerAsync(Guid customerUserId, Guid disputeId, CancellationToken cancellationToken);
    Task<DisputeResponse> GetAdminAsync(Guid disputeId, CancellationToken cancellationToken);
    Task<DisputeResponse> AddCustomerMessageAsync(Guid customerUserId, Guid disputeId, AddDisputeMessageRequest request, CancellationToken cancellationToken);
    Task<DisputeResponse> AddAdminMessageAsync(Guid adminUserId, Guid disputeId, AddDisputeMessageRequest request, bool internalNote, CancellationToken cancellationToken);
    Task<DisputeResponse> DecideAsync(Guid adminUserId, Guid disputeId, DisputeDecisionRequest request, CancellationToken cancellationToken);
}

public sealed record DisputeEvidenceContent(Stream Stream, string ContentType);
