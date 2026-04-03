namespace Amnezia.Panel.Api.Contracts;

public sealed record AgentSnapshotRequest(
    Guid ServerId,
    string Name,
    string Host,
    int SshPort,
    string RuntimeType,
    string? ContainerName,
    int? ListenPort);

public sealed record AgentServerSnapshot(
    string RuntimeType,
    string ContainerName,
    string InterfaceName,
    int ListenPort,
    string Status,
    int ActiveClients,
    double CpuPercent,
    double MemoryUsedMb,
    double MemoryTotalMb,
    double NetworkRxKbps,
    double NetworkTxKbps,
    IReadOnlyList<AgentClientSnapshot> Clients);

public sealed record AgentClientSnapshot(
    string PublicKey,
    string Name,
    string Address,
    string Status,
    long BytesSent,
    long BytesReceived,
    DateTimeOffset? LastHandshake,
    long SpeedUpKbps,
    long SpeedDownKbps);
