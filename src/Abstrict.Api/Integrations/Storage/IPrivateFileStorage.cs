namespace Abstrict.Api.Integrations.Storage;

public interface IPrivateFileStorage
{
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}
