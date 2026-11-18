using AutoMapper;
using AutoMapper.QueryableExtensions;
using BE_ZSM.Authorization;
using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Rbac;
using BE_ZSM.Extensions;
using BE_ZSM.Responses;
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

    // GET
    public async Task<IReadOnlyList<RoleResponseDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _roleRepository.All().AsNoTracking()
            .OrderBy(role => role.Name)
            .ProjectTo<RoleResponseDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    // POST
    public async Task<RoleResponseDto> CreateRoleAsync(SaveRoleDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name!.Trim();
        var description = dto.Description?.Trim() ?? string.Empty;
        var normalizedName = name.Trim().ToUpperInvariant();

        var role = new Role {Name = name, Description = description };
        await _roleRepository.CreateAsync(role);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<RoleResponseDto>(role);
    }

    // PUT
    public async Task<RoleResponseDto> UpdateRoleAsync(int id, SaveRoleDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name!.Trim();
        var description = dto.Description?.Trim() ?? string.Empty;
        var normalizedName = name.Trim().ToUpperInvariant();

        var role = await _roleRepository.FindAsync(item => item.Id == id) ?? throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

        if (IsDefaultRole(role) && role.Name.Trim().ToUpperInvariant() != normalizedName)
            throw new ConflictException("The default User and Admin role names cannot be changed", "DEFAULT_ROLE_NAME_IMMUTABLE");

        role.Name = name;
        role.Description = description;
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<RoleResponseDto>(role);
    }

    // DELETE
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
        await _unitOfWork.SaveChangesAsync();
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

        if (role.Id == 1 && permissions.Any(permission => PermissionCodes.RbacManagement.Contains(permission.Code)))
            throw new ConflictException("The default User role cannot receive RBAC management permissions", "USER_ROLE_RBAC_PERMISSION_FORBIDDEN");

        var currentAssignments = await _rolePermissionRepository.Where(item => item.RoleId == roleId)
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

        await _unitOfWork.SaveChangesAsync();
        return permissions.OrderBy(permission => permission.Code)
            .Select(_mapper.Map<PermissionResponseDto>)
            .ToList();
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
            .ProjectTo<RoleResponseDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoleResponseDto>> ReplaceUserRolesAsync(int userId, ReplaceUserRolesDto dto, CancellationToken cancellationToken = default)
    {
        var roleIds = dto.RoleIds;

        var user = await _userRepository.Where(u => u.Id == userId)
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("User not found", "USER_NOT_FOUND");

        if (user.IsSystemOwner)
            throw new ConflictException("System Owner cannot have their roles modified.", "SYSTEM_OWNER_PROTECTED");

        var currentAdminRole = user.UserRoles.FirstOrDefault(assignment => assignment.RoleId == 2);
        if (currentAdminRole is not null && !roleIds.Contains(2))
        {
            var otherAdmins = await _userRoleRepository.All().AsNoTracking()
                .CountAsync(assignment => assignment.RoleId == 2 && assignment.UserId != userId, cancellationToken);
            if (otherAdmins == 0)
                throw new ConflictException("The last Admin role assignment cannot be removed.", "LAST_ADMIN_PROTECTED");
        }

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
            ?? (roles.Count == 0 ? null : roles.Min(r => r.Id));

        await _unitOfWork.SaveChangesAsync();
        return _mapper.Map<List<RoleResponseDto>>(roles.OrderBy(r => r.Name).ToList());
    }

    public async Task<PagedResult<RoleUserDto>> GetRoleUsersAsync(int roleId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!await _roleRepository.All().AsNoTracking().AnyAsync(role => role.Id == roleId, cancellationToken))
            throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

        var users = _userRoleRepository.All().AsNoTracking()
            .Where(assignment => assignment.RoleId == roleId)
            .OrderBy(assignment => assignment.UserId)
            .Select(assignment => new RoleUserDto
            {
                UserId = assignment.UserId,
                DisplayName = assignment.User.DisplayName
            });

        return await users.ToPagedResultAsync(page, pageSize);
    }

    public async Task RemoveUserRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.Where(item => item.Id == userId)
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("User not found", "USER_NOT_FOUND");
        var role = await _roleRepository.FindAsync(item => item.Id == roleId) ?? throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

        if (user.IsSystemOwner)
            throw new ConflictException("System Owner cannot have their roles modified.", "SYSTEM_OWNER_PROTECTED");

        var assignment = user.UserRoles.SingleOrDefault(item => item.RoleId == roleId)
            ?? throw new NotFoundException("User role assignment not found", "USER_ROLE_NOT_FOUND");

        if (role.Id == 2 && role.NameNormalized == "ADMIN")
        {
            var otherAdmins = await _userRoleRepository.All().AsNoTracking()
                .CountAsync(item => item.RoleId == roleId && item.UserId != userId, cancellationToken);
            if (otherAdmins == 0)
                throw new ConflictException("The last Admin role assignment cannot be removed.", "LAST_ADMIN_PROTECTED");
        }

        _userRoleRepository.DeleteRangeAsync(new[] { assignment });
        if (user.RoleId == roleId)
        {
            user.RoleId = user.UserRoles.Where(item => item.RoleId != roleId)
                .Select(item => item.RoleId)
                .OrderBy(id => id)
                .Select(role => (int?)role)
                .FirstOrDefault();
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task BulkRemoveRoleFromUsersAsync(int roleId, BulkRemoveRoleUsersDto dto, CancellationToken cancellationToken = default)
    {
        var userIds = dto.UserIds;
        if (userIds is null || userIds.Count == 0)
            throw new BadRequestException("At least one user ID is required.", "USER_IDS_REQUIRED");
        if (userIds.Distinct().Count() != userIds.Count)
            throw new BadRequestException("User IDs must not contain duplicates.", "USER_IDS_DUPLICATE");
        if (userIds.Any(userId => userId <= 0))
            throw new BadRequestException("User IDs must be positive.", "USER_IDS_INVALID");

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;

        try
        {
            var role = await _roleRepository.All().AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == roleId, cancellationToken)
                ?? throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

            var users = await _userRepository.Where(user => userIds.Contains(user.Id))
                .Include(user => user.UserRoles)
                .ToListAsync(cancellationToken);
            if (users.Count != userIds.Count)
                throw new NotFoundException("One or more users were not found", "USER_NOT_FOUND");
            if (users.Any(user => user.IsSystemOwner))
                throw new ConflictException("System Owner cannot have their roles modified.", "SYSTEM_OWNER_PROTECTED");
            if (users.Any(user => user.UserRoles.All(assignment => assignment.RoleId != roleId)))
                throw new NotFoundException("One or more users do not have this role assignment", "USER_ROLE_NOT_FOUND");
            if (role.Id == 2 && role.NameNormalized == "ADMIN")
            {
                var selectedUserIds = userIds.ToHashSet();
                var remainingAdmins = await _userRoleRepository.All().AsNoTracking()
                    .CountAsync(assignment => assignment.RoleId == roleId && !selectedUserIds.Contains(assignment.UserId), cancellationToken);
                if (remainingAdmins == 0)
                    throw new ConflictException("The last Admin role assignment cannot be removed.", "LAST_ADMIN_PROTECTED");
            }

            var defaultUserRoleId = users.Any(user => user.RoleId == roleId)
                ? await _roleRepository.All().AsNoTracking()
                    .Where(item => item.NameNormalized == "USER")
                    .Select(item => (int?)item.Id)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;

            foreach (var user in users)
            {
                var assignment = user.UserRoles.Single(item => item.RoleId == roleId);
                _userRoleRepository.DeleteRangeAsync(new[] { assignment });
                if (user.RoleId == roleId)
                {
                    var remainingRoleIds = user.UserRoles.Where(item => item.RoleId != roleId)
                        .Select(item => item.RoleId)
                        .ToList();
                    user.RoleId = remainingRoleIds.Count == 0 ? null
                        : defaultUserRoleId.HasValue && remainingRoleIds.Contains(defaultUserRoleId.Value)
                        ? defaultUserRoleId.Value
                        : remainingRoleIds.Min();
                }
            }

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

    private static bool IsDefaultRole(Role role) => role.Id is 1 or 2
        || role.NameNormalized is "USER" or "ADMIN";

}
