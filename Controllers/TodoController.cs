using BE_ZSM.DTOs.Todos;
using BE_ZSM.Authorization;
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

    [HttpGet("me")]
    [HasPermission(PermissionCodes.TodoRead)]
    [EndpointSummary("Get the authenticated user's Todos")]
    [EndpointDescription("Returns a paginated list of Todos owned by the authenticated user.")]
    public async Task<IActionResult> GetMyTodos([FromQuery] TodoQueryDto query)
    {
        var userId = GetCurrentUserId();
        var todos = await _todoService.GetTodosAsync(userId, query);
        return Ok(todos);
    }

    [HttpGet]
    [HasPermission(PermissionCodes.TodosReadAll)]
    [EndpointSummary("Get Todos across all users")]
    [EndpointDescription("Returns a paginated list of Todos across the system.")]
    public async Task<IActionResult> GetAllTodos([FromQuery] TodoQueryDto query)
    {
        return Ok(await _todoService.GetAllTodosAsync(query));
    }

    [HttpGet("users/summary")]
    [HasPermission(PermissionCodes.TodosReadAll)]
    [EndpointSummary("Get paginated Todo counts by user")]
    [EndpointDescription("Returns user ID, display name, email and Todo count, with optional server-side search.")]
    public async Task<IActionResult> GetUserTodoSummary([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return Ok(await _todoService.GetUserTodoSummaryAsync(search, page, pageSize));
    }

    [HttpGet("users/{userId:int}")]
    [HasPermission(PermissionCodes.TodosReadAll)]
    [EndpointSummary("Get a user's Todos")]
    [EndpointDescription("Returns a paginated list of Todos owned by the requested user.")]
    public async Task<IActionResult> GetUserTodos(int userId, [FromQuery] TodoQueryDto query)
    {
        return Ok(await _todoService.GetTodosForUserAsync(userId, query));
    }

    [HttpPut("batch")]
    [HasPermission(PermissionCodes.TodoWrite)]
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
