using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartX.Domain.Entities;
using SmartX.Infrastructure.Persistence.Entities;
namespace SmartX.Infrastructure.Persistence.Configurations;
public sealed class CommandHistoryConfiguration : IEntityTypeConfiguration<CommandHistoryEntry>
{
    public void Configure(EntityTypeBuilder<CommandHistoryEntry> b)
    {
        b.ToTable("CommandHistory"); b.HasKey(x => x.Id);
        b.Property(x => x.Sequence).UseIdentityColumn(); b.HasIndex(x => x.Sequence).IsUnique();
        b.Property(x => x.Message).HasMaxLength(500).IsRequired();
        b.Property(x => x.Context).HasMaxLength(300).IsRequired();
        b.HasOne<Sensor>().WithMany().HasForeignKey(x => x.SensorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AtUtc);
    }
}
public sealed class InteractionConfiguration : IEntityTypeConfiguration<InteractionRecord>
{
    public void Configure(EntityTypeBuilder<InteractionRecord> b)
    {
        b.ToTable("Interactions"); b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        b.Property(x => x.Query).HasMaxLength(150).IsRequired();
        b.Property(x => x.Context).HasMaxLength(300).IsRequired();
        b.HasOne<Sensor>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AtUtc);
    }
}
public sealed class GatewayReceiptConfiguration : IEntityTypeConfiguration<GatewayReceipt>
{
    public void Configure(EntityTypeBuilder<GatewayReceipt> b)
    {
        b.ToTable("GatewayReceipts"); b.HasKey(x => x.SensorId);
        b.HasOne<Sensor>().WithMany().HasForeignKey(x => x.SensorId).OnDelete(DeleteBehavior.Cascade);
    }
}
