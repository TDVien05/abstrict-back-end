namespace Abstrict.Api.Services.Interfaces;

public interface IKycService
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}
