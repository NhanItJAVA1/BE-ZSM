using AutoMapper;
using AutoMapper.QueryableExtensions;
using BE_ZSM.Authorization;
using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Rbac;
using BE_ZSM.Entities;
using BE_ZSM.Entities.Rbac;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Services.Rbac;

public sealed class RbacManagementService : IRbacManagementService
{
    private readonly AppDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Role> _roleRepository;
    private readonly IGenericRepository<Permission> _permissionRepository;
    private readonly IGenericRepository<RolePermission> _rolePermissionRepository;
    private readonly IGenericRepository<User> _userRepository;
    private readonly IGenericRepository<UserRole> _userRoleRepository;
    private readonly IMapper _mapper;

    public RbacManagementService(AppDbContext dbContext, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _roleRepository = unitOfWork.GetRepository<Role>();
        _permissionRepository = unitOfWork.GetRepository<Permission>();
        _rolePermissionRepository = unitOfWork.GetRepository<RolePermission>();
        _userRepository = unitOfWork.GetRepository<User>();
        _userRoleRepository = unitOfWork.GetRepository<UserRole>();
    }

    // Role management
    public async Task<IReadOnlyList<RoleResponseDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _roleRepository.All().AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new RoleResponseDto { Id = role.Id, Name = role.Name, Description = role.Description })
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleResponseDto> CreateRoleAsync(SaveRoleDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name!.Trim();
        var description = dto.Description?.Trim() ?? string.Empty;

        var nextId = checked((await _roleRepository.All().MaxAsync(role => (int?)role.Id, cancellationToken) ?? 0) + 1);
        var role = new Role { Id = nextId, Name = name, Description = description };
        await _roleRepository.CreateAsync(role);
        await SaveChangesWithConflictHandlingAsync("ROLE_NAME_ALREADY_EXISTS", "A role with this name already exists");

        return _mapper.Map<RoleResponseDto>(role);
    }

    public async Task<RoleResponseDto> UpdateRoleAsync(int id, SaveRoleDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name!.Trim();
        var description = dto.Description?.Trim() ?? string.Empty;

        var role = await _roleRepository.FindAsync(item => item.Id == id) ?? throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

        if (IsDefaultRole(role) && !string.Equals(role.Name, name, StringComparison.Ordinal))
            throw new ConflictException("The default User and Admin role names cannot be changed", "DEFAULT_ROLE_NAME_IMMUTABLE");

        role.Name = name;
        role.Description = description;
        await SaveChangesWithConflictHandlingAsync("ROLE_NAME_ALREADY_EXISTS", "A role with this name already exists");

        return _mapper.Map<RoleResponseDto>(role);
    }

    public async Task DeleteRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.FindAsync(item => item.Id == id) ?? throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

        if (IsDefaultRole(role))
            throw new ConflictException("Default User and Admin roles cannot be deleted", "DEFAULT_ROLE_DELETE_FORBIDDEN");

        var usedByUsers = await _userRoleRepository.All().AnyAsync(item => item.RoleId == id, cancellationToken);
        var usedAsLegacyRole = await _userRepository.All().AnyAsync(item => item.RoleId == id, cancellationToken);
        var hasPermissions = await _rolePermissionRepository.All().AnyAsync(item => item.RoleId == id, cancellationToken);
        if (usedByUsers || usedAsLegacyRole || hasPermissions)
            throw new ConflictException("Role is still assigned or has permissions", "ROLE_IN_USE");

        _dbContext.Roles.Remove(role);
        await SaveChangesWithConflictHandlingAsync("ROLE_DELETE_CONFLICT", "Role could not be deleted because it is still in use");
    }

    // Permission management
    public async Task<IReadOnlyList<PermissionResponseDto>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await _permissionRepository.All().AsNoTracking()
            .OrderBy(permission => permission.Code)
            .ProjectTo<PermissionResponseDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionResponseDto>> GetRolePermissionsAsync(int roleId, CancellationToken cancellationToken = default)
    {
        if (!await _roleRepository.All().AnyAsync(role => role.Id == roleId, cancellationToken))
            throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

        return await _rolePermissionRepository.All().AsNoTracking()
            .Where(item => item.RoleId == roleId)
            .Select(item => item.Permission)
            .OrderBy(permission => permission.Code)
            .ProjectTo<PermissionResponseDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionResponseDto>> ReplaceRolePermissionsAsync(int roleId, ReplaceRolePermissionsDto dto,
        CancellationToken cancellationToken = default)
    {
        var permissionIds = dto.PermissionIds;

        var role = await _roleRepository.FindAsync(item => item.Id == roleId) ?? throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

        var permissions = await _permissionRepository.Where(permission => permissionIds.Contains(permission.Id))
            .ToListAsync(cancellationToken);

        if (permissions.Count != permissionIds.Count)
            throw new BadRequestException("One or more permission IDs do not exist", "PERMISSION_ID_INVALID");
        else if (role.NameNormalized == "USER" && permissions.Any(permission => PermissionCodes.RbacManagement.Contains(permission.Code)))
            throw new ConflictException("RBAC management permissions cannot be assigned to the default User role", "USER_ROLE_ADMIN_PERMISSION_FORBIDDEN");


        var currentAssignments = await _rolePermissionRepository.Where(item => item.RoleId == roleId)
            .Include(item => item.Permission)
            .ToListAsync(cancellationToken);
        var currentPermissionIds = currentAssignments.Select(item => item.PermissionId).ToHashSet();

        var requestedPermissionIds = permissionIds.ToHashSet();

        _rolePermissionRepository.DeleteRangeAsync(currentAssignments
            .Where(item => !requestedPermissionIds.Contains(item.PermissionId)));

        await _rolePermissionRepository.CreateRangeAsync(permissions
            .Where(permission => !currentPermissionIds.Contains(permission.Id))
            .Select(permission => new RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
            }));

        await SaveChangesWithConflictHandlingAsync("ROLE_PERMISSION_CONCURRENCY_CONFLICT", "Role permissions changed concurrently");

        return permissions.OrderBy(permission => permission.Code).Select(_mapper.Map<PermissionResponseDto>).ToList();
    }

    // User role management
    public async Task<IReadOnlyList<RoleResponseDto>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!await _userRepository.All().AnyAsync(user => user.Id == userId, cancellationToken))
            throw new NotFoundException("User not found", "USER_NOT_FOUND");

        return await _userRoleRepository.All().AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.Role)
            .OrderBy(role => role.Name)
            .Select(role => new RoleResponseDto { Id = role.Id, Name = role.Name, Description = role.Description })
            .ToListAsync(cancellationToken);
    }


    public async Task<IReadOnlyList<RoleResponseDto>> ReplaceUserRolesAsync(int userId, ReplaceUserRolesDto dto, CancellationToken cancellationToken = default)
    {
        var roleIds = dto.RoleIds;

        var user = await _userRepository.Where(u => u.Id == userId)
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("User not found", "USER_NOT_FOUND");

        var roles = await _roleRepository.Where(r => roleIds.Contains(r.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (roles.Count != roleIds.Count)
            throw new BadRequestException("One or more role IDs do not exist", "ROLE_ID_INVALID");

        var currentIds = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        var requestedIds = roleIds.ToHashSet();

        _userRoleRepository.DeleteRangeAsync(user.UserRoles.Where(ur => !requestedIds.Contains(ur.RoleId)).ToList());

        await _userRoleRepository.CreateRangeAsync(roles.Where(r => !currentIds.Contains(r.Id))
                .Select(r => new UserRole
                {
                    UserId = userId,
                    RoleId = r.Id
                }));

        user.RoleId = roles.FirstOrDefault(r => r.Id == user.RoleId)?.Id 
            ?? roles.FirstOrDefault(r => r.Name == "User")?.Id 
            ?? roles.Min(r => r.Id);

        await SaveChangesWithConflictHandlingAsync("USER_ROLE_CONCURRENCY_CONFLICT", "User roles changed concurrently");

        return _mapper.Map<List<RoleResponseDto>>(roles.OrderBy(r => r.Name).ToList());
    }
    
   
    // SaveChanges runs in EF Core's implicit transaction; translate expected constraint and concurrency failures.
    private async Task SaveChangesWithConflictHandlingAsync(string conflictCode, string conflictMessage)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ConflictException("A unique role or relationship constraint was violated", conflictCode);
        }
        catch (Exception exception) when (IsConcurrencyConflict(exception))
        {
            throw new ConflictException(conflictMessage, conflictCode);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is SqlException sqlException && sqlException.Number is 2601 or 2627)
                return true;
        return false;
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

    private static bool IsDefaultRole(Role role) => role.Id is 1 or 2
        || role.NameNormalized is "USER" or "ADMIN";

}
