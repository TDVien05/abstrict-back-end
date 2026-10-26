using System.ComponentModel.DataAnnotations;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;

namespace Abstrict.Api.DTOs.Requests;

/// <summary>Các trường chung khi tạo hoặc sửa gói. Mã và đối tượng chỉ đặt lúc tạo.</summary>
public abstract class PromotionPlanFields
{
    [Required, StringLength(PromotionPlanPolicy.MaximumNameLength * 2)]
    public required string Name { get; init; }

    [Required]
    public int? DurationDays { get; init; }

    [Required]
    public long? PriceVnd { get; init; }

    /// <summary>Bán kính ưu tiên hiển thị quanh khu vực nhận ca (km); bỏ trống nếu gói không giới hạn theo bán kính.</summary>
    public int? PriorityRadiusKm { get; init; }

    /// <summary>Số người làm tối đa được hưởng gói; chỉ dùng cho gói của doanh nghiệp.</summary>
    public int? WorkerLimit { get; init; }

    /// <summary>Danh sách quyền lợi hiển thị cho đối tác.</summary>
    [MaxLength(PromotionPlanPolicy.MaximumBenefits * 2)]
    public IReadOnlyCollection<string>? Benefits { get; init; }

    /// <summary>Gói đang bán hay không; mặc định bật.</summary>
    public bool IsActive { get; init; } = true;
}

public sealed class CreatePromotionPlanRequest : PromotionPlanFields
{
    [Required, StringLength(PromotionPlanPolicy.MaximumCodeLength * 2)]
    public required string Code { get; init; }

    [Required]
    public PromotionAudience? Audience { get; init; }
}

public sealed class UpdatePromotionPlanRequest : PromotionPlanFields;
