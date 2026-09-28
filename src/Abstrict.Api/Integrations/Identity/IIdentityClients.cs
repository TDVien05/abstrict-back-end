namespace Abstrict.Api.Integrations.Identity;

public interface IFptAiIdentityClient
{
    Task<FptAiIdentityResult> ReadAsync(Stream image, string fileName, bool includeFaceCrop, CancellationToken cancellationToken);
}

public interface IFaceVerificationClient
{
    Task<FaceDetectionResult> DetectAsync(Stream image, string fileName, CancellationToken cancellationToken);
    Task<FaceCompareResult> CompareAsync(Stream image1, string fileName1, Stream image2, string fileName2, CancellationToken cancellationToken);
}
