using AutoMapper;
using BE_ZSM.DTOs.Todos;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using BE_ZSM.Exceptions;
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
        private readonly IGenericRepository<TodoActivity> _activityRepo;
        public TodoService(IMapper mapper, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _todoRepo = _unitOfWork.GetRepository<Todo>();
            _categoryRepo = _unitOfWork.GetRepository<TodoCategory>();
            _activityRepo = _unitOfWork.GetRepository<TodoActivity>();
        }

        public async Task<TodoDto> GetTodoAsync(int id, int userId)
        {
            var todo = await _todoRepo.FindAsync(t => t.Id == id && t.UserId == userId);
            if (todo == null) throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

            return _mapper.Map<TodoDto>(todo);
        }

        public async Task<PagedResult<TodoDto>> GetTodosAsync(int userId, TodoQueryDto queryDto)
        {
            var query = _todoRepo
                        .All()
                        .AsNoTracking()
                        .Include(t => t.Category)
                        .Where(t => t.UserId == userId);

            if (!string.IsNullOrWhiteSpace(queryDto.Search))
            {
                var search = queryDto.Search.Trim();

                query = query.Where(t =>
                    t.Title.Contains(search) ||
                    (t.Description != null &&
                     t.Description.Contains(search)));
            }

            if (queryDto.Status.HasValue) query = query.Where(t => t.Status == queryDto.Status.Value);
            if (queryDto.Priority.HasValue) query = query.Where(t => t.Priority == queryDto.Priority.Value);
            if (queryDto.CategoryId.HasValue) query = query.Where(t => t.CategoryId == queryDto.CategoryId);

            var now = DateTime.UtcNow;

            if (queryDto.IsOverdue.HasValue)
            {
                query = queryDto.IsOverdue.Value
                    ? query.Where(t =>
                        t.DueDate.HasValue &&
                        t.DueDate.Value < now &&
                        t.Status != TodoStatus.Done)
                    : query.Where(t =>
                        !t.DueDate.HasValue ||
                        t.DueDate.Value >= now ||
                        t.Status == TodoStatus.Done);
            }

            var totalItems = await query.CountAsync();

            query = queryDto.SortBy?.ToLower() switch
            {
                "title" => queryDto.IsDescending
                    ? query.OrderByDescending(t => t.Title)
                    : query.OrderBy(t => t.Title),

                "priority" => queryDto.IsDescending
                    ? query.OrderByDescending(t => t.Priority)
                    : query.OrderBy(t => t.Priority),

                "status" => queryDto.IsDescending
                    ? query.OrderByDescending(t => t.Status)
                    : query.OrderBy(t => t.Status),

                "duedate" => queryDto.IsDescending
                    ? query.OrderByDescending(t => t.DueDate)
                    : query.OrderBy(t => t.DueDate),

                "createdat" => queryDto.IsDescending
                    ? query.OrderByDescending(t => t.CreatedAt)
                    : query.OrderBy(t => t.CreatedAt),

                _ => query.OrderByDescending(t => t.CreatedAt)
            };

            var page = queryDto.Page < 1
                ? 1
                : queryDto.Page;

            var pageSize = queryDto.PageSize < 1
                ? 10
                : queryDto.PageSize;

            if (pageSize > 100) pageSize = 100;

            var todos = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = _mapper.Map<List<TodoDto>>(todos);

            return new PagedResult<TodoDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(
                    totalItems / (double)pageSize)
            };
        }

        public async Task CreateTodoAsync(CreateTodoDto dto, int userId)
        {
            if (dto.CategoryId.HasValue)
            {
                var category = await _categoryRepo.FindAsync(c => c.Id == dto.CategoryId && c.UserId == userId);

                if (category == null)
                    throw new NotFoundException("Category not found", "CATEGORY_NOT_FOUND");
            }

            var todo = _mapper.Map<Todo>(dto);
            todo.UserId = userId;
            todo.CreatedAt = DateTime.UtcNow;

            await _todoRepo.CreateAsync(todo);
            await _unitOfWork.SaveChangesAsync();

            await AddActivityAsync(todo.Id, TodoActivityType.Created, "Todo created");
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteTodoAsync(int id, int userId)
        {
            var todo = await _todoRepo.FindAsync(t => t.Id == id && t.UserId == userId);

            if (todo == null) throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

            await _todoRepo.DeleteAsync(todo);
            await _unitOfWork.SaveChangesAsync();
        }        

        public async Task UpdateTodoAsync(int id, UpdateTodoDto dto, int userId)
        {
            var todo = await _todoRepo.FindAsync(t => t.Id == id && t.UserId == userId);

            if (todo == null) throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

            if (dto.CategoryId.HasValue)
            {
                var category = await _categoryRepo.FindAsync(c => c.Id == dto.CategoryId && c.UserId == userId);

                if (category == null)
                    throw new NotFoundException("Category not found", "CATEGORY_NOT_FOUND");
            }

            var oldPriority = todo.Priority;
            var oldCategoryId = todo.CategoryId;

            _mapper.Map(dto, todo);
            todo.UpdatedAt = DateTime.UtcNow;

            if (oldPriority != todo.Priority)
                await AddActivityAsync(todo.Id, TodoActivityType.Updated, $"Priority changed from {oldPriority} to {todo.Priority}");

            if (oldCategoryId != todo.CategoryId)
                await AddActivityAsync(todo.Id, TodoActivityType.CategoryChanged, "Category changed");

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task UpdateTodoStatusAsync(int id, int userId, UpdateTodoStatusDto dto)
        {
            var todo = await _todoRepo.FindAsync(t => t.Id == id && t.UserId == userId);

            if (todo == null)
                throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

            if (!IsValidStatusTransition(todo.Status, dto.Status))
                throw new ConflictException($"Cannot change status from {todo.Status} to {dto.Status}", "INVALID_TODO_STATUS_TRANSITION");

            var oldStatus = todo.Status;
            var now = DateTime.UtcNow;

            todo.Status = dto.Status;
            todo.UpdatedAt = now;
            todo.CompletedAt = dto.Status == TodoStatus.Done ? now : null;

            await AddActivityAsync(todo.Id, TodoActivityType.StatusChanged, $"Status changed from {oldStatus} to {dto.Status}");

            await _unitOfWork.SaveChangesAsync();
        }

        private static bool IsValidStatusTransition(TodoStatus currentStatus,TodoStatus newStatus)
        {
            if (currentStatus == newStatus)
                return true;

            return currentStatus switch
            {
                TodoStatus.Todo => newStatus == TodoStatus.InProgress ||  newStatus == TodoStatus.Done,
                TodoStatus.InProgress => newStatus == TodoStatus.Todo || newStatus == TodoStatus.Done,
                TodoStatus.Done => newStatus == TodoStatus.InProgress, _ => false
            };
        }

        private async Task AddActivityAsync(int todoId, TodoActivityType type, string description)
        {
            await _activityRepo.CreateAsync(new TodoActivity
            {
                TodoId = todoId,
                Type = type,
                Description = description,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task<List<TodoActivityDto>> GetActivitiesAsync(int id, int userId)
        {
            var todo = await _todoRepo.FindAsync(t => t.Id == id && t.UserId == userId);

            if (todo == null)
                throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

            var activities = await _activityRepo.All()
                .AsNoTracking()
                .Where(a => a.TodoId == id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return _mapper.Map<List<TodoActivityDto>>(activities);
        }
    }
}
