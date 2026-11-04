using Zrobim.Api.Data;

namespace Zrobim.Api.Bookings;

internal sealed class BookingExpirationWorker(
    IServiceScopeFactory scopes, TimeProvider time, ILogger<BookingExpirationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private const string MasterResponseTimeout = "MASTER_RESPONSE_TIMEOUT";
    private const string PaymentTimeout = "PAYMENT_TIMEOUT";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval, time);

        do
        {
            try
            {
                await ExpireBookings(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Could not expire bookings. Will retry on the next check.");
            }
        }
        while (await WaitForNextCheck(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextCheck(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }


    private async Task ExpireBookings(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ZrobimDbContext>();
        var now = time.GetUtcNow().UtcDateTime;

        // Leave bookings with an active payment attempt or collected money to the payment flow.
        var count = await db.Bookings
            .Where(b => (b.Status == BookingStatus.Pending && b.ResponseDeadlineAt <= now) ||
                (b.Status == BookingStatus.AwaitingPayment && b.PaymentDeadlineAt <= now))
            .WithoutActivePayment(db)
            .CancelAsync(now, null, b => b.Status == BookingStatus.Pending ? MasterResponseTimeout : PaymentTimeout, ct);

        if (count > 0)
            logger.LogInformation("Cancelled {Count} expired bookings.", count);
    }
}
