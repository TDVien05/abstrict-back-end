using System.Net;
using Abstrict.Api.Options;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Integrations.Storage;

public sealed class MinioPrivateFileStorage(IAmazonS3 client, IOptions<KycOptions> options) : IPrivateFileStorage
{
    private readonly string _bucket = options.Value.Storage.Bucket;

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        var objectKey = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}";
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, cancellationToken);
        return objectKey;
    }

    public async Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucket,
                Key = objectKey
            }, cancellationToken);
            return new ResponseStream(response);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound
            && exception.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = objectKey }, cancellationToken);
    }

    // The caller owns the stream; disposing it also releases the S3 response.
    private sealed class ResponseStream(GetObjectResponse response) : Stream
    {
        private Stream Inner => response.ResponseStream;
        public override bool CanRead => Inner.CanRead;
        public override bool CanSeek => Inner.CanSeek;
        public override bool CanWrite => Inner.CanWrite;
        public override long Length => Inner.Length;
        public override long Position { get => Inner.Position; set => Inner.Position = value; }
        public override void Flush() => Inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
        public override void SetLength(long value) => Inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => Inner.Write(buffer, offset, count);
        protected override void Dispose(bool disposing)
        {
            if (disposing) response.Dispose();
            base.Dispose(disposing);
        }
    }
}
