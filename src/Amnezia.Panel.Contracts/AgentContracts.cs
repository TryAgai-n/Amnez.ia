namespace Amnezia.Panel.Contracts;

public sealed record AgentRuntimeDiscoveryRequest(
    string RuntimeType = "awg",
    string? ContainerName = null);

public sealed record AgentRuntimeCandidate(
    string RuntimeType,
    string ContainerName,
    string InterfaceName,
    string ConfigPath,
    string QuickCommand,
    string ShowCommand,
    int ListenPort,
    string? VpnSubnet,
    string Status);

public sealed record AgentSnapshotRequest(
    Guid? ServerId,
    string? Name,
    string Host,
    int SshPort,
    string RuntimeType = "awg",
    string? ContainerName = null,
    int? ListenPort = null);

public sealed record AgentServerSnapshot(
    string RuntimeType,
    string ContainerName,
    string InterfaceName,
    string ConfigPath,
    string QuickCommand,
    string ShowCommand,
    int ListenPort,
    string Status,
    string? VpnSubnet,
    string? ServerPublicKey,
    string? PresharedKey,
    IReadOnlyDictionary<string, string?> AwgParameters,
    int ActiveClients,
    double CpuPercent,
    double MemoryUsedMb,
    double MemoryTotalMb,
    long NetworkRxBytes,
    long NetworkTxBytes,
    IReadOnlyList<AgentClientSnapshot> Clients);

public sealed record AgentClientSnapshot(
    string PublicKey,
    string Name,
    string Address,
    string AllowedIps,
    string? PresharedKey,
    string Status,
    long BytesSent,
    long BytesReceived,
    DateTimeOffset? LastHandshake,
    long SpeedUpKbps,
    long SpeedDownKbps);

public sealed record AgentCreateClientRequest(
    string RuntimeType = "awg",
    string? ContainerName = null,
    string Name = "",
    string Address = "",
    string AllowedIps = "",
    string? PresharedKey = null);

public sealed record AgentRestoreClientRequest(
    string RuntimeType = "awg",
    string? ContainerName = null,
    string Name = "",
    string PublicKey = "",
    string Address = "",
    string AllowedIps = "",
    string? PresharedKey = null);

public sealed record AgentRemoveClientRequest(
    string RuntimeType = "awg",
    string? ContainerName = null,
    string PublicKey = "");

public sealed record AgentClientMutationResult(
    string PublicKey,
    string? PrivateKey,
    string Name,
    string Address,
    string AllowedIps,
    string? PresharedKey);
