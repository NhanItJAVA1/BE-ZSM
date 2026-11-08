using BE_ZSM.Authorization;
using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Rbac;
using BE_ZSM.Entities;
using BE_ZSM.Entities.Rbac;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using BE_ZSM.Services.Rbac;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Services.Rbac;

public sealed class RbacManagementService : IRbacManagementService
{
    // Effective administrator status requires both permissions, including grants from different roles.
    private static readonly string[] AdministratorPermissionCodes =
    [
        PermissionCodes.RolesManagePermissions,
        PermissionCodes.UsersManageRoles
    ];

    private readonly AppDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<Role> _roleRepository;
    private readonly IGenericRepository<Permission> _permissionRepository;
    private readonly IGenericRepository<RolePermission> _rolePermissionRepository;
    private readonly IGenericRepository<User> _userRepository;
    private readonly IGenericRepository<UserRole> _userRoleRepository;

    public RbacManagementService(AppDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
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
        var name = NormalizeName(dto.Name);
        var description = NormalizeDescription(dto.Description);

        await EnsureRoleNameAvailableAsync(name, null, cancellationToken);

        // Role IDs are configured ValueGeneratedNever, so retain the existing MAX(Id) + 1 behavior.
        var nextId = checked((await _roleRepository.All().MaxAsync(role => (int?)role.Id, cancellationToken) ?? 0) + 1);
        var role = new Role { Id = nextId, Name = name, Description = description };
        await _roleRepository.CreateAsync(role);
        await SaveChangesWithConflictHandlingAsync("ROLE_NAME_ALREADY_EXISTS", "A role with this name already exists");

        return ToRoleDto(role);
    }

    public async Task<RoleResponseDto> UpdateRoleAsync(int id, SaveRoleDto dto, CancellationToken cancellationToken = default)
    {
        var name = NormalizeName(dto.Name);
        var description = NormalizeDescription(dto.Description);

        var role = await _roleRepository.FindAsync(item => item.Id == id);
        if (role == null)
            throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

        if (IsDefaultRole(role) && !string.Equals(role.Name, name, StringComparison.Ordinal))
            throw new ConflictException("The default User and Admin role names cannot be changed", "DEFAULT_ROLE_NAME_IMMUTABLE");

        await EnsureRoleNameAvailableAsync(name, id, cancellationToken);
        role.Name = name;
        role.Description = description;
        await SaveChangesWithConflictHandlingAsync("ROLE_NAME_ALREADY_EXISTS", "A role with this name already exists");

        return ToRoleDto(role);
    }

