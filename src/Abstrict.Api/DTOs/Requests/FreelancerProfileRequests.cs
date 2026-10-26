using System.ComponentModel.DataAnnotations;
using Abstrict.Api.Services.Implementations;

namespace Abstrict.Api.DTOs.Requests;

/// <summary>Cập nhật một phần: trường không gửi (hoặc <c>null</c>) được giữ nguyên.</summary>
public sealed class UpdateFreelancerProfileRequest
{
    /// <summary>Giới thiệu bản thân. Gửi chuỗi rỗng để xóa.</summary>
    [StringLength(FreelancerProfilePolicy.MaximumBioLength)]
    public string? Bio { get; init; }

    public bool? IsAcceptingBookings { get; init; }

    public bool? AutoAcceptBookings { get; init; }
}

/// <summary>Thêm hoặc sửa một kỹ năng (dịch vụ cung cấp) cùng giá theo giờ.</summary>
public sealed class SaveFreelancerServiceRequest
{
    [Required]
    public long? HourlyRateVnd { get; init; }

    [StringLength(FreelancerProfilePolicy.MaximumServiceDescriptionLength)]
    public string? Description { get; init; }

    /// <summary>Tắt để tạm ngừng cung cấp dịch vụ này mà vẫn giữ giá; mặc định bật.</summary>
    public bool IsActive { get; init; } = true;
}

public sealed class FreelancerServiceAreaInput
{
    public Guid ServiceAreaId { get; init; }

    [Range(FreelancerProfilePolicy.MinimumTravelRadiusKm, FreelancerProfilePolicy.MaximumTravelRadiusKm)]
    public int? TravelRadiusKm { get; init; }
}

/// <summary>Thay toàn bộ danh sách khu vực nhận ca.</summary>
public sealed class ReplaceFreelancerServiceAreasRequest
{
    [Required, MinLength(1), MaxLength(FreelancerProfilePolicy.MaximumServiceAreas)]
    public IReadOnlyCollection<FreelancerServiceAreaInput> ServiceAreas { get; init; } = [];
}
