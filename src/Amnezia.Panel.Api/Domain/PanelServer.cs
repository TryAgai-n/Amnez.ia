namespace Amnezia.Panel.Api.Domain;

public sealed class PanelServer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int SshPort { get; set; } = 22;

    public string RuntimeType { get; set; } = "awg";

    public string? ContainerName { get; set; }

    public string? ConfigPath { get; set; }

    public string? InterfaceName { get; set; }

    public string? QuickCommand { get; set; }

    public string? ShowCommand { get; set; }

    public int? ListenPort { get; set; }

    public string? VpnSubnet { get; set; }

    public string? ServerPublicKey { get; set; }

    public string? PresharedKey { get; set; }

    public string? AwgParametersJson { get; set; }

    public ServerStatus Status { get; set; } = ServerStatus.Provisioning;

    public DateTime? LastSyncedAt { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<VpnClient> Clients { get; set; } = [];

    public List<ServerMetric> Metrics { get; set; } = [];

    public List<JobRecord> Jobs { get; set; } = [];
}
