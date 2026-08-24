using AutoMapper;
using BE_ZSM.DTOs.Todos;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Services.TodoService
{
    public class TodoService : ITodoService
    {
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<Todo> _todoRepo;

        public TodoService(
            IMapper mapper,
            IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _todoRepo = _unitOfWork.GetRepository<Todo>();
        }

        public async Task<TodoDto> GetTodoAsync(int id, int userId)
        {
            var todo = _todoRepo.FindAsync(t => t.Id == id && t.UserId == userId);

            if (todo == null)
            {
                throw new NotFoundException(
                    "Todo not found",
                    "TODO_NOT_FOUND");
            }

            return _mapper.Map<TodoDto>(todo);
        }

        public async Task<List<TodoDto>> GetTodosAsync(int userId)
        {
            var todos = _todoRepo
                        .All()
                        .Where(t => t.UserId == userId)
                        .AsNoTracking()
                        .ToListAsync();

            return _mapper.Map<List<TodoDto>>(todos);
        }

        public async Task CreateTodoAsync(CreateTodoDto dto, int userId)
        {
            var todo = _mapper.Map<Todo>(dto);

            todo.UserId = userId;
            todo.CreatedAt = DateTime.UtcNow;

            await _todoRepo.CreateAsync(todo);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteTodoAsync(int id, int userId)
        {
            var todo = await _todoRepo.FindAsync(
                   t => t.Id == id && t.UserId == userId);

            if (todo == null)
            {
                throw new NotFoundException(
                    "Todo not found",
                    "TODO_NOT_FOUND");
            }

            await _todoRepo.DeleteAsync(todo);
            await _unitOfWork.SaveChangesAsync();
        }        

        public async Task UpdateTodoAsync(int id, UpdateTodoDto dto, int userId)
        {
            var todo = await _todoRepo.FindAsync(t => t.Id == id && t.UserId == userId);

            if (todo == null)
            {
                throw new NotFoundException(
                    "Todo not found",
                    "TODO_NOT_FOUND");
            }

            _mapper.Map(dto, todo);
            todo.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
