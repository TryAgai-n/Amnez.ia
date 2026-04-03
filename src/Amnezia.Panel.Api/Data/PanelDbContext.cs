using Amnezia.Panel.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Amnezia.Panel.Api.Data;

public sealed class PanelDbContext(DbContextOptions<PanelDbContext> options) : DbContext(options)
{
    public DbSet<PanelServer> Servers => Set<PanelServer>();

    public DbSet<VpnClient> VpnClients => Set<VpnClient>();

    public DbSet<ServerMetric> ServerMetrics => Set<ServerMetric>();

    public DbSet<ClientMetric> ClientMetrics => Set<ClientMetric>();

    public DbSet<JobRecord> Jobs => Set<JobRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var serverStatusConverter = new EnumToStringConverter<ServerStatus>();
        var clientStatusConverter = new EnumToStringConverter<ClientStatus>();
        var jobStatusConverter = new EnumToStringConverter<JobStatus>();

        modelBuilder.Entity<PanelServer>(entity =>
        {
            entity.ToTable("servers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Host).HasMaxLength(255);
            entity.Property(x => x.RuntimeType).HasMaxLength(32);
            entity.Property(x => x.ContainerName).HasMaxLength(255);
            entity.Property(x => x.ConfigPath).HasMaxLength(255);
            entity.Property(x => x.InterfaceName).HasMaxLength(64);
            entity.Property(x => x.QuickCommand).HasMaxLength(32);
            entity.Property(x => x.ShowCommand).HasMaxLength(32);
            entity.Property(x => x.VpnSubnet).HasMaxLength(64);
            entity.Property(x => x.ServerPublicKey).HasColumnType("text");
            entity.Property(x => x.PresharedKey).HasColumnType("text");
            entity.Property(x => x.AwgParametersJson).HasColumnType("text");
            entity.Property(x => x.LastError).HasColumnType("text");
            entity.Property(x => x.Status).HasConversion(serverStatusConverter).HasMaxLength(32);
            entity.HasMany(x => x.Clients).WithOne(x => x.Server).HasForeignKey(x => x.ServerId);
            entity.HasMany(x => x.Metrics).WithOne(x => x.Server).HasForeignKey(x => x.ServerId);
            entity.HasMany(x => x.Jobs).WithOne(x => x.Server).HasForeignKey(x => x.ServerId);
            entity.HasIndex(x => new { x.Host, x.SshPort }).IsUnique();
        });

        modelBuilder.Entity<VpnClient>(entity =>
        {
            entity.ToTable("vpn_clients");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Address).HasMaxLength(64);
            entity.Property(x => x.AllowedIps).HasMaxLength(255);
            entity.Property(x => x.PublicKey).HasMaxLength(255);
            entity.Property(x => x.PrivateKey).HasColumnType("text");
            entity.Property(x => x.PresharedKey).HasColumnType("text");
            entity.Property(x => x.Config).HasColumnType("text");
            entity.Property(x => x.QrCodeDataUri).HasColumnType("text");
            entity.Property(x => x.Status).HasConversion(clientStatusConverter).HasMaxLength(32);
            entity.HasIndex(x => new { x.ServerId, x.PublicKey }).IsUnique();
            entity.HasIndex(x => new { x.ServerId, x.Address }).IsUnique();
        });

        modelBuilder.Entity<ServerMetric>(entity =>
        {
            entity.ToTable("server_metrics");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ServerId, x.SampledAt });
        });

        modelBuilder.Entity<ClientMetric>(entity =>
        {
            entity.ToTable("client_metrics");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ClientId, x.SampledAt });
        });

        modelBuilder.Entity<JobRecord>(entity =>
        {
            entity.ToTable("jobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(64);
            entity.Property(x => x.RequestedBy).HasMaxLength(128);
            entity.Property(x => x.PayloadJson).HasColumnType("text");
            entity.Property(x => x.ResultJson).HasColumnType("text");
            entity.Property(x => x.Error).HasColumnType("text");
            entity.Property(x => x.Status).HasConversion(jobStatusConverter).HasMaxLength(32);
            entity.HasIndex(x => new { x.Status, x.RequestedAt });
        });
    }
}
