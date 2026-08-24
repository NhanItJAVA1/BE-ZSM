using BE_ZSM.DTOs.Todos;

namespace BE_ZSM.Services.TodoService
{
    public interface ITodoService
    {
        Task<TodoDto> GetTodoAsync(int id, int userId);
        Task<List<TodoDto>> GetTodosAsync(int userId);
        Task CreateTodoAsync(CreateTodoDto dto,int userId);
        Task UpdateTodoAsync(int id, UpdateTodoDto dto, int userId);
        Task DeleteTodoAsync(int id, int userId);
    }
}
