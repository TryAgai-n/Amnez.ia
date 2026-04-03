using Amnezia.Panel.Api.Data;
using Amnezia.Panel.Api.Domain;
using Amnezia.Panel.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Amnezia.Panel.Api.Endpoints;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/servers/{serverId:guid}/clients", async (
            Guid serverId,
            CreateClientRequest request,
            ClientLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var client = await lifecycleService.CreateClientAsync(
                serverId,
                new CreateClientCommand(request.Name, request.ExpiresInDays, request.TrafficLimitBytes),
                cancellationToken);

            return Results.Created($"/api/clients/{client.Id}", ToDetailResponse(client));
        })
        .WithTags("Clients")
        .WithName("CreateClient");

        var group = endpoints.MapGroup("/api/clients").WithTags("Clients");

        group.MapGet("/{id:guid}", async (Guid id, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var client = await db.VpnClients
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            return client is null ? Results.NotFound() : Results.Ok(ToDetailResponse(client));
        })
        .WithName("GetClient");

        group.MapGet("/{id:guid}/download", async (Guid id, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var client = await db.VpnClients
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (client is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(client.Config))
            {
                return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "Client configuration is not available.");
            }

            var fileName = BuildConfigFileName(client);
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(client.Config),
                "application/octet-stream",
                fileName);
        })
        .WithName("DownloadClientConfig");

        group.MapGet("/{id:guid}/qr", async (Guid id, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var client = await db.VpnClients
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (client is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new ClientQrCodeResponse(client.Id, client.QrCodeDataUri));
        })
        .WithName("GetClientQrCode");

        group.MapGet("/{id:guid}/metrics", async (Guid id, double? hours, PanelDbContext db, CancellationToken cancellationToken) =>
        {
            var exists = await db.VpnClients.AnyAsync(x => x.Id == id, cancellationToken);
            if (!exists)
            {
                return Results.NotFound();
            }

            var cutoff = DateTime.UtcNow.AddHours(-Math.Max(1d / 60d, hours ?? 24d));
            var metrics = await db.ClientMetrics
                .AsNoTracking()
                .Where(x => x.ClientId == id && x.SampledAt >= cutoff)
                .OrderBy(x => x.SampledAt)
                .Select(x => new ClientMetricResponse(
                    x.SampledAt,
                    x.BytesSent,
                    x.BytesReceived,
                    x.SpeedUpKbps,
                    x.SpeedDownKbps,
                    x.IsOnline))
                .ToListAsync(cancellationToken);

            return Results.Ok(metrics);
        })
        .WithName("ListClientMetrics");

        group.MapPost("/{id:guid}/revoke", async (Guid id, ClientLifecycleService lifecycleService, CancellationToken cancellationToken) =>
        {
            var client = await lifecycleService.RevokeClientAsync(id, cancellationToken);
            return Results.Ok(ToDetailResponse(client));
        })
        .WithName("RevokeClient");

        group.MapPost("/{id:guid}/restore", async (Guid id, ClientLifecycleService lifecycleService, CancellationToken cancellationToken) =>
        {
            var client = await lifecycleService.RestoreClientAsync(id, cancellationToken);
            return Results.Ok(ToDetailResponse(client));
        })
        .WithName("RestoreClient");

        group.MapPost("/{id:guid}/sync", async (Guid id, ClientLifecycleService lifecycleService, CancellationToken cancellationToken) =>
        {
            var client = await lifecycleService.SyncClientAsync(id, cancellationToken);
            return Results.Ok(ToDetailResponse(client));
        })
        .WithName("SyncClient");

        group.MapPost("/{id:guid}/set-expiration", async (
            Guid id,
            SetClientExpirationRequest request,
            ClientLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var client = await lifecycleService.UpdateExpirationAsync(id, request.ExpiresAt, cancellationToken);
            return Results.Ok(ToDetailResponse(client));
        })
        .WithName("SetClientExpiration");

        group.MapPost("/{id:guid}/set-traffic-limit", async (
            Guid id,
            SetClientTrafficLimitRequest request,
            ClientLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var client = await lifecycleService.UpdateTrafficLimitAsync(id, request.LimitBytes, cancellationToken);
            return Results.Ok(ToDetailResponse(client));
        })
        .WithName("SetClientTrafficLimit");

        group.MapDelete("/{id:guid}", async (Guid id, ClientLifecycleService lifecycleService, CancellationToken cancellationToken) =>
        {
            await lifecycleService.DeleteClientAsync(id, cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteClient");

        return endpoints;
    }

    private static ClientDetailResponse ToDetailResponse(VpnClient client) =>
        new(
            client.Id,
            client.ServerId,
            client.Name,
            client.Address,
            client.AllowedIps,
            client.PublicKey,
            client.Status.ToString(),
            client.BytesSent,
            client.BytesReceived,
            client.LastHandshakeAt,
            client.LastSyncedAt,
            client.ExpiresAt,
            client.TrafficLimitBytes,
            !string.IsNullOrWhiteSpace(client.Config),
            !string.IsNullOrWhiteSpace(client.QrCodeDataUri),
            client.CreatedAt,
            client.UpdatedAt);

    private static string BuildConfigFileName(VpnClient client)
    {
        var hasUnsafeChars = client.Name.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        return hasUnsafeChars
            ? $"client_{client.Id:N}.conf"
            : $"{client.Name}.conf";
    }

    public sealed record CreateClientRequest(
        string Name,
        int? ExpiresInDays = null,
        long? TrafficLimitBytes = null);

    public sealed record ClientQrCodeResponse(Guid Id, string? QrCodeDataUri);

    public sealed record ClientMetricResponse(
        DateTime SampledAt,
        long BytesSent,
        long BytesReceived,
        long SpeedUpKbps,
        long SpeedDownKbps,
        bool IsOnline);

    public sealed record SetClientExpirationRequest(DateTime? ExpiresAt);

    public sealed record SetClientTrafficLimitRequest(long? LimitBytes);

    public sealed record ClientDetailResponse(
        Guid Id,
        Guid ServerId,
        string Name,
        string Address,
        string AllowedIps,
        string PublicKey,
        string Status,
        long BytesSent,
        long BytesReceived,
        DateTime? LastHandshakeAt,
        DateTime? LastSyncedAt,
        DateTime? ExpiresAt,
        long? TrafficLimitBytes,
        bool HasConfig,
        bool HasQrCode,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
