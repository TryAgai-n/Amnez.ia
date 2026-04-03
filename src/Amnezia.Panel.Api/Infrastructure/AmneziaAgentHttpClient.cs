using System.Net.Http.Json;
using Amnezia.Panel.Api.Configuration;
using Amnezia.Panel.Api.Contracts;
using Amnezia.Panel.Api.Domain;
using Microsoft.Extensions.Options;

namespace Amnezia.Panel.Api.Infrastructure;

public sealed class AmneziaAgentHttpClient(
    HttpClient httpClient,
    IOptions<AgentOptions> options) : IAmneziaAgentClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly AgentOptions _options = options.Value;

    public async Task<AgentServerSnapshot> GetServerSnapshotAsync(PanelServer server, CancellationToken cancellationToken)
    {
        ConfigureHttpClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/runtime/snapshot")
        {
            Content = JsonContent.Create(new AgentSnapshotRequest(
                server.Id,
                server.Name,
                server.Host,
                server.SshPort,
                server.RuntimeType,
                server.ContainerName,
                server.ListenPort))
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", _options.ApiKey);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var snapshot = await response.Content.ReadFromJsonAsync<AgentServerSnapshot>(cancellationToken: cancellationToken);
        return snapshot ?? throw new InvalidOperationException("Agent returned an empty snapshot payload.");
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.BaseUrl, UriKind.Absolute);
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));
    }
}
