using Abstrict.Api.Services.Interfaces;

namespace Abstrict.Api.BackgroundJobs;

public sealed class KycOperationWorker(IServiceScopeFactory scopeFactory, ILogger<KycOperationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var kycService = scope.ServiceProvider.GetRequiredService<IKycService>();
                processed = await kycService.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "KYC operation worker iteration failed.");
            }

            if (!processed)
            {
                try
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
