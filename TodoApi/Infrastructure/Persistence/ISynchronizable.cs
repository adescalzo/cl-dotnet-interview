namespace TodoApi.Infrastructure.Persistence;

public interface ISynchronizable
{
    /// <summary>
    /// Called when this entity is synchronized with the database.
    /// </summary>
    void Synchronized(DateTime synchronizedAt);

    bool IsSynchronized { get; }

    DateTime? SynchronizedAt { get; }
}
