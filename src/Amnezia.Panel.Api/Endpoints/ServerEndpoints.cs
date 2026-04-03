using Amnezia.Panel.Api.Data;
using Amnezia.Panel.Api.Domain;
using Amnezia.Panel.Api.Services;
using Amnezia.Panel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Amnezia.Panel.Api.Endpoints;

public static class ServerEndpoints
{
    public static IEndpointRouteBuilder MapServerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/servers").WithTags("Servers");

        group.MapGet("/", async (PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var servers = await db.Servers
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x => new ServerSummaryResponse(
                    x.Id,
                    x.Name,
                    x.Host,
                    x.SshPort,
                    x.RuntimeType,
                    x.ContainerName,
                    x.InterfaceName,
                    x.ListenPort,
                    x.VpnSubnet,
                    x.Status.ToString(),
                    x.LastSyncedAt,
                    db.VpnClients.Count(c => c.ServerId == x.Id)))
                .ToListAsync(cancellationToken);

            return Results.Ok(servers);
        })
        .WithName("ListServers");

        group.MapGet("/import-candidates", async (ServerImportService importService, CancellationToken cancellationToken) =>
        {
            var candidates = await importService.ListImportCandidatesAsync(cancellationToken);
            return Results.Ok(candidates.Select(x => new ImportCandidateResponse(
                x.RuntimeType,
                x.ContainerName,
                x.InterfaceName,
                x.ConfigPath,
                x.ListenPort,
                x.VpnSubnet,
                x.Status)));
        })
        .WithName("ListServerImportCandidates");

        group.MapPost("/", async (CreateServerRequest request, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var server = new PanelServer
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Host = request.Host.Trim(),
                SshPort = request.SshPort,
                RuntimeType = string.IsNullOrWhiteSpace(request.RuntimeType) ? "awg" : request.RuntimeType.Trim().ToLowerInvariant(),
                ContainerName = string.IsNullOrWhiteSpace(request.ContainerName) ? null : request.ContainerName.Trim(),
                ListenPort = request.ListenPort,
                Status = ServerStatus.Provisioning,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            db.Servers.Add(server);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/servers/{server.Id}", ToDetailResponse(server, 0));
        })
        .WithName("CreateServer");

        group.MapPost("/import-existing", async (ImportExistingServerRequest request, ServerImportService importService, CancellationToken cancellationToken) =>
        {
            var server = await importService.ImportExistingServerAsync(
                new ImportExistingServerCommand(
                    request.Name,
                    request.Host,
                    request.SshPort,
                    request.RuntimeType,
                    request.ContainerName),
                cancellationToken);

            var clientCount = server.Clients.Count;
            return Results.Created($"/api/servers/{server.Id}", ToDetailResponse(server, clientCount));
        })
        .WithName("ImportExistingServer");

        group.MapGet("/{id:guid}", async (Guid id, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var server = await db.Servers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (server is null)
            {
                return Results.NotFound();
            }

            var clientCount = await db.VpnClients.CountAsync(x => x.ServerId == id, cancellationToken);
            return Results.Ok(ToDetailResponse(server, clientCount));
        })
        .WithName("GetServer");

        group.MapGet("/{id:guid}/clients", async (Guid id, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var exists = await db.Servers.AnyAsync(x => x.Id == id, cancellationToken);
            if (!exists)
            {
                return Results.NotFound();
            }

            var clients = await db.VpnClients
                .AsNoTracking()
                .Where(x => x.ServerId == id)
                .OrderBy(x => x.Name)
                .Select(x => new ClientResponse(
                    x.Id,
                    x.Name,
                    x.Address,
                    x.AllowedIps,
                    x.PublicKey,
                    x.Status.ToString(),
                    x.BytesSent,
                    x.BytesReceived,
                    x.LastHandshakeAt,
                    x.LastSyncedAt,
                    !string.IsNullOrWhiteSpace(x.Config),
                    !string.IsNullOrWhiteSpace(x.QrCodeDataUri)))
                .ToListAsync(cancellationToken);

            return Results.Ok(clients);
        })
        .WithName("ListServerClients");

        group.MapGet("/{id:guid}/metrics", async (Guid id, double? hours, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var exists = await db.Servers.AnyAsync(x => x.Id == id, cancellationToken);
            if (!exists)
            {
                return Results.NotFound();
            }

            var cutoff = DateTime.UtcNow.AddHours(-Math.Max(1d / 60d, hours ?? 24d));
            var metrics = await db.ServerMetrics
                .AsNoTracking()
                .Where(x => x.ServerId == id && x.SampledAt >= cutoff)
                .OrderBy(x => x.SampledAt)
                .Select(x => new ServerMetricResponse(
                    x.SampledAt,
                    x.CpuPercent,
                    x.MemoryUsedMb,
                    x.MemoryTotalMb,
                    0d,
                    0d,
                    x.NetworkRxKbps / 1024d,
                    x.NetworkTxKbps / 1024d,
                    x.ActiveClients))
                .ToListAsync(cancellationToken);

            return Results.Ok(metrics);
        })
        .WithName("ListServerMetrics");

        group.MapPost("/{id:guid}/sync", async (Guid id, JobService jobService, CancellationToken cancellationToken) =>
        {
            var job = await jobService.EnqueueServerSyncJobAsync(id, "api", cancellationToken);
            return Results.Accepted($"/api/jobs/{job.Id}", new JobAcceptedResponse(job.Id, job.Status.ToString(), job.ServerId));
        })
        .WithName("EnqueueServerSync");

        group.MapDelete("/{id:guid}", async (Guid id, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var server = await db.Servers
                .Include(x => x.Clients)
                .Include(x => x.Metrics)
                .Include(x => x.Jobs)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (server is null)
            {
                return Results.NotFound();
            }

            var clientIds = server.Clients.Select(x => x.Id).ToList();
            var clientMetrics = await db.ClientMetrics
                .Where(x => clientIds.Contains(x.ClientId))
                .ToListAsync(cancellationToken);

            db.ClientMetrics.RemoveRange(clientMetrics);
            db.VpnClients.RemoveRange(server.Clients);
            db.ServerMetrics.RemoveRange(server.Metrics);
            db.Jobs.RemoveRange(server.Jobs);
            db.Servers.Remove(server);
            await db.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteServer");

        return endpoints;
    }

    private static ServerDetailResponse ToDetailResponse(PanelServer server, int clientCount) =>
        new(
            server.Id,
            server.Name,
            server.Host,
            server.SshPort,
            server.RuntimeType,
            server.ContainerName,
            server.InterfaceName,
            server.ListenPort,
            server.VpnSubnet,
            server.Status.ToString(),
            server.LastSyncedAt,
            server.LastError,
            clientCount,
            server.CreatedAt,
            server.UpdatedAt);

    public sealed record CreateServerRequest(
        string Name,
        string Host,
        int SshPort = 22,
        string RuntimeType = "awg",
        string? ContainerName = null,
        int? ListenPort = null);

    public sealed record ServerSummaryResponse(
        Guid Id,
        string Name,
        string Host,
        int SshPort,
        string RuntimeType,
        string? ContainerName,
        string? InterfaceName,
        int? ListenPort,
        string? VpnSubnet,
        string Status,
        DateTime? LastSyncedAt,
        int ClientCount);

    public sealed record ImportCandidateResponse(
        string RuntimeType,
        string ContainerName,
        string InterfaceName,
        string ConfigPath,
        int ListenPort,
        string? VpnSubnet,
        string Status);

    public sealed record ServerDetailResponse(
        Guid Id,
        string Name,
        string Host,
        int SshPort,
        string RuntimeType,
        string? ContainerName,
        string? InterfaceName,
        int? ListenPort,
        string? VpnSubnet,
        string Status,
        DateTime? LastSyncedAt,
        string? LastError,
        int ClientCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public sealed record ServerMetricResponse(
        DateTime SampledAt,
        double CpuPercent,
        double RamUsedMb,
        double RamTotalMb,
        double DiskUsedGb,
        double DiskTotalGb,
        double NetworkRxMbps,
        double NetworkTxMbps,
        int ActiveClients);

    public sealed record ImportExistingServerRequest(
        string Name,
        string Host,
        int SshPort = 22,
        string RuntimeType = "awg",
        string? ContainerName = null);

    public sealed record ClientResponse(
        Guid Id,
        string Name,
        string Address,
        string AllowedIps,
        string PublicKey,
        string Status,
        long BytesSent,
        long BytesReceived,
        DateTime? LastHandshakeAt,
        DateTime? LastSyncedAt,
        bool HasConfig,
        bool HasQrCode);

    public sealed record JobAcceptedResponse(Guid JobId, string Status, Guid? ServerId);
}
