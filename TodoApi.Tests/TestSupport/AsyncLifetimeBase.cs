using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TodoApi.Data;
using TodoApi.Infrastructure;

namespace TodoApi.Tests.TestSupport;

public abstract class AsyncLifetimeBase : IAsyncLifetime
{
    protected TodoContext Context { get; }

    protected IClock Clock { get; }

    protected DateTime UtcNow { get; } = new(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);

    protected AsyncLifetimeBase()
    {
        var options = new DbContextOptionsBuilder<TodoContext>()
            .UseInMemoryDatabase($"TodoLists_Tests_{Guid.NewGuid()}")
            .Options;

        Context = new TodoContext(options);

        Clock = Substitute.For<IClock>();
        Clock.UtcNow.Returns(UtcNow);
    }

    protected Task SaveChangesAsync(CancellationToken ct = default) => Context.SaveChangesAsync(ct);

    public async Task InitializeAsync()
    {
        await Context.Database.EnsureCreatedAsync().ConfigureAwait(false);
        await OnInitializeAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await OnDisposeAsync().ConfigureAwait(false);
        await Context.Database.EnsureDeletedAsync().ConfigureAwait(false);
        await Context.DisposeAsync().ConfigureAwait(false);
    }

    protected virtual Task OnInitializeAsync() => Task.CompletedTask;

    protected virtual Task OnDisposeAsync() => Task.CompletedTask;
}
