using SmartX.Domain.Entities;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Infrastructure.Persistence.Seeding;

public sealed record HydroponicSeedDataset(
    IReadOnlyList<DeploymentNode> DeploymentNodes,
    IReadOnlyList<Sensor> Sensors,
    IReadOnlyList<TelemetryRecord> TelemetryRecords);
