using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Abstrict.Api.Options;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Services.Implementations;

public sealed record AdministrativeWard(string Code, string Name);

public sealed record AdministrativeDistrict(int Code, string Name, IReadOnlyList<AdministrativeWard> Wards);

/// <summary><c>Name</c> bỏ tiền tố "Thành phố"/"Tỉnh" (ví dụ "Hồ Chí Minh").</summary>
public sealed record AdministrativeProvince(int Code, string Name, IReadOnlyList<AdministrativeDistrict> Districts);

/// <param name="IsFallback"><c>true</c> khi chưa tải được AddressKit và đang dùng dữ liệu nhúng (không có phường/xã).</param>
public sealed record AdministrativeSnapshot(IReadOnlyList<AdministrativeProvince> Provinces, bool IsFallback, DateTimeOffset ExpiresAtUtc)
{
    // Mã quận/huyện không duy nhất toàn quốc (một số mã xuất hiện ở nhiều tỉnh), nên tra theo cả mã tỉnh.
    public AdministrativeDistrict? FindDistrict(int provinceCode, int districtCode) =>
        Provinces.FirstOrDefault(p => p.Code == provinceCode)?.Districts.FirstOrDefault(d => d.Code == districtCode);
}

/// <summary>
/// Danh mục hành chính (tỉnh → quận/huyện → phường/xã) lấy từ Cas AddressKit: hai request mỗi lần làm mới
/// (<c>/provinces</c> và <c>/communes</c>), giữ trong bộ nhớ <see cref="AddressKitOptions.CacheHours"/> giờ.
/// Lỗi mạng giữ lại bản cũ; chưa có bản nào thì dùng danh mục nhúng (tỉnh và quận/huyện, không có phường/xã) và thử lại sau vài phút.
/// </summary>
public sealed partial class AdministrativeDirectory(
    IHttpClientFactory httpClientFactory,
    IOptions<AddressKitOptions> options,
    TimeProvider timeProvider,
    ILogger<AdministrativeDirectory> logger)
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile AdministrativeSnapshot? _snapshot;

    public async Task<AdministrativeSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        var current = _snapshot;
        if (current is not null && timeProvider.GetUtcNow() < current.ExpiresAtUtc)
            return current;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            current = _snapshot;
            var now = timeProvider.GetUtcNow();
            if (current is not null && now < current.ExpiresAtUtc)
                return current;

            try
            {
                _snapshot = await FetchAsync(cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Không tải được danh mục hành chính từ AddressKit; {Action}.",
                    current is { IsFallback: false } ? "giữ bản đã tải trước đó" : "dùng danh mục nhúng");
                _snapshot = current is { IsFallback: false }
                    ? current with { ExpiresAtUtc = now + RetryDelay }
                    : Fallback(now + RetryDelay);
            }

            return _snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AdministrativeSnapshot> FetchAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var date = Uri.EscapeDataString(settings.EffectiveDate);
        using var client = httpClientFactory.CreateClient(AddressKitOptions.HttpClientName);

        var provinces = (await client.GetFromJsonAsync<ProvincesPayload>($"{date}/provinces", cancellationToken))?.Provinces ?? [];
        var communes = (await client.GetFromJsonAsync<CommunesPayload>($"{date}/communes", cancellationToken))?.Communes ?? [];
        if (provinces.Count == 0 || communes.Count == 0)
            throw new InvalidOperationException("AddressKit trả danh mục rỗng.");

        var districtsByProvince = communes
            .Where(c => !string.IsNullOrWhiteSpace(c.DistrictCode) && !string.IsNullOrWhiteSpace(c.DistrictName))
            .GroupBy(c => c.ProvinceCode)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AdministrativeDistrict>)g
                    .GroupBy(c => c.DistrictCode!)
                    .Select(d => new AdministrativeDistrict(
                        int.Parse(d.Key), d.First().DistrictName!,
                        d.OrderBy(c => c.Code, StringComparer.Ordinal).Select(c => new AdministrativeWard(c.Code, c.Name)).ToList()))
                    .OrderBy(d => d.Code)
                    .ToList());

        var result = provinces
            .OrderBy(p => p.Code, StringComparer.Ordinal)
            .Select(p => new AdministrativeProvince(int.Parse(p.Code), ProvincePrefix().Replace(p.Name, string.Empty),
                districtsByProvince.GetValueOrDefault(p.Code) ?? []))
            .ToList();

        return new AdministrativeSnapshot(result, false, timeProvider.GetUtcNow() + TimeSpan.FromHours(Math.Max(1, settings.CacheHours)));
    }

    private static AdministrativeSnapshot Fallback(DateTimeOffset expiresAtUtc) => new(
        VietnamAdministrativeDivisions.All
            .Select(p => new AdministrativeProvince(p.Code, p.Name, p.Districts.Select(d => new AdministrativeDistrict(d.Code, d.Name, [])).ToList()))
            .ToList(),
        true, expiresAtUtc);

    [GeneratedRegex(@"^(Thành phố|Tỉnh)\s+")]
    private static partial Regex ProvincePrefix();

    private sealed record ProvincesPayload([property: JsonPropertyName("provinces")] List<ProvinceItem>? Provinces);

    private sealed record ProvinceItem(string Code, string Name);

    private sealed record CommunesPayload([property: JsonPropertyName("communes")] List<CommuneItem>? Communes);

    private sealed record CommuneItem(string Code, string Name, string? DistrictCode, string? DistrictName, string ProvinceCode);
}
