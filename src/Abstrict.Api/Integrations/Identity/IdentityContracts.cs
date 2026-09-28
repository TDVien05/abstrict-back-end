namespace Abstrict.Api.Integrations.Identity;

public sealed record FptAiFieldConfidence(string Field, decimal? Confidence);

public sealed record FptAiIdentityResult(
    bool Succeeded,
    string? ErrorCode,
    string? ErrorMessage,
    string? ProviderRequestId,
    string? DocumentNumber,
    string? FullName,
    string? DateOfBirth,
    string? Gender,
    string? Address,
    string? IssueDate,
    string? IssuePlace,
    string? ExpiryDate,
    string? DocumentType,
    string? FaceImageUrl,
    IReadOnlyList<FptAiFieldConfidence> FieldConfidences)
{
    public static FptAiIdentityResult Failure(string errorCode, string errorMessage, string? providerRequestId = null) =>
        new(false, errorCode, errorMessage, providerRequestId, null, null, null, null, null, null, null, null, null, null, []);
}

public sealed record FaceDetectionResult(
    bool Succeeded,
    string? ErrorCode,
    string? ErrorMessage,
    int FaceCount,
    string? ProviderRequestId)
{
    public static FaceDetectionResult Failure(string errorCode, string errorMessage) =>
        new(false, errorCode, errorMessage, 0, null);
}

public sealed record FaceCompareResult(
    bool Succeeded,
    string? ErrorCode,
    string? ErrorMessage,
    decimal? Confidence,
    decimal? Threshold,
    string? ProviderRequestId)
{
    public static FaceCompareResult Failure(string errorCode, string errorMessage) =>
        new(false, errorCode, errorMessage, null, null, null);

    public static FaceCompareResult Failure(string errorCode, string errorMessage, string? providerRequestId) =>
        new(false, errorCode, errorMessage, null, null, providerRequestId);
}
