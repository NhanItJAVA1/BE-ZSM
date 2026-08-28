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

        //public async Task<TodoDto> GetTodoAsync(int id, int userId)
        //{
        //    var todo = await _todoRepo.FindAsync(t => t.Id == id && t.UserId == userId);
        //    if (todo == null) throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

        //    return _mapper.Map<TodoDto>(todo);
        //}


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

        // DELETE TODOS
        public async Task DeleteTodosAsync(List<int> ids, int userId)
        {
            var distinctIds = ids.Distinct().ToList();

            if (distinctIds.Count == 0) return;

            var todos = await _todoRepo.All()
                .Where(t => distinctIds.Contains(t.Id) && t.UserId == userId)
                .ToListAsync();

            if (todos.Count != distinctIds.Count)
                throw new NotFoundException("One or more todos not found", "TODO_NOT_FOUND");

            _todoRepo.DeleteRangeAsync(todos);
            await _unitOfWork.SaveChangesAsync();
        }

        // SAVE TODOS (CREATE, UPDATE, DELETE)
        public async Task SaveTodosAsync(List<SaveTodoDto> dtos, int userId)
        {
            if (dtos.Count == 0) return;

            var categoryIds = dtos
                .Where(x => !x.IsDeleted && x.CategoryId.HasValue)
                .Select(x => x.CategoryId!.Value)
                .Distinct()
                .ToList();

            if (categoryIds.Count > 0)
            {
                var validCategoryCount = await _categoryRepo.All()
                    .AsNoTracking()
                    .CountAsync(c => c.UserId == userId && categoryIds.Contains(c.Id));

                if (validCategoryCount != categoryIds.Count)
                    throw new NotFoundException("One or more categories not found", "CATEGORY_NOT_FOUND");
            }

            var ids = dtos
                .Where(x => x.Id.HasValue)
                .Select(x => x.Id!.Value)
                .Distinct()
                .ToList();

            var existingTodos = await _todoRepo.All()
                .Where(t => t.UserId == userId && ids.Contains(t.Id))
                .ToListAsync();

            if (existingTodos.Count != ids.Count)
                throw new NotFoundException("One or more todos not found", "TODO_NOT_FOUND");

            var todoMap = existingTodos.ToDictionary(t => t.Id);
            var newTodos = new List<Todo>();
            var deleteTodos = new List<Todo>();
            var activities = new List<TodoActivity>();
            var now = DateTime.UtcNow;

            foreach (var dto in dtos)
            {
                if (!dto.Id.HasValue)
                {
                    if (dto.IsDeleted) continue;

                    var todo = _mapper.Map<Todo>(dto);
                    todo.UserId = userId;
                    todo.CreatedAt = now;
                    todo.Priority = dto.Priority ?? TodoPriority.Medium;

                    newTodos.Add(todo);

                    activities.Add(new TodoActivity
                    {
                        Todo = todo,
                        Type = TodoActivityType.Created,
                        Description = "Todo created",
                        CreatedAt = now
                    });

                    continue;
                }

                var existing = todoMap[dto.Id.Value];

                if (dto.IsDeleted)
                {
                    deleteTodos.Add(existing);
                    continue;
                }

                var oldPriority = existing.Priority;
                var oldCategoryId = existing.CategoryId;

                _mapper.Map(dto, existing);
                existing.Priority = dto.Priority ?? existing.Priority;
                existing.UpdatedAt = now;

                if (oldPriority != existing.Priority)
                    activities.Add(new TodoActivity
                    {
                        TodoId = existing.Id,
                        Type = TodoActivityType.Updated,
                        Description = $"Priority changed from {oldPriority} to {existing.Priority}",
                        CreatedAt = now
                    });

                if (oldCategoryId != existing.CategoryId)
                    activities.Add(new TodoActivity
                    {
                        TodoId = existing.Id,
                        Type = TodoActivityType.CategoryChanged,
                        Description = "Category changed",
                        CreatedAt = now
                    });
            }

            if (newTodos.Count > 0)
                await _todoRepo.CreateRangeAsync(newTodos);

            if (deleteTodos.Count > 0)
                _todoRepo.DeleteRangeAsync(deleteTodos);

            if (activities.Count > 0)
                await _activityRepo.CreateRangeAsync(activities);

            await _unitOfWork.SaveChangesAsync();
        }
        //public async Task CreateTodoAsync(TodoRequestDto dto, int userId)
        //{
        //    await ValidateCategoryAsync(dto.CategoryId, userId);

        //    var todo = _mapper.Map<Todo>(dto);
        //    todo.UserId = userId;
        //    todo.CreatedAt = DateTime.UtcNow;
        //    todo.Priority = dto.Priority ?? TodoPriority.Medium;

        //    await _todoRepo.CreateAsync(todo);
        //    await _unitOfWork.SaveChangesAsync();

        //    await AddActivityAsync(todo.Id, TodoActivityType.Created, "Todo created");
        //    await _unitOfWork.SaveChangesAsync();
        //}

        //public async Task CreateTodosAsync(List<TodoRequestDto> dtos, int userId)
        //{
        //    var categoryIds = dtos
        //        .Where(x => x.CategoryId.HasValue)
        //        .Select(x => x.CategoryId!.Value)
        //        .Distinct()
        //        .ToList();

        //    if (categoryIds.Count > 0)
        //    {
        //        var validCategoryIds = await _categoryRepo
        //            .All()
        //            .AsNoTracking()
        //            .Where(c => c.UserId == userId && categoryIds.Contains(c.Id))
        //            .Select(c => c.Id)
        //            .ToListAsync();

        //        if (validCategoryIds.Count != categoryIds.Count)
        //            throw new NotFoundException("One or more categories not found", "CATEGORY_NOT_FOUND");
        //    }

        //    var todos = _mapper.Map<List<Todo>>(dtos);
        //    var now = DateTime.UtcNow;

        //    for (var i = 0; i < todos.Count; i++)
        //    {
        //        todos[i].UserId = userId;
        //        todos[i].CreatedAt = now;
        //        todos[i].Priority = dtos[i].Priority ?? TodoPriority.Medium;
        //    }


        //    //await _todoRepo.CreateRangeAsync(todos);
        //    //await _unitOfWork.SaveChangesAsync();

        //    //var activities = todos.Select(todo => new TodoActivity
        //    //{
        //    //    TodoId = todo.Id,
        //    //    Type = TodoActivityType.Created,
        //    //    Description = "Todo created",
        //    //    CreatedAt = now
        //    //}).ToList();

        //    //await _activityRepo.CreateRangeAsync(activities);
        //    //await _unitOfWork.SaveChangesAsync();


        //    await _todoRepo.CreateRangeAsync(todos);
        //    foreach (var todo in todos)
        //        await AddActivityAsync(todo.Id, TodoActivityType.Created, "Todo created");

        //    await _unitOfWork.SaveChangesAsync();
        //}

        //public async Task DeleteTodoAsync(int id, int userId)
        //{
        //    var todo = await _todoRepo.FindAsync(t => t.Id == id);

        //    if (todo == null) throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

        //    if (todo.UserId != userId)
        //        throw new ForbiddenException("You cannot delete this todo", "TODO_FORBIDDEN");

        //    await _todoRepo.DeleteAsync(todo);
        //    await _unitOfWork.SaveChangesAsync();
        //}

        //public async Task UpdateTodoAsync(int id, TodoRequestDto dto, int userId)
        //{
        //    var todo = await _todoRepo.FindAsync(t => t.Id == id);

        //    if (todo == null)
        //        throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

        //    if (todo.UserId != userId)
        //        throw new ForbiddenException("You cannot update this todo", "TODO_FORBIDDEN");

        //    if (!dto.Priority.HasValue)
        //        throw new ConflictException("Priority is required", "PRIORITY_REQUIRED");

        //    await ValidateCategoryAsync(dto.CategoryId, userId);

        //    var oldPriority = todo.Priority;
        //    var oldCategoryId = todo.CategoryId;

        //    _mapper.Map(dto, todo);
        //    todo.Priority = dto.Priority.Value;
        //    todo.UpdatedAt = DateTime.UtcNow;

        //    if (oldPriority != todo.Priority)
        //        await AddActivityAsync(todo.Id, TodoActivityType.Updated, $"Priority changed from {oldPriority} to {todo.Priority}");

        //    if (oldCategoryId != todo.CategoryId)
        //        await AddActivityAsync(todo.Id, TodoActivityType.CategoryChanged, "Category changed");

        //    await _unitOfWork.SaveChangesAsync();
        //}

        //public async Task UpdateTodoStatusAsync(int id, int userId, UpdateTodoStatusDto dto)
        //{
        //    var todo = await _todoRepo.FindAsync(t => t.Id == id);

        //    if (todo == null)
        //        throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

        //    if (todo.UserId != userId)
        //        throw new ForbiddenException("You cannot delete this todo", "TODO_FORBIDDEN");

        //    if (!IsValidStatusTransition(todo.Status, dto.Status))
        //        throw new ConflictException($"Cannot change status from {todo.Status} to {dto.Status}", "INVALID_TODO_STATUS_TRANSITION");

        //    var oldStatus = todo.Status;
        //    var now = DateTime.UtcNow;

        //    todo.Status = dto.Status;
        //    todo.UpdatedAt = now;
        //    todo.CompletedAt = dto.Status == TodoStatus.Done ? now : null;

        //    await AddActivityAsync(todo.Id, TodoActivityType.StatusChanged, $"Status changed from {oldStatus} to {dto.Status}");
        //    await _unitOfWork.SaveChangesAsync();
        //}

        //private static bool IsValidStatusTransition(TodoStatus currentStatus,TodoStatus newStatus)
        //{
        //    if (currentStatus == newStatus)
        //        return true;

        //    return currentStatus switch
        //    {
        //        TodoStatus.Todo => newStatus == TodoStatus.InProgress ||  newStatus == TodoStatus.Done,
        //        TodoStatus.InProgress => newStatus == TodoStatus.Todo || newStatus == TodoStatus.Done,
        //        TodoStatus.Done => newStatus == TodoStatus.InProgress, _ => false
        //    };
        //}

        //private async Task AddActivityAsync(int todoId, TodoActivityType type, string description)
        //{
        //    await _activityRepo.CreateAsync(new TodoActivity
        //    {
        //        TodoId = todoId,
        //        Type = type,
        //        Description = description,
        //        CreatedAt = DateTime.UtcNow
        //    });
        //}

        //public async Task<List<TodoActivityDto>> GetActivitiesAsync(int id, int userId)
        //{
        //    var todo = await _todoRepo.FindAsync(t => t.Id == id);

        //    if (todo == null)
        //        throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

        //    if (todo.UserId != userId)
        //        throw new ForbiddenException("You cannot delete this todo", "TODO_FORBIDDEN");

        //    var activities = await _activityRepo.All()
        //        .AsNoTracking()
        //        .Where(a => a.TodoId == id)
        //        .OrderByDescending(a => a.CreatedAt)
        //        .ToListAsync();

        //    return _mapper.Map<List<TodoActivityDto>>(activities);
        //}
        //private async Task ValidateCategoryAsync(int? categoryId, int userId)
        //{
        //    if (!categoryId.HasValue) return;

        //    var category = await _categoryRepo.FindAsync(c => c.Id == categoryId && c.UserId == userId);

        //    if (category == null)
        //        throw new NotFoundException("Category not found", "CATEGORY_NOT_FOUND");
        //}


    }
}
