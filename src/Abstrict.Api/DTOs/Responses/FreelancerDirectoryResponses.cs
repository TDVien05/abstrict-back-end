namespace Abstrict.Api.DTOs.Responses;

public sealed record FreelancerServiceResponse(string Code, string Name, long HourlyRateVnd, string? Description);

public sealed record FreelancerServiceAreaResponse(Guid Id, string City, string District, string? WardOrComplex, int? TravelRadiusKm);

/// <summary>Hồ sơ công khai: không chứa số điện thoại, CCCD, ngày sinh hay địa chỉ chi tiết.</summary>
public sealed record FreelancerSummaryResponse(
    Guid ProviderId,
    string DisplayName,
    string? Bio,
    double AverageRating,
    int RatingCount,
    int CompletedBookingCount,
    int ExperienceYears,
    bool IsAcceptingBookings,
    long? MinHourlyRateVnd,
    IReadOnlyCollection<string> ServiceCodes);

public sealed record FreelancerDetailResponse(
    Guid ProviderId,
    string DisplayName,
    string? Bio,
    string Gender,
    double AverageRating,
    int RatingCount,
    int CompletedBookingCount,
    int ExperienceYears,
    bool IsAcceptingBookings,
    IReadOnlyCollection<FreelancerServiceResponse> Services,
    IReadOnlyCollection<FreelancerServiceAreaResponse> ServiceAreas);
