using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Data;
using NewsPortal.Api.Models;
namespace NewsPortal.Api.Services;
public class ScheduledPublisher(IServiceScopeFactory scopes, ILogger<ScheduledPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow;
                var count = await db.Articles.Where(a => a.Status == ArticleStatus.Approved && a.ScheduledAt != null && a.ScheduledAt <= now)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, ArticleStatus.Published).SetProperty(a => a.PublishedAt, now).SetProperty(a => a.ScheduledAt, (DateTime?)null).SetProperty(a => a.UpdatedAt, now), stoppingToken);
                if (count > 0) { scope.ServiceProvider.GetRequiredService<PublicCache>().Invalidate(); logger.LogInformation("Published {Count} scheduled articles", count); }
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError(e, "Scheduled publishing failed; will retry on next interval"); }
        }
    }
}
