using Abstrict.Api.Options;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Integrations.Identity;

public sealed class StubFptAiIdentityClient : IFptAiIdentityClient
{
    public Task<FptAiIdentityResult> ReadAsync(Stream image, string fileName, bool includeFaceCrop, CancellationToken cancellationToken)
    {
        var result = new FptAiIdentityResult(
            true, null, null, $"stub-{Guid.NewGuid():N}",
            "079203001234", "NGUYEN VAN FREELANCER", "1990-05-12", "Nam",
            "123 Duong Mau, Phuong 1, Quan 1, TP Ho Chi Minh",
            "2021-06-01", "Cuc Canh sat QLHC ve TTXH", "2031-06-01", "CCCD",
            includeFaceCrop ? "stub://face-crop/portrait" : null,
            [new FptAiFieldConfidence("id", 99.0m), new FptAiFieldConfidence("name", 98.0m)]);
        return Task.FromResult(result);
    }
}

public sealed class StubFaceVerificationClient(IOptions<KycOptions> options) : IFaceVerificationClient
{
    private readonly decimal _threshold = options.Value.FacePlusPlus.MatchThreshold ?? 80m;

    public Task<FaceDetectionResult> DetectAsync(Stream image, string fileName, CancellationToken cancellationToken) =>
        Task.FromResult(new FaceDetectionResult(true, null, null, 1, $"stub-detect-{Guid.NewGuid():N}"));

    public Task<FaceCompareResult> CompareAsync(Stream image1, string fileName1, Stream image2, string fileName2, CancellationToken cancellationToken) =>
        Task.FromResult(new FaceCompareResult(true, null, null, 92.50m, _threshold, $"stub-compare-{Guid.NewGuid():N}"));
}
