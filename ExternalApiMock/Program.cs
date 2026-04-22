using System.Text.Json;
using ExternalApiMock.Endpoints;
using ExternalApiMock.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddSingleton<TodoStore>(sp =>
{
    var store = new TodoStore();
    store.Seed();
    return store;
});

var app = builder.Build();

TodoListsEndpoints.Map(app);
TodoItemsEndpoints.Map(app);

await app.RunAsync().ConfigureAwait(false);
