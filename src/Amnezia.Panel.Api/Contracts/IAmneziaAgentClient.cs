using Amnezia.Panel.Api.Domain;
using Amnezia.Panel.Contracts;

namespace Amnezia.Panel.Api.Contracts;

public interface IAmneziaAgentClient
{
    Task<IReadOnlyList<AgentRuntimeCandidate>> ListAwgRuntimesAsync(CancellationToken cancellationToken);

    Task<AgentServerSnapshot> DiscoverRuntimeAsync(AgentRuntimeDiscoveryRequest request, CancellationToken cancellationToken);

    Task<AgentServerSnapshot> GetServerSnapshotAsync(PanelServer server, CancellationToken cancellationToken);

    Task<AgentClientMutationResult> CreateClientAsync(
        PanelServer server,
        string name,
        string address,
        string allowedIps,
        string? presharedKey,
        CancellationToken cancellationToken);

    Task<AgentClientMutationResult> RestoreClientAsync(
        PanelServer server,
        string name,
        string publicKey,
        string address,
        string allowedIps,
        string? presharedKey,
        CancellationToken cancellationToken);

    Task RemoveClientAsync(PanelServer server, string publicKey, CancellationToken cancellationToken);
}
