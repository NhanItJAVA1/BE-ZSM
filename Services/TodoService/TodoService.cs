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
        private readonly IGenericRepository<TodoActivity> _activityRepo;
        public TodoService(IMapper mapper, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _todoRepo = _unitOfWork.GetRepository<Todo>();
            _categoryRepo = _unitOfWork.GetRepository<TodoCategory>();
            _activityRepo = _unitOfWork.GetRepository<TodoActivity>();
        }

        // GET ALL/FILTER/SORT/SEARCH TODOS
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
                    t.Title.Contains(search) || (t.Description != null && t.Description.Contains(search)));
            }

            if (queryDto.Status.HasValue) query = query.Where(t => t.Status == queryDto.Status.Value);
            if (queryDto.Priority.HasValue) query = query.Where(t => t.Priority == queryDto.Priority.Value);
            if (queryDto.CategoryId.HasValue) query = query.Where(t => t.CategoryId == queryDto.CategoryId);

            var now = DateTime.UtcNow;

            if (queryDto.IsOverdue.HasValue)
            {
                query = queryDto.IsOverdue.Value
                    ? query.Where(t => t.DueDate.HasValue && t.DueDate.Value < now && t.Status != TodoStatus.Done)
                    : query.Where(t => !t.DueDate.HasValue || t.DueDate.Value >= now || t.Status == TodoStatus.Done);
            }
            query = queryDto.SortBy?.ToLower() switch
            {
                "title" => queryDto.IsDescending ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
                "priority" => queryDto.IsDescending ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
                "status" => queryDto.IsDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                "duedate" => queryDto.IsDescending ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate),
                "createdat" => queryDto.IsDescending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
                _ => query.OrderByDescending(t => t.CreatedAt)
            };

            return await query.ToPagedResultAsync<Todo, TodoDto>(queryDto.Page, queryDto.PageSize, _mapper);
        }

        // SAVE TODOS (CREATE, UPDATE, DELETE)
        public async Task SaveTodosAsync(List<SaveTodoDto> dtos, int userId)
        {
            if (dtos.Count == 0) return;

            await ValidateCategoriesAsync(dtos, userId);

            var todoMap = await GetExistingTodosAsync(dtos, userId);

            var context = ProcessTodos(dtos, todoMap, userId);

            await PersistChangesAsync(context);
        }

        private async Task ValidateCategoriesAsync(List<SaveTodoDto> dtos, int userId)
        {
            var categoryIds = dtos
                .Where(x => !x.IsDeleted && x.CategoryId.HasValue)
                .Select(x => x.CategoryId!.Value)
                .Distinct()
                .ToList();

            if (categoryIds.Count == 0)
                return;

            var validCategoryCount = await _categoryRepo.All()
                .AsNoTracking()
                .CountAsync(category =>
                    category.UserId == userId &&
                    categoryIds.Contains(category.Id));

            if (validCategoryCount != categoryIds.Count)
            {
                throw new NotFoundException(
                    "One or more categories not found",
                    "CATEGORY_NOT_FOUND");
            }
        }

        private async Task<Dictionary<int, Todo>> GetExistingTodosAsync(List<SaveTodoDto> dtos, int userId)
        {
            var todoIds = dtos
                .Where(x => x.Id.HasValue)
                .Select(x => x.Id!.Value)
                .Distinct()
                .ToList();

            if (todoIds.Count == 0) return new Dictionary<int, Todo>();

            var existingTodos = await _todoRepo
                .Where(todo => todo.UserId == userId && todoIds.Contains(todo.Id))
                .ToListAsync();

            if (existingTodos.Count != todoIds.Count)            
                throw new NotFoundException("One or more todos not found", "TODO_NOT_FOUND");           

            return existingTodos.ToDictionary(x => x.Id);
        }

        private TodoSaveContext ProcessTodos(List<SaveTodoDto> dtos, Dictionary<int, Todo> todoMap, int userId)
        {
            var context = new TodoSaveContext();
            var now = DateTime.UtcNow;

            foreach (var dto in dtos)
            {
                if (!dto.Id.HasValue)
                {
                    ProcessCreate(dto, userId, now, context);
                    continue;
                }

                var existingTodo = todoMap[dto.Id.Value];
                SetConcurrencyVersion(dto, existingTodo);

                if (dto.IsDeleted)
                {
                    ProcessDelete(existingTodo, context);
                    continue;
                }

                ProcessUpdate(dto, existingTodo, now, context);
            }

            return context;
        }

        private void ProcessCreate(SaveTodoDto dto, int userId, DateTime now, TodoSaveContext context)
        {
            if (dto.IsDeleted) return;

            var todo = _mapper.Map<Todo>(dto);

            todo.UserId = userId;
            todo.CreatedAt = now;
            todo.Priority = dto.Priority ?? TodoPriority.Medium;

            context.NewTodos.Add(todo);

            context.Activities.Add(new TodoActivity
            {
                Todo = todo,
                Type = TodoActivityType.Created,
                Description = "Todo created",
                CreatedAt = now
            });
        }

        private void SetConcurrencyVersion(SaveTodoDto dto,  Todo existingTodo)
        {
            if (dto.RowVersion == null)
            {
                throw new ConflictException(
                    "RowVersion is required",
                    "ROW_VERSION_REQUIRED");
            }

            _todoRepo.SetOriginalValue(
                existingTodo,
                todo => todo.RowVersion,
                dto.RowVersion);
        }

        private static void ProcessDelete(Todo todo, TodoSaveContext context)
        {
            context.DeletedTodos.Add(todo);
        }

        private void ProcessUpdate(SaveTodoDto dto, Todo existingTodo, DateTime now, TodoSaveContext context)
        {
            var oldPriority = existingTodo.Priority;
            var oldCategoryId = existingTodo.CategoryId;

            _mapper.Map(dto, existingTodo);

            existingTodo.Priority = dto.Priority ?? oldPriority;

            existingTodo.UpdatedAt = now;

            AddUpdateActivities(
                existingTodo,
                oldPriority,
                oldCategoryId,
                now,
                context);
        }

        private static void AddUpdateActivities(Todo todo, TodoPriority oldPriority, int? oldCategoryId, DateTime now, TodoSaveContext context)
        {
            if (oldPriority != todo.Priority)
            {
                context.Activities.Add(new TodoActivity
                {
                    TodoId = todo.Id,
                    Type = TodoActivityType.Updated,
                    Description =
                        $"Priority changed from {oldPriority} to {todo.Priority}",
                    CreatedAt = now
                });
            }

            if (oldCategoryId != todo.CategoryId)
            {
                context.Activities.Add(new TodoActivity
                {
                    TodoId = todo.Id,
                    Type = TodoActivityType.CategoryChanged,
                    Description = "Category changed",
                    CreatedAt = now
                });
            }
        }

        private async Task PersistChangesAsync(TodoSaveContext context)
        {
            if (context.NewTodos.Count > 0)
                await _todoRepo.CreateRangeAsync(context.NewTodos);

            if (context.DeletedTodos.Count > 0)
                 _todoRepo.DeleteRangeAsync(context.DeletedTodos);

            if (context.Activities.Count > 0)
                await _activityRepo.CreateRangeAsync(context.Activities);

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(
                    "One or more todos were modified or deleted by another request",
                    "TODO_CONCURRENCY_CONFLICT");
            }
        }

        private sealed class TodoSaveContext
        {
            public List<Todo> NewTodos { get; } = new();
            public List<Todo> DeletedTodos { get; } = new();
            public List<TodoActivity> Activities { get; } = new();
        }

    }
}
