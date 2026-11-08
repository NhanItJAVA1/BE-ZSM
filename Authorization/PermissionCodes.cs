namespace BE_ZSM.Authorization;

public static class PermissionCodes
{
    public const string TodoRead = "todo.read";
    public const string TodoWrite = "todo.write";
    public const string TodoCategoryRead = "todo-category.read";
    public const string TodoCategoryCreate = "todo-category.create";
    public const string TodoCategoryUpdate = "todo-category.update";
    public const string TodoCategoryDelete = "todo-category.delete";
    public const string CatalogUpload = "catalog.upload";
    public const string UsersRead = "users.read";
    public const string UsersUpdate = "users.update";
    public const string UsersDelete = "users.delete";
    public const string RolesRead = "roles.read";
    public const string RolesCreate = "roles.create";
    public const string RolesUpdate = "roles.update";
    public const string RolesDelete = "roles.delete";
    public const string RolesManagePermissions = "roles.manage-permissions";
    public const string UsersManageRoles = "users.manage-roles";
    public const string PermissionsRead = "permissions.read";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        TodoRead,
        TodoWrite,
        TodoCategoryRead,
        TodoCategoryCreate,
        TodoCategoryUpdate,
        TodoCategoryDelete,
        CatalogUpload,
        UsersRead,
        UsersUpdate,
        UsersDelete,
        RolesRead,
        RolesCreate,
        RolesUpdate,
        RolesDelete,
        RolesManagePermissions,
        UsersManageRoles,
        PermissionsRead
    };

    public static IReadOnlySet<string> RbacManagement { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        RolesRead,
        RolesCreate,
        RolesUpdate,
        RolesDelete,
        RolesManagePermissions,
        UsersManageRoles,
        PermissionsRead
    };
}
