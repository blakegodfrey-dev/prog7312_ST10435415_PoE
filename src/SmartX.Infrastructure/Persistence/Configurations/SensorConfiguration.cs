using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartX.Domain.Entities;

namespace SmartX.Infrastructure.Persistence.Configurations;

public sealed class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> builder)
    {
        builder.ToTable("Sensors", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Sensors_Category",
                "[Category] BETWEEN 1 AND 3");

            tableBuilder.HasCheckConstraint(
                "CK_Sensors_ValueKind",
                "[ValueKind] BETWEEN 1 AND 3");

            tableBuilder.HasCheckConstraint(
                "CK_Sensors_ExpectedRange",
                "([ExpectedMinimum] IS NULL AND [ExpectedMaximum] IS NULL) " +
                "OR ([ExpectedMinimum] IS NOT NULL " +
                "AND [ExpectedMaximum] IS NOT NULL " +
                "AND [ExpectedMinimum] <= [ExpectedMaximum])");

            tableBuilder.HasCheckConstraint(
                "CK_Sensors_BooleanRange",
                "[ValueKind] <> 3 OR " +
                "([ExpectedMinimum] IS NULL AND [ExpectedMaximum] IS NULL)");
        });

        builder.HasKey(sensor => sensor.Id);

        builder.Property(sensor => sensor.MacAddress)
            .HasMaxLength(17)
            .IsRequired();

        builder.HasIndex(sensor => sensor.MacAddress)
            .IsUnique();

        builder.Property(sensor => sensor.FriendlyName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(sensor => sensor.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(sensor => sensor.MeasuredProperty)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(sensor => sensor.ValueKind)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(sensor => sensor.Unit)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(sensor => sensor.ExpectedMinimum)
            .HasColumnType("float");

        builder.Property(sensor => sensor.ExpectedMaximum)
            .HasColumnType("float");

        builder.HasIndex(sensor => sensor.DeploymentNodeId);

        builder.HasOne(sensor => sensor.DeploymentNode)
            .WithMany()
            .HasForeignKey(sensor => sensor.DeploymentNodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
