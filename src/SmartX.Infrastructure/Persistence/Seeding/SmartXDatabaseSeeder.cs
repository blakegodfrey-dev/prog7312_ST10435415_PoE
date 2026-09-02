using Microsoft.EntityFrameworkCore;

namespace SmartX.Infrastructure.Persistence.Seeding;

public sealed class SmartXDatabaseSeeder
{
    private readonly SmartXDbContext _context;

    public SmartXDatabaseSeeder(SmartXDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SeedAsync(
        DateTimeOffset seedEndUtc,
        CancellationToken cancellationToken = default)
    {
        if (await _context.DeploymentNodes.AnyAsync(cancellationToken))
        {
            return false;
        }

        var seedData = HydroponicSeedData.Create(seedEndUtc);

        _context.DeploymentNodes.AddRange(seedData.DeploymentNodes);
        _context.Sensors.AddRange(seedData.Sensors);
        _context.TelemetryRecords.AddRange(seedData.TelemetryRecords);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
