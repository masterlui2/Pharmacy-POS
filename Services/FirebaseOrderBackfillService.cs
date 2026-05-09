using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;

namespace PharmacyPOS.Services;

public sealed class FirebaseOrderBackfillService(
    IServiceScopeFactory scopeFactory,
    FirebaseAppInitializer firebaseAppInitializer,
    ILogger<FirebaseOrderBackfillService> logger) : BackgroundService
{
    private const int BackfillLimit = 250;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!firebaseAppInitializer.IsFirestoreAvailable)
        {
            logger.LogWarning(
                "Skipped Firebase order backfill because Firestore is unavailable. {Reason}",
                firebaseAppInitializer.FirestoreUnavailableReason ??
                firebaseAppInitializer.UnavailableReason ??
                "No additional details were provided.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PharmacyPosDbContext>();
            var firebaseSyncService = scope.ServiceProvider.GetRequiredService<IFirebaseSyncService>();
            var customerUidResolver = scope.ServiceProvider.GetRequiredService<IFirebaseCustomerUidResolver>();

            var orders = await dbContext.Orders
                .Include(order => order.Account)
                .Include(order => order.Items)
                .Include(order => order.Payment)
                .Where(order => !string.IsNullOrWhiteSpace(order.OrderNumber))
                .OrderByDescending(order => order.CreatedAtUtc)
                .Take(BackfillLimit)
                .ToListAsync(stoppingToken);

            var linkedUidCount = 0;
            foreach (var order in orders.Where(order => string.IsNullOrWhiteSpace(order.CustomerUid)))
            {
                var customerUid = await customerUidResolver.ResolveCustomerUidAsync(
                    order.Account,
                    order.CustomerEmail,
                    stoppingToken);
                if (string.IsNullOrWhiteSpace(customerUid))
                {
                    continue;
                }

                order.CustomerUid = customerUid;
                if (order.Account is not null &&
                    !string.Equals(order.Account.FirebaseUid, customerUid, StringComparison.Ordinal))
                {
                    order.Account.FirebaseUid = customerUid;
                }

                linkedUidCount++;
            }

            if (linkedUidCount > 0)
            {
                await dbContext.SaveChangesAsync(stoppingToken);
            }

            var syncedCount = 0;
            foreach (var order in orders)
            {
                try
                {
                    await firebaseSyncService.SyncOrderAsync(order, order.Payment, stoppingToken);
                    syncedCount++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(
                        exception,
                        "Firebase order backfill failed for order {OrderNumber}.",
                        order.OrderNumber);
                }
            }

            logger.LogInformation(
                "Firebase order backfill completed. Synced {SyncedCount} of {OrderCount} recent POS orders; linked {LinkedUidCount} Firebase customer UIDs.",
                syncedCount,
                orders.Count,
                linkedUidCount);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Firebase order backfill failed before completion.");
        }
    }
}
