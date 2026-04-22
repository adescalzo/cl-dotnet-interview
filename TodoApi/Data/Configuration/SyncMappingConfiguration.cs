using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApi.Data.Entities;

namespace TodoApi.Data.Configuration;

public class SyncMappingConfiguration : IEntityTypeConfiguration<SyncMapping>
{
    public void Configure(EntityTypeBuilder<SyncMapping> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.EntityType).IsRequired();
        builder.Property(s => s.LocalId).IsRequired();
        builder.Property(s => s.ExternalId).IsRequired().HasMaxLength(500);
        builder.Property(s => s.ExternalUpdatedAt).IsRequired().ValueGeneratedNever();
        builder.Property(s => s.LastSyncedAt).IsRequired().ValueGeneratedNever();

        builder.HasIndex(s => new { s.EntityType, s.LocalId }).IsUnique();
        builder.HasIndex(s => new { s.EntityType, s.ExternalId }).IsUnique();
    }
}
