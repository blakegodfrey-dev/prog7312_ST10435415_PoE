using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartX.Domain.Entities;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Infrastructure.Persistence.Configurations;

public sealed class SensorAttachmentRecordConfiguration
    : IEntityTypeConfiguration<SensorAttachmentRecord>
{
    public void Configure(EntityTypeBuilder<SensorAttachmentRecord> builder)
    {
        builder.ToTable("SensorAttachments", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_SensorAttachments_Category",
                "[Category] BETWEEN 1 AND 3");

            tableBuilder.HasCheckConstraint(
                "CK_SensorAttachments_SizeBytes",
                "[SizeBytes] > 0");
        });

        builder.HasKey(attachment => attachment.Id);

        builder.Property(attachment => attachment.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(attachment => attachment.OriginalFileName)
            .HasMaxLength(SensorAttachmentRecord.MaximumFileNameLength)
            .IsRequired();

        builder.Property(attachment => attachment.StoredFileName)
            .HasMaxLength(SensorAttachmentRecord.MaximumFileNameLength)
            .IsRequired();

        builder.HasIndex(attachment => attachment.StoredFileName)
            .IsUnique();

        builder.Property(attachment => attachment.ContentType)
            .HasMaxLength(SensorAttachmentRecord.MaximumContentTypeLength)
            .IsRequired();

        builder.Property(attachment => attachment.SizeBytes)
            .IsRequired();

        builder.Property(attachment => attachment.RelativePath)
            .HasMaxLength(SensorAttachmentRecord.MaximumRelativePathLength)
            .IsRequired();

        builder.HasIndex(attachment => attachment.RelativePath)
            .IsUnique();

        builder.Property(attachment => attachment.UploadedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.HasIndex(attachment => new
        {
            attachment.SensorId,
            attachment.UploadedAtUtc
        });

        builder.HasOne<Sensor>()
            .WithMany()
            .HasForeignKey(attachment => attachment.SensorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
