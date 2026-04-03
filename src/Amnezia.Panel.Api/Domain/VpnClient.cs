namespace Amnezia.Panel.Api.Domain;

public sealed class VpnClient
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ServerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string AllowedIps { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    public string? PrivateKey { get; set; }

    public string? PresharedKey { get; set; }

    public string? Config { get; set; }

    public string? QrCodeDataUri { get; set; }

    public ClientStatus Status { get; set; } = ClientStatus.Unknown;

    public long BytesSent { get; set; }

    public long BytesReceived { get; set; }

    public DateTime? LastHandshakeAt { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public long? TrafficLimitBytes { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public PanelServer Server { get; set; } = null!;

    public List<ClientMetric> Metrics { get; set; } = [];
}
