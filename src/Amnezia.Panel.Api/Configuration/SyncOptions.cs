namespace Amnezia.Panel.Api.Configuration;

public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    public int InitialDelaySeconds { get; set; } = 5;

    public int IntervalSeconds { get; set; } = 30;

    public int ServerBatchSize { get; set; } = 20;

    public int JobBatchSize { get; set; } = 20;
}
