using ExternalApiMock.Models;
using ExternalApiMock.Services;

namespace ExternalApiMock.Endpoints;

public static class TodoListsEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/todolists", (TodoStore store, ILogger<Program> logger) =>
        {
            var lists = store.All.Select(DeepCopy).ToList();
            var rng = Random.Shared;

            foreach (var list in lists)
            {
                if (rng.NextDouble() < 0.70)
                {
                    var item = new TodoItem(
                        Guid.NewGuid().ToString(),
                        null,
                        $"[INJECT] Random item {rng.Next(1000)}",
                        false,
                        DateTime.UtcNow,
                        DateTime.UtcNow
                    );
                    list.Items.Add(item);
                    logger.LogInjected(item.Description, list.Name);
                }

                if (list.Items.Count > 0 && rng.NextDouble() < 0.70)
                {
                    var target = list.Items[rng.Next(list.Items.Count)];
                    var original = target.Description;
                    target.Description = $"[MUTATE] {original} (mutated)";
                    target.UpdatedAt = DateTime.UtcNow;
                    logger.LogMutated(original, list.Name);
                }
            }

            return Results.Ok(lists);
        });

        app.MapPost("/todolists", (TodoStore store, CreateTodoListBody body) =>
        {
            var now = DateTime.UtcNow;
            var list = new TodoList(
                Guid.NewGuid().ToString(),
                body.SourceId,
                body.Name ?? "Untitled",
                now,
                now,
                []
            );

            foreach (var itemBody in body.Items ?? [])
            {
                list.Items.Add(new TodoItem(
                    Guid.NewGuid().ToString(),
                    itemBody.SourceId,
                    itemBody.Description ?? string.Empty,
                    itemBody.Completed ?? false,
                    now,
                    now
                ));
            }

            store.Add(list);
            return Results.Created($"/todolists/{list.Id}", list);
        });

        app.MapMethods(
            "/todolists/{todolistId}",
            ["PATCH"],
            (TodoStore store, string todolistId, UpdateTodoListBody body) =>
            {
                var list = store.FindList(todolistId);
                if (list is null) return Results.NotFound();

                list.Name = body.Name ?? list.Name;
                list.UpdatedAt = DateTime.UtcNow;

                return Results.Ok(list);
            }
        );

        app.MapDelete("/todolists/{todolistId}", (TodoStore store, string todolistId) =>
        {
            var removed = store.RemoveList(todolistId);
            return removed ? Results.NoContent() : Results.NotFound();
        });
    }

    private static TodoList DeepCopy(TodoList source)
    {
        var items = source.Items
            .Select(i => new TodoItem(i.Id, i.SourceId, i.Description, i.Completed, i.CreatedAt, i.UpdatedAt))
            .ToList();
        return new TodoList(source.Id, source.SourceId, source.Name, source.CreatedAt, source.UpdatedAt, items);
    }
}

internal static partial class TodoListsEndpointsLoggerDefinition
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        EventName = "ItemInjected",
        Message = "[INJECT] Injected item '{Description}' into list '{Name}'"
    )]
    public static partial void LogInjected(this ILogger logger, string description, string name);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        EventName = "ItemMutated",
        Message = "[MUTATE] Mutated item '{Original}' in list '{Name}'"
    )]
    public static partial void LogMutated(this ILogger logger, string original, string name);
}
