using System.Text.Json;
using Amnezia.Panel.Api.Contracts;
using Amnezia.Panel.Api.Data;
using Amnezia.Panel.Api.Domain;
using Amnezia.Panel.Contracts;
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
            return await ApplySnapshotAsync(server, snapshot, cancellationToken);
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

    public async Task<ServerSyncResult> ApplySnapshotAsync(PanelServer server, AgentServerSnapshot snapshot, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var previousServerMetric = await db.ServerMetrics
            .AsNoTracking()
            .Where(x => x.ServerId == server.Id)
            .OrderByDescending(x => x.SampledAt)
            .FirstOrDefaultAsync(cancellationToken);

        server.RuntimeType = snapshot.RuntimeType;
        server.ContainerName = snapshot.ContainerName;
        server.ConfigPath = snapshot.ConfigPath;
        server.InterfaceName = snapshot.InterfaceName;
        server.QuickCommand = snapshot.QuickCommand;
        server.ShowCommand = snapshot.ShowCommand;
        server.ListenPort = snapshot.ListenPort;
        server.VpnSubnet = snapshot.VpnSubnet;
        server.ServerPublicKey = snapshot.ServerPublicKey;
        server.PresharedKey = snapshot.PresharedKey;
        server.AwgParametersJson = JsonSerializer.Serialize(snapshot.AwgParameters);
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
            NetworkRxBytes = snapshot.NetworkRxBytes,
            NetworkTxBytes = snapshot.NetworkTxBytes,
            NetworkRxKbps = ComputeRateKbps(previousServerMetric?.NetworkRxBytes ?? snapshot.NetworkRxBytes, snapshot.NetworkRxBytes, previousServerMetric?.SampledAt, now),
            NetworkTxKbps = ComputeRateKbps(previousServerMetric?.NetworkTxBytes ?? snapshot.NetworkTxBytes, snapshot.NetworkTxBytes, previousServerMetric?.SampledAt, now),
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

            client.Name = ResolveClientName(remoteClient, client);
            client.Address = string.IsNullOrWhiteSpace(remoteClient.Address) ? client.Address : remoteClient.Address;
            client.AllowedIps = string.IsNullOrWhiteSpace(remoteClient.AllowedIps) ? client.AllowedIps : remoteClient.AllowedIps;
            client.PresharedKey = string.IsNullOrWhiteSpace(remoteClient.PresharedKey) ? client.PresharedKey : remoteClient.PresharedKey;
            client.Status = ParseClientStatus(remoteClient.Status);
            client.BytesSent = remoteClient.BytesSent;
            client.BytesReceived = remoteClient.BytesReceived;
            client.LastHandshakeAt = remoteClient.LastHandshake?.UtcDateTime;
            client.LastSyncedAt = now;
            client.UpdatedAt = now;

            var previousClientMetric = await db.ClientMetrics
                .AsNoTracking()
                .Where(x => x.ClientId == client.Id)
                .OrderByDescending(x => x.SampledAt)
                .FirstOrDefaultAsync(cancellationToken);

            db.ClientMetrics.Add(new ClientMetric
            {
                Client = client,
                SampledAt = now,
                BytesSent = remoteClient.BytesSent,
                BytesReceived = remoteClient.BytesReceived,
                SpeedUpKbps = ComputeRateKbps(previousClientMetric?.BytesSent ?? remoteClient.BytesSent, remoteClient.BytesSent, previousClientMetric?.SampledAt, now),
                SpeedDownKbps = ComputeRateKbps(previousClientMetric?.BytesReceived ?? remoteClient.BytesReceived, remoteClient.BytesReceived, previousClientMetric?.SampledAt, now),
                IsOnline = remoteClient.LastHandshake is not null &&
                           remoteClient.LastHandshake.Value >= DateTimeOffset.UtcNow.AddMinutes(-5),
            });

            upserted++;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ServerSyncResult(server.Id, upserted, snapshot.ActiveClients, now);
    }

    private static ServerStatus ParseServerStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "active" => ServerStatus.Active,
            "running" => ServerStatus.Active,
            "degraded" => ServerStatus.Degraded,
            "stopped" => ServerStatus.Stopped,
            "exited" => ServerStatus.Stopped,
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

    private static string ResolveClientName(AgentClientSnapshot remoteClient, VpnClient client)
    {
        if (!string.IsNullOrWhiteSpace(remoteClient.Name))
        {
            return remoteClient.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(client.Name))
        {
            return client.Name;
        }

        return $"Imported {remoteClient.Address}";
    }

    private static long ComputeRateKbps(long previousBytes, long currentBytes, DateTime? previousSampleAt, DateTime currentSampleAt)
    {
        if (previousSampleAt is null || currentBytes < previousBytes)
        {
            return 0;
        }

        var elapsedSeconds = Math.Max(0, (currentSampleAt - previousSampleAt.Value).TotalSeconds);
        if (elapsedSeconds <= 0.5d)
        {
            return 0;
        }

        var deltaBytes = currentBytes - previousBytes;
        return (long)Math.Round(deltaBytes / elapsedSeconds / 1024d);
    }
}

public sealed record ServerSyncResult(
    Guid ServerId,
    int ClientsUpserted,
    int ActiveClients,
    DateTime SyncedAt);
