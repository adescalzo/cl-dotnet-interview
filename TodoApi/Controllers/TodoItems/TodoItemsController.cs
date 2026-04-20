using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Dtos;
using TodoApi.Data.Entities;

namespace TodoApi.Controllers.TodoItems;

[Route("api/todolists/{tlid:guid}/todoitems")]
[ApiController]
public class TodoItemsController : ControllerBase
{
    private readonly TodoContext _context;

    public TodoItemsController(TodoContext context)
    {
        _context = context;
    }

    // GET: api/todolists/5/todoitems
    [HttpGet]
    public async Task<ActionResult<IList<TodoItem>>> GetTodoItems(Guid tlid)
    {
        var todoList = await _context.TodoItem.Where(x => x.TodoListId == tlid).ToListAsync();

        return Ok(todoList);
    }

    // GET: api/todolists/5/todoitems/1
    [HttpGet("{id}")]
    public async Task<ActionResult<TodoItem>> GetTodoItem(Guid tlid, long id)
    {
        var todoItem = await _context.TodoItem.FirstOrDefaultAsync(x =>
            x.Id == id && x.TodoListId == tlid
        );

        if (todoItem == null)
        {
            return NotFound();
        }

        return Ok(todoItem);
    }

    // POST: api/todolists/5/todoitems
    [HttpPost]
    public async Task<ActionResult<TodoItem>> PostTodoItem(Guid tlid, CreateTodoItem payload)
    {
        var todoList = await _context.TodoList.FindAsync(tlid);

        if (todoList == null)
        {
            return NotFound();
        }

        var todoItem = new TodoItem { Name = payload.Name, TodoListId = tlid };

        _context.TodoItem.Add(todoItem);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Id = todoItem.Id,
            Name = todoItem.Name,
            IsComplete = todoItem.IsComplete,
        });
    }

    // PUT: api/todolists/5/todoitems/1
    [HttpPut("{id}")]
    public async Task<ActionResult<TodoItem>> PutTodoItem(Guid tlid, long id, UpdateTodoItem payload)
    {
        var todoItem = await _context.TodoItem.FirstOrDefaultAsync(x =>
            x.Id == id && x.TodoListId == tlid
        );

        if (todoItem == null)
        {
            return NotFound();
        }

        todoItem.Name = payload.Name;
        todoItem.IsComplete = payload.IsComplete;
        await _context.SaveChangesAsync();

        return Ok(todoItem);
    }

    // DELETE: api/todolists/5/todoitems/1
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTodoItem(Guid tlid, long id)
    {
        var todoItem = await _context.TodoItem.FirstOrDefaultAsync(x =>
            x.Id == id && x.TodoListId == tlid
        );

        if (todoItem == null)
        {
            return NotFound();
        }

        _context.TodoItem.Remove(todoItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
