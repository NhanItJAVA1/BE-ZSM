using AutoMapper;
using BE_ZSM.DTOs.Todos;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using BE_ZSM.Exceptions;
using BE_ZSM.Extensions;
using BE_ZSM.Repositories.Generic;
using BE_ZSM.Responses;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Services.TodoService
{
    public class TodoService : ITodoService
    {
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<Todo> _todoRepo;
        private readonly IGenericRepository<TodoCategory> _categoryRepo;
        private readonly IGenericRepository<User> _userRepo;
        public TodoService(IMapper mapper, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _todoRepo = _unitOfWork.GetRepository<Todo>();
            _categoryRepo = _unitOfWork.GetRepository<TodoCategory>();
            _userRepo = _unitOfWork.GetRepository<User>();
        }

        // GET OWN TODOS/FILTER/SORT/SEARCH TODOS
        public async Task<PagedResult<TodoDto>> GetTodosAsync(int userId, TodoQueryDto queryDto)
        {
            return await GetTodosAsync(queryDto, userId);
        }

        public async Task<PagedResult<TodoDto>> GetTodosForUserAsync(int userId, TodoQueryDto queryDto)
        {
            if (!await _userRepo.All().AsNoTracking().AnyAsync(user => user.Id == userId))
                throw new NotFoundException("User not found", "USER_NOT_FOUND");

            return await GetTodosAsync(queryDto, userId);
        }

        public async Task<PagedResult<TodoDto>> GetAllTodosAsync(TodoQueryDto queryDto)
        {
            return await GetTodosAsync(queryDto, null);
        }

        public async Task<PagedResult<TodoUserSummaryDto>> GetUserTodoSummaryAsync(string? search, int page, int pageSize)
        {
            var users = _userRepo.All().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                users = users.Where(user => user.DisplayName.Contains(keyword) || user.Email.Contains(keyword));
            }

            var summaries = users
                .OrderBy(user => user.DisplayName)
                .ThenBy(user => user.Id)
                .Select(user => new TodoUserSummaryDto
                {
                    UserId = user.Id,
                    DisplayName = user.DisplayName,
                    Email = user.Email,
                    TodoCount = user.Todos.Count()
                });

            return await summaries.ToPagedResultAsync(page, pageSize);
        }

        private async Task<PagedResult<TodoDto>> GetTodosAsync(TodoQueryDto queryDto, int? userId)
        {
            var query = _todoRepo.All().AsNoTracking().Include(todo => todo.Category).AsQueryable();
            if (userId.HasValue)
                query = query.Where(todo => todo.UserId == userId.Value);

            if (!string.IsNullOrWhiteSpace(queryDto.Search))
            {
                var search = queryDto.Search.Trim();
                query = query.Where(todo => todo.Title.Contains(search) || (todo.Description != null && todo.Description.Contains(search)));
            }

            if (queryDto.Status.HasValue) query = query.Where(todo => todo.Status == queryDto.Status.Value);
            if (queryDto.Priority.HasValue) query = query.Where(todo => todo.Priority == queryDto.Priority.Value);
            if (queryDto.CategoryId.HasValue) query = query.Where(todo => todo.CategoryId == queryDto.CategoryId);

            var now = DateTime.UtcNow;

            if (queryDto.IsOverdue.HasValue)
            {
                query = queryDto.IsOverdue.Value
                    ? query.Where(todo => todo.DueDate.HasValue && todo.DueDate.Value < now && todo.Status != TodoStatus.Done)
                    : query.Where(todo => !todo.DueDate.HasValue || todo.DueDate.Value >= now || todo.Status == TodoStatus.Done);
            }
            var orderedQuery = queryDto.SortBy?.ToLower() switch
            {
                "title" => queryDto.IsDescending ? query.OrderByDescending(todo => todo.Title) : query.OrderBy(todo => todo.Title),
                "priority" => queryDto.IsDescending ? query.OrderByDescending(todo => todo.Priority) : query.OrderBy(todo => todo.Priority),
                "status" => queryDto.IsDescending ? query.OrderByDescending(todo => todo.Status) : query.OrderBy(todo => todo.Status),
                "duedate" => queryDto.IsDescending ? query.OrderByDescending(todo => todo.DueDate) : query.OrderBy(todo => todo.DueDate),
                "createdat" => queryDto.IsDescending ? query.OrderByDescending(todo => todo.CreatedAt) : query.OrderBy(todo => todo.CreatedAt),
                _ => query.OrderByDescending(todo => todo.CreatedAt)
            };
            return await orderedQuery.ThenBy(todo => todo.Id)
                .ToPagedResultAsync<Todo, TodoDto>(queryDto.Page, queryDto.PageSize, _mapper);
        }

        // SAVE BATCH OF TODOS (CREATE, UPDATE, DELETE)
        public async Task SaveTodosAsync(List<SaveTodoDto> dtos, int userId)
        {
            // Validate category ownership
            var categoryIds = dtos.Where(x => !x.IsDeleted && x.CategoryId.HasValue)
                .Select(x => x.CategoryId!.Value)
                .Distinct()
                .ToList();

            if (categoryIds.Count > 0 && await _categoryRepo.Where(x => x.UserId == userId && categoryIds.Contains(x.Id)).CountAsync() != categoryIds.Count)
                throw new NotFoundException("One or more categories not found", "CATEGORY_NOT_FOUND");

            // Validate todo ownership
            var todoIds = dtos.Where(x => x.Id.HasValue)
                .Select(x => x.Id!.Value)
                .Distinct()
                .ToList();

            if (todoIds.Count > 0 && await _todoRepo.Where(x => x.UserId == userId && todoIds.Contains(x.Id)).CountAsync() != todoIds.Count)
                throw new NotFoundException("One or more todos not found", "TODO_NOT_FOUND");

            var creates = _mapper.Map<List<Todo>>(dtos.Where(x => !x.Id.HasValue && !x.IsDeleted), opt => opt.Items["UserId"] = userId);
            await _todoRepo.CreateRangeAsync(creates);

            var updates = _mapper.Map<List<Todo>>(dtos.Where(x => x.Id.HasValue && !x.IsDeleted), opt => opt.Items["UserId"] = userId);
            _todoRepo.UpdateRange(updates);

            var deletedIds = dtos.Where(x => x.Id.HasValue && x.IsDeleted).Select(x => x.Id!.Value).ToList();
            await _todoRepo.DeleteAsync(x => x.UserId == userId && deletedIds.Contains(x.Id));

            await _unitOfWork.SaveChangesAsync();
        }
    }
}
