using Abstrict.Api.Options;
using Amazon.S3;

namespace Abstrict.Api.Integrations.Storage;

public static class PrivateFileStorageRegistration
{
    public static IServiceCollection AddPrivateFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var storage = configuration.GetSection("Kyc:Storage").Get<KycStorageOptions>() ?? new();
        if (storage.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPrivateFileStorage, LocalPrivateFileStorage>();
            return services;
        }

        if (!storage.Provider.Equals("MinIO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Kyc:Storage:Provider must be Local or MinIO.");
        if (!Uri.TryCreate(storage.Endpoint, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Kyc:Storage:Endpoint must be an absolute HTTP or HTTPS URL.");
        if (string.IsNullOrWhiteSpace(storage.Bucket) || string.IsNullOrWhiteSpace(storage.AccessKey)
            || string.IsNullOrWhiteSpace(storage.SecretKey))
            throw new InvalidOperationException("Kyc:Storage:Bucket, AccessKey and SecretKey must be configured for MinIO.");

        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(storage.AccessKey, storage.SecretKey, new AmazonS3Config
        {
            ServiceURL = storage.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        }));
        services.AddSingleton<IPrivateFileStorage, MinioPrivateFileStorage>();
        return services;
    }
}
