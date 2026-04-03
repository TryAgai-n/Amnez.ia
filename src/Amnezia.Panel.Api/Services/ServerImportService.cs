using Amnezia.Panel.Api.Contracts;
using Amnezia.Panel.Api.Data;
using Amnezia.Panel.Api.Domain;
using Amnezia.Panel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Amnezia.Panel.Api.Services;

public sealed class ServerImportService(
    PanelDbContext db,
    IAmneziaAgentClient agentClient,
    ServerSyncService serverSyncService)
{
    public Task<IReadOnlyList<AgentRuntimeCandidate>> ListImportCandidatesAsync(CancellationToken cancellationToken) =>
        agentClient.ListAwgRuntimesAsync(cancellationToken);

    public async Task<PanelServer> ImportExistingServerAsync(ImportExistingServerCommand command, CancellationToken cancellationToken)
    {
        var snapshot = await agentClient.DiscoverRuntimeAsync(
            new AgentRuntimeDiscoveryRequest(command.RuntimeType, command.ContainerName),
            cancellationToken);

        var duplicate = await db.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ContainerName == snapshot.ContainerName, cancellationToken);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Runtime '{snapshot.ContainerName}' is already imported as server '{duplicate.Id}'.");
        }

        var server = new PanelServer
        {
            Id = Guid.NewGuid(),
            Name = command.Name.Trim(),
            Host = command.Host.Trim(),
            SshPort = command.SshPort,
            RuntimeType = snapshot.RuntimeType,
            ContainerName = snapshot.ContainerName,
            Status = ServerStatus.Provisioning,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Servers.Add(server);
        await db.SaveChangesAsync(cancellationToken);

        await db.Entry(server).Collection(x => x.Clients).LoadAsync(cancellationToken);
        await serverSyncService.ApplySnapshotAsync(server, snapshot, cancellationToken);

        return server;
    }
}

public sealed record ImportExistingServerCommand(
    string Name,
    string Host,
    int SshPort = 22,
    string RuntimeType = "awg",
    string? ContainerName = null);
