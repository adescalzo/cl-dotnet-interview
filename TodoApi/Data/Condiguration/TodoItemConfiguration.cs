using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TodoApi.Data.Entities;

namespace TodoApi.Data.Configuration;

public class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.IsComplete).IsRequired();
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
