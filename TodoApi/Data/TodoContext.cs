using Microsoft.EntityFrameworkCore;
using TodoApi.Data.Entities;

namespace TodoApi.Data;

public class TodoContext : DbContext
{
    public TodoContext(DbContextOptions<TodoContext> options)
        : base(options) { }

    public DbSet<TodoList> TodoList { get; set; } = default!;

    public DbSet<TodoItem> TodoItem { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TodoContext).Assembly);

        // Set default schema
        modelBuilder.HasDefaultSchema("zea");
    }
}
