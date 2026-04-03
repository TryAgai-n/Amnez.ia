using System.Text.Json;
using Amnezia.Panel.Api.Data;
using Amnezia.Panel.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Amnezia.Panel.Api.Services;

public sealed class JobService(
    PanelDbContext db,
    ServerSyncService serverSyncService,
    ILogger<JobService> logger)
{
    public async Task<JobRecord> EnqueueServerSyncJobAsync(Guid serverId, string requestedBy, CancellationToken cancellationToken)
    {
        var job = new JobRecord
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            Type = "sync-server",
            Status = JobStatus.Pending,
            RequestedBy = requestedBy,
            RequestedAt = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(new { serverId }),
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<JobRecord> RunServerSyncNowAsync(Guid serverId, string requestedBy, CancellationToken cancellationToken)
    {
        var job = new JobRecord
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            Type = "sync-server",
            Status = JobStatus.Running,
            RequestedBy = requestedBy,
            RequestedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(new { serverId }),
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        await ExecuteJobAsync(job, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return job;
    }

    public async Task<int> ProcessPendingJobsAsync(int batchSize, CancellationToken cancellationToken)
    {
        var jobs = await db.Jobs
            .Where(x => x.Status == JobStatus.Pending)
            .OrderBy(x => x.RequestedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var job in jobs)
        {
            processed++;
            job.Status = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await ExecuteJobAsync(job, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }

    private async Task ExecuteJobAsync(JobRecord job, CancellationToken cancellationToken)
    {
        try
        {
            switch (job.Type)
            {
                case "sync-server" when job.ServerId.HasValue:
                {
                    var result = await serverSyncService.SyncServerAsync(job.ServerId.Value, cancellationToken);
                    job.ResultJson = JsonSerializer.Serialize(result);
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unsupported job type '{job.Type}'.");
            }

            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.Error = null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed", job.Id);
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.Error = ex.Message;
        }
    }
}
