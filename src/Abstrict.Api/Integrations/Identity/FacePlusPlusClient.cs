using System.Net.Http.Headers;
using System.Text.Json;
using Abstrict.Api.Options;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Integrations.Identity;

public sealed class FacePlusPlusClient(HttpClient httpClient, IOptions<KycOptions> options) : IFaceVerificationClient
{
    private readonly FacePlusPlusOptions _settings = options.Value.FacePlusPlus;

    public async Task<FaceDetectionResult> DetectAsync(Stream image, string fileName, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(_settings.ApiKey), "api_key" },
            { new StringContent(_settings.ApiSecret), "api_secret" }
        };
        var fileContent = new StreamContent(image);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "image_file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.DetectPath) { Content = content };
        var (document, failure) = await SendAsync(request, "detect", cancellationToken);
        if (failure is not null || document is null)
            return FaceDetectionResult.Failure("KYC_PROVIDER_UNAVAILABLE", failure ?? "Face++ không phản hồi hợp lệ.");

        if (document.Value.TryGetProperty("error_message", out var errorMessage))
            return FaceDetectionResult.Failure("KYC_PROVIDER_UNAVAILABLE", errorMessage.GetString() ?? "Face++ trả lỗi.");

        var faceCount = document.Value.TryGetProperty("faces", out var faces) && faces.ValueKind == JsonValueKind.Array
            ? faces.GetArrayLength()
            : 0;
        var requestId = document.Value.TryGetProperty("request_id", out var id) ? id.GetString() : null;
        return new FaceDetectionResult(true, null, null, faceCount, requestId);
    }

    public async Task<FaceCompareResult> CompareAsync(Stream image1, string fileName1, Stream image2, string fileName2, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(_settings.ApiKey), "api_key" },
            { new StringContent(_settings.ApiSecret), "api_secret" }
        };
        var fileOne = new StreamContent(image1);
        fileOne.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileOne, "image_file1", fileName1);
        var fileTwo = new StreamContent(image2);
        fileTwo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileTwo, "image_file2", fileName2);

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ComparePath) { Content = content };
        var (document, failure) = await SendAsync(request, "compare", cancellationToken);
        if (failure is not null || document is null)
            return FaceCompareResult.Failure("KYC_PROVIDER_UNAVAILABLE", failure ?? "Face++ không phản hồi hợp lệ.");

        if (document.Value.TryGetProperty("error_message", out var errorMessage))
            return FaceCompareResult.Failure("KYC_PROVIDER_UNAVAILABLE", errorMessage.GetString() ?? "Face++ trả lỗi.");

        var requestId = document.Value.TryGetProperty("request_id", out var id) ? id.GetString() : null;
        decimal? confidence = document.Value.TryGetProperty("confidence", out var score) && score.TryGetDecimal(out var scoreValue)
            ? scoreValue
            : null;
        if (confidence is null)
            return FaceCompareResult.Failure("FACE_NOT_MATCHED", "Face++ không trả điểm so khớp.", requestId);

        decimal? threshold = null;
        if (document.Value.TryGetProperty("thresholds", out var thresholds) &&
            thresholds.ValueKind == JsonValueKind.Object &&
            thresholds.TryGetProperty("1e-5", out var thresholdValue) &&
            thresholdValue.TryGetDecimal(out var thresholdNumber))
        {
            threshold = thresholdNumber;
        }

        return new FaceCompareResult(true, null, null, confidence, threshold, requestId);
    }

    private async Task<(JsonElement? Document, string? Failure)> SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return (null, exception.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, "Face++ không phản hồi trong thời gian cho phép.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (null, $"Face++ trả HTTP {(int)response.StatusCode}.");

        try
        {
            using var document = JsonDocument.Parse(body);
            return (document.RootElement.Clone(), null);
        }
        catch (JsonException)
        {
            return (null, $"Không đọc được phản hồi Face++ ({operation}).");
        }
    }
}
