using Microsoft.EntityFrameworkCore;
using SmartX.Domain.Entities;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Tests.Infrastructure;

public sealed class SmartXDbContextModelTests
{
    [Fact]
    public void Model_ContainsAllCurrentPersistenceTypes()
    {
        using var context = CreateContext();

        Assert.NotNull(context.Model.FindEntityType(typeof(DeploymentNode)));
        Assert.NotNull(context.Model.FindEntityType(typeof(Sensor)));
        Assert.NotNull(context.Model.FindEntityType(typeof(TelemetryRecord)));
    }

    [Fact]
    public void Model_UsesExpectedTableNames()
    {
        using var context = CreateContext();

        Assert.Equal(
            "DeploymentNodes",
            GetEntityType<DeploymentNode>(context).GetTableName());
        Assert.Equal(
            "Sensors",
            GetEntityType<Sensor>(context).GetTableName());
        Assert.Equal(
            "TelemetryRecords",
            GetEntityType<TelemetryRecord>(context).GetTableName());
    }

    [Fact]
    public void TelemetryRecord_UsesNativeSqlColumnTypes()
    {
        using var context = CreateContext();
        var entityType = GetEntityType<TelemetryRecord>(context);

        Assert.Equal(
            "real",
            entityType.FindProperty(nameof(TelemetryRecord.FloatValue))!
                .GetColumnType());
        Assert.Equal(
            "int",
            entityType.FindProperty(nameof(TelemetryRecord.IntegerValue))!
                .GetColumnType());
        Assert.Equal(
            "bit",
            entityType.FindProperty(nameof(TelemetryRecord.BooleanValue))!
                .GetColumnType());
    }

    [Fact]
    public void SensorAndDeploymentCodes_HaveUniqueIndexes()
    {
        using var context = CreateContext();

        var sensorHasUniqueMacAddress = GetEntityType<Sensor>(context)
            .GetIndexes()
            .Any(index =>
                index.IsUnique &&
                index.Properties.Count == 1 &&
                index.Properties[0].Name == nameof(Sensor.MacAddress));

        var deploymentHasUniqueCode = GetEntityType<DeploymentNode>(context)
            .GetIndexes()
            .Any(index =>
                index.IsUnique &&
                index.Properties.Count == 1 &&
                index.Properties[0].Name == nameof(DeploymentNode.Code));

        Assert.True(sensorHasUniqueMacAddress);
        Assert.True(deploymentHasUniqueCode);
    }

    [Fact]
    public void TelemetryRecord_HasSensorTimestampHistoryIndex()
    {
        using var context = CreateContext();
        var entityType = GetEntityType<TelemetryRecord>(context);

        var hasHistoryIndex = entityType.GetIndexes().Any(index =>
            index.Properties.Count == 2 &&
            index.Properties[0].Name == nameof(TelemetryRecord.SensorId) &&
            index.Properties[1].Name == nameof(TelemetryRecord.RecordedAtUtc));

        Assert.True(hasHistoryIndex);
    }

    [Fact]
    public void TelemetryRecord_SensorRelationshipUsesCascadeDelete()
    {
        using var context = CreateContext();
        var entityType = GetEntityType<TelemetryRecord>(context);

        var sensorForeignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(typeof(Sensor), sensorForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, sensorForeignKey.DeleteBehavior);
    }

    [Fact]
    public void Sensor_DeploymentRelationshipUsesRestrictedDelete()
    {
        using var context = CreateContext();
        var entityType = GetEntityType<Sensor>(context);

        var deploymentForeignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(
            typeof(DeploymentNode),
            deploymentForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(
            DeleteBehavior.Restrict,
            deploymentForeignKey.DeleteBehavior);
    }

    private static SmartXDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartXDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;" +
                "Database=SmartXModelTests;" +
                "Trusted_Connection=True;")
            .Options;

        return new SmartXDbContext(options);
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IEntityType
        GetEntityType<TEntity>(SmartXDbContext context)
    {
        return context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).Name}' is missing.");
    }
}
