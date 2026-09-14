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
        //private readonly IGenericRepository<TodoCategory> _categoryRepo;
        //private readonly IGenericRepository<TodoActivity> _activityRepo;
        public TodoService(IMapper mapper, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _todoRepo = _unitOfWork.GetRepository<Todo>();
            //_categoryRepo = _unitOfWork.GetRepository<TodoCategory>();
            //_activityRepo = _unitOfWork.GetRepository<TodoActivity>();
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

        // SAVE BATCH OF TODOS (CREATE, UPDATE, DELETE)
        public async Task SaveTodosAsync(List<SaveTodoDto> dtos, int userId)
        {
            //var creates = _mapper.Map<List<Todo>>(dtos.Where(x => !x.Id.HasValue && !x.IsDeleted));
            //creates.ForEach(x => x.UserId = userId);
            //await _todoRepo.CreateRangeAsync(creates);

            //var updates = _mapper.Map<List<Todo>>(dtos.Where(x => x.Id.HasValue && !x.IsDeleted));
            //updates.ForEach(x => { x.UserId = userId; x.UpdatedAt = DateTime.UtcNow; });
            //_todoRepo.UpdateRange(updates);

            //AutoMapper => map UserId from context items to Todo.UserId
            var creates = _mapper.Map<List<Todo>>(dtos.Where(x => !x.Id.HasValue && !x.IsDeleted), opt => opt.Items["UserId"] = userId);
            await _todoRepo.CreateRangeAsync(creates);

            var updates = _mapper.Map<List<Todo>>(dtos.Where(x => x.Id.HasValue && !x.IsDeleted), opt => opt.Items["UserId"] = userId);
            _todoRepo.UpdateRange(updates);

            var deletedIds = dtos.Where(x => x.Id.HasValue && x.IsDeleted).Select(x => x.Id!.Value).ToList();
            await _todoRepo.DeleteAsync(x => x.UserId == userId && deletedIds.Contains(x.Id));

            await _unitOfWork.SaveChangesAsync();
        }


        //Main Flow: if-elseif-else
        private async Task ProcessTodoAsync(SaveTodoDto dto, int userId)
        {
            var todo = _mapper.Map<Todo>(dto);
            if (!dto.Id.HasValue)
            {
                todo.UserId = userId;
                todo.CreatedAt = DateTime.UtcNow;
                await _todoRepo.CreateAsync(todo);
            }
            else if (dto.IsDeleted)
            {
                await _todoRepo.DeleteByIdAsync(dto.Id!.Value);
            }
            else {
                todo.UpdatedAt = DateTime.UtcNow;
                await _todoRepo.UpdateAsync(todo);
            }            
        }

        //public async Task SaveTodosAsync(List<SaveTodoDto> dtos, int userId)
        //{
        //    if (dtos.Count == 0) return;

        //    var categoryIds = dtos
        //        .Where(x => !x.IsDeleted && x.CategoryId.HasValue)
        //        .Select(x => x.CategoryId!.Value)
        //        .Distinct()
        //        .ToList();

        //    if (categoryIds.Count > 0)
        //    {
        //        var validCategoryCount = await _categoryRepo.All()
        //            .AsNoTracking()
        //            .CountAsync(c => c.UserId == userId && categoryIds.Contains(c.Id));

        //        if (validCategoryCount != categoryIds.Count)
        //            throw new NotFoundException(
        //                "One or more categories not found",
        //                "CATEGORY_NOT_FOUND");
        //    }

        //    var ids = dtos
        //        .Where(x => x.Id.HasValue)
        //        .Select(x => x.Id!.Value)
        //        .Distinct()
        //        .ToList();

        //    var existingTodos = await _todoRepo
        //        .Where(t => t.UserId == userId && ids.Contains(t.Id))
        //        .ToListAsync();

        //    if (existingTodos.Count != ids.Count)
        //        throw new NotFoundException(
        //            "One or more todos not found",
        //            "TODO_NOT_FOUND");

        //    var todoMap = existingTodos.ToDictionary(t => t.Id);

        //    var newTodos = new List<Todo>();
        //    var deleteTodos = new List<Todo>();
        //    var activities = new List<TodoActivity>();
        //    var now = DateTime.UtcNow;

        //    foreach (var dto in dtos)
        //    {
        //        // Create
        //        if (!dto.Id.HasValue)
        //        {
        //            if (dto.IsDeleted)
        //                continue;

        //            var todo = _mapper.Map<Todo>(dto);

        //            todo.UserId = userId;
        //            todo.CreatedAt = now;
        //            todo.Priority = dto.Priority ?? TodoPriority.Medium;

        //            newTodos.Add(todo);

        //            activities.Add(new TodoActivity
        //            {
        //                Todo = todo,
        //                Type = TodoActivityType.Created,
        //                Description = "Todo created",
        //                CreatedAt = now
        //            });

        //            continue;
        //        }

        //        var existing = todoMap[dto.Id.Value];

        //        if (dto.RowVersion == null)
        //            throw new ConflictException(
        //                "RowVersion is required",
        //                "ROW_VERSION_REQUIRED");

        //        _todoRepo.SetOriginalValue(
        //            existing,
        //            t => t.RowVersion,
        //            dto.RowVersion);

        //        // Delete
        //        if (dto.IsDeleted)
        //        {
        //            deleteTodos.Add(existing);
        //            continue;
        //        }

        //        // Update
        //        var oldPriority = existing.Priority;
        //        var oldCategoryId = existing.CategoryId;

        //        _mapper.Map(dto, existing);

        //        existing.Priority = dto.Priority ?? existing.Priority;
        //        existing.UpdatedAt = now;

        //        if (oldPriority != existing.Priority)
        //        {
        //            activities.Add(new TodoActivity
        //            {
        //                TodoId = existing.Id,
        //                Type = TodoActivityType.Updated,
        //                Description =
        //                    $"Priority changed from {oldPriority} to {existing.Priority}",
        //                CreatedAt = now
        //            });
        //        }

        //        if (oldCategoryId != existing.CategoryId)
        //        {
        //            activities.Add(new TodoActivity
        //            {
        //                TodoId = existing.Id,
        //                Type = TodoActivityType.CategoryChanged,
        //                Description = "Category changed",
        //                CreatedAt = now
        //            });
        //        }
        //    }

        //    if (newTodos.Count > 0)
        //        await _todoRepo.CreateRangeAsync(newTodos);

        //    if (deleteTodos.Count > 0)
        //        _todoRepo.DeleteRangeAsync(deleteTodos);

        //    if (activities.Count > 0)
        //        await _activityRepo.CreateRangeAsync(activities);

        //    try
        //    {
        //        await _unitOfWork.SaveChangesAsync();
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        throw new ConflictException(
        //            "One or more todos were modified or deleted by another request",
        //            "TODO_CONCURRENCY_CONFLICT");
        //    }
        //}
    }
}
