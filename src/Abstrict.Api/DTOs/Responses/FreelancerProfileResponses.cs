using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Responses;

/// <summary><c>IsListed</c> là <c>true</c> khi dịch vụ đang bật và danh mục dịch vụ vẫn hoạt động, tức là đang hiện công khai và đặt được.</summary>
public sealed record MyFreelancerServiceResponse(
    Guid ServiceCategoryId, string Code, string Name, long HourlyRateVnd, string? Description, bool IsActive, bool IsListed);

public sealed record MyFreelancerServiceAreaResponse(
    Guid ServiceAreaId, string City, string District, string? WardOrComplex, int? TravelRadiusKm, bool IsActive);

/// <summary>
/// Hồ sơ của chính freelancer, gồm cả phần chưa hiện công khai (dịch vụ đang tắt, hồ sơ chưa duyệt).
/// <c>MissingForBookings</c> liệt kê điều kiện còn thiếu để nhận đơn: <c>NOT_APPROVED</c>, <c>NO_ACTIVE_SERVICE</c>, <c>NO_SERVICE_AREA</c>.
/// <c>AcceptingBookingsPaused</c> chỉ <c>true</c> trong phản hồi của thao tác vừa làm hệ thống tự tắt nhận đơn vì không còn đủ điều kiện.
/// </summary>
public sealed record MyFreelancerProfileResponse(
    Guid ProviderId,
    string DisplayName,
    ApprovalStatus ApprovalStatus,
    string? Bio,
    int ExperienceYears,
    bool IsAcceptingBookings,
    bool AutoAcceptBookings,
    bool CanAcceptBookings,
    IReadOnlyCollection<string> MissingForBookings,
    bool AcceptingBookingsPaused,
    IReadOnlyCollection<MyFreelancerServiceResponse> Services,
    IReadOnlyCollection<MyFreelancerServiceAreaResponse> ServiceAreas);
