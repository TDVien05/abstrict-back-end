using System.Net.Http.Headers;
using System.Text.Json;
using Abstrict.Api.Options;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Integrations.Identity;

public sealed class FptAiIdentityClient(HttpClient httpClient, IOptions<KycOptions> options) : IFptAiIdentityClient
{
    private readonly FptAiOptions _settings = options.Value.FptAi;

    public async Task<FptAiIdentityResult> ReadAsync(Stream image, string fileName, bool includeFaceCrop, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(image);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "image", fileName);
        if (includeFaceCrop)
            content.Add(new StringContent("1"), "face");

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.IdentityPath) { Content = content };
        request.Headers.TryAddWithoutValidation(_settings.ApiKeyHeaderName, _settings.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return FptAiIdentityResult.Failure("KYC_PROVIDER_UNAVAILABLE", exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FptAiIdentityResult.Failure("KYC_PROVIDER_UNAVAILABLE", "FPT AI không phản hồi trong thời gian cho phép.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FptAiIdentityResult.Failure("KYC_PROVIDER_UNAVAILABLE", "FPT AI không phản hồi trong thời gian cho phép.");
        }

        var requestId = response.Headers.TryGetValues("x-request-id", out var values) ? values.FirstOrDefault() : null;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return FptAiIdentityResult.Failure("KYC_PROVIDER_UNAVAILABLE", $"FPT AI trả HTTP {(int)response.StatusCode}.", requestId);

        FptAiEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<FptAiEnvelope>(body);
        }
        catch (JsonException)
        {
            return FptAiIdentityResult.Failure("OCR_INCOMPLETE", "Không đọc được phản hồi FPT AI.", requestId);
        }

        if (envelope is null)
            return FptAiIdentityResult.Failure("OCR_INCOMPLETE", "FPT AI trả phản hồi rỗng.", requestId);

        return FptAiResponseMapper.Map(envelope, requestId);
    }
}
