using ExternalApiMock.Models;
using ExternalApiMock.Services;

namespace ExternalApiMock.Endpoints;

public static class TodoItemsEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapMethods(
            "/todolists/{todolistId}/todoitems/{todoitemId}",
            ["PATCH"],
            (TodoStore store, string todolistId, string todoitemId, UpdateTodoItemBody body) =>
            {
                var list = store.FindList(todolistId);
                if (list is null) return Results.NotFound();

                var item = list.Items.FirstOrDefault(i => i.Id == todoitemId);
                if (item is null) return Results.NotFound();

                item.Description = body.Description ?? item.Description;
                item.Completed = body.Completed ?? item.Completed;
                item.UpdatedAt = DateTime.UtcNow;
                list.UpdatedAt = DateTime.UtcNow;

                return Results.Ok(item);
            }
        );

        app.MapDelete(
            "/todolists/{todolistId}/todoitems/{todoitemId}",
            (TodoStore store, string todolistId, string todoitemId) =>
            {
                var list = store.FindList(todolistId);
                if (list is null) return Results.NotFound();

                var removed = list.Items.RemoveAll(i => i.Id == todoitemId);
                return removed > 0 ? Results.NoContent() : Results.NotFound();
            }
        );
    }
}
