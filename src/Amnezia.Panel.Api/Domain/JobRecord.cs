namespace Amnezia.Panel.Api.Domain;

public sealed class JobRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ServerId { get; set; }

    public string Type { get; set; } = string.Empty;

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public string RequestedBy { get; set; } = "system";

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Error { get; set; }

    public string? PayloadJson { get; set; }

    public string? ResultJson { get; set; }

    public PanelServer? Server { get; set; }
}
