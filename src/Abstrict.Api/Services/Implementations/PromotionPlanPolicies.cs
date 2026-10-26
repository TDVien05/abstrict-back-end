using System.Text.Json;
using System.Text.RegularExpressions;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Services.Implementations;

/// <summary>Quy tắc dữ liệu của gói tăng hiển thị do admin cấu hình. Giá và thời hạn do admin quyết định, không hard-code ở nơi khác.</summary>
public static partial class PromotionPlanPolicy
{
    public const int MinimumCodeLength = 3;
    public const int MaximumCodeLength = 40;
    public const int MinimumNameLength = 2;
    public const int MaximumNameLength = 120;
    public const int MinimumDurationDays = 1;
    public const int MaximumDurationDays = 365;
    public const long MaximumPriceVnd = 100_000_000; // TODO: POLICY
    public const int MinimumPriorityRadiusKm = 1;
    public const int MaximumPriorityRadiusKm = 100;
    public const int MinimumWorkerLimit = 1;
    public const int MaximumWorkerLimit = 1000;
    public const int MaximumBenefits = 10;
    public const int MaximumBenefitLength = 200;

    /// <summary>Mã gói viết hoa, chỉ gồm chữ không dấu, số và dấu gạch dưới (ví dụ <c>FREELANCER_FEATURED_7D</c>).</summary>
    public static string NormalizeCode(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length is < MinimumCodeLength or > MaximumCodeLength || !CodePattern().IsMatch(normalized))
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_PLAN_CODE",
                $"Mã gói dài {MinimumCodeLength} đến {MaximumCodeLength} ký tự, chỉ gồm chữ không dấu, số và dấu gạch dưới.");
        return normalized;
    }

    public static string NormalizeName(string? name)
    {
        var normalized = string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length is < MinimumNameLength or > MaximumNameLength)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_PLAN_NAME",
                $"Tên gói dài {MinimumNameLength} đến {MaximumNameLength} ký tự.");
        return normalized;
    }

    public static void EnsureValidValues(PromotionAudience audience, int durationDays, long priceVnd, int? priorityRadiusKm, int? workerLimit)
    {
        if (durationDays is < MinimumDurationDays or > MaximumDurationDays)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_PLAN_DURATION",
                $"Thời hạn gói từ {MinimumDurationDays} đến {MaximumDurationDays} ngày.");
        if (priceVnd is < 0 or > MaximumPriceVnd)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_PLAN_PRICE", $"Giá gói từ 0 đến {MaximumPriceVnd} VND.");
        if (priorityRadiusKm is < MinimumPriorityRadiusKm or > MaximumPriorityRadiusKm)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_PRIORITY_RADIUS",
                $"Bán kính ưu tiên từ {MinimumPriorityRadiusKm} đến {MaximumPriorityRadiusKm} km.");
        if (workerLimit is { } limit)
        {
            if (audience != PromotionAudience.Company)
                throw new BookingFlowException(StatusCodes.Status400BadRequest, "WORKER_LIMIT_NOT_ALLOWED", "Giới hạn số người làm chỉ áp dụng cho gói của doanh nghiệp.");
            if (limit is < MinimumWorkerLimit or > MaximumWorkerLimit)
                throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_WORKER_LIMIT",
                    $"Giới hạn số người làm từ {MinimumWorkerLimit} đến {MaximumWorkerLimit}.");
        }
    }

    /// <summary>Cắt khoảng trắng, bỏ dòng trống; ném lỗi khi quá nhiều dòng hoặc dòng quá dài.</summary>
    public static IReadOnlyList<string> NormalizeBenefits(IReadOnlyCollection<string>? benefits)
    {
        var cleaned = (benefits ?? []).Select(x => x?.Trim() ?? string.Empty).Where(x => x.Length > 0).ToList();
        if (cleaned.Count > MaximumBenefits || cleaned.Any(x => x.Length > MaximumBenefitLength))
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "INVALID_PLAN_BENEFITS",
                $"Tối đa {MaximumBenefits} quyền lợi, mỗi quyền lợi tối đa {MaximumBenefitLength} ký tự.");
        return cleaned;
    }

    public static string? SerializeBenefits(IReadOnlyCollection<string> benefits) =>
        benefits.Count == 0 ? null : JsonSerializer.Serialize(benefits);

    /// <summary>Đọc cột <c>benefits_json</c>; dữ liệu không đúng dạng mảng chuỗi (ví dụ sửa tay trong DB) được coi là không có quyền lợi.</summary>
    public static IReadOnlyList<string> ParseBenefits(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static PromotionAudience AudienceOf(UserRole role) => role switch
    {
        UserRole.Freelancer => PromotionAudience.Freelancer,
        UserRole.Company => PromotionAudience.Company,
        _ => throw new BookingFlowException(StatusCodes.Status403Forbidden, "NOT_A_PARTNER", "Chỉ freelancer và doanh nghiệp xem được gói tăng hiển thị.")
    };

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_]*$")]
    private static partial Regex CodePattern();
}
