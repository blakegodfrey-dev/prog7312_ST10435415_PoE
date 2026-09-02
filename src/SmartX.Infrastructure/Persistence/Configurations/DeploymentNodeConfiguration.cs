using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartX.Domain.Entities;

namespace SmartX.Infrastructure.Persistence.Configurations;

public sealed class DeploymentNodeConfiguration
    : IEntityTypeConfiguration<DeploymentNode>
{
    public void Configure(EntityTypeBuilder<DeploymentNode> builder)
    {
        builder.ToTable("DeploymentNodes", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_DeploymentNodes_NodeType",
                "[NodeType] BETWEEN 1 AND 4");
        });

        builder.HasKey(node => node.Id);

        builder.Property(node => node.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(node => node.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(node => node.Code)
            .IsUnique();

        builder.Property(node => node.NodeType)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(node => node.ParentId);

        builder.HasOne(node => node.Parent)
            .WithMany(node => node.Children)
            .HasForeignKey(node => node.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(node => node.Children)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
