using System.Net.Http.Json;
using Amnezia.Panel.Api.Configuration;
using Amnezia.Panel.Api.Contracts;
using Amnezia.Panel.Api.Domain;
using Amnezia.Panel.Contracts;
using Microsoft.Extensions.Options;

namespace Amnezia.Panel.Api.Infrastructure;

public sealed class AmneziaAgentHttpClient(
    HttpClient httpClient,
    IOptions<AgentOptions> options) : IAmneziaAgentClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly AgentOptions _options = options.Value;

    public async Task<IReadOnlyList<AgentRuntimeCandidate>> ListAwgRuntimesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/runtimes/awgs");
        ApplyApiKey(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var candidates = await response.Content.ReadFromJsonAsync<IReadOnlyList<AgentRuntimeCandidate>>(cancellationToken: cancellationToken);
        return candidates ?? [];
    }

    public async Task<AgentServerSnapshot> DiscoverRuntimeAsync(AgentRuntimeDiscoveryRequest requestPayload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/runtime/discover")
        {
            Content = JsonContent.Create(requestPayload)
        };
        ApplyApiKey(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var snapshot = await response.Content.ReadFromJsonAsync<AgentServerSnapshot>(cancellationToken: cancellationToken);
        return snapshot ?? throw new InvalidOperationException("Agent returned an empty discovery payload.");
    }

    public async Task<AgentServerSnapshot> GetServerSnapshotAsync(PanelServer server, CancellationToken cancellationToken)
    {
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
        ApplyApiKey(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var snapshot = await response.Content.ReadFromJsonAsync<AgentServerSnapshot>(cancellationToken: cancellationToken);
        return snapshot ?? throw new InvalidOperationException("Agent returned an empty snapshot payload.");
    }

    public async Task<AgentClientMutationResult> CreateClientAsync(
        PanelServer server,
        string name,
        string address,
        string allowedIps,
        string? presharedKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/clients/create")
        {
            Content = JsonContent.Create(new AgentCreateClientRequest(
                server.RuntimeType,
                server.ContainerName,
                name,
                address,
                allowedIps,
                presharedKey))
        };
        ApplyApiKey(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AgentClientMutationResult>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Agent returned an empty create-client payload.");
    }

    public async Task<AgentClientMutationResult> RestoreClientAsync(
        PanelServer server,
        string name,
        string publicKey,
        string address,
        string allowedIps,
        string? presharedKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/clients/restore")
        {
            Content = JsonContent.Create(new AgentRestoreClientRequest(
                server.RuntimeType,
                server.ContainerName,
                name,
                publicKey,
                address,
                allowedIps,
                presharedKey))
        };
        ApplyApiKey(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AgentClientMutationResult>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Agent returned an empty restore-client payload.");
    }

    public async Task RemoveClientAsync(PanelServer server, string publicKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/clients/remove")
        {
            Content = JsonContent.Create(new AgentRemoveClientRequest(
                server.RuntimeType,
                server.ContainerName,
                publicKey))
        };
        ApplyApiKey(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private void ApplyApiKey(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", _options.ApiKey);
        }
    }
}
