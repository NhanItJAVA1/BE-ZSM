using BE_ZSM.DTOs.Todos;
using BE_ZSM.Responses;

namespace BE_ZSM.Services.TodoService
{
    public interface ITodoService
    {
        Task<TodoDto> GetTodoAsync(int id, int userId);
        Task<PagedResult<TodoDto>> GetTodosAsync(int userId, TodoQueryDto queryDto);
        Task CreateTodoAsync(TodoRequestDto dto,int userId);
        Task UpdateTodoAsync(int id, TodoRequestDto dto, int userId);
        Task DeleteTodoAsync(int id, int userId);
        Task UpdateTodoStatusAsync(int id, int userId, UpdateTodoStatusDto dto);
        Task<List<TodoActivityDto>> GetActivitiesAsync(int id, int userId);
        Task CreateTodosAsync(List<TodoRequestDto> dtos, int userId);
    }
}
