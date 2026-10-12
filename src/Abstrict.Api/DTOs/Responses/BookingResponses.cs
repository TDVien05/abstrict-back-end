using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Responses;

public sealed record CustomerAddressResponse(
    Guid Id,
    string Label,
    string ApartmentNumber,
    string? Floor,
    string BuildingName,
    string StreetAddress,
    string Ward,
    string District,
    string City,
    string? AccessInstructions,
    bool IsDefault);

public sealed record AvailabilityWindowResponse(Guid Id, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

public sealed record TimeRangeResponse(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

public sealed record AvailabilityDayResponse(
    DateOnly Date,
    IReadOnlyCollection<TimeRangeResponse> Free,
    IReadOnlyCollection<TimeRangeResponse> Busy);

/// <summary>Lịch rảnh/bận công khai: khoảng bận không kèm thông tin khách hay đơn.</summary>
public sealed record ProviderAvailabilityResponse(
    Guid ProviderId,
    string TimeZone,
    int SlotStepMinutes,
    int MinimumDurationMinutes,
    IReadOnlyCollection<AvailabilityDayResponse> Days);

public sealed record ChecklistItemResponse(
    Guid Id,
    Guid? ParentItemId,
    string AreaName,
    string Description,
    int SortOrder,
    bool IsRequired);

public sealed record ChecklistTemplateResponse(
    Guid TemplateId,
    string Name,
    int Version,
    IReadOnlyCollection<ChecklistItemResponse> Items);

public sealed record BookingQuoteResponse(
    long UnitPriceVnd,
    int DurationMinutes,
    long BaseAmountVnd,
    long SurchargeAmountVnd,
    long DepositAmountVnd,
    int DepositPercent,
    long RemainingAmountVnd,
    int HoldMinutes);

public sealed record BookingSummaryResponse(
    Guid Id,
    string BookingNumber,
    BookingStatus Status,
    Guid ProviderId,
    string ProviderDisplayName,
    string ServiceCategoryCode,
    string ServiceCategoryName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    long BaseAmountVnd,
    long DepositAmountVnd,
    DateTimeOffset? HoldExpiresAtUtc);

public sealed record BookingTaskResponse(Guid Id, string AreaName, string Description, int SortOrder, BookingTaskStatus Status);

/// <summary><c>Reason</c> là mã ổn định (Created, DepositPaid, …); <c>Note</c> là ghi chú tự do của người hủy/từ chối.</summary>
public sealed record BookingTimelineEntryResponse(BookingStatus FromStatus, BookingStatus ToStatus, string? Reason, string? Note, DateTimeOffset AtUtc);

public enum BookingAction { PayDeposit, Cancel, ShowCheckInQr }

public sealed record BookingDetailResponse(
    Guid Id,
    string BookingNumber,
    BookingStatus Status,
    Guid ProviderId,
    string ProviderDisplayName,
    string ServiceCategoryCode,
    string ServiceCategoryName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    long BaseAmountVnd,
    long DepositAmountVnd,
    DateTimeOffset? HoldExpiresAtUtc,
    string AddressSnapshot,
    string? CustomerNote,
    long UnitPriceVnd,
    long SurchargeAmountVnd,
    long ExtensionAmountVnd,
    long RemainingAmountVnd,
    DateTimeOffset? ProviderConfirmationDeadlineUtc,
    DateTimeOffset? CancelledAtUtc,
    string? CancellationReason,
    PaymentStatus? DepositPaymentStatus,
    DepositPaymentResponse? DepositPayment,
    IReadOnlyCollection<BookingTaskResponse> Tasks,
    IReadOnlyCollection<BookingTimelineEntryResponse> Timeline,
    IReadOnlyCollection<BookingAction> AllowedActions,
    DateTimeOffset ServerTimeUtc);

public sealed record DepositPaymentResponse(
    Guid PaymentId,
    PaymentStatus Status,
    long AmountVnd,
    PaymentMethod Method,
    DateTimeOffset? ExpiresAtUtc,
    string? CheckoutUrl);

public sealed record CancellationPreviewResponse(
    bool FreeCancellation,
    long RefundAmountVnd,
    long RetainedAmountVnd,
    DateTimeOffset? FreeCancellationUntilUtc);

public sealed record CheckInTokenResponse(string Token, DateTimeOffset ExpiresAtUtc, DateTimeOffset ServerTimeUtc);

public enum FreelancerBookingAction { Confirm, Reject }

/// <summary>Số đơn theo từng trạng thái; trạng thái không có đơn vẫn xuất hiện với giá trị 0.</summary>
public sealed record FreelancerBookingCountsResponse(IReadOnlyDictionary<BookingStatus, int> Counts, int Total);

public sealed record FreelancerBookingSummaryResponse(
    Guid Id,
    string BookingNumber,
    BookingStatus Status,
    string CustomerName,
    string ServiceCategoryCode,
    string ServiceCategoryName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    long BaseAmountVnd,
    long DepositAmountVnd,
    string? AreaLabel,
    DateTimeOffset? ProviderConfirmationDeadlineUtc,
    IReadOnlyCollection<FreelancerBookingAction> AllowedActions);

/// <summary>Địa chỉ đầy đủ chỉ có khi đơn đã được xác nhận; không bao giờ kèm số điện thoại khách.</summary>
public sealed record FreelancerBookingDetailResponse(
    Guid Id,
    string BookingNumber,
    BookingStatus Status,
    string CustomerName,
    string ServiceCategoryCode,
    string ServiceCategoryName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    long BaseAmountVnd,
    long DepositAmountVnd,
    string? AreaLabel,
    DateTimeOffset? ProviderConfirmationDeadlineUtc,
    string? AddressSnapshot,
    string? CustomerNote,
    IReadOnlyCollection<BookingTaskResponse> Tasks,
    IReadOnlyCollection<BookingTimelineEntryResponse> Timeline,
    IReadOnlyCollection<FreelancerBookingAction> AllowedActions,
    DateTimeOffset ServerTimeUtc);
