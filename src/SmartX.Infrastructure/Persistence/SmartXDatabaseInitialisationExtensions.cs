using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartX.Infrastructure.Persistence.Seeding;

namespace SmartX.Infrastructure.Persistence;

public static class SmartXDatabaseInitialisationExtensions
{
    public static async Task InitialiseSmartXDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider
            .GetRequiredService<SmartXDbContext>();
        var seeder = scope.ServiceProvider
            .GetRequiredService<SmartXDatabaseSeeder>();

        await context.Database.MigrateAsync(cancellationToken);
        await seeder.SeedAsync(DateTimeOffset.UtcNow, cancellationToken);
    }
}
