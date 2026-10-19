using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Interfaces;

/// <summary>Booking phía khách hàng: báo giá, tạo đơn giữ slot, xem, hủy và phát hành QR check-in.</summary>
public interface IBookingService
{
    Task<BookingQuoteResponse> QuoteAsync(Guid userId, CreateBookingRequest request, CancellationToken cancellationToken);
    Task<BookingDetailResponse> CreateAsync(Guid userId, CreateBookingRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<BookingSummaryResponse>> ListAsync(Guid userId, BookingStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<BookingDetailResponse> GetAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken);
    Task<CancellationPreviewResponse> PreviewCancellationAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken);
    Task<BookingDetailResponse> CancelAsync(Guid userId, Guid bookingId, string? reason, CancellationToken cancellationToken);
    Task<BookingDetailResponse> AcceptWorkAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken);
    Task<CheckInTokenResponse> IssueCheckInTokenAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken);
    Task<BookingPhotoContent?> OpenPhotoAsync(Guid userId, Guid bookingId, Guid photoId, CancellationToken cancellationToken);
}

public sealed record BookingPhotoContent(Stream Stream, string ContentType);
