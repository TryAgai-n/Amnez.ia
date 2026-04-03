using Amnezia.Agent.Configuration;
using Amnezia.Agent.Services;
using Amnezia.Panel.Contracts;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentSecurityOptions>(builder.Configuration.GetSection(AgentSecurityOptions.SectionName));
builder.Services.AddSingleton<ShellCommandRunner>();
builder.Services.AddSingleton<AwgRuntimeService>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    var options = context.RequestServices.GetRequiredService<IOptions<AgentSecurityOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        await next();
        return;
    }

    var requestApiKey = context.Request.Headers["X-Api-Key"].ToString();
    if (!string.Equals(requestApiKey, options.ApiKey, StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
        return;
    }

    await next();
});

app.MapGet("/", () => Results.Redirect("/v1"))
    .ExcludeFromDescription();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

app.MapGet("/health/ready", async (AwgRuntimeService runtimeService, CancellationToken cancellationToken) =>
{
    var ready = await runtimeService.CanAccessDockerAsync(cancellationToken);
    return ready
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: "Docker access is unavailable.");
});

app.MapGet("/v1", () => Results.Ok(new
{
    service = "amnezia-agent",
    version = "0.1.0",
    capabilities = new[] { "awg-discovery", "awg-snapshot" },
}));

app.MapGet("/v1/runtimes/awgs", async (AwgRuntimeService runtimeService, CancellationToken cancellationToken) =>
{
    var runtimes = await runtimeService.ListAwgRuntimesAsync(cancellationToken);
    return Results.Ok(runtimes);
});

app.MapPost("/v1/runtime/discover", async (AgentRuntimeDiscoveryRequest request, AwgRuntimeService runtimeService, CancellationToken cancellationToken) =>
{
    var snapshot = await runtimeService.DiscoverRuntimeAsync(request, cancellationToken);
    return Results.Ok(snapshot);
});

app.MapPost("/v1/runtime/snapshot", async (AgentSnapshotRequest request, AwgRuntimeService runtimeService, CancellationToken cancellationToken) =>
{
    var snapshot = await runtimeService.GetSnapshotAsync(request, cancellationToken);
    return Results.Ok(snapshot);
});

app.Run();
