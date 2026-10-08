using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartX.Application.Operations;
using SmartX.Infrastructure.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartX.Application.Attachments;
using SmartX.Application.Live;
using SmartX.Infrastructure.Live;
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

        var capacityText = configuration["LiveTelemetry:RecentHistoryCapacity"]
            ?? LiveTelemetryStore.DefaultHistoryCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!int.TryParse(capacityText, out var capacity))
            throw new InvalidOperationException("LiveTelemetry:RecentHistoryCapacity must be an integer.");
        services.AddSingleton(new LiveTelemetryStore(capacity));
        services.AddScoped<LiveTelemetryService>();
        services.AddScoped<SmartXDatabaseSeeder>();

        services.AddOptions<OperationsOptions>().Bind(configuration.GetSection("Operations"))
            .Validate(o => o.QueueCapacity > 0 && o.QueueCapacity <= 100000 && o.StaleSeconds > 0 && o.DisconnectedSeconds > o.StaleSeconds && o.ScanSeconds > 0 && o.ProcessingDelayMilliseconds is >= 0 and <= 1000, "Invalid operation limits.").ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new IncidentTracker(sp.GetRequiredService<TimeProvider>(),
            TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<OperationsOptions>>().Value.StaleSeconds),
            TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<OperationsOptions>>().Value.DisconnectedSeconds)));
        services.AddSingleton(sp => new TelemetryWorkQueue<TelemetryWork>(sp.GetRequiredService<IOptions<OperationsOptions>>().Value.QueueCapacity));
        services.AddSingleton<TelemetryDispatcher>();
        services.AddSingleton<CommandService>();
        services.AddHostedService<TelemetryProcessor>();
        services.AddHostedService<ConnectionMonitor>();
        return services;
    }
}