    public async Task DeleteRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.FindAsync(item => item.Id == id);
        if (role == null)
            throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

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
            .Select(ToPermissionProjection())
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
            .Select(ToPermissionProjection())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionResponseDto>> ReplaceRolePermissionsAsync(
        int roleId,
        ReplaceRolePermissionsDto dto,
        CancellationToken cancellationToken = default)
    {
        var permissionIds = ValidateDistinctIds(dto.PermissionIds, "PermissionIds");

        var role = await _roleRepository.FindAsync(item => item.Id == roleId);
        if (role == null)
            throw new NotFoundException("Role not found", "ROLE_NOT_FOUND");

        var permissions = await _permissionRepository.Where(permission => permissionIds.Contains(permission.Id))
            .ToListAsync(cancellationToken);
        if (permissions.Count != permissionIds.Count)
            throw new BadRequestException("One or more permission IDs do not exist", "PERMISSION_ID_INVALID");

        if (role.NameNormalized == "USER" && permissions.Any(permission => PermissionCodes.RbacManagement.Contains(permission.Code)))
            throw new ConflictException("RBAC management permissions cannot be assigned to the default User role", "USER_ROLE_ADMIN_PERMISSION_FORBIDDEN");

        await EnsureLastAdministratorSurvivesPermissionChangeAsync(roleId, permissions, cancellationToken);

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

        await SaveChangesWithConflictHandlingAsync(
            "ROLE_PERMISSION_CONCURRENCY_CONFLICT",
            "Role permissions changed concurrently");

        return permissions.OrderBy(permission => permission.Code).Select(ToPermissionDto).ToList();
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

    public async Task<IReadOnlyList<RoleResponseDto>> ReplaceUserRolesAsync(
        int userId,
        ReplaceUserRolesDto dto,
        CancellationToken cancellationToken = default)
    {
        var roleIds = ValidateDistinctIds(dto.RoleIds, "RoleIds");
        if (roleIds.Count == 0)
            throw new BadRequestException("A user must have at least one role", "USER_ROLE_REQUIRED");

        var user = await _userRepository.Where(item => item.Id == userId)
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(cancellationToken);
        if (user == null)
            throw new NotFoundException("User not found", "USER_NOT_FOUND");

        var roles = await _roleRepository.Where(role => roleIds.Contains(role.Id))
            .ToListAsync(cancellationToken);
        if (roles.Count != roleIds.Count)
            throw new BadRequestException("One or more role IDs do not exist", "ROLE_ID_INVALID");

        var currentAdministratorIds = await GetAdministratorUserIdsAsync(cancellationToken);
        if (currentAdministratorIds.Count == 1
            && currentAdministratorIds[0] == userId
            && !await RoleIdsGrantAdministratorPermissionsAsync(roleIds, cancellationToken))
        {
            throw new ConflictException("The last RBAC administrator cannot lose both management permissions", "LAST_ADMIN_PROTECTED");
        }

        var currentRoleIds = user.UserRoles.Select(item => item.RoleId).ToHashSet();
        var requestedRoleIds = roleIds.ToHashSet();
        var rolesToRemove = user.UserRoles.Where(item => !requestedRoleIds.Contains(item.RoleId)).ToList();
        _userRoleRepository.DeleteRangeAsync(rolesToRemove);

        var rolesToAdd = roles.Where(role => !currentRoleIds.Contains(role.Id)).ToList();
        await _userRoleRepository.CreateRangeAsync(rolesToAdd.Select(role => new UserRole
        {
            UserId = userId,
            RoleId = role.Id
        }));

        var legacyRole = roles.FirstOrDefault(role => role.Id == user.RoleId)
            ?? roles.FirstOrDefault(role => role.Name == "User")
            ?? roles.OrderBy(role => role.Id).First();
        user.RoleId = legacyRole.Id;

        await SaveChangesWithConflictHandlingAsync(
            "USER_ROLE_CONCURRENCY_CONFLICT",
            "User roles changed concurrently");

        return roles.OrderBy(role => role.Name).Select(ToRoleDto).ToList();
    }

    // Business rules

    private async Task EnsureRoleNameAvailableAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        var normalized = name.ToUpperInvariant();
        var exists = await _roleRepository.All().AnyAsync(role =>
            role.NameNormalized == normalized && (!exceptId.HasValue || role.Id != exceptId.Value), cancellationToken);
        if (exists)
            throw new ConflictException("A role with this name already exists", "ROLE_NAME_ALREADY_EXISTS");
    }

