using BE_ZSM.DTOs.Todos.Categories;

namespace BE_ZSM.Services.Category
{
    public interface ITodoCategoryService
    {
        Task<List<TodoCategoryDto>> GetCategoriesAsync(int userId);
        Task<TodoCategoryDto> GetCategoryAsync(int id, int userId);
        Task CreateCategoryAsync(CreateTodoCategoryDto dto, int userId);
        Task UpdateCategoryAsync(int id, UpdateTodoCategoryDto dto, int userId);
        Task DeleteCategoryAsync(int id, int userId, bool deleteTodos);
    }
}
