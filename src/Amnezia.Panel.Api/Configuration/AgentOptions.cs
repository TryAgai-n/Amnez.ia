namespace Amnezia.Panel.Api.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string BaseUrl { get; set; } = "http://127.0.0.1:9180/";

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 10;
}
