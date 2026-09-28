using System.Text.Json;
using System.Text.Json.Serialization;

namespace Abstrict.Api.Integrations.Identity;

public sealed class FptAiEnvelope
{
    [JsonPropertyName("errorCode")]
    public JsonElement ErrorCode { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("data")]
    public List<Dictionary<string, JsonElement>>? Data { get; set; }
}

public static class FptAiResponseMapper
{
    private static readonly string[] DocumentNumberKeys = ["id", "document_number", "id_number", "so"];
    private static readonly string[] NameKeys = ["name", "full_name", "ho_ten"];
    private static readonly string[] DateOfBirthKeys = ["dob", "date_of_birth", "birth_day", "ngay_sinh"];
    private static readonly string[] GenderKeys = ["sex", "gender", "gioi_tinh"];
    private static readonly string[] AddressKeys = ["address", "permanent_address", "dia_chi", "origin_location"];
    private static readonly string[] IssueDateKeys = ["issue_date", "ngay_cap"];
    private static readonly string[] IssuePlaceKeys = ["issue_loc", "issue_place", "noi_cap"];
    private static readonly string[] ExpiryDateKeys = ["expiry_date", "doe", "ngay_het_han"];
    private static readonly string[] DocumentTypeKeys = ["type", "document_type", "loai"];
    private static readonly string[] FaceKeys = ["face", "face_url", "face_image"];

    public static FptAiIdentityResult Map(FptAiEnvelope envelope, string? providerRequestId)
    {
        if (envelope.ErrorCode.ValueKind == JsonValueKind.Number && envelope.ErrorCode.GetInt32() != 0)
            return FptAiIdentityResult.Failure(
                $"FPT_{envelope.ErrorCode.GetInt32()}", envelope.ErrorMessage ?? "FPT AI trả mã lỗi.", providerRequestId);

        if (envelope.ErrorCode.ValueKind == JsonValueKind.String &&
            !string.Equals(envelope.ErrorCode.GetString(), "0", StringComparison.Ordinal))
            return FptAiIdentityResult.Failure(
                "FPT_UNKNOWN", envelope.ErrorMessage ?? "FPT AI trả mã lỗi.", providerRequestId);

        var record = envelope.Data?.FirstOrDefault();
        if (record is null)
            return FptAiIdentityResult.Failure("OCR_INCOMPLETE", "FPT AI không trả dữ liệu nhận dạng.", providerRequestId);

        var confidences = new List<FptAiFieldConfidence>();
        foreach (var pair in record)
        {
            if (pair.Value.ValueKind == JsonValueKind.Number && pair.Value.TryGetDecimal(out var value))
                confidences.Add(new FptAiFieldConfidence(pair.Key, value));
        }

        return new FptAiIdentityResult(
            true,
            null,
            null,
            providerRequestId,
            First(record, DocumentNumberKeys),
            First(record, NameKeys),
            First(record, DateOfBirthKeys),
            First(record, GenderKeys),
            First(record, AddressKeys),
            First(record, IssueDateKeys),
            First(record, IssuePlaceKeys),
            First(record, ExpiryDateKeys),
            First(record, DocumentTypeKeys),
            First(record, FaceKeys),
            confidences);
    }

    private static string? First(Dictionary<string, JsonElement> record, string[] keys)
    {
        foreach (var key in keys)
        {
            if (!record.TryGetValue(key, out var element))
                continue;

            var value = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
