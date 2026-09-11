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

    [HttpPut("batch")]
    public async Task<IActionResult> SaveTodos([FromBody] List<SaveTodoDto> dtos)
    {
        await _todoService.SaveTodosAsync(dtos, GetCurrentUserId());
        return Ok(new { message = "Todos saved successfully" });
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            throw new UnauthorizedAccessException("User ID not found");
        return int.Parse(userIdClaim.Value);
    }   
}