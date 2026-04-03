using Amnezia.Panel.Api.Domain;
using Amnezia.Panel.Contracts;

namespace Amnezia.Panel.Api.Contracts;

public interface IAmneziaAgentClient
{
    Task<IReadOnlyList<AgentRuntimeCandidate>> ListAwgRuntimesAsync(CancellationToken cancellationToken);

    Task<AgentServerSnapshot> DiscoverRuntimeAsync(AgentRuntimeDiscoveryRequest request, CancellationToken cancellationToken);

    Task<AgentServerSnapshot> GetServerSnapshotAsync(PanelServer server, CancellationToken cancellationToken);
}
