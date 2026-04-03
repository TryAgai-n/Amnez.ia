using Amnezia.Panel.Api.Configuration;
using Amnezia.Panel.Api.Contracts;
using Amnezia.Panel.Api.Data;
using Amnezia.Panel.Api.Endpoints;
using Amnezia.Panel.Api.Infrastructure;
using Amnezia.Panel.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("PanelDatabase")
    ?? throw new InvalidOperationException("Connection string 'PanelDatabase' is required.");

builder.Services.AddDbContext<PanelDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddHttpClient<IAmneziaAgentClient, AmneziaAgentHttpClient>();
builder.Services.AddScoped<ServerSyncService>();
builder.Services.AddScoped<JobService>();
builder.Services.AddHostedService<MetricsSyncBackgroundService>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/", () => Results.Redirect("/api"))
    .ExcludeFromDescription();

app.MapGet("/api", () => Results.Ok(new
{
    service = "amnezia-panel-api",
    version = "0.1.0",
    runtime = ".NET 10",
    uiMode = "temporary-php-ui",
}))
.WithName("ApiRoot");

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .WithName("LiveHealth");

app.MapGet("/health/ready", async (PanelDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail = "Database is not reachable.");
})
.WithName("ReadyHealth");

app.MapServerEndpoints();
app.MapJobEndpoints();

app.Run();
