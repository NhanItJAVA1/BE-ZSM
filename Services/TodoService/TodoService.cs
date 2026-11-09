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
        public TodoService(IMapper mapper, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _todoRepo = _unitOfWork.GetRepository<Todo>();
            _categoryRepo = _unitOfWork.GetRepository<TodoCategory>();
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
                query = query.Where(t => t.Title.Contains(search) || (t.Description != null && t.Description.Contains(search)));
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
