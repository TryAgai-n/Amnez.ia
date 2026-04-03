using Amnezia.Panel.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Amnezia.Panel.Api.Services;

public sealed class MetricsSyncBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<SyncOptions> options,
    ILogger<MetricsSyncBackgroundService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly SyncOptions _options = options.Value;
    private readonly ILogger<MetricsSyncBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.InitialDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.IntervalSeconds)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var jobService = scope.ServiceProvider.GetRequiredService<JobService>();
                var syncService = scope.ServiceProvider.GetRequiredService<ServerSyncService>();

                var processedJobs = await jobService.ProcessPendingJobsAsync(_options.JobBatchSize, stoppingToken);
                var syncedServers = await syncService.SyncDueServersAsync(_options.ServerBatchSize, stoppingToken);

                if (processedJobs > 0 || syncedServers.Count > 0)
                {
                    _logger.LogInformation(
                        "Background sync iteration complete. Jobs processed: {JobsProcessed}, servers synced: {ServersSynced}",
                        processedJobs,
                        syncedServers.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background sync iteration failed");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
