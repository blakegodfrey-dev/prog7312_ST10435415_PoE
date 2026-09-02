using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Seeding;

namespace SmartX.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(
            "SmartXDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'SmartXDatabase' is not configured.");
        }

        services.AddDbContext<SmartXDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<SmartXDatabaseSeeder>();

        return services;
    }
}
