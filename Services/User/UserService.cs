using AutoMapper;
using BE_ZSM.Contexts;
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
    private readonly AppDbContext _dbContext;

    public UserService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        AppDbContext dbContext)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _userRepo = _unitOfWork.GetRepository<User>();
        _dbContext = dbContext;
    }

    public async Task<List<UserResponseDto>> GetUsersAsync()
    {
        var users = await _userRepo.All()
            .Include(u => u.Role)
            .Include(u => u.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission)
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<List<UserResponseDto>>(users);
    }

    public async Task<UserResponseDto> GetUserAsync(int id)
    {
        var user = await _userRepo.Where(u => u.Id == id)
            .Include(u => u.Role)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        var permissions = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == id)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();

        if (user == null)
            throw new NotFoundException("User not found", "USER_NOT_FOUND");

        var response = _mapper.Map<UserResponseDto>(user);
        response.Permissions = permissions;

        return response;
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

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepo.Where(u => u.Id == id).FirstOrDefaultAsync(cancellationToken);
        if (user == null)
            throw new NotFoundException("User not found", "USER_NOT_FOUND");

        if (user.IsSystemOwner)
            throw new ConflictException("The System Owner account cannot be deleted", "SYSTEM_OWNER_DELETE_FORBIDDEN");

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
