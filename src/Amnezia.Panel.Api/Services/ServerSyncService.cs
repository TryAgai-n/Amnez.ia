using System.Text.Json;
using Amnezia.Panel.Api.Contracts;
using Amnezia.Panel.Api.Data;
using Amnezia.Panel.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Amnezia.Panel.Api.Services;

public sealed class ServerSyncService(
    PanelDbContext db,
    IAmneziaAgentClient agentClient,
    ILogger<ServerSyncService> logger)
{
    public async Task<ServerSyncResult> SyncServerAsync(Guid serverId, CancellationToken cancellationToken)
    {
        var server = await db.Servers
            .Include(x => x.Clients)
            .FirstOrDefaultAsync(x => x.Id == serverId, cancellationToken)
            ?? throw new KeyNotFoundException($"Server '{serverId}' was not found.");

        try
        {
            var snapshot = await agentClient.GetServerSnapshotAsync(server, cancellationToken);
            var now = DateTime.UtcNow;

            server.RuntimeType = snapshot.RuntimeType;
            server.ContainerName = snapshot.ContainerName;
            server.ListenPort = snapshot.ListenPort;
            server.Status = ParseServerStatus(snapshot.Status);
            server.LastSyncedAt = now;
            server.LastError = null;
            server.UpdatedAt = now;

            db.ServerMetrics.Add(new ServerMetric
            {
                ServerId = server.Id,
                SampledAt = now,
                CpuPercent = snapshot.CpuPercent,
                MemoryUsedMb = snapshot.MemoryUsedMb,
                MemoryTotalMb = snapshot.MemoryTotalMb,
                NetworkRxKbps = snapshot.NetworkRxKbps,
                NetworkTxKbps = snapshot.NetworkTxKbps,
                ActiveClients = snapshot.ActiveClients,
            });

            var existingClients = server.Clients.ToDictionary(x => x.PublicKey, StringComparer.Ordinal);
            var upserted = 0;

            foreach (var remoteClient in snapshot.Clients)
            {
                if (!existingClients.TryGetValue(remoteClient.PublicKey, out var client))
                {
                    client = new VpnClient
                    {
                        Id = Guid.NewGuid(),
                        ServerId = server.Id,
                        PublicKey = remoteClient.PublicKey,
                        CreatedAt = now,
                    };

                    db.VpnClients.Add(client);
                    existingClients[remoteClient.PublicKey] = client;
                }

                client.Name = string.IsNullOrWhiteSpace(remoteClient.Name) ? client.Name : remoteClient.Name;
                client.Address = remoteClient.Address;
                client.Status = ParseClientStatus(remoteClient.Status);
                client.BytesSent = remoteClient.BytesSent;
                client.BytesReceived = remoteClient.BytesReceived;
                client.LastHandshakeAt = remoteClient.LastHandshake?.UtcDateTime;
                client.LastSyncedAt = now;
                client.UpdatedAt = now;

                db.ClientMetrics.Add(new ClientMetric
                {
                    Client = client,
                    SampledAt = now,
                    BytesSent = remoteClient.BytesSent,
                    BytesReceived = remoteClient.BytesReceived,
                    SpeedUpKbps = remoteClient.SpeedUpKbps,
                    SpeedDownKbps = remoteClient.SpeedDownKbps,
                    IsOnline = remoteClient.LastHandshake is not null &&
                               remoteClient.LastHandshake.Value >= DateTimeOffset.UtcNow.AddMinutes(-5),
                });

                upserted++;
            }

            await db.SaveChangesAsync(cancellationToken);

            return new ServerSyncResult(server.Id, upserted, snapshot.ActiveClients, now);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync server {ServerId}", serverId);
            server.Status = ServerStatus.Error;
            server.LastError = ex.Message;
            server.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<ServerSyncResult>> SyncDueServersAsync(int batchSize, CancellationToken cancellationToken)
    {
        var serverIds = await db.Servers
            .OrderBy(x => x.LastSyncedAt ?? DateTime.MinValue)
            .Select(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var results = new List<ServerSyncResult>(serverIds.Count);
        foreach (var serverId in serverIds)
        {
            results.Add(await SyncServerAsync(serverId, cancellationToken));
        }

        return results;
    }

    private static ServerStatus ParseServerStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "active" => ServerStatus.Active,
            "degraded" => ServerStatus.Degraded,
            "stopped" => ServerStatus.Stopped,
            "error" => ServerStatus.Error,
            _ => ServerStatus.Provisioning,
        };

    private static ClientStatus ParseClientStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "active" => ClientStatus.Active,
            "revoked" => ClientStatus.Revoked,
            "expired" => ClientStatus.Expired,
            _ => ClientStatus.Unknown,
        };
}

public sealed record ServerSyncResult(
    Guid ServerId,
    int ClientsUpserted,
    int ActiveClients,
    DateTime SyncedAt);
