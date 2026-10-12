using System.Reflection;
using System.Text.Json;

namespace Abstrict.Api.Services.Implementations;

/// <summary>63 tỉnh/thành và quận/huyện của Việt Nam, đọc một lần từ tài nguyên nhúng <c>vietnam-divisions.json</c>.</summary>
public static class VietnamAdministrativeDivisions
{
    public sealed record District(int Code, string Name);

    public sealed record Province(int Code, string Name, IReadOnlyList<District> Districts);

    private static readonly Lazy<IReadOnlyList<Province>> Loaded = new(Load);

    public static IReadOnlyList<Province> All => Loaded.Value;

    private static IReadOnlyList<Province> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("vietnam-divisions.json")
            ?? throw new InvalidOperationException("Thiếu tài nguyên nhúng vietnam-divisions.json.");
        var provinces = JsonSerializer.Deserialize<List<Province>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("vietnam-divisions.json không hợp lệ.");
        return provinces;
    }
}
