using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class RatingService(AppDbContext db, TimeProvider clock) : IRatingService
{
    public async Task<RatingResponse> CreateProviderRatingAsync(Guid customerUserId, Guid bookingId, CreateProviderRatingRequest request, CancellationToken cancellationToken)
    {
        if (request.Score is < 1 or > 5)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_RATING_SCORE", "Điểm đánh giá phải từ 1 đến 5.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var booking = await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE id = {bookingId} FOR UPDATE")
            .SingleOrDefaultAsync(x => x.CustomerUserId == customerUserId, cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "BOOKING_NOT_FOUND", "Không tìm thấy đơn đặt dịch vụ.");
        if (booking.Status != BookingStatus.Completed)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_NOT_COMPLETED", "Chỉ có thể đánh giá sau khi đơn hoàn tất.");
        if (await db.Ratings.AnyAsync(x => x.BookingId == bookingId && x.ReviewerUserId == customerUserId, cancellationToken))
            throw new BookingFlowException(StatusCodes.Status409Conflict, "BOOKING_ALREADY_RATED", "Đơn này đã được đánh giá.");

        // Serialize score aggregation per provider so simultaneous ratings cannot overwrite each other.
        var provider = await db.Providers.FromSqlInterpolated($"SELECT * FROM providers WHERE id = {booking.ProviderId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var rating = new Rating
        {
            BookingId = bookingId,
            ReviewerUserId = customerUserId,
            SubjectType = RatingSubjectType.Provider,
            SubjectProviderId = booking.ProviderId,
            Score = request.Score,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            PublishAfterUtc = now,
            PublishedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Ratings.Add(rating);
        provider.AverageRating = Math.Round(((provider.AverageRating * provider.RatingCount) + request.Score) / (provider.RatingCount + 1d), 2);
        provider.RatingCount++;
        provider.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(rating, booking, provider);
    }

    public async Task<RatingResponse> GetCustomerRatingAsync(Guid customerUserId, Guid bookingId, CancellationToken cancellationToken)
    {
        var rating = await db.Ratings.AsNoTracking()
            .Include(x => x.Booking).ThenInclude(x => x.Provider)
            .SingleOrDefaultAsync(x => x.BookingId == bookingId && x.ReviewerUserId == customerUserId && x.SubjectType == RatingSubjectType.Provider, cancellationToken)
            ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "RATING_NOT_FOUND", "Đơn này chưa có đánh giá của bạn.");
        return ToResponse(rating, rating.Booking, rating.Booking.Provider);
    }

    public async Task<PagedResponse<PublicRatingResponse>> ListProviderRatingsAsync(Guid providerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Ratings.AsNoTracking().Where(x => x.SubjectProviderId == providerId
            && x.SubjectType == RatingSubjectType.Provider && x.PublishedAtUtc != null && x.PublishedAtUtc <= clock.GetUtcNow());
        if (!await db.Providers.AnyAsync(x => x.Id == providerId && x.Type == ProviderType.Freelancer && x.ApprovalStatus == ApprovalStatus.Approved, cancellationToken))
            throw new BookingFlowException(StatusCodes.Status404NotFound, "FREELANCER_NOT_FOUND", "Không tìm thấy người làm.");
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.PublishedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new PublicRatingResponse(x.Id, x.BookingId, x.Booking.BookingNumber, x.Score, x.Comment, x.PublishedAtUtc!.Value))
            .ToListAsync(cancellationToken);
        return new PagedResponse<PublicRatingResponse>(rows, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    private static RatingResponse ToResponse(Rating rating, Booking booking, Provider provider) => new(
        rating.Id, booking.Id, booking.BookingNumber, provider.Id, provider.DisplayName, rating.Score, rating.Comment, rating.CreatedAtUtc, rating.PublishedAtUtc);
}
