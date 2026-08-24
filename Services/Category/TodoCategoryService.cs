using AutoMapper;
using BE_ZSM.DTOs.Todos.Categories;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using Microsoft.EntityFrameworkCore;
namespace BE_ZSM.Services.Category
{
    public class TodoCategoryService : ITodoCategoryService
    {
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<TodoCategory> _categoryRepo;
        private readonly IGenericRepository<Todo> _todoRepo;

        public TodoCategoryService(
            IMapper mapper,
            IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;

            _categoryRepo = _unitOfWork.GetRepository<TodoCategory>();

            _todoRepo = _unitOfWork.GetRepository<Todo>();
        }

        public async Task<List<TodoCategoryDto>> GetCategoriesAsync(int userId)
        {
            var categories = await _categoryRepo
                .All()
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .ToListAsync();

            return _mapper.Map<List<TodoCategoryDto>>(categories);
        }

        public async Task<TodoCategoryDto> GetCategoryAsync(
            int id,
            int userId)
        {
            var category = await _categoryRepo.FindAsync(
                c => c.Id == id && c.UserId == userId);

            if (category == null)
                throw new NotFoundException(
                    "Category not found",
                    "CATEGORY_NOT_FOUND");

            return _mapper.Map<TodoCategoryDto>(category);
        }

        public async Task CreateCategoryAsync(
            CreateTodoCategoryDto dto,
            int userId)
        {
            var category = _mapper.Map<TodoCategory>(dto);

            category.UserId = userId;

            await _categoryRepo.CreateAsync(category);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task UpdateCategoryAsync(
            int id,
            UpdateTodoCategoryDto dto,
            int userId)
        {
            var category = await _categoryRepo.FindAsync(
                c => c.Id == id && c.UserId == userId);

            if (category == null)
                throw new NotFoundException(
                    "Category not found",
                    "CATEGORY_NOT_FOUND");

            _mapper.Map(dto, category);

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteCategoryAsync(int id, int userId, bool deleteTodos)
        {
            var category = await _categoryRepo.FindAsync(c => c.Id == id && c.UserId == userId);

            if (category == null)
                throw new NotFoundException("Category not found", "CATEGORY_NOT_FOUND");

            var todos = await _todoRepo
                .All()
                .Where(t => t.CategoryId == id &&  t.UserId == userId)
                .ToListAsync();

            if (deleteTodos)
                foreach (var todo in todos)
                    await _todoRepo.DeleteAsync(todo);
            else
                foreach (var todo in todos)
                    todo.CategoryId = null;

            await _categoryRepo.DeleteAsync(category);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}

