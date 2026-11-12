using BE_ZSM.Authorization;
using BE_ZSM.Contexts;
using BE_ZSM.DTOs.AdminRecovery;
using BE_ZSM.Entities;
using BE_ZSM.Entities.Rbac;
using BE_ZSM.Exceptions;
using BE_ZSM.Extensions;
using BE_ZSM.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace BE_ZSM.Services.AdminRecovery;

public sealed class AdminRecoveryService(AppDbContext dbContext) : IAdminRecoveryService
{
    private const int DefaultAdminRoleId = 2;
    private const string AuditAction = "ADMIN_ROLE_ACCESS_RESTORED";
    private const int MaxConcurrencyAttempts = 3;
    private static readonly SemaphoreSlim RecoveryLock = new(1, 1);

    public async Task<PagedResult<AdminRecoveryUserDto>> SearchUsersAsync(int actorUserId, string? search, int page = 1,
        int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!await IsSystemOwnerAsync(actorUserId, cancellationToken))
            throw new ForbiddenException("Only the System Owner can search recovery targets", "SYSTEM_OWNER_REQUIRED");

        var users = dbContext.Users.AsNoTracking().Where(user => !user.IsSystemOwner);
        var keyword = search?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            var normalizedKeyword = keyword.ToUpperInvariant();
            var hasUserId = int.TryParse(keyword, NumberStyles.None, CultureInfo.InvariantCulture, out var userId);
            users = users.Where(user => user.DisplayName.ToUpper().Contains(normalizedKeyword)
                || user.Email.ToUpper().Contains(normalizedKeyword)
                || (hasUserId && user.Id == userId));
        }

        var results = users
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Id)
            .Select(user => new AdminRecoveryUserDto
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email
            });

        return await results.ToPagedResultAsync(page, pageSize);
    }

    public async Task RestoreAsync(int actorUserId, int targetUserId, string reason, CancellationToken cancellationToken = default)
    {
        await RecoveryLock.WaitAsync(cancellationToken);
        try
        {
            await RestoreCoreAsync(actorUserId, targetUserId, reason, cancellationToken);
        }
        finally
        {
            RecoveryLock.Release();
        }
    }

    private async Task RestoreCoreAsync(int actorUserId, int targetUserId, string reason, CancellationToken cancellationToken)
    {
        if (!await IsSystemOwnerAsync(actorUserId, cancellationToken))
        {
            await WriteAuditAsync(actorUserId, targetUserId, reason?.Trim() ?? string.Empty, "failure", cancellationToken);
            throw new ForbiddenException("Only the System Owner can restore administrative access", "SYSTEM_OWNER_REQUIRED");
        }

        var auditReason = (reason ?? string.Empty).Trim();
        if (auditReason.Length is 0 or > 1000)
        {
            await WriteAuditAsync(actorUserId, targetUserId, auditReason, "failure", cancellationToken);
            throw new BadRequestException("A recovery reason of 1 to 1000 characters is required", "RECOVERY_REASON_INVALID");
        }

        if (targetUserId == actorUserId)
        {
            await WriteAuditAsync(actorUserId, targetUserId, auditReason, "failure", cancellationToken);
            throw new BadRequestException("The System Owner account cannot be the recovery target", "RECOVERY_TARGET_OWNER_FORBIDDEN");
        }

        for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
        {
            try
            {
                await RestoreAdminRoleOnceAsync(actorUserId, targetUserId, auditReason, cancellationToken);
                return;
            }
            catch (Exception exception)
            {
                dbContext.ChangeTracker.Clear();
                if (IsConcurrencyConflict(exception))
                {
                    if (await HasAdminAccessAsync(targetUserId, cancellationToken))
                    {
                        await WriteAuditAsync(actorUserId, targetUserId, auditReason, "already restored", cancellationToken);
                        return;
                    }

                    if (attempt < MaxConcurrencyAttempts)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
                        continue;
                    }
                }

                await WriteAuditAsync(actorUserId, targetUserId, auditReason, "failure", cancellationToken);
                throw;
            }
        }
    }

    private async Task RestoreAdminRoleOnceAsync(int actorUserId, int targetUserId, string reason, CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        try
        {
            if (!await IsSystemOwnerAsync(actorUserId, cancellationToken))
                throw new ForbiddenException("Only the System Owner can restore administrative access", "SYSTEM_OWNER_REQUIRED");

            if (!await dbContext.Users.AnyAsync(user => user.Id == targetUserId, cancellationToken))
                throw new NotFoundException("User not found", "USER_NOT_FOUND");

            var adminRole = await dbContext.Roles.SingleOrDefaultAsync(role => role.Id == DefaultAdminRoleId
                && role.Name.ToUpper() == "ADMIN", cancellationToken);
            if (adminRole is null)
                throw new AppException("The default ADMIN role (ID 2) is missing or has an unexpected name.", 500, "DEFAULT_ADMIN_ROLE_NOT_FOUND");

            var requiredPermissions = await dbContext.Permissions
                .Where(permission => PermissionCodes.AdminRecoveryPermissions.Contains(permission.Code))
                .ToListAsync(cancellationToken);
            if (requiredPermissions.Count != PermissionCodes.AdminRecoveryPermissions.Count)
                throw new AppException("One or more required ADMIN recovery permissions are not seeded.", 500, "ADMIN_RECOVERY_PERMISSIONS_NOT_SEEDED");

            var requiredPermissionIds = requiredPermissions.Select(permission => permission.Id).ToHashSet();
            var existingPermissionIds = await dbContext.RolePermissions
                .Where(item => item.RoleId == adminRole.Id && requiredPermissionIds.Contains(item.PermissionId))
                .Select(item => item.PermissionId)
                .ToListAsync(cancellationToken);
            var missingPermissions = requiredPermissions.Where(permission => !existingPermissionIds.Contains(permission.Id)).ToList();
            dbContext.RolePermissions.AddRange(missingPermissions.Select(permission => new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = permission.Id
            }));

            var alreadyAssigned = await dbContext.UserRoles.AnyAsync(item => item.UserId == targetUserId
                && item.RoleId == adminRole.Id, cancellationToken);
            if (!alreadyAssigned)
                dbContext.UserRoles.Add(new UserRole { UserId = targetUserId, RoleId = adminRole.Id });

            var alreadyRestored = alreadyAssigned && missingPermissions.Count == 0;
            dbContext.AdminRecoveryAudits.Add(new AdminRecoveryAudit
            {
                ActorUserId = actorUserId,
                TargetUserId = targetUserId,
                Action = AuditAction,
                Reason = reason,
                CreatedAtUtc = DateTime.UtcNow,
                Outcome = alreadyRestored ? "already restored" : "success"
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private Task<bool> IsSystemOwnerAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsSystemOwner, cancellationToken);

    private static bool IsConcurrencyConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
            return true;
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is SqlException sqlException && sqlException.Number is 1205 or 1222 or 3960 or 2601 or 2627)
                return true;
        return false;
    }

    private async Task<bool> HasAdminAccessAsync(int targetUserId, CancellationToken cancellationToken)
    {
        var isAssigned = await dbContext.UserRoles.AsNoTracking()
            .AnyAsync(item => item.UserId == targetUserId && item.RoleId == DefaultAdminRoleId, cancellationToken);
        if (!isAssigned)
            return false;

        var requiredPermissionIds = await dbContext.Permissions.AsNoTracking()
            .Where(permission => PermissionCodes.AdminRecoveryPermissions.Contains(permission.Code))
            .Select(permission => permission.Id)
            .ToListAsync(cancellationToken);
        if (requiredPermissionIds.Count != PermissionCodes.AdminRecoveryPermissions.Count)
            return false;

        var assignedPermissionIds = await dbContext.RolePermissions.AsNoTracking()
            .Where(item => item.RoleId == DefaultAdminRoleId && requiredPermissionIds.Contains(item.PermissionId))
            .Select(item => item.PermissionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return assignedPermissionIds.Count == requiredPermissionIds.Count;
    }

    private async Task WriteAuditAsync(int actorId, int targetId, string reason, string outcome, CancellationToken cancellationToken)
    {
        dbContext.AdminRecoveryAudits.Add(new AdminRecoveryAudit
        {
            ActorUserId = actorId,
            TargetUserId = targetId,
            Action = AuditAction,
            Reason = reason.Length > 1000 ? reason[..1000] : reason,
            CreatedAtUtc = DateTime.UtcNow,
            Outcome = outcome
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
