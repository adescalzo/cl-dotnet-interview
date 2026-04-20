using TodoApi.Data;

namespace TodoApi.Infrastructure.Mediator;

/// <summary>
/// Wolverine middleware that manages database transactions for commands.
/// Applied only to Commands via policy configuration (not queries).
/// Automatically saves changes after successful command execution.
/// Implements the Unit of Work pattern for transactional consistency.
/// </summary>
public sealed class TransactionMiddleware
{
    /// <summary>
    /// Always executed after the handler completes (successfully or with exception).
    /// Saves changes only if there are tracked changes in the DbContext.
    /// This ensures commands are transactional without explicit SaveChanges calls.
    /// IUnitOfWork is injected by Wolverine's code generation.
    /// </summary>
    public async Task Finally(TodoContext dbContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        // Only save if there are actual changes
        // This prevents unnecessary database calls for read-only operations
        if (!dbContext.ChangeTracker.HasChanges())
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
