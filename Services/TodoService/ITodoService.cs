using BE_ZSM.DTOs.Todos;
using BE_ZSM.Responses;

namespace BE_ZSM.Services.TodoService;

public interface ITodoService
{
    Task<PagedResult<TodoDto>> GetTodosAsync(int userId, TodoQueryDto queryDto);
    Task<PagedResult<TodoDto>> GetTodosForUserAsync(int userId, TodoQueryDto queryDto);
    Task<PagedResult<TodoDto>> GetAllTodosAsync(TodoQueryDto queryDto);
    Task<PagedResult<TodoDto>> GetSharedTodosAsync(int userId, TodoQueryDto queryDto);
    Task<PagedResult<TodoUserSummaryDto>> GetUserTodoSummaryAsync(string? search, int page, int pageSize);
    Task SaveTodosAsync(List<SaveTodoDto> dtos, int userId);
    Task<IReadOnlyList<TodoShareDto>> GetTodoSharesAsync(int todoId, int ownerId);
    Task ReplaceTodoSharesAsync(int todoId, ReplaceTodoSharesDto dto, int ownerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TodoNoteDto>> GetTodoNotesAsync(int todoId, int userId, bool canReadOwned, bool canReadShared);
    Task<TodoNoteDto> CreateTodoNoteAsync(int todoId, CreateTodoNoteDto dto, int userId, bool canReadOwned, bool canReadShared);
}
