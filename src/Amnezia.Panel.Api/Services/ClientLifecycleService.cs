using System.Net;
using System.Text.Json;
using Amnezia.Panel.Api.Contracts;
using Amnezia.Panel.Api.Data;
using Amnezia.Panel.Api.Domain;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace Amnezia.Panel.Api.Services;

public sealed class ClientLifecycleService(
    PanelDbContext db,
    IAmneziaAgentClient agentClient,
    ServerSyncService serverSyncService)
{
    public async Task<VpnClient> CreateClientAsync(Guid serverId, CreateClientCommand command, CancellationToken cancellationToken)
    {
        var server = await db.Servers
            .Include(x => x.Clients)
            .FirstOrDefaultAsync(x => x.Id == serverId, cancellationToken)
            ?? throw new KeyNotFoundException($"Server '{serverId}' was not found.");

        EnsureServerReady(server);

        var name = NormalizeClientName(command.Name);
        var address = GetNextAvailableAddress(server.VpnSubnet, server.Clients.Select(x => x.Address));
        var allowedIps = $"{address}/32";
        var presharedKey = string.IsNullOrWhiteSpace(server.PresharedKey) ? null : server.PresharedKey;

        var mutation = await agentClient.CreateClientAsync(server, name, address, allowedIps, presharedKey, cancellationToken);
        var config = BuildClientConfig(server, mutation.PrivateKey, mutation.Address, mutation.PresharedKey);
        var qrCodeDataUri = string.IsNullOrWhiteSpace(config) ? null : GenerateQrCodeDataUri(config);
        var now = DateTime.UtcNow;

        var client = new VpnClient
        {
            Id = Guid.NewGuid(),
            ServerId = server.Id,
            Name = mutation.Name,
            Address = mutation.Address,
            AllowedIps = mutation.AllowedIps,
            PublicKey = mutation.PublicKey,
            PrivateKey = mutation.PrivateKey,
            PresharedKey = mutation.PresharedKey,
            Config = config,
            QrCodeDataUri = qrCodeDataUri,
            Status = ClientStatus.Active,
            BytesSent = 0,
            BytesReceived = 0,
            LastSyncedAt = now,
            ExpiresAt = command.ExpiresInDays.HasValue ? now.AddDays(command.ExpiresInDays.Value) : null,
            TrafficLimitBytes = command.TrafficLimitBytes,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.VpnClients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return client;
    }

    public async Task<VpnClient> GetClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        return await db.VpnClients
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == clientId, cancellationToken)
            ?? throw new KeyNotFoundException($"Client '{clientId}' was not found.");
    }

    public async Task<VpnClient> RevokeClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await db.VpnClients
            .Include(x => x.Server)
            .FirstOrDefaultAsync(x => x.Id == clientId, cancellationToken)
            ?? throw new KeyNotFoundException($"Client '{clientId}' was not found.");

        if (client.Status != ClientStatus.Revoked)
        {
            EnsureServerReady(client.Server);
            await agentClient.RemoveClientAsync(client.Server, client.PublicKey, cancellationToken);
        }

        client.Status = ClientStatus.Revoked;
        client.RevokedAt = DateTime.UtcNow;
        client.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return client;
    }

    public async Task<VpnClient> RestoreClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await db.VpnClients
            .Include(x => x.Server)
            .FirstOrDefaultAsync(x => x.Id == clientId, cancellationToken)
            ?? throw new KeyNotFoundException($"Client '{clientId}' was not found.");

        EnsureServerReady(client.Server);

        var allowedIps = string.IsNullOrWhiteSpace(client.AllowedIps) ? $"{client.Address}/32" : client.AllowedIps;
        var mutation = await agentClient.RestoreClientAsync(
            client.Server,
            client.Name,
            client.PublicKey,
            client.Address,
            allowedIps,
            client.PresharedKey ?? client.Server.PresharedKey,
            cancellationToken);

        client.Name = mutation.Name;
        client.Address = mutation.Address;
        client.AllowedIps = mutation.AllowedIps;
        client.PresharedKey = mutation.PresharedKey;
        client.Status = ClientStatus.Active;
        client.RevokedAt = null;
        client.UpdatedAt = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(client.Config) && !string.IsNullOrWhiteSpace(client.PrivateKey))
        {
            client.Config = BuildClientConfig(client.Server, client.PrivateKey, client.Address, client.PresharedKey);
            client.QrCodeDataUri = string.IsNullOrWhiteSpace(client.Config) ? null : GenerateQrCodeDataUri(client.Config);
        }

        await db.SaveChangesAsync(cancellationToken);
        return client;
    }

    public async Task DeleteClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await db.VpnClients
            .Include(x => x.Server)
            .Include(x => x.Metrics)
            .FirstOrDefaultAsync(x => x.Id == clientId, cancellationToken)
            ?? throw new KeyNotFoundException($"Client '{clientId}' was not found.");

        if (client.Status == ClientStatus.Active)
        {
            EnsureServerReady(client.Server);
            await agentClient.RemoveClientAsync(client.Server, client.PublicKey, cancellationToken);
        }

        db.ClientMetrics.RemoveRange(client.Metrics);
        db.VpnClients.Remove(client);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<VpnClient> SyncClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await db.VpnClients
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == clientId, cancellationToken)
            ?? throw new KeyNotFoundException($"Client '{clientId}' was not found.");

        await serverSyncService.SyncServerAsync(client.ServerId, cancellationToken);

        return await db.VpnClients
            .AsNoTracking()
            .FirstAsync(x => x.Id == clientId, cancellationToken);
    }

    private static void EnsureServerReady(PanelServer server)
    {
        if (server.Status == ServerStatus.Provisioning)
        {
            throw new InvalidOperationException("Server runtime metadata is not ready yet.");
        }

        if (string.IsNullOrWhiteSpace(server.ContainerName))
        {
            throw new InvalidOperationException("Server container name is not configured.");
        }
    }

    private static string NormalizeClientName(string name)
    {
        var normalized = name.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Client name is required.");
        }

        return normalized;
    }

    private static string GetNextAvailableAddress(string? vpnSubnet, IEnumerable<string> existingAddresses)
    {
        var cidr = string.IsNullOrWhiteSpace(vpnSubnet) ? "10.8.1.0/24" : vpnSubnet.Trim();
        var parts = cidr.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var networkAddress))
        {
            throw new InvalidOperationException($"Invalid VPN subnet '{cidr}'.");
        }

        if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0 || prefixLength > 32)
        {
            throw new InvalidOperationException($"Invalid VPN subnet prefix '{cidr}'.");
        }

        var networkBytes = networkAddress.GetAddressBytes();
        if (networkBytes.Length != 4)
        {
            throw new InvalidOperationException("Only IPv4 VPN subnets are supported.");
        }

        var network = ReadUInt32BigEndian(networkBytes);
        var hostBits = 32 - prefixLength;
        if (hostBits <= 1)
        {
            throw new InvalidOperationException($"VPN subnet '{cidr}' is too small for clients.");
        }

        var totalHosts = 1u << hostBits;
        var used = existingAddresses
            .Where(x => !string.IsNullOrWhiteSpace(x) && IPAddress.TryParse(x, out _))
            .Select(IPAddress.Parse)
            .Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(x => ReadUInt32BigEndian(x.GetAddressBytes()))
            .ToHashSet();

        for (var offset = 1u; offset < totalHosts - 1; offset++)
        {
            var candidate = network + offset;
            if (!used.Contains(candidate))
            {
                return new IPAddress(GetBigEndianBytes(candidate)).ToString();
            }
        }

        throw new InvalidOperationException($"No free client IP addresses remain in subnet '{cidr}'.");
    }

    private static string BuildClientConfig(PanelServer server, string? privateKey, string address, string? presharedKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(server.ServerPublicKey))
        {
            throw new InvalidOperationException("Server public key is not available.");
        }

        if (!server.ListenPort.HasValue)
        {
            throw new InvalidOperationException("Server listen port is not available.");
        }

        var awgParameters = ParseAwgParameters(server.AwgParametersJson);
        var lines = new List<string>
        {
            "[Interface]",
            $"PrivateKey = {privateKey}",
            $"Address = {address}/32",
            "DNS = 1.1.1.1, 1.0.0.1",
        };

        foreach (var key in new[] { "Jc", "Jmin", "Jmax", "S1", "S2", "H1", "H2", "H3", "H4" })
        {
            if (awgParameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                lines.Add($"{key} = {value}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("[Peer]");
        lines.Add($"PublicKey = {server.ServerPublicKey}");
        if (!string.IsNullOrWhiteSpace(presharedKey))
        {
            lines.Add($"PresharedKey = {presharedKey}");
        }
        lines.Add($"Endpoint = {server.Host}:{server.ListenPort.Value}");
        lines.Add("AllowedIPs = 0.0.0.0/0, ::/0");
        lines.Add("PersistentKeepalive = 25");

        return string.Join('\n', lines) + "\n";
    }

    private static IReadOnlyDictionary<string, string?> ParseAwgParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }
    }

    private static string GenerateQrCodeDataUri(string config)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(config, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(8);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    private static uint ReadUInt32BigEndian(byte[] bytes) =>
        ((uint)bytes[0] << 24) |
        ((uint)bytes[1] << 16) |
        ((uint)bytes[2] << 8) |
        bytes[3];

    private static byte[] GetBigEndianBytes(uint value) =>
    [
        (byte)(value >> 24),
        (byte)(value >> 16),
        (byte)(value >> 8),
        (byte)value,
    ];
}

public sealed record CreateClientCommand(
    string Name,
    int? ExpiresInDays = null,
    long? TrafficLimitBytes = null);
