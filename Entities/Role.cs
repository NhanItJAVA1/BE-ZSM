
namespace BE_ZSM.Entities
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameNormalized
        {
            get => Name.ToUpperInvariant();
            private set { }
        }
        public string Description { get; set; } = string.Empty;
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Rbac.UserRole> UserRoles { get; set; } = new List<Rbac.UserRole>();
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    }
}

