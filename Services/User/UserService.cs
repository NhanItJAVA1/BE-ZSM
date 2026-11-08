using AutoMapper;
using BE_ZSM.Authorization;
using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using BE_ZSM.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.SqlClient;
using System.Data;

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
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
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

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        try
        {
            if (_dbContext.Database.IsRelational())
                transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var user = await _userRepo.Where(u => u.Id == id).FirstOrDefaultAsync(cancellationToken);
            if (user == null)
                throw new NotFoundException("User not found", "USER_NOT_FOUND");

            var administratorUserIds = await _dbContext.Users.AsNoTracking()
                .Where(candidate => candidate.UserRoles.Any(userRole => userRole.Role.RolePermissions
                        .Any(rolePermission => rolePermission.Permission.Code == PermissionCodes.RolesManagePermissions))
                    && candidate.UserRoles.Any(userRole => userRole.Role.RolePermissions
                        .Any(rolePermission => rolePermission.Permission.Code == PermissionCodes.UsersManageRoles)))
                .Select(candidate => candidate.Id)
                .ToListAsync(cancellationToken);
            if (administratorUserIds.Count == 1 && administratorUserIds[0] == id)
                throw new ConflictException("The last RBAC administrator cannot be deleted", "LAST_ADMIN_PROTECTED");

            await _userRepo.DeleteAsync(user);
            await _unitOfWork.SaveChangesAsync();
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsConcurrencyConflict(exception))
        {
            if (transaction != null)
                await transaction.RollbackAsync(CancellationToken.None);
            throw new ConflictException("User administration changed concurrently; retry the operation", "RBAC_CONCURRENCY_CONFLICT");
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }

    }

    private async Task EnsureEmailAvailableAsync(int userId, string email)
    {
        var exists = await _userRepo.Where(u => u.Email == email && u.Id != userId)
            .AsNoTracking()
            .AnyAsync();

        if (exists)
            throw new ConflictException("Email already exists", "EMAIL_ALREADY_EXISTS");
    }

    private static bool IsConcurrencyConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
            return true;
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is SqlException sqlException && sqlException.Number is 1205 or 3960 or 1222)
                return true;
        return false;
    }
}
