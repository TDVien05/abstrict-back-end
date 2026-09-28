using Abstrict.Api.Options;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Integrations.Storage;

public sealed class LocalPrivateFileStorage : IPrivateFileStorage
{
    private readonly string _rootPath;

    public LocalPrivateFileStorage(IOptions<KycOptions> options)
    {
        var configured = options.Value.Storage.LocalRootPath;
        _rootPath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "abstrict-private-storage")
            : configured;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        var objectKey = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}";
        var fullPath = ResolvePath(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);
        return objectKey;
    }

    public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(objectKey);
        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(objectKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolvePath(string objectKey)
    {
        var normalizedRoot = Path.GetFullPath(_rootPath);
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, objectKey));
        if (!candidate.StartsWith(normalizedRoot, StringComparison.Ordinal))
            throw new InvalidOperationException("Object key resolves outside of the private storage root.");
        return candidate;
    }
}
