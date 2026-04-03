namespace Amnezia.Agent.Configuration;

public sealed class AgentSecurityOptions
{
    public const string SectionName = "Security";

    public string ApiKey { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 15;
}
