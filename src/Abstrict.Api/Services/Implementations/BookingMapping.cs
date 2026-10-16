using System.Security.Cryptography;
using System.Text;
using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;

namespace Abstrict.Api.Services.Implementations;

public static class BookingNumbers
{
    // Bỏ các ký tự dễ nhầm (0/O, 1/I/L).
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate(DateTimeOffset nowUtc) =>
        $"AB{nowUtc.ToOffset(VietnamTime.Offset):yyMMdd}{RandomNumberGenerator.GetString(Alphabet, 6)}";
}

public static class CheckInTokens
{
    /// <summary>Sinh token thô để hiển thị QR; DB chỉ lưu <see cref="Hash"/>.</summary>
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}

public static class BookingMapping
{
    public static string ComposeAddressSnapshot(CustomerAddress address)
    {
        var parts = new[]
        {
            address.ApartmentNumber, address.Floor, address.BuildingName, address.StreetAddress,
            address.Ward, address.District, address.City
        }.Where(x => !string.IsNullOrWhiteSpace(x));
        var snapshot = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(address.AccessInstructions)
            ? snapshot
            : $"{snapshot} (Ghi chú: {address.AccessInstructions})";
    }

    public static bool IsCheckInOpen(Booking booking, DateTimeOffset now, BookingOptions options) =>
        IsCheckInOpen(booking.Status, booking.StartsAtUtc, booking.EndsAtUtc, now, options);

    public static bool IsCheckInOpen(BookingStatus status, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, DateTimeOffset now, BookingOptions options) =>
        status == BookingStatus.Confirmed
        && now >= startsAtUtc.AddMinutes(-options.CheckInOpensMinutesBeforeStart)
        && now <= endsAtUtc;

    public static List<BookingAction> AllowedActions(Booking booking, DateTimeOffset now, BookingOptions options)
    {
        var actions = new List<BookingAction>();
        if (booking.Status == BookingStatus.PendingDeposit && booking.HoldExpiresAtUtc >= now)
            actions.Add(BookingAction.PayDeposit);
        if (booking.Status is BookingStatus.PendingDeposit or BookingStatus.PendingProviderConfirmation or BookingStatus.Confirmed)
            actions.Add(BookingAction.Cancel);
        if (IsCheckInOpen(booking, now, options))
            actions.Add(BookingAction.ShowCheckInQr);
        return actions;
    }

    public static List<FreelancerBookingAction> FreelancerActions(
        BookingStatus status, DateTimeOffset? confirmationDeadlineUtc, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, DateTimeOffset now, BookingOptions options)
    {
        var actions = new List<FreelancerBookingAction>();
        if (status == BookingStatus.PendingProviderConfirmation)
        {
            if (confirmationDeadlineUtc is null || confirmationDeadlineUtc >= now)
                actions.Add(FreelancerBookingAction.Confirm);
            actions.Add(FreelancerBookingAction.Reject);
        }
        if (IsCheckInOpen(status, startsAtUtc, endsAtUtc, now, options))
            actions.Add(FreelancerBookingAction.CheckIn);
        if (status is BookingStatus.CheckedIn or BookingStatus.InProgress)
        {
            actions.Add(FreelancerBookingAction.UploadPhotos);
            actions.Add(FreelancerBookingAction.CompleteWork);
        }
        return actions;
    }

    public static List<BookingPhotoResponse> Photos(Booking booking, string contentRouteBase) =>
        booking.Photos.OrderBy(x => x.TakenAtUtc)
            .Select(x => new BookingPhotoResponse(
                x.Id, x.Type, x.BookingTaskId, x.Caption, x.TakenAtUtc, x.UploadedByUserId, $"{contentRouteBase}/{x.Id}/content"))
            .ToList();


    public static List<BookingTaskResponse> Tasks(Booking booking) =>
        booking.Tasks.OrderBy(x => x.SortOrder)
            .Select(x => new BookingTaskResponse(x.Id, x.AreaNameSnapshot, x.DescriptionSnapshot, x.SortOrder, x.Status))
            .ToList();

    public static List<BookingTimelineEntryResponse> Timeline(Booking booking) =>
        booking.StatusHistory.OrderBy(x => x.CreatedAtUtc)
            .Select(x => new BookingTimelineEntryResponse(x.FromStatus, x.ToStatus, x.Reason, x.Note, x.CreatedAtUtc))
            .ToList();

    /// <summary>Cần nạp sẵn Provider, ServiceCategory, Tasks, StatusHistory, Photos và Payments.</summary>
    public static BookingDetailResponse ToDetail(Booking booking, DateTimeOffset now, BookingOptions options)
    {
        var deposit = booking.Payments.Where(x => x.Purpose == PaymentPurpose.Deposit)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
        var photosRoute = $"/{ApiRoutes.Version1}/bookings/{booking.Id}/photos";
        return new BookingDetailResponse(
            booking.Id, booking.BookingNumber, booking.Status, booking.ProviderId, booking.Provider.DisplayName,
            booking.ServiceCategory.Code, booking.ServiceCategory.Name, booking.StartsAtUtc, booking.EndsAtUtc,
            booking.BaseAmountVnd, booking.DepositAmountVnd, booking.HoldExpiresAtUtc, booking.AddressSnapshot, booking.CustomerNote,
            booking.UnitPriceVnd, booking.SurchargeAmountVnd, booking.ExtensionAmountVnd,
            booking.BaseAmountVnd + booking.SurchargeAmountVnd - booking.DepositAmountVnd,
            booking.ProviderConfirmationDeadlineUtc, booking.CancelledAtUtc, booking.CancellationReason, deposit?.Status,
            deposit is null ? null : new DepositPaymentResponse(
                deposit.Id, deposit.Status, deposit.AmountVnd, deposit.Method, booking.HoldExpiresAtUtc, null),
            Tasks(booking), Timeline(booking), AllowedActions(booking, now, options), now, Photos(booking, photosRoute));
    }
}
