namespace Abstrict.Api.Options;

/// <summary>Cấu hình nguồn dữ liệu hành chính Cas AddressKit (https://addresskit.cas.so/).</summary>
public sealed class AddressKitOptions
{
    public const string SectionName = "AddressKit";
    public const string HttpClientName = "addresskit";

    public string BaseUrl { get; set; } = "https://production.cas.so/address-kit/";

    /// <summary>
    /// Ngày hiệu lực của danh mục (<c>yyyy-MM-dd</c> hoặc <c>latest</c>). Mặc định <c>2025-06-30</c>: danh mục 3 cấp
    /// (tỉnh - quận/huyện - phường/xã) trước ngày bỏ cấp quận/huyện, khớp với khu vực dịch vụ đang lưu theo quận/huyện.
    /// <c>latest</c> trả danh mục 2 cấp (không có quận/huyện).
    /// </summary>
    public string EffectiveDate { get; set; } = "2025-06-30";

    /// <summary>Thời gian giữ dữ liệu trong bộ nhớ trước khi tải lại.</summary>
    public int CacheHours { get; set; } = 24;

    public int TimeoutSeconds { get; set; } = 30;
}
