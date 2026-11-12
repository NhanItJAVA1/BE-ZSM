using BE_ZSM.DTOs.Todos;
using BE_ZSM.Responses;

namespace BE_ZSM.Services.TodoService;

public interface ITodoService
{
    Task<PagedResult<TodoDto>> GetTodosAsync(int userId, TodoQueryDto queryDto);
    Task<PagedResult<TodoDto>> GetTodosForUserAsync(int userId, TodoQueryDto queryDto);
    Task<PagedResult<TodoDto>> GetAllTodosAsync(TodoQueryDto queryDto);
    Task<PagedResult<TodoUserSummaryDto>> GetUserTodoSummaryAsync(string? search, int page, int pageSize);
    Task SaveTodosAsync(List<SaveTodoDto> dtos, int userId);
}
