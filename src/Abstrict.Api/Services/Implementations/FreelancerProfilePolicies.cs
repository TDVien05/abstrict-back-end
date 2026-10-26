using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Implementations;

/// <summary>Quy tắc nghiệp vụ khi freelancer tự quản lý hồ sơ công khai (kỹ năng/giá, khu vực, giới thiệu, nhận đơn).</summary>
public static class FreelancerProfilePolicy
{
    public const long MinimumHourlyRateVnd = 10_000; // TODO: POLICY
    public const long MaximumHourlyRateVnd = 1_000_000; // TODO: POLICY
    public const int MaximumBioLength = 1000;
    public const int MaximumServiceDescriptionLength = 500;
    public const int MaximumServiceAreas = 20;
    public const int MinimumTravelRadiusKm = 1;
    public const int MaximumTravelRadiusKm = 100;
    public const int DefaultTravelRadiusKm = 10;

    public const string MissingNotApproved = "NOT_APPROVED";
    public const string MissingActiveService = "NO_ACTIVE_SERVICE";
    public const string MissingServiceArea = "NO_SERVICE_AREA";

    public static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static void EnsureValidHourlyRate(long hourlyRateVnd)
    {
        if (hourlyRateVnd < MinimumHourlyRateVnd || hourlyRateVnd > MaximumHourlyRateVnd)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_HOURLY_RATE",
                $"Giá theo giờ phải từ {MinimumHourlyRateVnd} đến {MaximumHourlyRateVnd} VND.");
    }

    /// <summary>Hồ sơ bị tạm ngưng thì chỉ được xem, không sửa; các trạng thái khác vẫn chuẩn bị hồ sơ được (hồ sơ chưa duyệt không hiện công khai).</summary>
    public static void EnsureEditable(ApprovalStatus status)
    {
        if (status == ApprovalStatus.Suspended)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "PROVIDER_SUSPENDED", "Hồ sơ đang bị tạm ngưng nên chưa thể chỉnh sửa.");
    }

    /// <summary>Những điều kiện còn thiếu để nhận đơn: đã duyệt, có ít nhất một dịch vụ đang bật và một khu vực phục vụ đang hoạt động.</summary>
    public static IReadOnlyList<string> MissingForBookings(ApprovalStatus status, int listedServiceCount, int activeServiceAreaCount)
    {
        var missing = new List<string>(3);
        if (status != ApprovalStatus.Approved)
            missing.Add(MissingNotApproved);
        if (listedServiceCount == 0)
            missing.Add(MissingActiveService);
        if (activeServiceAreaCount == 0)
            missing.Add(MissingServiceArea);
        return missing;
    }

    public static void EnsureCanAcceptBookings(IReadOnlyCollection<string> missing)
    {
        if (missing.Count > 0)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "PROFILE_NOT_READY",
                $"Chưa thể bật nhận đơn, còn thiếu: {string.Join(", ", missing)}.");
    }

    /// <summary>Kiểm tra danh sách khu vực mới và điền bán kính mặc định; ném lỗi khi trống, quá nhiều, trùng hoặc bán kính ngoài khoảng cho phép.</summary>
    public static IReadOnlyList<NormalizedServiceArea> NormalizeServiceAreas(IReadOnlyCollection<FreelancerServiceAreaInput> inputs)
    {
        if (inputs.Count is 0 or > MaximumServiceAreas)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_SERVICE_AREA", $"Cần chọn từ 1 đến {MaximumServiceAreas} khu vực.");
        if (inputs.Select(x => x.ServiceAreaId).Distinct().Count() != inputs.Count)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "DUPLICATE_SERVICE_AREA", "Mỗi khu vực chỉ được chọn một lần.");

        return inputs.Select(x =>
        {
            var radius = x.TravelRadiusKm ?? DefaultTravelRadiusKm;
            if (radius is < MinimumTravelRadiusKm or > MaximumTravelRadiusKm)
                throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_TRAVEL_RADIUS",
                    $"Bán kính di chuyển phải từ {MinimumTravelRadiusKm} đến {MaximumTravelRadiusKm} km.");
            return new NormalizedServiceArea(x.ServiceAreaId, radius);
        }).ToList();
    }
}

public readonly record struct NormalizedServiceArea(Guid ServiceAreaId, int TravelRadiusKm);
