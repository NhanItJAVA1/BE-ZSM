using AutoMapper;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using BE_ZSM.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IGenericRepository<User> _userRepo;

    public UserService(
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _userRepo = _unitOfWork.GetRepository<User>();
    }

    public async Task<List<UserResponseDto>> GetUsersAsync()
    {
        var users = await _userRepo.All()
            .Include(u => u.Role)
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<List<UserResponseDto>>(users);
    }

    public async Task<UserResponseDto> GetUserAsync(int id)
    {
        var user = await _userRepo.Where(u => u.Id == id)
            .Include(u => u.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (user == null)
            throw new NotFoundException("User not found", "USER_NOT_FOUND");

        return _mapper.Map<UserResponseDto>(user);
    }

    public async Task UpdateAsync(int id, UpdateUserDto dto)
    {
        var user = await _userRepo.Where(u => u.Id == id)
            .FirstOrDefaultAsync();

        if (user == null)
            throw new NotFoundException("User not found", "USER_NOT_FOUND");

        await EnsureEmailAvailableAsync(id, dto.Email);

        _mapper.Map(dto, user);
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepo.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var user = await _userRepo.FindAsync(u => u.Id == id);

        if (user == null)
            throw new NotFoundException("User not found", "USER_NOT_FOUND");

        await _userRepo.DeleteAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task EnsureEmailAvailableAsync(int userId, string email)
    {
        var exists = await _userRepo.Where(u => u.Email == email && u.Id != userId)
            .AsNoTracking()
            .AnyAsync();

        if (exists)
            throw new ConflictException("Email already exists", "EMAIL_ALREADY_EXISTS");
    }
}
