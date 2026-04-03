using Amnezia.Panel.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Amnezia.Panel.Api.Endpoints;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/jobs").WithTags("Jobs");

        group.MapGet("/", async (string? status, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var query = db.Jobs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.Status.ToString().ToLower() == status.Trim().ToLower());
            }

            var jobs = await query
                .OrderByDescending(x => x.RequestedAt)
                .Take(100)
                .Select(x => new JobResponse(
                    x.Id,
                    x.ServerId,
                    x.Type,
                    x.Status.ToString(),
                    x.RequestedBy,
                    x.RequestedAt,
                    x.StartedAt,
                    x.CompletedAt,
                    x.Error,
                    x.ResultJson))
                .ToListAsync(cancellationToken);

            return Results.Ok(jobs);
        })
        .WithName("ListJobs");

        group.MapGet("/{id:guid}", async (Guid id, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var job = await db.Jobs
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new JobResponse(
                    x.Id,
                    x.ServerId,
                    x.Type,
                    x.Status.ToString(),
                    x.RequestedBy,
                    x.RequestedAt,
                    x.StartedAt,
                    x.CompletedAt,
                    x.Error,
                    x.ResultJson))
                .FirstOrDefaultAsync(cancellationToken);

            return job is null ? Results.NotFound() : Results.Ok(job);
        })
        .WithName("GetJob");

        return endpoints;
    }

    public sealed record JobResponse(
        Guid Id,
        Guid? ServerId,
        string Type,
        string Status,
        string RequestedBy,
        DateTime RequestedAt,
        DateTime? StartedAt,
        DateTime? CompletedAt,
        string? Error,
        string? ResultJson);
}
