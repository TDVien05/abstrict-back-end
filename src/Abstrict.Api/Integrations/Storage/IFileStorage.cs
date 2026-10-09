namespace Abstrict.Api.Integrations.Storage;

public interface IFileStorage
{
    /// <summary>Lưu nội dung và trả về object key do hệ thống sinh ra (không chứa tên file người dùng).</summary>
    Task<string> SaveAsync(string folder, string extension, Stream content, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}
