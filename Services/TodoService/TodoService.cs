using AutoMapper;
using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Todos;
using BE_ZSM.Services.Cache;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using BE_ZSM.Exceptions;
using BE_ZSM.Extensions;
using BE_ZSM.Repositories.Generic;
using BE_ZSM.Responses;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Data;

namespace BE_ZSM.Services.TodoService
{
    public class TodoService : ITodoService
    {
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<Todo> _todoRepo;
        private readonly IGenericRepository<TodoCategory> _categoryRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IGenericRepository<TodoShare> _todoShareRepo;
        private readonly IGenericRepository<TodoNote> _todoNoteRepo;
        private readonly AppDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private static readonly TimeSpan TodoCacheExpiration = TimeSpan.FromMinutes(1);

        public TodoService(IMapper mapper, IUnitOfWork unitOfWork, ICacheService cacheService, AppDbContext dbContext)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _todoRepo = _unitOfWork.GetRepository<Todo>();
            _categoryRepo = _unitOfWork.GetRepository<TodoCategory>();
            _userRepo = _unitOfWork.GetRepository<User>();
            _todoShareRepo = _unitOfWork.GetRepository<TodoShare>();
            _todoNoteRepo = _unitOfWork.GetRepository<TodoNote>();
            _dbContext = dbContext;
            _cacheService = cacheService;
        }

        // GET OWN TODOS/FILTER/SORT/SEARCH TODOS
        public async Task<PagedResult<TodoDto>> GetTodosAsync(int userId, TodoQueryDto queryDto)
        {
            var cacheKey = $"todos:me:{userId}";
            var queryKey = CreateQueryCacheKey(queryDto);
            var cached = await _cacheService.GetAsync<CachedTodoPage>(cacheKey);
            if (cached?.QueryKey == queryKey)
                return cached.Result;

            var result = await GetTodosAsync(queryDto, userId);
            await _cacheService.SetAsync(cacheKey, new CachedTodoPage(queryKey, result), TodoCacheExpiration);
            return result;
        }

        public async Task<PagedResult<TodoDto>> GetTodosForUserAsync(int userId, TodoQueryDto queryDto)
        {
            if (!await _userRepo.All().AsNoTracking().AnyAsync(user => user.Id == userId))
                throw new NotFoundException("User not found", "USER_NOT_FOUND");

            return await GetTodosAsync(queryDto, userId);
        }

        public async Task<PagedResult<TodoDto>> GetAllTodosAsync(TodoQueryDto queryDto)
        {
            return await GetTodosAsync(queryDto, (int?)null);
        }

        public async Task<PagedResult<TodoDto>> GetSharedTodosAsync(int userId, TodoQueryDto queryDto)
        {
            var query = _todoRepo.All().AsNoTracking()
                .Where(todo => todo.Shares.Any(share => share.SharedWithUserId == userId));
            return await GetTodosAsync(queryDto, query);
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
            var query = _todoRepo.All().AsNoTracking().AsQueryable();
            if (userId.HasValue)
                query = query.Where(todo => todo.UserId == userId.Value);

            return await GetTodosAsync(queryDto, query);
        }

