namespace Amnezia.Panel.Api.Domain;

public sealed class VpnClient
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ServerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    public ClientStatus Status { get; set; } = ClientStatus.Unknown;

    public long BytesSent { get; set; }

    public long BytesReceived { get; set; }

    public DateTime? LastHandshakeAt { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public PanelServer Server { get; set; } = null!;

    public List<ClientMetric> Metrics { get; set; } = [];
}
