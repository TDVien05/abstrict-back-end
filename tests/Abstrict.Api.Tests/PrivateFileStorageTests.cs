using System.Net;
using System.Text;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Options;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Tests;

public sealed class PrivateFileStorageTests
{
    [Fact]
    public async Task SaveReadDelete_PreservesBytesAndCallerStreamOwnership()
    {
        using var client = new StorageClient();
        var storage = Create(client);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("image-content"));
        var key = await storage.SaveAsync(input, "image/png", CancellationToken.None);
        Assert.True(input.CanRead);
        Assert.Equal("private-bucket", client.Bucket);
        Assert.Equal("image/png", client.ContentType);
        Assert.Matches(@"^\d{4}/\d{2}/\d{2}/[0-9a-f]{32}$", key);

        var output = await storage.OpenReadAsync(key, CancellationToken.None);
        Assert.NotNull(output);
        using (var reader = new StreamReader(output!))
            Assert.Equal("image-content", await reader.ReadToEndAsync());
        Assert.False(client.LastResponseStream!.CanRead);

        await storage.DeleteAsync(key, CancellationToken.None);
        Assert.Null(await storage.OpenReadAsync(key, CancellationToken.None));
        await storage.DeleteAsync(key, CancellationToken.None);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "AccessDenied")]
    [InlineData(HttpStatusCode.NotFound, "NoSuchBucket")]
    public async Task Read_DoesNotHidePermissionsOrMissingBucket(HttpStatusCode status, string code)
    {
        using var client = new StorageClient { ReadError = new AmazonS3Exception(code) { StatusCode = status, ErrorCode = code } };
        await Assert.ThrowsAsync<AmazonS3Exception>(() => Create(client).OpenReadAsync("key", CancellationToken.None));
    }

    [Fact]
    public void Registration_UsesMinioEvenWhenKycIsDisabled()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kyc:Enabled"] = "false",
            ["Kyc:Storage:Provider"] = "MinIO",
            ["Kyc:Storage:Endpoint"] = "http://minio:9000",
            ["Kyc:Storage:Bucket"] = "private-bucket",
            ["Kyc:Storage:AccessKey"] = "test-user",
            ["Kyc:Storage:SecretKey"] = "test-password"
        }).Build();
        var services = new ServiceCollection();
        services.Configure<KycOptions>(configuration.GetSection("Kyc"));
        services.AddPrivateFileStorage(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.IsType<MinioPrivateFileStorage>(provider.GetRequiredService<IPrivateFileStorage>());
        var config = Assert.IsType<AmazonS3Config>(provider.GetRequiredService<IAmazonS3>().Config);
        Assert.True(config.ForcePathStyle);
        Assert.Equal("http://minio:9000/", config.ServiceURL);
    }

    [Theory]
    [InlineData("", "http://minio:9000", "private-bucket")]
    [InlineData("Unknown", "http://minio:9000", "private-bucket")]
    [InlineData("MinIO", "not-a-url", "private-bucket")]
    [InlineData("MinIO", "ftp://minio:9000", "private-bucket")]
    [InlineData("MinIO", "http://minio:9000", "")]
    public void Registration_FailsEarlyForInvalidConfiguration(string provider, string endpoint, string bucket)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kyc:Storage:Provider"] = provider,
            ["Kyc:Storage:Endpoint"] = endpoint,
            ["Kyc:Storage:Bucket"] = bucket,
            ["Kyc:Storage:AccessKey"] = "test-user",
            ["Kyc:Storage:SecretKey"] = "test-password"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddPrivateFileStorage(configuration));
    }

    private static MinioPrivateFileStorage Create(IAmazonS3 client) => new(client,
        Microsoft.Extensions.Options.Options.Create(new KycOptions { Storage = new KycStorageOptions { Bucket = "private-bucket" } }));

    private sealed class StorageClient() : AmazonS3Client("test-user", "test-password", new AmazonS3Config
    {
        ServiceURL = "http://localhost:9000", ForcePathStyle = true
    })
    {
        private readonly Dictionary<string, byte[]> _objects = [];
        public string? Bucket { get; private set; }
        public string? ContentType { get; private set; }
        public MemoryStream? LastResponseStream { get; private set; }
        public AmazonS3Exception? ReadError { get; init; }

        public override async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
        {
            Bucket = request.BucketName;
            ContentType = request.ContentType;
            using var copy = new MemoryStream();
            await request.InputStream.CopyToAsync(copy, cancellationToken);
            _objects[request.Key] = copy.ToArray();
            if (request.AutoCloseStream) request.InputStream.Dispose();
            return new PutObjectResponse();
        }

        public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken = default)
        {
            if (ReadError is not null) throw ReadError;
            if (!_objects.TryGetValue(request.Key, out var bytes))
                throw new AmazonS3Exception("Missing") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NoSuchKey" };
            LastResponseStream = new MemoryStream(bytes);
            return Task.FromResult(new GetObjectResponse { ResponseStream = LastResponseStream });
        }

        public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default)
        {
            _objects.Remove(request.Key);
            return Task.FromResult(new DeleteObjectResponse());
        }
    }
}