        private async Task<PagedResult<TodoDto>> GetTodosAsync(TodoQueryDto queryDto, IQueryable<Todo> source)
        {
            IQueryable<Todo> query = source.Include(todo => todo.Category);

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

        public async Task<IReadOnlyList<TodoShareDto>> GetTodoSharesAsync(int todoId, int ownerId)
        {
            if (!await _todoRepo.All().AsNoTracking().AnyAsync(todo => todo.Id == todoId && todo.UserId == ownerId))
                throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");

            var shares = await _todoShareRepo.All().AsNoTracking()
                .Where(share => share.TodoId == todoId)
                .OrderBy(share => share.SharedWithUserId)
                .ToListAsync();
            return _mapper.Map<List<TodoShareDto>>(shares);
        }

        public async Task ReplaceTodoSharesAsync(int todoId, ReplaceTodoSharesDto dto, int ownerId, CancellationToken cancellationToken = default)
        {
            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                if (_dbContext.Database.IsSqlServer())
                {
                    var lockedTodo = await _dbContext.Todos
                        .FromSqlInterpolated($"SELECT * FROM [Todos] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {todoId} AND [UserId] = {ownerId}")
                        .AsNoTracking()
                        .ToListAsync(cancellationToken);
                    if (lockedTodo.Count == 0)
                        throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");
                }
                else if (!await _todoRepo.All().AsNoTracking()
                    .AnyAsync(todo => todo.Id == todoId && todo.UserId == ownerId, cancellationToken))
                {
                    throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");
                }

                if (dto.UserIds == null)
                    throw new BadRequestException("userIds is required", "SHARE_USER_IDS_REQUIRED");

                var userIds = dto.UserIds.Distinct().ToList();
                if (userIds.Contains(ownerId))
                    throw new BadRequestException("A Todo cannot be shared with its owner", "TODO_SELF_SHARE_FORBIDDEN");

                if (userIds.Count > 0)
                {
                    var existingUserIds = await _userRepo.All().AsNoTracking()
                        .Where(user => userIds.Contains(user.Id))
                        .Select(user => user.Id)
                        .ToListAsync(cancellationToken);
                    if (existingUserIds.Count != userIds.Count)
                        throw new BadRequestException("One or more users were not found", "SHARE_USER_NOT_FOUND");
                }

                var currentShares = await _todoShareRepo.Where(share => share.TodoId == todoId)
                    .ToListAsync(cancellationToken);
                _todoShareRepo.DeleteRangeAsync(currentShares);
                await _todoShareRepo.CreateRangeAsync(userIds.Select(userId => new TodoShare
                {
                    TodoId = todoId,
                    SharedWithUserId = userId,
                    CreatedAt = DateTime.UtcNow
                }));

                await _unitOfWork.SaveChangesAsync();
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task<IReadOnlyList<TodoNoteDto>> GetTodoNotesAsync(int todoId, int userId,
            bool canReadOwned, bool canReadShared)
        {
            await EnsureTodoIsAccessibleAsync(todoId, userId, canReadOwned, canReadShared);
            var notes = await _todoNoteRepo.All().AsNoTracking()
                .Where(note => note.TodoId == todoId)
                .OrderBy(note => note.CreatedAt).ThenBy(note => note.Id)
                .ToListAsync();
            return _mapper.Map<List<TodoNoteDto>>(notes);
        }

        public async Task<TodoNoteDto> CreateTodoNoteAsync(int todoId, CreateTodoNoteDto dto, int userId,
            bool canReadOwned, bool canReadShared)
        {
            if (string.IsNullOrWhiteSpace(dto.Content))
                throw new BadRequestException("Note content is required", "TODO_NOTE_CONTENT_REQUIRED");
            if (dto.Content.Length > 2000)
                throw new BadRequestException("Note content cannot exceed 2000 characters", "TODO_NOTE_CONTENT_TOO_LONG");

            await EnsureTodoIsAccessibleAsync(todoId, userId, canReadOwned, canReadShared);
            var note = new TodoNote
            {
                TodoId = todoId,
                UserId = userId,
                Content = dto.Content,
                CreatedAt = DateTime.UtcNow
            };
            await _todoNoteRepo.CreateAsync(note);
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<TodoNoteDto>(note);
        }

        private async Task EnsureTodoIsAccessibleAsync(int todoId, int userId, bool canReadOwned, bool canReadShared)
        {
            var query = _todoRepo.All().AsNoTracking().Where(todo => todo.Id == todoId);
            query = query.Where(todo =>
                (canReadOwned && todo.UserId == userId)
                || (canReadShared && todo.Shares.Any(share => share.SharedWithUserId == userId)));
            if (!await query.AnyAsync())
                throw new NotFoundException("Todo not found", "TODO_NOT_FOUND");
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
            if (deletedIds.Count > 0)
                await _todoRepo.DeleteAsync(x => x.UserId == userId && deletedIds.Contains(x.Id));

            await _unitOfWork.SaveChangesAsync();

            await _cacheService.RemoveAsync($"todos:me:{userId}");
        }

        private static string CreateQueryCacheKey(TodoQueryDto queryDto)
        {
            var query = JsonSerializer.Serialize(new
            {
                Search = queryDto.Search?.Trim(),
                queryDto.Status,
                queryDto.Priority,
                queryDto.Page,
                queryDto.PageSize,
                SortBy = queryDto.SortBy?.ToLowerInvariant(),
                queryDto.IsDescending,
                queryDto.IsOverdue,
                queryDto.CategoryId
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(query)));
        }

        private sealed record CachedTodoPage(string QueryKey, PagedResult<TodoDto> Result);
    }
}
