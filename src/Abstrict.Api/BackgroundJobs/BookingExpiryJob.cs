using Abstrict.Api.Data;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;
using Abstrict.Api.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.BackgroundJobs;

/// <summary>Định kỳ giải phóng hold hết hạn và đơn freelancer không phản hồi kịp. Chạy lặp an toàn.</summary>
public sealed class BookingExpiryJob(
    IServiceScopeFactory scopeFactory, TimeProvider timeProvider, IOptions<BookingOptions> options, ILogger<BookingExpiryJob> logger)
    : BackgroundService
{
    public const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.ExpiryJobIntervalSeconds));
        using var timer = new PeriodicTimer(interval, timeProvider);
        try
        {
            do
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Booking expiry job failed");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        List<Guid> dueIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dueIds = await dbContext.Bookings.AsNoTracking()
                .Where(x => (x.Status == BookingStatus.PendingDeposit && x.HoldExpiresAtUtc < now)
                    || (x.Status == BookingStatus.PendingProviderConfirmation && x.ProviderConfirmationDeadlineUtc < now))
                .OrderBy(x => x.UpdatedAtUtc)
                .Select(x => x.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
        }

        foreach (var bookingId in dueIds)
        {
            try
            {
                // Mỗi đơn một scope và transaction riêng để lỗi một đơn không ảnh hưởng đơn khác.
                await using var scope = scopeFactory.CreateAsyncScope();
                var workflow = scope.ServiceProvider.GetRequiredService<BookingWorkflow>();
                await workflow.ExpireIfDueAsync(bookingId, now, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Failed to expire booking {BookingId}", bookingId);
            }
        }
    }
}
