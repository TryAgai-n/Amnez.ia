namespace Amnezia.Panel.Api.Domain;

public sealed class ServerMetric
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTime SampledAt { get; set; } = DateTime.UtcNow;

    public double CpuPercent { get; set; }

    public double MemoryUsedMb { get; set; }

    public double MemoryTotalMb { get; set; }

    public double NetworkRxKbps { get; set; }

    public double NetworkTxKbps { get; set; }

    public int ActiveClients { get; set; }

    public PanelServer Server { get; set; } = null!;
}
