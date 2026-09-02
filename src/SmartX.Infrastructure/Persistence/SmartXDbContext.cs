using Microsoft.EntityFrameworkCore;
using SmartX.Domain.Entities;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Infrastructure.Persistence;

public sealed class SmartXDbContext : DbContext
{
    public SmartXDbContext(DbContextOptions<SmartXDbContext> options)
        : base(options)
    {
    }

    public DbSet<DeploymentNode> DeploymentNodes =>
        Set<DeploymentNode>();

    public DbSet<Sensor> Sensors => Set<Sensor>();

    public DbSet<TelemetryRecord> TelemetryRecords =>
        Set<TelemetryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SmartXDbContext).Assembly);
    }
}
