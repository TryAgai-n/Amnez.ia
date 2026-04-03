using Amnezia.Panel.Api.Domain;

namespace Amnezia.Panel.Api.Contracts;

public interface IAmneziaAgentClient
{
    Task<AgentServerSnapshot> GetServerSnapshotAsync(PanelServer server, CancellationToken cancellationToken);
}