    private async Task<List<int>> GetAdministratorUserIdsAsync(CancellationToken cancellationToken)
    {
        return await _userRepository.All().AsNoTracking()
            .Where(user => user.UserRoles.Any(userRole => userRole.Role.RolePermissions
                    .Any(rolePermission => rolePermission.Permission.Code == PermissionCodes.RolesManagePermissions))
                && user.UserRoles.Any(userRole => userRole.Role.RolePermissions
                    .Any(rolePermission => rolePermission.Permission.Code == PermissionCodes.UsersManageRoles)))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureLastAdministratorSurvivesPermissionChangeAsync(
        int roleId,
        IReadOnlyCollection<Permission> replacementPermissions,
        CancellationToken cancellationToken)
    {
        var currentAdministratorIds = await GetAdministratorUserIdsAsync(cancellationToken);
        if (currentAdministratorIds.Count == 0)
            return;

        var usersAssignedToRole = await _userRoleRepository.All()
            .Where(userRole => userRole.RoleId == roleId)
            .Select(userRole => userRole.UserId)
            .ToListAsync(cancellationToken);

        var hasUnaffectedAdministrator = currentAdministratorIds.Any(
            userId => !usersAssignedToRole.Contains(userId));
        if (hasUnaffectedAdministrator)
            return;

        var hasAffectedAdministrator = await HasAdministratorAfterPermissionChangeAsync(
            roleId,
            usersAssignedToRole,
            replacementPermissions,
            cancellationToken);
        if (hasAffectedAdministrator)
            return;

        throw new ConflictException(
            "At least one RBAC administrator must retain both management permissions",
            "LAST_ADMIN_PROTECTED");
    }

    private async Task<bool> HasAdministratorAfterPermissionChangeAsync(
        int roleId,
        IReadOnlyCollection<int> affectedUserIds,
        IReadOnlyCollection<Permission> replacementPermissions,
        CancellationToken cancellationToken)
    {
        var replacementPermissionCodes = replacementPermissions
            .Where(permission => AdministratorPermissionCodes.Contains(permission.Code))
            .Select(permission => permission.Code)
            .ToArray();
        var administratorGrantsFromOtherRoles = await _userRoleRepository.All()
            .Where(userRole => affectedUserIds.Contains(userRole.UserId) && userRole.RoleId != roleId)
            .SelectMany(userRole => userRole.Role.RolePermissions
                .Where(rolePermission => AdministratorPermissionCodes.Contains(rolePermission.Permission.Code))
                .Select(rolePermission => new { userRole.UserId, rolePermission.Permission.Code }))
            .ToListAsync(cancellationToken);

        var permissionCodesByUser = administratorGrantsFromOtherRoles
            .GroupBy(grant => grant.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(grant => grant.Code).ToHashSet(StringComparer.Ordinal));

        return affectedUserIds.Any(userId =>
        {
            IEnumerable<string> grantsFromOtherRoles = permissionCodesByUser.TryGetValue(userId, out var permissionCodes)
                ? permissionCodes
                : Enumerable.Empty<string>();
            var effectivePermissionCodes = grantsFromOtherRoles.Concat(replacementPermissionCodes);

            return AdministratorPermissionCodes.All(effectivePermissionCodes.Contains);
        });
    }

    private async Task<bool> RoleIdsGrantAdministratorPermissionsAsync(IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken)
    {
        var codes = await _rolePermissionRepository.All()
            .Where(item => roleIds.Contains(item.RoleId)
                && AdministratorPermissionCodes.Contains(item.Permission.Code))
            .Select(item => item.Permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);
        return AdministratorPermissionCodes.All(codes.Contains);
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

    // DTO validation and mapping

    private static List<int> ValidateDistinctIds(List<int> ids, string fieldName)
    {
        if (ids == null || ids.Any(id => id <= 0))
            throw new BadRequestException($"{fieldName} must contain positive IDs", "RBAC_IDS_INVALID");
        if (ids.Count != ids.Distinct().Count())
            throw new BadRequestException($"{fieldName} must not contain duplicates", "RBAC_IDS_DUPLICATE");
        return ids;
    }

    private static string NormalizeName(string? name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new BadRequestException("Role name is required", "ROLE_NAME_REQUIRED");
        if (normalized.Length > 50)
            throw new BadRequestException("Role name cannot exceed 50 characters", "ROLE_NAME_TOO_LONG");
        return normalized;
    }

    private static string NormalizeDescription(string? description)
    {
        var normalized = description?.Trim() ?? string.Empty;
        if (normalized.Length > 500)
            throw new BadRequestException("Role description cannot exceed 500 characters", "ROLE_DESCRIPTION_TOO_LONG");
        return normalized;
    }

    private static bool IsDefaultRole(Role role) => role.Id is 1 or 2
        || role.NameNormalized is "USER" or "ADMIN";

    private static RoleResponseDto ToRoleDto(Role role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description
    };

    private static PermissionResponseDto ToPermissionDto(Permission permission) => new()
    {
        Id = permission.Id,
        Code = permission.Code,
        Description = permission.Description
    };

    private static System.Linq.Expressions.Expression<Func<Permission, PermissionResponseDto>> ToPermissionProjection() =>
        permission => new PermissionResponseDto
        {
            Id = permission.Id,
            Code = permission.Code,
            Description = permission.Description
        };
}
