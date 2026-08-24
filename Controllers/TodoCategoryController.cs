using BE_ZSM.DTOs.Todos.Categories;
using BE_ZSM.Services.Category;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BE_ZSM.Controllers;

[ApiController]
[Route("api/todo-categories")]
[Authorize]
public class TodoCategoryController : ControllerBase
{
    private readonly ITodoCategoryService _categoryService;

    public TodoCategoryController(ITodoCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _categoryService.GetCategoriesAsync(GetCurrentUserId());
        return Ok(categories);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCategory(int id)
    {
        var category = await _categoryService.GetCategoryAsync(id, GetCurrentUserId());
        return Ok(category);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory(CreateTodoCategoryDto dto)
    {
        await _categoryService.CreateCategoryAsync(dto, GetCurrentUserId());
        return Ok(new { message = "Category created successfully" });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, UpdateTodoCategoryDto dto)
    {
        await _categoryService.UpdateCategoryAsync(id, dto, GetCurrentUserId());
        return Ok(new { message = "Category updated successfully" });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, [FromQuery] bool deleteTodos = false)
    {
        await _categoryService.DeleteCategoryAsync(id, GetCurrentUserId(), deleteTodos);
        return Ok(new { message = "Category deleted successfully" });
    }

    private int GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
            throw new UnauthorizedAccessException("User ID not found");

        return int.Parse(userId);
    }
}