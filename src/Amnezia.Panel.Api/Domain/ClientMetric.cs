namespace Amnezia.Panel.Api.Domain;

public sealed class ClientMetric
{
    public long Id { get; set; }

    public Guid ClientId { get; set; }

    public DateTime SampledAt { get; set; } = DateTime.UtcNow;

    public long BytesSent { get; set; }

    public long BytesReceived { get; set; }

    public long SpeedUpKbps { get; set; }

    public long SpeedDownKbps { get; set; }

    public bool IsOnline { get; set; }

    public VpnClient Client { get; set; } = null!;
}
