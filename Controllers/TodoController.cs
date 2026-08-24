using BE_ZSM.DTOs.Todos;
using BE_ZSM.Services.TodoService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BE_ZSM.Controllers;

[ApiController]
[Route("api/todos")]
[Authorize]
public class TodoController : ControllerBase
{
    private readonly ITodoService _todoService;

    public TodoController(ITodoService todoService)
    {
        _todoService = todoService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTodos([FromQuery] TodoQueryDto query)
    {
        var userId = GetCurrentUserId();
        var todos = await _todoService.GetTodosAsync(userId, query);
        return Ok(todos);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTodo(int id)
    {
        var userId = GetCurrentUserId();
        var todo = await _todoService.GetTodoAsync(id, userId);
        return Ok(todo);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTodo(CreateTodoDto dto)
    {
        var userId = GetCurrentUserId();
        await _todoService.CreateTodoAsync(dto, userId);
        return Ok(new
        {
            message = "Todo created successfully"
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTodo(int id, UpdateTodoDto dto)
    {
        var userId = GetCurrentUserId();
        await _todoService.UpdateTodoAsync(id, dto, userId);
        return Ok(new
        {
            message = "Todo updated successfully"
        });
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateTodoStatusDto dto)
    {
        var userId = GetCurrentUserId();

        await _todoService.UpdateTodoStatusAsync(
            id,
            userId,
            dto);

        return Ok(new
        {
            message = "Todo status updated successfully"
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTodo(int id)
    {
        var userId = GetCurrentUserId();        
        await _todoService.DeleteTodoAsync(id, userId);
        return Ok(new
        {
            message = "Todo deleted successfully"
        });
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            throw new UnauthorizedAccessException("User ID not found");
        }

        return int.Parse(userIdClaim.Value);
    }

    [HttpGet("{id:int}/activities")]
    public async Task<IActionResult> GetActivities(int id)
    {
        var result = await _todoService.GetActivitiesAsync(id, GetCurrentUserId());
        return Ok(result);
    }
}