using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Amnezia.Panel.Api.Data;

public sealed class PanelDbContextFactory : IDesignTimeDbContextFactory<PanelDbContext>
{
    public PanelDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PanelDatabase")
            ?? "Host=127.0.0.1;Port=5432;Database=amnezia_panel_dotnet;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<PanelDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new PanelDbContext(optionsBuilder.Options);
    }
}
