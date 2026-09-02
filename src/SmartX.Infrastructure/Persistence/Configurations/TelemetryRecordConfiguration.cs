using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartX.Domain.Entities;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Infrastructure.Persistence.Configurations;

public sealed class TelemetryRecordConfiguration
    : IEntityTypeConfiguration<TelemetryRecord>
{
    public void Configure(EntityTypeBuilder<TelemetryRecord> builder)
    {
        builder.ToTable("TelemetryRecords", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_TelemetryRecords_TypedValue",
                "([ValueKind] = 1 AND [FloatValue] IS NOT NULL " +
                "AND [IntegerValue] IS NULL AND [BooleanValue] IS NULL) OR " +
                "([ValueKind] = 2 AND [FloatValue] IS NULL " +
                "AND [IntegerValue] IS NOT NULL AND [BooleanValue] IS NULL) OR " +
                "([ValueKind] = 3 AND [FloatValue] IS NULL " +
                "AND [IntegerValue] IS NULL AND [BooleanValue] IS NOT NULL)");

            tableBuilder.HasCheckConstraint(
                "CK_TelemetryRecords_Validation",
                "([IsValid] = 1 AND [ValidationMessage] IS NULL) OR " +
                "([IsValid] = 0 AND [ValidationMessage] IS NOT NULL)");
        });

        builder.HasKey(record => record.Id);

        builder.Property(record => record.ValueKind)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(record => record.FloatValue)
            .HasColumnType("real");

        builder.Property(record => record.IntegerValue)
            .HasColumnType("int");

        builder.Property(record => record.BooleanValue)
            .HasColumnType("bit");

        builder.Property(record => record.RecordedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(record => record.ReceivedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(record => record.IsValid)
            .IsRequired();

        builder.Property(record => record.ValidationMessage)
            .HasMaxLength(TelemetryRecord.MaximumValidationMessageLength);

        builder.HasIndex(record => new
        {
            record.SensorId,
            record.RecordedAtUtc
        });

        builder.HasOne<Sensor>()
            .WithMany()
            .HasForeignKey(record => record.SensorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
