using System.Text.Json;
using System.Text.RegularExpressions;
using Amnezia.Panel.Contracts;

namespace Amnezia.Agent.Services;

public sealed class AwgRuntimeService(
    ShellCommandRunner commandRunner,
    ILogger<AwgRuntimeService> logger)
{
    public async Task<bool> CanAccessDockerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var output = await commandRunner.RunAsync("docker version --format '{{.Server.Version}}'", cancellationToken);
            return !string.IsNullOrWhiteSpace(output);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Docker access check failed");
            return false;
        }
    }

    public async Task<IReadOnlyList<AgentRuntimeCandidate>> ListAwgRuntimesAsync(CancellationToken cancellationToken)
    {
        var containers = await GetAwgContainerNamesAsync(cancellationToken);
        var candidates = new List<AgentRuntimeCandidate>(containers.Count);

        foreach (var container in containers)
        {
            try
            {
                var runtime = await ResolveRuntimeAsync(container, cancellationToken);
                candidates.Add(new AgentRuntimeCandidate(
                    runtime.RuntimeType,
                    runtime.ContainerName,
                    runtime.InterfaceName,
                    runtime.ConfigPath,
                    runtime.QuickCommand,
                    runtime.ShowCommand,
                    runtime.ListenPort,
                    runtime.VpnSubnet,
                    runtime.Status));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping unreadable AWG runtime candidate {ContainerName}", container);
            }
        }

        return candidates;
    }

    public async Task<AgentServerSnapshot> DiscoverRuntimeAsync(AgentRuntimeDiscoveryRequest request, CancellationToken cancellationToken)
    {
        var runtime = await ResolveRuntimeAsync(request.ContainerName, cancellationToken);
        return await BuildSnapshotAsync(runtime, cancellationToken);
    }

    public async Task<AgentServerSnapshot> GetSnapshotAsync(AgentSnapshotRequest request, CancellationToken cancellationToken)
    {
        var runtime = await ResolveRuntimeAsync(request.ContainerName, cancellationToken);
        return await BuildSnapshotAsync(runtime, cancellationToken);
    }

    private async Task<AgentServerSnapshot> BuildSnapshotAsync(ResolvedRuntime runtime, CancellationToken cancellationToken)
    {
        var config = await commandRunner.RunAsync(
            $"docker exec -i {Shell(runtime.ContainerName)} cat {Shell(runtime.ConfigPath)}",
            cancellationToken);

        var peers = ParsePeerBlocks(config);
        var keyValues = ParseKeyValueConfig(config);
        var clientsTableJson = await commandRunner.RunAsync(
            $"docker exec -i {Shell(runtime.ContainerName)} sh -lc {Shell("cat /opt/amnezia/awg/clientsTable 2>/dev/null || echo []")}",
            cancellationToken);

        var clientsTable = DeserializeClientsTable(clientsTableJson);
        var dumpOutput = await commandRunner.RunAsync(
            $"docker exec -i {Shell(runtime.ContainerName)} {runtime.ShowCommand} show {Shell(runtime.InterfaceName)} dump",
            cancellationToken);
        var dumpStats = ParseWireGuardDump(dumpOutput);

        var statsOutput = await commandRunner.RunAsync(
            $"docker stats --no-stream --format {Shell("{{.CPUPerc}}|{{.MemUsage}}")} {Shell(runtime.ContainerName)}",
            cancellationToken);
        var interfaceBytesOutput = await commandRunner.RunAsync(
            $"docker exec -i {Shell(runtime.ContainerName)} sh -lc {Shell($"cat /sys/class/net/{runtime.InterfaceName}/statistics/rx_bytes && cat /sys/class/net/{runtime.InterfaceName}/statistics/tx_bytes")}",
            cancellationToken);

        var cpuPercent = ParseCpuPercent(statsOutput);
        var (memoryUsedMb, memoryTotalMb) = ParseMemoryUsage(statsOutput);
        var (networkRxBytes, networkTxBytes) = ParseInterfaceTotals(interfaceBytesOutput);

        var serverPublicKey = await TryReadCommandAsync(
            $"docker exec -i {Shell(runtime.ContainerName)} sh -lc {Shell("cat /opt/amnezia/awg/wireguard_server_public_key.key 2>/dev/null || true")}",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(serverPublicKey))
        {
            serverPublicKey = await TryReadCommandAsync(
                $"docker exec -i {Shell(runtime.ContainerName)} {runtime.ShowCommand} show {Shell(runtime.InterfaceName)} public-key",
                cancellationToken);
        }

        var presharedKey = await TryReadCommandAsync(
            $"docker exec -i {Shell(runtime.ContainerName)} sh -lc {Shell("cat /opt/amnezia/awg/wireguard_psk.key 2>/dev/null || true")}",
            cancellationToken);

        var clientNameByKey = clientsTable
            .Where(x => !string.IsNullOrWhiteSpace(x.ClientId))
            .ToDictionary(
                x => x.ClientId!,
                x => x.UserData?.ClientName ?? string.Empty,
                StringComparer.Ordinal);

        var clientSnapshots = new List<AgentClientSnapshot>(peers.Count);
        foreach (var peer in peers)
        {
            if (!peer.TryGetValue("PublicKey", out var publicKey) || string.IsNullOrWhiteSpace(publicKey))
            {
                continue;
            }

            peer.TryGetValue("AllowedIPs", out var allowedIps);
            var address = StripCidr(allowedIps);
            if (string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            dumpStats.TryGetValue(publicKey, out var stat);
            stat ??= new WireGuardDumpPeer(0, 0, 0);

            DateTimeOffset? lastHandshake = stat.LastHandshakeEpoch > 0
                ? DateTimeOffset.FromUnixTimeSeconds(stat.LastHandshakeEpoch)
                : null;

            clientSnapshots.Add(new AgentClientSnapshot(
                publicKey,
                ResolveClientName(clientNameByKey, publicKey, address),
                address,
                allowedIps ?? string.Empty,
                "active",
                stat.BytesSent,
                stat.BytesReceived,
                lastHandshake,
                0,
                0));
        }

        var activeClients = clientSnapshots.Count(x => x.LastHandshake is not null && x.LastHandshake.Value >= DateTimeOffset.UtcNow.AddMinutes(-5));

        return new AgentServerSnapshot(
            runtime.RuntimeType,
            runtime.ContainerName,
            runtime.InterfaceName,
            runtime.ConfigPath,
            runtime.QuickCommand,
            runtime.ShowCommand,
            runtime.ListenPort,
            runtime.Status,
            runtime.VpnSubnet,
            string.IsNullOrWhiteSpace(serverPublicKey) ? null : serverPublicKey,
            string.IsNullOrWhiteSpace(presharedKey) ? null : presharedKey,
            runtime.AwgParameters,
            activeClients,
            cpuPercent,
            memoryUsedMb,
            memoryTotalMb,
            networkRxBytes,
            networkTxBytes,
            clientSnapshots);
    }

    private async Task<ResolvedRuntime> ResolveRuntimeAsync(string? explicitContainerName, CancellationToken cancellationToken)
    {
        string containerName;
        if (!string.IsNullOrWhiteSpace(explicitContainerName))
        {
            containerName = explicitContainerName.Trim();
            var exists = await ContainerExistsAsync(containerName, cancellationToken);
            if (!exists)
            {
                throw new InvalidOperationException($"Container '{containerName}' was not found.");
            }
        }
        else
        {
            var containers = await GetAwgContainerNamesAsync(cancellationToken);
            containerName = containers.FirstOrDefault()
                ?? throw new InvalidOperationException("No existing Amnezia AWG runtime was found.");
        }

        var configPath = await commandRunner.RunAsync(
            $"docker exec -i {Shell(containerName)} sh -lc {Shell("if [ -f /opt/amnezia/awg/awg0.conf ]; then echo /opt/amnezia/awg/awg0.conf; elif [ -f /opt/amnezia/awg/wg0.conf ]; then echo /opt/amnezia/awg/wg0.conf; fi")}",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new InvalidOperationException($"Container '{containerName}' does not expose awg0.conf or wg0.conf.");
        }

        var interfaceName = Path.GetFileNameWithoutExtension(configPath);
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            interfaceName = "awg0";
        }

        var isAwg = interfaceName.StartsWith("awg", StringComparison.OrdinalIgnoreCase);
        var showCommand = isAwg ? "awg" : "wg";
        var quickCommand = isAwg ? "awg-quick" : "wg-quick";

        var status = await commandRunner.RunAsync(
            $"docker inspect -f {Shell("{{.State.Status}}")} {Shell(containerName)}",
            cancellationToken);
        var config = await commandRunner.RunAsync(
            $"docker exec -i {Shell(containerName)} cat {Shell(configPath)}",
            cancellationToken);
        var parsed = ParseKeyValueConfig(config);
        var listenPort = parsed.TryGetValue("ListenPort", out var listenPortText) && int.TryParse(listenPortText, out var parsedListenPort)
            ? parsedListenPort
            : 0;

        var awgParameters = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var key in new[] { "Jc", "Jmin", "Jmax", "S1", "S2", "S3", "S4", "H1", "H2", "H3", "H4" })
        {
            if (parsed.TryGetValue(key, out var value))
            {
                awgParameters[key] = value;
            }
        }

        return new ResolvedRuntime(
            isAwg ? "awg" : "wg",
            containerName,
            interfaceName,
            configPath,
            quickCommand,
            showCommand,
            listenPort,
            parsed.TryGetValue("Address", out var address) ? address : null,
            status,
            awgParameters);
    }

    private async Task<List<string>> GetAwgContainerNamesAsync(CancellationToken cancellationToken)
    {
        var output = await commandRunner.RunAsync(
            "docker ps -a --format '{{.Names}}' | grep -E '^amnezia-awg([[:alnum:]_.-]*)?$' || true",
            cancellationToken);

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<bool> ContainerExistsAsync(string containerName, CancellationToken cancellationToken)
    {
        var output = await commandRunner.RunAsync(
            $"docker ps -a --format '{{{{.Names}}}}' | grep -Fx -- {Shell(containerName)} >/dev/null && echo 1 || true",
            cancellationToken);
        return string.Equals(output.Trim(), "1", StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ParseKeyValueConfig(string config)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in config.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] is '#' or '[')
            {
                continue;
            }

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                result[parts[0]] = parts[1];
            }
        }

        return result;
    }

    private static List<Dictionary<string, string>> ParsePeerBlocks(string config)
    {
        var peers = new List<Dictionary<string, string>>();
        Dictionary<string, string>? current = null;

        foreach (var rawLine in config.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line == "[Peer]")
            {
                if (current is { Count: > 0 })
                {
                    peers.Add(current);
                }

                current = new Dictionary<string, string>(StringComparer.Ordinal);
                continue;
            }

            if (line.StartsWith('['))
            {
                if (current is { Count: > 0 })
                {
                    peers.Add(current);
                }

                current = null;
                continue;
            }

            if (current is null)
            {
                continue;
            }

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                current[parts[0]] = parts[1];
            }
        }

        if (current is { Count: > 0 })
        {
            peers.Add(current);
        }

        return peers;
    }

    private static Dictionary<string, WireGuardDumpPeer> ParseWireGuardDump(string output)
    {
        var result = new Dictionary<string, WireGuardDumpPeer>(StringComparer.Ordinal);

        foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = Regex.Split(rawLine.Trim(), "\\s+");
            if (parts.Length < 7)
            {
                continue;
            }

            if (!long.TryParse(parts[4], out var handshake))
            {
                handshake = 0;
            }

            _ = long.TryParse(parts[5], out var bytesSent);
            _ = long.TryParse(parts[6], out var bytesReceived);

            result[parts[0]] = new WireGuardDumpPeer(bytesSent, bytesReceived, handshake);
        }

        return result;
    }

    private static List<ClientsTableEntry> DeserializeClientsTable(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ClientsTableEntry>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string ResolveClientName(
        IReadOnlyDictionary<string, string> clientNameByKey,
        string publicKey,
        string address)
    {
        if (clientNameByKey.TryGetValue(publicKey, out var clientName) && !string.IsNullOrWhiteSpace(clientName))
        {
            return clientName.Trim();
        }

        return $"Imported {address}";
    }

    private static double ParseCpuPercent(string statsOutput)
    {
        var cpuText = statsOutput.Split('|', 2, StringSplitOptions.TrimEntries).FirstOrDefault();
        return double.TryParse(cpuText?.TrimEnd('%'), out var cpuPercent)
            ? cpuPercent
            : 0d;
    }

    private static (double UsedMb, double TotalMb) ParseMemoryUsage(string statsOutput)
    {
        var parts = statsOutput.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return (0d, 0d);
        }

        var usageParts = parts[1].Split('/', StringSplitOptions.TrimEntries);
        if (usageParts.Length < 2)
        {
            return (0d, 0d);
        }

        return (ParseByteSizeToMb(usageParts[0]), ParseByteSizeToMb(usageParts[1]));
    }

    private static (long RxBytes, long TxBytes) ParseInterfaceTotals(string interfaceBytesOutput)
    {
        var lines = interfaceBytesOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length < 2)
        {
            return (0L, 0L);
        }

        _ = long.TryParse(lines[0], out var rxBytes);
        _ = long.TryParse(lines[1], out var txBytes);
        return (rxBytes, txBytes);
    }

    private static string StripCidr(string? allowedIps)
    {
        if (string.IsNullOrWhiteSpace(allowedIps))
        {
            return string.Empty;
        }

        var first = allowedIps.Split(',', StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        var slashIndex = first.IndexOf('/');
        return slashIndex >= 0 ? first[..slashIndex] : first;
    }

    private static double ParseByteSizeToMb(string input)
    {
        var match = Regex.Match(input.Trim(), @"^(?<value>[0-9]+(?:\.[0-9]+)?)(?<unit>B|kB|KB|KiB|MB|MiB|GB|GiB)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return 0d;
        }

        var value = double.Parse(match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value.ToUpperInvariant();

        return unit switch
        {
            "B" => value / 1024d / 1024d,
            "KB" => value / 1024d,
            "KIB" => value / 1024d,
            "MB" => value,
            "MIB" => value,
            "GB" => value * 1024d,
            "GIB" => value * 1024d,
            _ => 0d,
        };
    }

    private async Task<string> TryReadCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            return await commandRunner.RunAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Optional command failed: {Command}", command);
            return string.Empty;
        }
    }

    private static string Shell(string value) => $"'{value.Replace("'", "'\"'\"'")}'";

    private sealed record ResolvedRuntime(
        string RuntimeType,
        string ContainerName,
        string InterfaceName,
        string ConfigPath,
        string QuickCommand,
        string ShowCommand,
        int ListenPort,
        string? VpnSubnet,
        string Status,
        IReadOnlyDictionary<string, string?> AwgParameters);

    private sealed record WireGuardDumpPeer(long BytesSent, long BytesReceived, long LastHandshakeEpoch);

    private sealed record ClientsTableEntry(string? ClientId, ClientsTableUserData? UserData);

    private sealed record ClientsTableUserData(string? ClientName);
}
