namespace Indlela.Services.TechPulse;

/// <summary>
/// Refreshes Tech Pulse every 6 hours (UC-22) without needing Hangfire.
/// When you add Hangfire, delete this and register instead:
/// RecurringJob.AddOrUpdate&lt;ITechPulseService&gt;("techpulse-refresh", s => s.RefreshAsync(CancellationToken.None), "0 */6 * * *");
/// </summary>
public sealed class TechPulseRefreshHostedService(IServiceScopeFactory scopes, ILogger<TechPulseRefreshHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay so the app finishes starting (and seeding) first.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        using var timer = new PeriodicTimer(TechPulseService.CacheDuration);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ITechPulseService>().RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduled Tech Pulse refresh failed; will retry next cycle.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
