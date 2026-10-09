using Microsoft.Extensions.Options;

namespace Abstrict.Api.Integrations.Storage;

public sealed class LocalFileStorageOptions
{
    public string RootPath { get; set; } = string.Empty;
}

/// <summary>Lưu file trên ổ đĩa riêng của server, không nằm trong wwwroot nên không thể truy cập công khai.</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<LocalFileStorageOptions> options, IHostEnvironment environment)
    {
        var configured = options.Value.RootPath;
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "storage")
            : configured);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string folder, string extension, Stream content, CancellationToken cancellationToken)
    {
        var key = $"{folder}/{Guid.NewGuid():N}{extension}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(target, cancellationToken);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        var path = Resolve(objectKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        var path = Resolve(objectKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string objectKey)
    {
        var path = Path.GetFullPath(Path.Combine(_root, objectKey));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Object key nằm ngoài vùng lưu trữ.");
        return path;
    }
}
