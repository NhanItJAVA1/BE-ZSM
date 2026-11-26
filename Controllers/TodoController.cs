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
    private readonly IAuthorizationService _authorizationService;

    public TodoController(ITodoService todoService, IAuthorizationService authorizationService)
    {
        _todoService = todoService;
        _authorizationService = authorizationService;
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

    [HttpGet("shared")]
    [HasPermission(PermissionCodes.TodosReadShared)]
    [EndpointSummary("Get Todos shared with the authenticated user")]
    public async Task<IActionResult> GetSharedTodos([FromQuery] TodoQueryDto query)
    {
        return Ok(await _todoService.GetSharedTodosAsync(GetCurrentUserId(), query));
    }

    [HttpGet("{id:int}/shares")]
    [HasPermission(PermissionCodes.TodosShare)]
    [EndpointSummary("Get recipients for an owned Todo")]
    public async Task<IActionResult> GetTodoShares(int id)
    {
        return Ok(await _todoService.GetTodoSharesAsync(id, GetCurrentUserId()));
    }

    [HttpPut("{id:int}/shares")]
    [HasPermission(PermissionCodes.TodosShare)]
    [EndpointSummary("Replace recipients for an owned Todo")]
    public async Task<IActionResult> ReplaceTodoShares(int id, [FromBody] ReplaceTodoSharesDto dto,
        CancellationToken cancellationToken)
    {
        await _todoService.SetTodoShareRecipientsAsync(id, dto, GetCurrentUserId(), cancellationToken);
        return Ok(new { message = "Todo shares updated successfully" });
    }

    [HttpGet("{id:int}/notes")]
    [EndpointSummary("Get notes for an accessible Todo")]
    public async Task<IActionResult> GetTodoNotes(int id)
    {
        var canReadOwned = await HasPermissionAsync(PermissionCodes.TodoRead);
        var canReadShared = await HasPermissionAsync(PermissionCodes.TodosReadShared);
        if (!canReadOwned && !canReadShared)
            throw new BE_ZSM.Exceptions.ForbiddenException("A Todo read permission is required", "TODO_READ_PERMISSION_REQUIRED");

        return Ok(await _todoService.GetTodoNotesAsync(id, GetCurrentUserId(), canReadOwned, canReadShared));
    }

    [HttpPost("{id:int}/notes")]
    [EndpointSummary("Add a note to an accessible Todo")]
    public async Task<IActionResult> CreateTodoNote(int id, [FromBody] CreateTodoNoteDto dto)
    {
        if (!await HasPermissionAsync(PermissionCodes.TodosNotesCreate))
            throw new BE_ZSM.Exceptions.ForbiddenException("The Todo note create permission is required", "TODO_NOTE_CREATE_PERMISSION_REQUIRED");

        var canReadOwned = await HasPermissionAsync(PermissionCodes.TodoRead);
        var canReadShared = await HasPermissionAsync(PermissionCodes.TodosReadShared);
        if (!canReadOwned && !canReadShared)
            throw new BE_ZSM.Exceptions.ForbiddenException("A Todo read permission is required", "TODO_READ_PERMISSION_REQUIRED");

        var note = await _todoService.CreateTodoNoteAsync(id, dto, GetCurrentUserId(), canReadOwned, canReadShared);
        return Ok(note);
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

    private async Task<bool> HasPermissionAsync(string permissionCode)
    {
        var result = await _authorizationService.AuthorizeAsync(User,
            $"{HasPermissionAttribute.PolicyPrefix}{permissionCode}");
        return result.Succeeded;
    }
}
