using System.ComponentModel.DataAnnotations;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Requests;

public sealed class SaveCustomerAddressRequest
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string Label { get; init; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 1)]
    public string ApartmentNumber { get; init; } = string.Empty;

    [StringLength(20)]
    public string? Floor { get; init; }

    [Required, StringLength(160, MinimumLength = 1)]
    public string BuildingName { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string StreetAddress { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 1)]
    public string Ward { get; init; } = string.Empty;

    /// <summary>Phải khớp chính xác <c>district</c> của một khu vực trong <c>/catalog/service-areas</c> để đặt được lịch.</summary>
    [Required, StringLength(120, MinimumLength = 1)]
    public string District { get; init; } = string.Empty;

    /// <summary>Phải khớp chính xác <c>city</c> của một khu vực trong <c>/catalog/service-areas</c> để đặt được lịch.</summary>
    [Required, StringLength(120, MinimumLength = 1)]
    public string City { get; init; } = string.Empty;

    [StringLength(500)]
    public string? AccessInstructions { get; init; }

    public bool IsDefault { get; init; }
}

public sealed class CreateAvailabilityWindowRequest
{
    public DateTimeOffset StartsAtUtc { get; init; }

    public DateTimeOffset EndsAtUtc { get; init; }
}

/// <summary>Dùng chung cho báo giá và tạo booking. Số tiền luôn do server tính.</summary>
public sealed class CreateBookingRequest
{
    public Guid ProviderId { get; init; }

    public Guid ServiceCategoryId { get; init; }

    public Guid CustomerAddressId { get; init; }

    public DateTimeOffset StartsAtUtc { get; init; }

    public DateTimeOffset EndsAtUtc { get; init; }

    /// <summary>Các mục checklist tùy chọn; mục bắt buộc của template luôn được thêm.</summary>
    [MaxLength(100)]
    public IReadOnlyCollection<Guid>? ChecklistItemIds { get; init; }

    [StringLength(1000)]
    public string? CustomerNote { get; init; }
}

public sealed class InitiateDepositRequest
{
    public PaymentMethod Method { get; init; }
}

public sealed class CancelBookingRequest
{
    [StringLength(500)]
    public string? Reason { get; init; }
}

public sealed class RejectBookingRequest
{
    [StringLength(500)]
    public string? Reason { get; init; }
}

/// <summary>Token quét từ QR check-in do khách hiển thị; ngắn hạn và dùng một lần.</summary>
public sealed class CheckInRequest
{
    [Required, StringLength(128, MinimumLength = 8)]
    public string Token { get; init; } = string.Empty;
}

/// <summary>Trạng thái mới cho một mục checklist trong ca; chỉ nhận Pending, InProgress hoặc Completed.</summary>
public sealed class UpdateBookingTaskStatusRequest
{
    public BookingTaskStatus Status { get; init; }
}

public sealed class SandboxPaymentResultRequest
{
    public bool Succeeded { get; init; }
}
