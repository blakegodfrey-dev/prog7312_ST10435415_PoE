using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartX.Application.Attachments;
using SmartX.Infrastructure.Attachments;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Seeding;

namespace SmartX.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            throw new ArgumentException(
                "A content root path is required.",
                nameof(contentRootPath));
        }

        var connectionString = configuration.GetConnectionString(
            "SmartXDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'SmartXDatabase' is not configured.");
        }

        services.AddDbContext<SmartXDbContext>(options =>
            options.UseSqlServer(connectionString));

        var configuredStoragePath = configuration["Attachments:StoragePath"];
        var storagePath = string.IsNullOrWhiteSpace(configuredStoragePath)
            ? "uploads/sensor-attachments"
            : configuredStoragePath;
        var storageRootPath = Path.IsPathRooted(storagePath)
            ? storagePath
            : Path.GetFullPath(storagePath, contentRootPath);

        services.AddSingleton<IAttachmentFileStorage>(
            new LocalAttachmentFileStorage(storageRootPath));

        services.AddScoped<SmartXDatabaseSeeder>();

        return services;
    }
}
