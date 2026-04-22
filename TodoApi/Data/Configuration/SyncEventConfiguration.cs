using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApi.Data.Entities;

namespace TodoApi.Data.Configuration;

public class SyncEventConfiguration : IEntityTypeConfiguration<SyncEvent>
{
    public void Configure(EntityTypeBuilder<SyncEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.EntityType).IsRequired();
        builder.Property(s => s.EntityId).IsRequired();
        builder.Property(s => s.EventType).IsRequired();
        builder.Property(s => s.Payload).IsRequired();
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired().ValueGeneratedNever();

        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => new { s.EntityType, s.EntityId });
    }
}
